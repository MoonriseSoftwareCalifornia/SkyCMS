using Cosmos.BlobService;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Sky.Tests
{
    // Runs once for the entire Sky.Tests assembly, before any [TestClass] executes.
    [TestClass]
    public class AssemblyHooks
    {
        [AssemblyInitialize]
        public static void GlobalInitialize(TestContext context)
        {
            // One-time setup for all tests in this assembly.
            // Example: configure environment, logging, seed static data, etc.
            // Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            // Validate environment variables - halt all tests if validation fails
            //if (!ValidateEnvironmentVariables(context))
            //{
            //    // Abort further initialization if environment is invalid
            //    context.WriteLine("Environment variables are invalid. Aborting further tests.");
            //    return;
            //}

            // Lightweight configuration (all in-memory).
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets(typeof(SkyCmsTestBase).Assembly, optional: true)
                .AddInMemoryCollection()
                .AddEnvironmentVariables()
                .Build();

            // Provide a safe fallback for storage if no connection string is configured.
            var storageConnectionString = configuration.GetConnectionString("StorageConnectionString")
                                         ?? "UseDevelopmentStorage=true;";

            var cache = new MemoryCache(new MemoryCacheOptions());

            var storage = new StorageContext(configuration, cache, null);

            try
            {
                var task = storage.DeleteFolderAsync("/");
                task.Wait();
            }
            catch (Exception ex)
            {
                // Ignore cleanup errors - tests should still run
                context?.WriteLine($"Warning: Failed to clean storage during initialization: {ex.Message}");
            }
        }

        [AssemblyCleanup]
        public static void GlobalCleanup()
        {
            // One-time teardown after all tests in this assembly have run.
        }

        private static readonly string[] RequiredVariables = new[]
{
            // ✅ Core Application Settings
            "AdminEmail",
            
            // ✅ Database Connection Strings (at least ONE is required)
            // These are checked separately below
            
            // ✅ Storage Connection Strings (at least ONE is required)
            // "ConnectionStrings:StorageConnectionString" OR one of the provider-specific strings below
             
        };

        private static readonly string[] OptionalVariables = new[]
        {
            // Storage Providers (at least one should be configured)
            "ConnectionStrings:StorageConnectionString",
            "ConnectionStrings:AzureBlobStorageConnectionString",
            "ConnectionStrings:AmazonS3ConnectionString",
            "ConnectionStrings:CloudflareR2ConnectionString",
            
            // Database Providers (at least one should be configured)
            "ConnectionStrings:CosmosDB",
            "ConnectionStrings:SqlServer",
            "ConnectionStrings:MySQL",
            "ConnectionStrings:SQLite",
            
            // Test Settings (optional with defaults)
            "TestSettings:UseCloudDatabases",
            "TestSettings:SkipSlowTests",
            "TestSettings:CleanupAfterTests",
            
            // CDN Integration Tests (optional)
            "CdnIntegrationTests:Cloudflare:ApiToken",
            "CdnIntegrationTests:Cloudflare:ZoneId",
            "CdnIntegrationTests:Cloudflare:TestDomain",
            "CdnIntegrationTests:Azure:SubscriptionId",
            "CdnIntegrationTests:Azure:ResourceGroup",
            "CdnIntegrationTests:Azure:ProfileName",
            "CdnIntegrationTests:Azure:EndpointName",
            "CdnIntegrationTests:AzureFrontDoor:SubscriptionId",
            "CdnIntegrationTests:AzureFrontDoor:ResourceGroup",
            "CdnIntegrationTests:AzureFrontDoor:ProfileName",
            "CdnIntegrationTests:AzureFrontDoor:EndpointName",
            "CdnIntegrationTests:Sucuri:ApiKey",
            "CdnIntegrationTests:Sucuri:ApiSecret",
            
            // SendGrid Email (optional)
            "CosmosSendGridApiKey",
            
            // Microsoft OAuth (optional)
            "MicrosoftOAuth:ClientId",
            "MicrosoftOAuth:ClientSecret",
            "AzureAD:ClientId",
            "AzureAD:ClientSecret"
        };

        /// <summary>
        /// Validates configuration before ANY test class runs.
        /// This uses [AssemblyInitialize] so it runs once per test assembly.
        /// </summary>
        public static bool ValidateEnvironmentVariables(TestContext context)
        {
            var configuration = GetConfiguration();
            var missing = new List<string>();
            var warnings = new List<string>();

            context.WriteLine("🔍 Validating Configuration...");

            // Validate required variables
            foreach (var key in RequiredVariables)
            {
                var value = configuration[key];
                context.WriteLine($"   Checking '{key}': {(string.IsNullOrWhiteSpace(value) ? "❌ MISSING" : "✅ Found")}");
                if (string.IsNullOrWhiteSpace(value))
                {
                    missing.Add(key);
                }
            }

            // Check that at least ONE database connection is configured
            context.WriteLine("   Checking database connections...");
            var cosmosDb = configuration.GetConnectionString("CosmosDB");
            var sqlServer = configuration.GetConnectionString("SqlServer");
            var mysql = configuration.GetConnectionString("MySQL");
            var sqlite = configuration.GetConnectionString("SQLite");

            context.WriteLine($"      CosmosDB: {(!string.IsNullOrWhiteSpace(cosmosDb) ? "✅ Configured" : "❌ Not configured")}");
            context.WriteLine($"      SqlServer: {(!string.IsNullOrWhiteSpace(sqlServer) ? "✅ Configured" : "❌ Not configured")}");
            context.WriteLine($"      MySQL: {(!string.IsNullOrWhiteSpace(mysql) ? "✅ Configured" : "❌ Not configured")}");
            context.WriteLine($"      SQLite: {(!string.IsNullOrWhiteSpace(sqlite) ? "✅ Configured" : "❌ Not configured")}");

            var hasDatabaseConnection =
                !string.IsNullOrWhiteSpace(cosmosDb) ||
                !string.IsNullOrWhiteSpace(sqlServer) ||
                !string.IsNullOrWhiteSpace(mysql) ||
                !string.IsNullOrWhiteSpace(sqlite);

            if (!hasDatabaseConnection)
            {
                missing.Add("At least ONE database connection (CosmosDB, SqlServer, MySQL, or SQLite)");
            }

            // Check that at least ONE storage connection is configured
            context.WriteLine("   Checking storage connections...");
            var storageDefault = configuration.GetConnectionString("StorageConnectionString");
            var azureBlob = configuration.GetConnectionString("AzureBlobStorageConnectionString");
            var amazonS3 = configuration.GetConnectionString("AmazonS3ConnectionString");
            var cloudflareR2 = configuration.GetConnectionString("CloudflareR2ConnectionString");

            context.WriteLine($"      StorageConnectionString: {(!string.IsNullOrWhiteSpace(storageDefault) ? "✅ Configured" : "❌ Not configured")}");
            context.WriteLine($"      AzureBlobStorageConnectionString: {(!string.IsNullOrWhiteSpace(azureBlob) ? "✅ Configured" : "❌ Not configured")}");
            context.WriteLine($"      AmazonS3ConnectionString: {(!string.IsNullOrWhiteSpace(amazonS3) ? "✅ Configured" : "❌ Not configured")}");
            context.WriteLine($"      CloudflareR2ConnectionString: {(!string.IsNullOrWhiteSpace(cloudflareR2) ? "✅ Configured" : "❌ Not configured")}");

            var hasStorageConnection =
                !string.IsNullOrWhiteSpace(storageDefault) ||
                !string.IsNullOrWhiteSpace(azureBlob) ||
                !string.IsNullOrWhiteSpace(amazonS3) ||
                !string.IsNullOrWhiteSpace(cloudflareR2);

            if (!hasStorageConnection)
            {
                missing.Add("At least ONE storage connection (StorageConnectionString, AzureBlobStorageConnectionString, AmazonS3ConnectionString, or CloudflareR2ConnectionString)");
            }

            // Check optional variables for warnings
            foreach (var key in OptionalVariables)
            {
                var value = configuration[key];
                if (string.IsNullOrWhiteSpace(value))
                {
                    warnings.Add(key);
                }
            }

            // Report warnings (but don't fail)
            if (warnings.Any())
            {
                context.WriteLine("⚠️  Optional configuration values not set:");
                foreach (var warning in warnings)
                {
                    context.WriteLine($"   - {warning}");
                }
                context.WriteLine("   Some tests may be skipped if they require these values.");
            }

            // Fail if any critical configuration is missing
            if (missing.Any())
            {
                var errorMessage = $"❌ Missing required configuration values:\n  - {string.Join("\n  - ", missing)}\n\n" +
                                  GetConfigurationInstructions();

                context.WriteLine(errorMessage);
                Assert.Inconclusive(errorMessage);
                return false;
            }

            // Log success
            context.WriteLine("✅ All required environment variables are present");

            // Log configured providers
            LogConfiguredProviders(configuration, context);
            return true;
        }

        private static IConfigurationRoot GetConfiguration()
        {
            var jsonConfig = Path.Combine(Environment.CurrentDirectory, "appsettings.json");

            var builder = new ConfigurationBuilder()
                .AddJsonFile(jsonConfig, optional: true)
                .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            // Add debug output to see what's being loaded
            Console.WriteLine("🔍 Configuration Debug Information:");
            Console.WriteLine($"   Current Directory: {Environment.CurrentDirectory}");
            Console.WriteLine($"   appsettings.json exists: {File.Exists(jsonConfig)}");
            Console.WriteLine($"   Environment Variables loaded: {Environment.GetEnvironmentVariables().Count}");

            return configuration;
        }

        private static string GetConfigurationInstructions()
        {
            return "These values should be configured in:\n" +
                   "  • User Secrets (for local development) - RECOMMENDED\n" +
                   "  • Environment Variables (for CI/CD)\n" +
                   "  • appsettings.json (not recommended for sensitive data)\n\n" +
                   "📖 Configuration Instructions:\n\n" +
                   "1️⃣  Initialize User Secrets:\n" +
                   "   dotnet user-secrets init --project Tests\n\n" +
                   "2️⃣  Configure Required Application Settings:\n" +
                   "   dotnet user-secrets set \"AdminEmail\" \"admin@example.com\" --project Tests\n" +
                   "3️⃣  Configure Database Connection (choose at least ONE):\n" +
                   "   # SQLite (recommended for testing)\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:SQLite\" \"Data Source=:memory:;Mode=Memory;Cache=Shared;\" --project Tests\n\n" +
                   "   # OR Cosmos DB\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:CosmosDB\" \"AccountEndpoint=...;AccountKey=...;Database=...\" --project Tests\n\n" +
                   "   # OR SQL Server\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:SqlServer\" \"Server=...;Database=...\" --project Tests\n\n" +
                   "   # OR MySQL\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:MySQL\" \"Server=...;Port=3306;Database=...\" --project Tests\n\n" +
                   "4️⃣  Configure Storage Connection (choose at least ONE):\n" +
                   "   # Azure Blob Storage (recommended)\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:StorageConnectionString\" \"DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...\" --project Tests\n\n" +
                   "   # OR Azure Blob Storage (explicit)\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:AzureBlobStorageConnectionString\" \"DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...\" --project Tests\n\n" +
                   "   # OR Amazon S3\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:AmazonS3ConnectionString\" \"Bucket=...;Region=...;KeyId=...;Key=...\" --project Tests\n\n" +
                   "   # OR Cloudflare R2\n" +
                   "   dotnet user-secrets set \"ConnectionStrings:CloudflareR2ConnectionString\" \"AccountId=...;Bucket=...;KeyId=...;Key=...\" --project Tests\n\n" +
                   "5️⃣  Configure Test Settings (optional):\n" +
                   "   dotnet user-secrets set \"TestSettings:UseCloudDatabases\" \"false\" --project Tests\n" +
                   "   dotnet user-secrets set \"TestSettings:SkipSlowTests\" \"false\" --project Tests\n" +
                   "   dotnet user-secrets set \"TestSettings:CleanupAfterTests\" \"true\" --project Tests\n\n" +
                   "6️⃣  Configure CDN Integration Tests (optional):\n" +
                   "   # Cloudflare\n" +
                   "   dotnet user-secrets set \"CdnIntegrationTests:Cloudflare:ApiToken\" \"your-token\" --project Tests\n" +
                   "   dotnet user-secrets set \"CdnIntegrationTests:Cloudflare:ZoneId\" \"your-zone-id\" --project Tests\n" +
                   "   dotnet user-secrets set \"CdnIntegrationTests:Cloudflare:TestDomain\" \"www.example.com\" --project Tests\n\n" +
                   "📚 See also:\n" +
                   "   • Docs/DatabaseConfig.md - Database configuration guide\n" +
                   "   • Docs/StorageConfig.md - Storage configuration guide\n" +
                   "   • Tests/README.md - Test suite documentation";
        }

        private static void LogConfiguredProviders(IConfiguration configuration, TestContext context)
        {
            context.WriteLine("\n📊 Configured Providers:");

            // Database Providers
            var databases = new List<string>();
            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("CosmosDB")))
                databases.Add("Cosmos DB");
            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("SqlServer")))
                databases.Add("SQL Server");
            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("MySQL")))
                databases.Add("MySQL");
            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("SQLite")))
                databases.Add("SQLite");

            context.WriteLine($"   Databases: {(databases.Any() ? string.Join(", ", databases) : "None configured")}");

            // Storage Providers
            var storage = new List<string>();
            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("StorageConnectionString")))
                storage.Add("Storage (default)");
            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("AzureBlobStorageConnectionString")))
                storage.Add("Azure Blob");
            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("AmazonS3ConnectionString")))
                storage.Add("Amazon S3");
            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("CloudflareR2ConnectionString")))
                storage.Add("Cloudflare R2");

            context.WriteLine($"   Storage: {(storage.Any() ? string.Join(", ", storage) : "None configured")}");

            // Multi-Tenant
            var isMultiTenant = false;
            context.WriteLine($"   Multi-Tenant: {(isMultiTenant ? "Enabled" : "Disabled")}");

            // CDN Tests
            var hasCloudflareCdn = !string.IsNullOrWhiteSpace(configuration["CdnIntegrationTests:Cloudflare:ApiToken"]);
            if (hasCloudflareCdn)
            {
                context.WriteLine("   CDN Tests: Cloudflare configured");
            }
        }
    }
}
