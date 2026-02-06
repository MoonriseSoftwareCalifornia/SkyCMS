using AspNetCore.Identity.FlexDb;
using AspNetCore.Identity.FlexDb.Stores;
using Microsoft.AspNetCore.Identity;
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
        private static readonly Dictionary<string, bool> _initializedProviders = new Dictionary<string, bool>();
        private static readonly object _initLock = new object();

        protected static void InitializeClass(string connectionString, string databaseName, bool backwardCompatibility = false)
        {
            //
            // Setup context.
            //
            _testUtilities = new TestUtilities();
            _random = new Random();

            // Create a unique key for this provider configuration
            var providerKey = $"{connectionString}_{databaseName}";

            lock (_initLock)
            {
                // Only initialize once per provider configuration
                if (_initializedProviders.ContainsKey(providerKey))
                {
                    return;
                }

                // Create fresh database with a new context
                using (var dbContext = _testUtilities.GetDbContext(connectionString, databaseName, backwardCompatibility: backwardCompatibility))
                {
                    _ = dbContext.Database.EnsureCreatedAsync().Result;
                }


                // Verify tables were created with yet another fresh context
                using (var dbContext = _testUtilities.GetDbContext(connectionString, databaseName, backwardCompatibility: backwardCompatibility))
                {
                    // MySQL can take longer to finalize schema, so use more retries with longer delays
                    VerifyTablesExist(dbContext);
                }

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
            // Verify by attempting to query each DbSet
            var usersExist = dbContext.Users.CountAsync().Result;
            var rolesExist = dbContext.Roles.CountAsync().Result;
            var userRolesExist = dbContext.UserRoles.CountAsync().Result;
            var userClaimsExist = dbContext.UserClaims.CountAsync().Result;
            var roleClaimsExist = dbContext.RoleClaims.CountAsync().Result;
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
            InitializeClass(provider.ConnectionString, provider.DatabaseName, backwardCompatibility);
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
            CosmosRoleStore<IdentityUser, IdentityRole, string> roleStore, bool saveToDatabase = true)
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
            // Clear the initialization cache to force database recreation on next test
            // This ensures test isolation
            lock (_initLock)
            {

                _initializedProviders.Clear();
            }
        }
    }
}
