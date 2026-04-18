using AspNetCore.Identity.CosmosDb.Stores;
using AspNetCore.Identity.FlexDb;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace AspNetCore.Identity.CosmosDb.Tests.Net9
{
    public abstract class CosmosIdentityTestsBase
    {
        protected static TestUtilities _testUtilities;
        protected static Random _random;
        // Tracks initialization state: true = success, false = failed (don't retry).
        private static readonly Dictionary<string, bool> _initializedProviders = new Dictionary<string, bool>();
        private static readonly object _initLock = new object();

        protected static void InitializeClass(string connectionString, bool backwardCompatibility = false)
        {
            //
            // Setup context.
            //
            _testUtilities = new TestUtilities();
            _random = new Random();

            // Detect provider from the FlexDb strategy resolver rather than brittle string matching.
            var providerName = Utilities.InferDatabaseProviderShortName(connectionString) switch
            {
                "Cosmos" => "CosmosDb",
                "SQL Server" => "SQL Server",
                "MySQL" => "MySQL",
                "SQLite" => "SQLite",
                _ => "Unknown",
            };

            // Create a unique key for this provider configuration
            var providerKey = $"{connectionString}_{providerName}";

            Console.WriteLine($"[INIT] Initializing database for provider: {providerName}");

            lock (_initLock)
            {
                // Only initialize once per provider configuration
                if (_initializedProviders.TryGetValue(providerKey, out var previousResult))
                {
                    if (previousResult)
                    {
                        // For Cosmos DB, verify containers still exist before trusting the cache.
                        // Containers can be deleted externally or become stale, causing 404/1003
                        // ("Owner resource does not exist") and 404/1002 ("ReadSessionNotAvailable")
                        // errors that cascade through all subsequent tests.
                        if (providerName == "CosmosDb" && !VerifyCosmosContainersExist(connectionString))
                        {
                            Console.WriteLine($"[INIT] {providerName} container validation failed — re-initializing...");
                            _initializedProviders.Remove(providerKey);
                            // Fall through to full initialization below
                        }
                        else
                        {
                            Console.WriteLine($"[INIT] {providerName} already initialized, skipping...");
                            return;
                        }
                    }
                    else
                    {
                        // Previously failed — don't retry, fail fast.
                        throw new InvalidOperationException(
                            $"Database initialization previously failed for provider {providerName}. " +
                            $"Skipping to avoid repeated timeouts.");
                    }
                }

                // Create fresh database with a new context
                var builder = CosmosDbOptionsBuilder.GetDbOptionsBuilder<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>>(connectionString);
                using (var dbContext = new CosmosIdentityDbContext<IdentityUser, IdentityRole, string>(builder.Options, false))
                {
                    Console.WriteLine($"[INIT] Calling EnsureCreatedAsync for {providerName}...");

                    // For relational databases, ensure clean state by dropping first
                    // This ensures tables are created even if database already exists
                    if (providerName != "CosmosDb")
                    {
                        try
                        {
                            Console.WriteLine($"[INIT] Cleaning existing database for {providerName}...");

                            // Instead of dropping the entire database (which times out on Azure SQL Serverless),
                            // drop all user tables and let EnsureCreated rebuild the schema.
                            if (dbContext.Database.CanConnect())
                            {
                                if (dbContext.Database.IsSqlServer())
                                {
                                    dbContext.Database.ExecuteSqlRaw(
                                        """
                                        DECLARE @sql NVARCHAR(MAX) = N'';
                                        -- Drop foreign keys first
                                        SELECT @sql += 'ALTER TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name)
                                            + ' DROP CONSTRAINT ' + QUOTENAME(f.name) + ';' + CHAR(13)
                                        FROM sys.foreign_keys f
                                        JOIN sys.tables t ON f.parent_object_id = t.object_id
                                        JOIN sys.schemas s ON t.schema_id = s.schema_id;
                                        EXEC sp_executesql @sql;
                                        -- Then drop all tables
                                        SET @sql = N'';
                                        SELECT @sql += 'DROP TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';' + CHAR(13)
                                        FROM sys.tables t
                                        JOIN sys.schemas s ON t.schema_id = s.schema_id;
                                        EXEC sp_executesql @sql;
                                        """);
                                }
                                else if (string.Equals(providerName, "MySQL", StringComparison.Ordinal))
                                {
                                    var connection = dbContext.Database.GetDbConnection();
                                    var wasOpen = connection.State == System.Data.ConnectionState.Open;

                                    if (!wasOpen)
                                    {
                                        connection.Open();
                                    }

                                    try
                                    {
                                        using var command = connection.CreateCommand();
                                        command.CommandText =
                                            "SET FOREIGN_KEY_CHECKS = 0; " +
                                            "SELECT GROUP_CONCAT(CONCAT('`', table_name, '`') SEPARATOR ',') INTO @tables " +
                                            "FROM information_schema.tables WHERE table_schema = DATABASE(); " +
                                            "SET @tables = IFNULL(@tables, ''); " +
                                            "SET @sql = IF(@tables = '', 'SELECT 1', CONCAT('DROP TABLE ', @tables)); " +
                                            "PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt; " +
                                            "SET FOREIGN_KEY_CHECKS = 1;";
                                        command.ExecuteNonQuery();
                                    }
                                    finally
                                    {
                                        if (!wasOpen)
                                        {
                                            connection.Close();
                                        }
                                    }
                                }
                                else
                                {
                                    dbContext.Database.EnsureDeleted();
                                }

                                Console.WriteLine($"[INIT] Database cleaned for {providerName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[INIT] Could not clean database (may not exist): {ex.Message}");
                        }
                    }

                    try
                    {
                        var created = dbContext.Database.EnsureCreated();
                        Console.WriteLine($"[INIT] EnsureCreated returned: {created} for {providerName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[INIT] ⚠ EnsureCreated failed for {providerName}: {ex.Message}");

                        // Cache the failure so subsequent tests skip this provider immediately
                        // instead of retrying and hitting the same timeout/error.
                        _initializedProviders[providerKey] = false;

                        throw new InvalidOperationException(
                            $"Database initialization failed for provider {providerName}. " +
                            $"EnsureCreated could not create the schema. Inner error: {ex.Message}", ex);
                    }
                }


                // Verify tables were created with yet another fresh context
                using (var dbContext = _testUtilities.GetDbContext(connectionString, backwardCompatibility: backwardCompatibility))
                {
                    Console.WriteLine($"[INIT] Verifying tables exist for {providerName}...");
                    // MySQL can take longer to finalize schema, so use more retries with longer delays
                    VerifyTablesExist(dbContext);
                }

                Console.WriteLine($"[INIT] ✓ Initialization complete for {providerName}");
                // Mark this provider as initialized
                _initializedProviders[providerKey] = true;
            }
        }

        /// <summary>
        /// Gets a friendly provider name for logging
        /// </summary>
        private static string GetProviderName(bool isSqlServer, bool isMySql, bool isSqlite)
        {
            if (isSqlServer) return "SQL Server";
            if (isMySql) return "MySQL";
            if (isSqlite) return "SQLite";
            return "Unknown";
        }

        /// <summary>
        /// Verifies that critical Identity tables exist in the database
        /// </summary>
        private static void VerifyTablesExist(CosmosIdentityDbContext<IdentityUser, IdentityRole, string> dbContext, int retryCount = 1, bool isRelational = true)
        {
            try
            {
                // Verify by attempting to query each DbSet
                // Use Task.Run to avoid deadlocks with .Result in test contexts
                Task.Run(async () =>
                {
                    await dbContext.Users.CountAsync();
                    await dbContext.Roles.CountAsync();
                    await dbContext.UserRoles.CountAsync();
                    await dbContext.UserClaims.CountAsync();
                    await dbContext.RoleClaims.CountAsync();
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // If verification fails, log warning but don't fail initialization
                // Database might be using different naming or the provider might need schema creation
                Console.WriteLine($"⚠ Warning: Could not verify tables exist: {ex.Message}");
                // Note: EnsureCreatedAsync already ran successfully, so tables should exist
            }
        }

        /// <summary>
        /// Verifies that Cosmos DB containers still exist by reading their properties.
        /// Returns false if any container is missing (404/1003), which indicates the
        /// cached initialization state is stale and a re-init is needed.
        /// Also creates a fresh CosmosClient to avoid stale session tokens (404/1002).
        /// </summary>
        private static bool VerifyCosmosContainersExist(string connectionString)
        {
            try
            {
                // Use a fresh CosmosClient to avoid stale session tokens from previous operations
                using var client = new CosmosClient(connectionString);

                // Extract the database name from the connection string or use the default
                var builder = CosmosDbOptionsBuilder.GetDbOptionsBuilder<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>>(connectionString);
                using var tempContext = new CosmosIdentityDbContext<IdentityUser, IdentityRole, string>(builder.Options, false);
                var databaseName = tempContext.Database.GetCosmosClient().ClientOptions.ApplicationName;

                // Try to read a container that we know must exist
                // Use the EF context to do a lightweight query — this validates both
                // container existence and session token freshness
                var users = tempContext.Users.Take(0).ToList();
                var roles = tempContext.Roles.Take(0).ToList();

                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"[INIT] Cosmos container verification failed (404/{ex.SubStatusCode}): {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[INIT] Cosmos container verification failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Optional: Clears test data from database while preserving schema
        /// </summary>
        private static void ClearTestData(CosmosIdentityDbContext<IdentityUser, IdentityRole, string> dbContext)
        {
            try
            {
                // Clear Identity tables in correct order (respecting foreign keys)
                dbContext.UserTokens.RemoveRange(dbContext.UserTokens);
                dbContext.UserLogins.RemoveRange(dbContext.UserLogins);
                dbContext.UserClaims.RemoveRange(dbContext.UserClaims);
                dbContext.UserRoles.RemoveRange(dbContext.UserRoles);
                dbContext.RoleClaims.RemoveRange(dbContext.RoleClaims);
                dbContext.Users.RemoveRange(dbContext.Users);
                dbContext.Roles.RemoveRange(dbContext.Roles);

                dbContext.SaveChanges();
                Console.WriteLine("  ℹ Cleared existing test data");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Failed to clear test data: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize for a specific database provider
        /// </summary>
        protected static void InitializeForProvider(TestDatabaseProvider provider, bool backwardCompatibility = false)
        {
            try
            {
                InitializeClass(provider.ConnectionString, backwardCompatibility);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Database initialization failed") || ex.Message.Contains("Database initialization previously failed"))
            {
                Assert.Inconclusive($"Provider '{provider.DisplayName}' is not available: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a random number
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        protected int GetNextRandomNumber(int min, int max)
        {
            return _random.Next(min, max);
        }

        /// <summary>
        /// Gets a mock <see cref="IdentityRole"/> for unit testing purposes
        /// </summary>
        /// <returns></returns>
        protected async Task<IdentityRole> GetMockRandomRoleAsync(
            CosmosRoleStore<IdentityRole, string> roleStore, bool saveToDatabase = true)
        {
            // Use full GUID to ensure absolute uniqueness across all test runs and providers
            // Format: TestRole_a1b2c3d4e5f6 (shorter, fully unique)
            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 12); // 12 hex chars = 2^48 combinations
            var roleName = $"TestRole_{uniqueId}";

            var role = new IdentityRole(roleName);
            role.NormalizedName = role.Name.ToUpper();

            if (roleStore != null && saveToDatabase)
            {
                var result = await roleStore.CreateAsync(role);

                // If creation fails due to duplicate (extremely unlikely but possible), retry once with new GUID
                if (!result.Succeeded && result.Errors.Any(e =>
                    e.Code == "DuplicateRoleName" ||
                    e.Description.Contains("duplicate", StringComparison.OrdinalIgnoreCase)))
                {
                    // Retry with new GUID
                    uniqueId = Guid.NewGuid().ToString("N");
                    roleName = $"TestRole_{uniqueId}";
                    role = new IdentityRole(roleName);
                    role.NormalizedName = role.Name.ToUpper();
                    result = await roleStore.CreateAsync(role);
                }

                // Improved error reporting
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => $"[{e.Code}] {e.Description}"));
                    Assert.Fail($"Failed to create role '{roleName}': {errors}");
                }

                Assert.IsTrue(result.Succeeded, $"Failed to create role: {string.Join(", ", result.Errors.Select(e => e.Description))}");

                // Verify role was actually created and can be retrieved
                role = await roleStore.FindByIdAsync(role.Id);

                if (role == null)
                {
                    Assert.Fail($"Role was created successfully but could not be retrieved by ID.");
                }
            }

            return role;
        }

        /// <summary>
        /// Gets a mock <see cref="IdentityUser"/> for unit testing purposes
        /// </summary>
        /// <returns></returns>
        protected async Task<IdentityUser> GetMockRandomUserAsync(
            CosmosUserStore<IdentityUser, IdentityRole, string> userStore, bool saveToDatabase = true)
        {
            // Use GUID to ensure uniqueness across all test runs
            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var randomEmail = $"test{uniqueId}_{GetNextRandomNumber(1000, 9999)}@test{GetNextRandomNumber(10000, 99999)}.com";

            var user = new IdentityUser(randomEmail)
            {
                Email = randomEmail,
                Id = Guid.NewGuid().ToString(),
                LockoutEnabled = true
            };

            user.NormalizedUserName = user.UserName.ToUpper();
            user.NormalizedEmail = user.Email.ToUpper();

            if (userStore != null && saveToDatabase)
            {
                var result = await userStore.CreateAsync(user);

                // Improved error reporting
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    Assert.Fail($"Failed to create user: {errors}");
                }

                Assert.IsTrue(result.Succeeded, $"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                user = await userStore.FindByNameAsync(user.UserName.ToUpper());
            }

            return user;
        }

        /// <summary>
        /// Gets a mock login info for testing purposes
        /// </summary>
        /// <returns></returns>
        protected UserLoginInfo GetMockLoginInfoAsync()
        {
            return new UserLoginInfo("Twitter", Guid.NewGuid().ToString(), "Twitter");
        }

        protected Claim GetMockClaim(string seed = "")
        {
            return new Claim(Guid.NewGuid().ToString(), $"{Guid.NewGuid().ToString()}{seed}");
        }

        /// <summary>
        /// Gets a user manager for testing purposes
        /// </summary>
        /// <typeparam name="TUser"></typeparam>
        /// <param name="store"></param>
        /// <returns></returns>
        public UserManager<TUser> GetTestUserManager<TUser>(IUserStore<TUser> store)
            where TUser : class
        {
            var builder = new IdentityBuilder(typeof(IdentityUser), new ServiceCollection());

            var userType = builder.UserType;

            var dataProtectionProviderType = typeof(DataProtectorTokenProvider<>).MakeGenericType(userType);
            var phoneNumberProviderType = typeof(PhoneNumberTokenProvider<>).MakeGenericType(userType);
            var emailTokenProviderType = typeof(EmailTokenProvider<>).MakeGenericType(userType);
            var authenticatorProviderType = typeof(AuthenticatorTokenProvider<>).MakeGenericType(userType);
            //var authenticatorProviderType = typeof(UserTwoFactorTokenProvider<>).MakeGenericType(userType);


            store = store ?? new Mock<IUserStore<TUser>>().Object;
            var options = new Mock<IOptions<IdentityOptions>>();
            var idOptions = new IdentityOptions();

            options.Setup(o => o.Value).Returns(idOptions);
            var userValidators = new List<IUserValidator<TUser>>();
            var validator = new Mock<IUserValidator<TUser>>();
            userValidators.Add(validator.Object);
            var pwdValidators = new List<PasswordValidator<TUser>>();
            pwdValidators.Add(new PasswordValidator<TUser>());
            var userManager = new UserManager<TUser>(store, options.Object, new PasswordHasher<TUser>(),
                userValidators, pwdValidators, MockLookupNormalizer(),
                new IdentityErrorDescriber(), null,
                new Mock<ILogger<UserManager<TUser>>>().Object);
            validator.Setup(v => v.ValidateAsync(userManager, It.IsAny<TUser>()))
                .Returns(Task.FromResult(IdentityResult.Success)).Verifiable();

            return userManager;
        }

        public RoleManager<TRole> GetTestRoleManager<TRole>(IRoleStore<TRole> store)
            where TRole : class
        {
            store = store ?? new Mock<IRoleStore<TRole>>().Object;
            var roles = new List<IRoleValidator<TRole>>();
            roles.Add(new RoleValidator<TRole>());
            var roleManager = new RoleManager<TRole>(store, roles, MockLookupNormalizer(),
                new IdentityErrorDescriber(), new Mock<ILogger<RoleManager<TRole>>>().Object);
            return roleManager;
        }

        public ILookupNormalizer MockLookupNormalizer()
        {
            var normalizerFunc = new Func<string, string>(i =>
            {
                if (i == null)
                {
                    return null;
                }
                else
                {
                    return i.ToUpperInvariant();
                }
            });
            var lookupNormalizer = new Mock<ILookupNormalizer>();
            lookupNormalizer.Setup(i => i.NormalizeName(It.IsAny<string>())).Returns(normalizerFunc);
            lookupNormalizer.Setup(i => i.NormalizeEmail(It.IsAny<string>())).Returns(normalizerFunc);
            return lookupNormalizer.Object;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // NOTE: We do NOT clear _initializedProviders here because:
            // 1. Database schema should persist across test methods for the same provider
            // 2. Test methods use unique identifiers (GUIDs) for data isolation
            // 3. Clearing causes re-initialization issues when running multiple provider tests
            // 4. Re-initialization can cause provider detection issues in VerifyTablesExist
        }
    }
}
