using AspNetCore.Identity.FlexDb;
using AspNetCore.Identity.FlexDb.Containers;
using AspNetCore.Identity.FlexDb.Repositories;
using AspNetCore.Identity.FlexDb.Stores;
using Microsoft.AspNetCore.Identity;
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
            var databaseName = config["CosmosIdentityDbName"] ?? "localtests";

            // Check for CosmosDb
            var cosmosConnection = config.GetConnectionString("CosmosDb");
            if (!string.IsNullOrEmpty(cosmosConnection))
            {
                providers.Add(new TestDatabaseProvider(DatabaseProvider.CosmosDb, cosmosConnection, databaseName));
            }

            // Check for SQL Server
            var sqlConnection = config.GetConnectionString("SqlServer");
            if (!string.IsNullOrEmpty(sqlConnection))
            {
                providers.Add(new TestDatabaseProvider(DatabaseProvider.SqlServer, sqlConnection, databaseName));
            }

            // Check for MySQL
            var mysqlConnection = config.GetConnectionString("MySql");
            if (!string.IsNullOrEmpty(mysqlConnection))
            {
                providers.Add(new TestDatabaseProvider(DatabaseProvider.MySql, mysqlConnection, databaseName));
            }

            // Check for SQLite
            var sqliteConnection = config.GetConnectionString("Sqlite");
            if (!string.IsNullOrEmpty(sqliteConnection))
            {
                providers.Add(new TestDatabaseProvider(DatabaseProvider.Sqlite, sqliteConnection, databaseName));
            }

            // If no providers configured, tests will be skipped (this is intentional - tests require database setup)
            if (providers.Count == 0)
            {
                Console.WriteLine("??  WARNING: No database providers configured for FlexDb tests.");
                Console.WriteLine("   Tests will not run. Configure at least one connection string:");
                Console.WriteLine("   - CosmosDb, SqlServer, MySql, or Sqlite");
                Console.WriteLine("   via appsettings.json, user secrets, or environment variables.");
            }

            return providers;
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
        /// <param name="databaseName">Database name (used for Cosmos DB, ignored for other providers)</param>
        /// <returns></returns>
        public DbContextOptions GetDbOptions(string connectionString, string databaseName)
        {
            // For Cosmos DB, append database name to connection string if not already present
            if (connectionString.Contains("AccountEndpoint=", StringComparison.InvariantCultureIgnoreCase) &&
                !connectionString.Contains("Database=", StringComparison.InvariantCultureIgnoreCase))
            {
                connectionString = $"{connectionString.TrimEnd(';')};Database={databaseName}";
            }

            return CosmosDbOptionsBuilder.GetDbOptions<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>>(connectionString);
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
        /// <param name="databaseName">Database name (used for Cosmos DB)</param>
        /// <param name="backwardCompatibility">Enable backward compatibility for Cosmos DB EF 8 databases</param>
        /// <returns></returns>
        public CosmosIdentityDbContext<IdentityUser, IdentityRole, string> GetDbContext(
            string connectionString, string databaseName, bool backwardCompatibility = false)
        {                       
            var dbContext =
                new CosmosIdentityDbContext<IdentityUser, IdentityRole, string>(GetDbOptions(connectionString, databaseName), backwardCompatibility);
            return dbContext;
        }

        /// <summary>
        /// Get an instance of the user store.
        /// </summary>
        /// <param name="connectionString">Connection string for any supported provider</param>
        /// <param name="databaseName">Database name (used for Cosmos DB)</param>
        /// <returns></returns>
        public CosmosUserStore<IdentityUser, IdentityRole, string> GetUserStore(string connectionString, string databaseName)
        {
            var repository =
                new CosmosIdentityRepository<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>, IdentityUser,
                    IdentityRole, string>(GetDbContext(connectionString, databaseName));
            var userStore = new CosmosUserStore<IdentityUser, IdentityRole, string>(repository);
            return userStore;
        }

        /// <summary>
        /// Get an instance of the role store
        /// </summary>
        /// <param name="connectionString">Connection string for any supported provider</param>
        /// <param name="databaseName">Database name (used for Cosmos DB)</param>
        /// <returns></returns>
        public CosmosRoleStore<IdentityUser, IdentityRole, string> GetRoleStore(string connectionString, string databaseName)
        {
            var repository =
                new CosmosIdentityRepository<CosmosIdentityDbContext<IdentityUser, IdentityRole, string>, IdentityUser,
                    IdentityRole, string>(GetDbContext(connectionString, databaseName));
            var rolestore = new CosmosRoleStore<IdentityUser, IdentityRole, string>(repository);
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
            var userStore = GetRoleStore(connectionString, databaseName);
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
