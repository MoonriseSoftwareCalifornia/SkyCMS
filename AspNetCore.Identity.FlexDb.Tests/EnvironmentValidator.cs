using Microsoft.Extensions.Configuration;
using System.Data.Common;

namespace AspNetCore.Identity.CosmosDb.Tests.Net9
{
    /// <summary>
    /// Validates that all required environment variables and configuration values are present before tests run.
    /// </summary>
    [TestClass]
    public class EnvironmentValidator
    {

        /// <summary>
        /// Validates configuration before ANY test class runs.
        /// This uses [AssemblyInitialize] so it runs once per test assembly.
        /// </summary>
        [AssemblyInitialize]
        public static void ValidateEnvironmentVariables(TestContext context)
        {
            var configuration = TestUtilities.GetConfig();
            var missing = new List<string>();

            DeleteSqliteDatabaseIfConfigured(configuration, context);

            // At least one database connection must be configured
            var hasDatabase = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("CosmosDB")) ||
                             !string.IsNullOrWhiteSpace(configuration.GetConnectionString("SqlServer")) ||
                             !string.IsNullOrWhiteSpace(configuration.GetConnectionString("MySQL")) ||
                             !string.IsNullOrWhiteSpace(configuration.GetConnectionString("SQLite"));

            if (!hasDatabase)
            {
                missing.Add("At least ONE database connection (CosmosDB, SqlServer, MySQL, or SQLite)");
            }

            // Fail if any critical configuration is missing
            if (missing.Any())
            {
                var errorMessage = $"❌ Missing required configuration values:\n  - {string.Join("\n  - ", missing)}\n\n" +
                                  GetConfigurationInstructions();

                Assert.Inconclusive(errorMessage);
                return;
            }

            // Log success
            context.WriteLine("✅ All required environment variables are present");
            
            // Log configured providers
            var providers = TestUtilities.GetAvailableProviders();
            context.WriteLine($"✅ Configured database providers: {string.Join(", ", providers.Select(p => p.DisplayName))}");
        }

        private static string GetConfigurationInstructions()
        {
            return "These values should be configured in:\n" +
                   "  • User Secrets (for local development) - RECOMMENDED\n" +
                   "  • Environment Variables (for CI/CD)\n" +
                   "  • appsettings.json (not recommended for sensitive data)\n\n" +
                   "📖 Configuration Instructions:\n\n" +
                   "1️⃣  Initialize User Secrets:\n" +
                   "   dotnet user-secrets init --project AspNetCore.Identity.FlexDb.Tests\n\n" +
                   "2️⃣  Configure Required Values:\n" +
                   "   dotnet user-secrets set \"CosmosIdentityDbName\" \"localtests\" --project AspNetCore.Identity.FlexDb.Tests\n\n" +
                   "3️⃣  Configure Database Connection (at least ONE required):\n" +
                   "   # SQLite (recommended for local testing)\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:SQLite\" \"Data Source=test_identity.db\" --project AspNetCore.Identity.FlexDb.Tests\n\n" +
                   "   # OR Cosmos DB\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:CosmosDB\" \"AccountEndpoint=https://...;AccountKey=...;Database=localtests;\" --project AspNetCore.Identity.FlexDb.Tests\n\n" +
                   "   # OR SQL Server\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:SqlServer\" \"Server=tcp:...;Initial Catalog=...;User ID=...;Password=...\" --project AspNetCore.Identity.FlexDb.Tests\n\n" +
                   "   # OR MySQL\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:MySQL\" \"Server=...;Port=3306;Database=...;uid=...;pwd=...\" --project AspNetCore.Identity.FlexDb.Tests\n\n" +
                   "📚 See also: AspNetCore.Identity.FlexDb/README.md for detailed configuration";
        }

        private static void DeleteSqliteDatabaseIfConfigured(IConfigurationRoot configuration, TestContext context)
        {
            var sqliteProvider = TestUtilities.GetAvailableProviders()
                .FirstOrDefault(provider => provider.Provider == DatabaseProvider.Sqlite);

            if (sqliteProvider == null)
            {
                return;
            }

            var sqlitePath = TryGetSqliteDatabasePath(sqliteProvider.ConnectionString);

            if (string.IsNullOrWhiteSpace(sqlitePath))
            {
                context.WriteLine("SQLite configured, but database path could not be resolved.");
                return;
            }

            if (!Path.IsPathRooted(sqlitePath))
            {
                sqlitePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, sqlitePath));
            }

            if (!File.Exists(sqlitePath))
            {
                return;
            }

            File.Delete(sqlitePath);
            context.WriteLine($"Deleted SQLite database: {sqlitePath}");
        }

        private static string? TryGetSqliteDatabasePath(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return null;
            }

            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            var keys = new[] { "Data Source", "DataSource", "Filename", "FileName" };

            foreach (var key in keys)
            {
                if (!builder.TryGetValue(key, out var value))
                {
                    continue;
                }

                var pathValue = value?.ToString();

                if (string.IsNullOrWhiteSpace(pathValue))
                {
                    continue;
                }

                if (string.Equals(pathValue, ":memory:", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return pathValue;
            }

            return null;
        }
    }
}
