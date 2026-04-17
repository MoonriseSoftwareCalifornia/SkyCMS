using AspNetCore.Identity.CosmosDb.Containers;
using AspNetCore.Identity.CosmosDb.Repositories;
using AspNetCore.Identity.CosmosDb.Stores;
using AspNetCore.Identity.FlexDb;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace AspNetCore.Identity.CosmosDb.Tests.Net9
{
    public class TestUtilities
    {
        /// <summary>
        /// Non-normalized email address for user 1
        /// </summary>
        public const string IDENUSER1EMAIL = "Foo1@acme.com";

        /// <summary>
        /// Non-normalized email address for user 2
        /// </summary>
        public const string IDENUSER2EMAIL = "Foo2@acme.com";

        public const string IDENUSER1ID = "507b7565-493e-49d7-94c7-d60e21036b4a";

        public const string IDENUSER2ID = "55250c6f-7c91-465a-a9ce-ea9bbe6caf81";

        /// <summary>
        /// Gets all available test database providers from configuration
        /// </summary>
        /// <returns>List of available providers with their connection strings</returns>
        public static List<TestDatabaseProvider> GetAvailableProviders()
        {
            var config = GetConfig();
            var providers = new List<TestDatabaseProvider>();

            // Check for CosmosDb – verify the account is reachable
            var cosmosConnection = config.GetConnectionString("CosmosDb");
            if (!string.IsNullOrEmpty(cosmosConnection))
            {
                if (CanConnectCosmos(cosmosConnection))
                {
                    providers.Add(new TestDatabaseProvider(DatabaseProvider.CosmosDb, cosmosConnection));
                }
                else
                {
                    Console.WriteLine("[PROVIDER] Skipping CosmosDb – connectivity check failed.");
                }
            }

            // Check for SQL Server – ensure a short connect timeout so tests don't hang
            // when the server is unreachable.
            var sqlConnection = config.GetConnectionString("SqlServer");
            if (!string.IsNullOrEmpty(sqlConnection))
            {
                // Always force a short timeout for the connectivity check so tests don't hang
                // when the server is unreachable, even if the connection string already has a timeout.
                var sqlBuilder = new SqlConnectionStringBuilder(sqlConnection)
                {
                    ConnectTimeout = 5
                };
                var sqlCheckConnection = sqlBuilder.ConnectionString;

                if (CanConnectRelational(sqlCheckConnection))
                {
                    providers.Add(new TestDatabaseProvider(DatabaseProvider.SqlServer, sqlConnection));
                }
                else
                {
                    Console.WriteLine("[PROVIDER] Skipping SqlServer – connectivity check failed.");
                }
            }

            // Check for MySQL – ensure a short connect timeout so tests don't hang
            var mysqlConnection = config.GetConnectionString("MySql");
            if (!string.IsNullOrEmpty(mysqlConnection))
            {
                if (!mysqlConnection.Contains("Connection Timeout", StringComparison.OrdinalIgnoreCase)
                    && !mysqlConnection.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase))
                {
                    mysqlConnection = mysqlConnection.TrimEnd(';') + ";Connection Timeout=5;";
                }

                if (CanConnectRelational(mysqlConnection, useMySql: true))
                {
                    providers.Add(new TestDatabaseProvider(DatabaseProvider.MySql, mysqlConnection));
                }
                else
                {
                    Console.WriteLine("[PROVIDER] Skipping MySql – connectivity check failed.");
                }
            }

            // Check for SQLite (always available if configured – file-based)
            var sqliteConnection = config.GetConnectionString("Sqlite");
            if (!string.IsNullOrEmpty(sqliteConnection))
            {
                providers.Add(new TestDatabaseProvider(DatabaseProvider.Sqlite, sqliteConnection));
            }

            // If no providers configured, tests will be skipped (this is intentional - tests require database setup)
            if (providers.Count == 0)
            {
                Console.WriteLine("⚠  WARNING: No database providers configured for FlexDb tests.");
                Console.WriteLine("   Tests will not run. Configure at least one connection string:");
                Console.WriteLine("   - CosmosDb, SqlServer, MySql, or Sqlite");
                Console.WriteLine("   via appsettings.json, user secrets, or environment variables.");
            }

            return providers;
        }

        /// <summary>
        /// Attempts a lightweight SQL connection to verify the database is reachable.
        /// </summary>
        private static bool CanConnectRelational(string connectionString, bool useMySql = false)
        {
            try
            {
                using var conn = useMySql
                    ? (System.Data.Common.DbConnection)new MySqlConnector.MySqlConnection(connectionString)
                    : new SqlConnection(connectionString);
                conn.Open();
                conn.Close();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROVIDER] Connectivity check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts a lightweight Cosmos DB connectivity check by reading the account.
        /// </summary>
        private static bool CanConnectCosmos(string connectionString)
        {
            try
            {
                using var client = new Microsoft.Azure.Cosmos.CosmosClient(connectionString);
                client.ReadAccountAsync().GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROVIDER] Cosmos connectivity check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the configuration
        /// </summary>
        /// <returns></returns>
        public static IConfigurationRoot GetConfig()
        {
            // the type specified here is just so the secrets library can 
            // find the UserSecretId we added in the csproj file
            var jsonConfig = Path.Combine(Environment.CurrentDirectory, "appsettings.json");

            var builder = new ConfigurationBuilder()
                .AddJsonFile(jsonConfig, true)
                .AddEnvironmentVariables() // Added to read environment variables from GitHub Actions
                .AddUserSecrets(Assembly.GetExecutingAssembly(), true); // User secrets override all - put here

            return Retry.Do(() => builder.Build(), TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Gets the value of a configuration key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetKeyValue(string key)
        {
            return GetKeyValue(GetConfig(), key);
        }

        private static string GetKeyValue(IConfigurationRoot config, string key)
        {
            var data = config[key];

            if (string.IsNullOrEmpty(data))
            {
                // First attempt to get the value of the key as named.
                data = Environment.GetEnvironmentVariable(key);

                if (string.IsNullOrEmpty(data))
                {
                    // For Github Actions, secrets are forced upper case
                    data = Environment.GetEnvironmentVariable(key.ToUpper());
                }

                // Connection string maybe?
                if (string.IsNullOrEmpty(data))
                {
                    data = config.GetConnectionString(key);
                }

                // Connection all caps string maybe?
                if (string.IsNullOrEmpty(data))
                {
                    data = config.GetConnectionString(key.ToUpper());
                }
            }

            return string.IsNullOrEmpty(data) ? string.Empty : data;
        }

        /// <summary>
        /// Get DB Options using CosmosDbOptionsBuilder to automatically detect provider
        /// </summary>
        /// <param name="connectionString">Connection string for any supported provider (Cosmos DB, SQL Server, MySQL, SQLite)</param>
        /// <param name="databaseName">Database name applied to the connection string for all providers</param>
        /// <returns></returns>
        public DbContextOptions GetDbOptions(string connectionString, string databaseName)
        {
            Console.WriteLine($"[DEBUG-GetDbOptions] Called with connection string (first 50 chars): {connectionString.Substring(0, Math.Min(50, connectionString.Length))}");
            Console.WriteLine($"[DEBUG-GetDbOptions] Database name parameter: {databaseName}");

            // For Cosmos DB, ensure we use the specified databaseName parameter
            // Remove any existing Database= from connection string and append the correct one
            if (connectionString.Contains("AccountEndpoint=", StringComparison.InvariantCultureIgnoreCase))
            {
                // Parse and rebuild connection string with correct database name
                var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => !p.TrimStart().StartsWith("Database=", StringComparison.InvariantCultureIgnoreCase))
                    .ToList();

                connectionString = string.Join(";", parts).TrimEnd(';') + $";Database={databaseName};";
                Console.WriteLine($"[DEBUG-GetDbOptions] Rebuilt Cosmos connection string with database: {databaseName}");
            }
            // For SQL Server, replace the Initial Catalog with the specified databaseName
            // so tests use an isolated database instead of the one hardcoded in the connection string
            else if (connectionString.Contains("Initial Catalog=", StringComparison.InvariantCultureIgnoreCase))
            {
                var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.TrimStart().StartsWith("Initial Catalog=", StringComparison.InvariantCultureIgnoreCase)
                        ? $"Initial Catalog={databaseName}"
                        : p)
                    .ToList();

                connectionString = string.Join(";", parts);
                Console.WriteLine($"[DEBUG-GetDbOptions] Rebuilt SQL Server connection string with database: {databaseName}");
            }

            Console.WriteLine($"[DEBUG-GetDbOptions] Calling CosmosDbOptionsBuilder.GetDbOptions...");
            var options = CosmosDbOptionsBuilder.GetDbOptions<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>>(connectionString);
            Console.WriteLine($"[DEBUG-GetDbOptions] Options created successfully");
            return options;
        }

        /// <summary>
        /// Gets an instance of the container utilities (Cosmos DB only)
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        public ContainerUtilities? GetContainerUtilities(string connectionString, string databaseName)
        {
            // Container utilities are only for Cosmos DB
            if (!connectionString.Contains("AccountEndpoint=", StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var utilities = new ContainerUtilities(connectionString, databaseName);
            return utilities;
        }

        /// <summary>
        /// Get an instance of the Identity DB context.
        /// </summary>
        /// <param name="connectionString">Connection string for any supported provider</param>
        /// <param name="backwardCompatibility">Enable backward compatibility for Cosmos DB EF 8 databases</param>
        /// <returns></returns>
        public CosmosIdentityDbContext<IdentityUser, IdentityRole, string> GetDbContext(
            string connectionString, bool backwardCompatibility = false)
        {
            var builder =
                CosmosDbOptionsBuilder.GetDbOptionsBuilder<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>>(connectionString);
            var dbContext =
                new CosmosIdentityDbContext<IdentityUser, IdentityRole, string>(builder.Options, backwardCompatibility);
            return dbContext;
        }

        /// <summary>
        /// Get an instance of the user store.
        /// </summary>
        /// <param name="connectionString">Connection string for any supported provider</param>
        /// <param name="databaseName">Database name (used for Cosmos DB)</param>
        /// <returns></returns>
        public FlexDbUserStore<IdentityUser, IdentityRole, string> GetUserStore(string connectionString)
        {
            var repository =
                new CosmosIdentityRepository<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>, IdentityUser,
                    IdentityRole, string>(GetDbContext(connectionString));
            var userStore = new FlexDbUserStore<IdentityUser, IdentityRole, string>(repository);
            return userStore;
        }

        /// <summary>
        /// Get an instance of the role store
        /// </summary>
        /// <param name="connectionString">Connection string for any supported provider</param>
        /// <param name="databaseName">Database name (used for Cosmos DB)</param>
        /// <returns></returns>
        public FlexDbRoleStore<IdentityRole, string> GetRoleStore(string connectionString)
        {
            var repository =
                new CosmosIdentityRepository<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>, IdentityUser,
                    IdentityRole, string>(GetDbContext(connectionString));
            var rolestore = new FlexDbRoleStore<IdentityRole, string>(repository);
            return rolestore;
        }

        /// <summary>
        /// Get an instance of the role manager
        /// </summary>
        /// <param name="connectionString">Connection string for any supported provider</param>
        /// <param name="databaseName">Database name (used for Cosmos DB)</param>
        /// <returns></returns>
        public RoleManager<IdentityRole> GetRoleManager(string connectionString, string databaseName)
        {
            var userStore = GetRoleStore(connectionString);
            var userManager =
                new RoleManager<IdentityRole>(userStore, null, null, null, GetLogger<RoleManager<IdentityRole>>());
            return userManager;
        }

        /// <summary>
        /// Get a mock logger
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public ILogger<T> GetLogger<T>()
        {
            return new Logger<T>(new NullLoggerFactory());
        }
    }
}
