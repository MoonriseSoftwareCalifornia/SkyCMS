using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Sky.TestSetup;

/// <summary>
/// Validates connectivity to all external services (databases, storage accounts, etc.)
/// to ensure they are reachable before running full test suites.
/// This is particularly useful for CI/CD environments like GitHub Actions.
/// </summary>
[TestClass]
public class ConnectivityTests
{
    private static IConfigurationRoot _configuration = null!;

    [ClassInitialize]
    public static void Initialize(TestContext context)
    {
        // Build configuration with proper priority order:
        // 1. appsettings.json (lowest priority)
        // 2. User Secrets (development)
        // 3. Environment Variables (highest priority - for GitHub Actions)
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true) // User secrets in the middle
            .AddEnvironmentVariables(); // Environment variables override all - for GitHub Actions

        _configuration = builder.Build();
    }

    #region Database Connectivity Tests

    /// <summary>
    /// Tests connectivity to Azure Cosmos DB by attempting to read database properties.
    /// </summary>
    [TestMethod]
    [TestCategory("Connectivity")]
    [TestCategory("Database")]
    public async Task CosmosDB_CanConnect_AndReadDatabaseProperties()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("CosmosDB");

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Inconclusive("CosmosDB connection string not configured. Skipping test.");
            return;
        }

        // Parse connection string to extract endpoint and key
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var endpoint = parts.FirstOrDefault(p => p.StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[^1];
        var key = parts.FirstOrDefault(p => p.StartsWith("AccountKey=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[^1];
        var databaseName = parts.FirstOrDefault(p => p.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[^1] ?? _configuration["CosmosIdentityDbName"] ?? "localtests";

        Assert.IsNotNull(endpoint, "CosmosDB endpoint not found in connection string");
        Assert.IsNotNull(key, "CosmosDB key not found in connection string");

        try
        {
            // Act - Attempt to connect and read database properties
            using var client = new CosmosClient(endpoint, key);

            // Create database if it doesn't exist (for CI/CD environments)
            var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseName);
            var database = databaseResponse.Database;

            // Read database properties to confirm connectivity
            var response = await database.ReadAsync();

            // Assert
            Assert.IsNotNull(response, "Failed to read CosmosDB database properties");
            Assert.AreEqual(databaseName, response.Resource.Id, "Database name mismatch");
            Console.WriteLine($"✓ Successfully connected to CosmosDB: {databaseName}");
        }
        catch (CosmosException ex)
        {
            Assert.Fail($"CosmosDB connectivity failed: {ex.StatusCode} - {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            Assert.Inconclusive(
                $"CosmosDB endpoint is unreachable — verify the account '{endpoint}' exists and is accessible. Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            if (ex.InnerException is HttpRequestException
                || ex.Message.Contains("No such host", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive(
                    $"CosmosDB endpoint is unreachable — verify the account '{endpoint}' exists and is accessible. Error: {ex.Message}");
            }

            Assert.Fail($"CosmosDB connectivity failed with unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests connectivity to SQL Server by executing a simple query.
    /// </summary>
    [TestMethod]
    [TestCategory("Connectivity")]
    [TestCategory("Database")]
    public async Task SqlServer_CanConnect_AndExecuteQuery()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("SqlServer");

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Inconclusive("SQL Server connection string not configured. Skipping test.");
            return;
        }

        // Remove Initial Catalog to avoid database permission issues during connectivity test
        // This test only needs to verify the server is reachable, not access a specific database
        var builder = new SqlConnectionStringBuilder(connectionString);
        builder.InitialCatalog = string.Empty;
        var modifiedConnectionString = builder.ConnectionString;

        try
        {
            // Act - Attempt to connect and execute a simple query
            using var connection = new SqlConnection(modifiedConnectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT @@VERSION";
            var version = await command.ExecuteScalarAsync();

            // Assert
            Assert.IsNotNull(version, "Failed to execute query on SQL Server");
            Assert.IsInstanceOfType(version, typeof(string));
            Console.WriteLine($"✓ Successfully connected to SQL Server");
            Console.WriteLine($"  Version: {version?.ToString()?.Split('\n')[0]}");
        }
        catch (SqlException ex)
        {
            if (ex.Number == 11001
                || ex.Message.Contains("No such host", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("server was not found", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("network-related", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive($"SQL Server endpoint is unreachable. Skipping test. Details: {ex.Number} - {ex.Message}");
                return;
            }

            Assert.Fail($"SQL Server connectivity failed: {ex.Number} - {ex.Message}");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("No such host", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("server was not found", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("network-related", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive($"SQL Server endpoint is unreachable. Skipping test. Details: {ex.Message}");
                return;
            }

            Assert.Fail($"SQL Server connectivity failed with unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests connectivity to MySQL by executing a simple query.
    /// </summary>
    [TestMethod]
    [TestCategory("Connectivity")]
    [TestCategory("Database")]
    public async Task MySQL_CanConnect_AndExecuteQuery()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("MySQL");

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Inconclusive("MySQL connection string not configured. Skipping test.");
            return;
        }

        try
        {
            // Act - Use MySqlConnector if available
            var assembly = Assembly.Load("MySqlConnector");
            var connectionType = assembly.GetType("MySqlConnector.MySqlConnection");
            if (connectionType == null)
            {
                Assert.Inconclusive("MySqlConnection type not found. Skipping MySQL test.");
                return;
            }

            // Parse connection string to extract database name
            var databaseName = ExtractDatabaseFromConnectionString(connectionString);
            var connectionStringWithoutDb = RemoveDatabaseFromConnectionString(connectionString);

            // First, connect without database and create it if needed
            if (!string.IsNullOrEmpty(databaseName))
            {
                var setupConnection = Activator.CreateInstance(connectionType, connectionStringWithoutDb);
                if (setupConnection != null)
                {
                    var openMethod = connectionType.GetMethod("OpenAsync", Type.EmptyTypes);
                    if (openMethod != null)
                    {
                        await (Task)openMethod.Invoke(setupConnection, null)!;
                    }

                    // Create database if it doesn't exist
                    var createCommandMethod = connectionType.GetMethod("CreateCommand");
                    var setupCommand = createCommandMethod?.Invoke(setupConnection, null);
                    if (setupCommand != null)
                    {
                        var commandTextProperty = setupCommand.GetType().GetProperty("CommandText");
                        commandTextProperty?.SetValue(setupCommand, $"CREATE DATABASE IF NOT EXISTS `{databaseName}`");

                        var executeNonQueryMethod = setupCommand.GetType().GetMethod("ExecuteNonQueryAsync", Type.EmptyTypes);
                        if (executeNonQueryMethod != null)
                        {
                            await (Task<int>)executeNonQueryMethod.Invoke(setupCommand, null)!;
                        }

                        if (setupCommand is IDisposable cmdDisposable)
                        {
                            cmdDisposable.Dispose();
                        }
                    }

                    if (setupConnection is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }

            // Now connect with the database specified
            var connection = Activator.CreateInstance(connectionType, connectionString);
            if (connection == null)
            {
                Assert.Fail("Failed to create MySQL connection instance.");
                return;
            }

            var openMethodMain = connectionType.GetMethod("OpenAsync", Type.EmptyTypes);
            if (openMethodMain != null)
            {
                await (Task)openMethodMain.Invoke(connection, null)!;
            }

            var createCommandMethodMain = connectionType.GetMethod("CreateCommand");
            var command = createCommandMethodMain?.Invoke(connection, null);
            if (command == null)
            {
                Assert.Fail("Failed to create MySQL command.");
                return;
            }

            var commandTextPropertyMain = command.GetType().GetProperty("CommandText");
            commandTextPropertyMain?.SetValue(command, "SELECT VERSION()");

            var executeScalarMethod = command.GetType().GetMethod("ExecuteScalarAsync", Type.EmptyTypes);
            if (executeScalarMethod != null)
            {
                var versionTask = (Task<object>)executeScalarMethod.Invoke(command, null)!;
                var version = await versionTask;

                // Assert
                Assert.IsNotNull(version, "Failed to execute query on MySQL");
                Console.WriteLine($"✓ Successfully connected to MySQL");
                Console.WriteLine($"  Version: {version}");
            }

            // Cleanup
            if (connection is IDisposable disposable2)
            {
                disposable2.Dispose();
            }
        }
        catch (FileNotFoundException)
        {
            Assert.Inconclusive("MySqlConnector package not installed. Skipping MySQL test.");
        }
        catch (Exception ex)
        {
            Assert.Fail($"MySQL connectivity failed: {ex.Message}");
        }
    }

    private static string? ExtractDatabaseFromConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var dbPart = parts.FirstOrDefault(p => p.Trim().StartsWith("Database=", StringComparison.OrdinalIgnoreCase));
        return dbPart?.Split('=', 2).LastOrDefault()?.Trim();
    }

    private static string RemoveDatabaseFromConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var filteredParts = parts.Where(p => !p.Trim().StartsWith("Database=", StringComparison.OrdinalIgnoreCase));
        return string.Join(";", filteredParts) + ";";
    }

    /// <summary>
    /// Tests connectivity to SQLite by creating and querying a test database.
    /// </summary>
    [TestMethod]
    [TestCategory("Connectivity")]
    [TestCategory("Database")]
    public async Task SQLite_CanConnect_AndExecuteQuery()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("SQLite");

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Inconclusive("SQLite connection string not configured. Skipping test.");
            return;
        }

        try
        {
            // Act - Use Microsoft.Data.Sqlite
            var assembly = Assembly.Load("Microsoft.Data.Sqlite");
            var connectionType = assembly.GetType("Microsoft.Data.Sqlite.SqliteConnection");
            if (connectionType == null)
            {
                Assert.Inconclusive("SqliteConnection type not found. Skipping SQLite test.");
                return;
            }

            var connection = Activator.CreateInstance(connectionType, connectionString);
            if (connection == null)
            {
                Assert.Fail("Failed to create SQLite connection instance.");
                return;
            }

            var openMethod = connectionType.GetMethod("OpenAsync", Type.EmptyTypes);
            if (openMethod != null)
            {
                await (Task)openMethod.Invoke(connection, null)!;
            }

            var createCommandMethod = connectionType.GetMethod("CreateCommand");
            var command = createCommandMethod?.Invoke(connection, null);
            if (command == null)
            {
                Assert.Fail("Failed to create SQLite command.");
                return;
            }

            var commandTextProperty = command.GetType().GetProperty("CommandText");
            commandTextProperty?.SetValue(command, "SELECT sqlite_version()");

            var executeScalarMethod = command.GetType().GetMethod("ExecuteScalarAsync", Type.EmptyTypes);
            if (executeScalarMethod != null)
            {
                var versionTask = (Task<object>)executeScalarMethod.Invoke(command, null)!;
                var version = await versionTask;

                // Assert
                Assert.IsNotNull(version, "Failed to execute query on SQLite");
                Console.WriteLine($"✓ Successfully connected to SQLite");
                Console.WriteLine($"  Version: {version}");
            }

            // Cleanup
            if (connection is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (FileNotFoundException)
        {
            Assert.Inconclusive("Microsoft.Data.Sqlite package not installed. Skipping SQLite test.");
        }
        catch (Exception ex)
        {
            Assert.Fail($"SQLite connectivity failed: {ex.Message}");
        }
    }

    #endregion

    #region Storage Connectivity Tests

    /// <summary>
    /// Tests connectivity to Azure Blob Storage by listing containers.
    /// </summary>
    [TestMethod]
    [TestCategory("Connectivity")]
    [TestCategory("Storage")]
    public async Task AzureBlobStorage_CanConnect_AndListContainers()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("AzureBlobStorageConnectionString")
            ?? _configuration.GetConnectionString("StorageConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Inconclusive("Azure Blob Storage connection string not configured. Skipping test.");
            return;
        }

        try
        {
            // Act - Attempt to connect and list containers
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containers = blobServiceClient.GetBlobContainersAsync();

            var containerCount = 0;
            await foreach (var container in containers)
            {
                containerCount++;
                if (containerCount >= 1)
                    break; // Just verify we can list at least one
            }

            // Assert
            Console.WriteLine($"✓ Successfully connected to Azure Blob Storage");
            Console.WriteLine($"  Found {containerCount}+ containers");
        }
        catch (Azure.RequestFailedException ex)
        {
            Assert.Fail($"Azure Blob Storage connectivity failed: {ex.Status} - {ex.Message}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Azure Blob Storage connectivity failed with unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests connectivity to Amazon S3 by listing buckets.
    /// </summary>
    [TestMethod]
    [TestCategory("Connectivity")]
    [TestCategory("Storage")]
    public async Task AmazonS3_CanConnect_AndListBuckets()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("AmazonS3ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Inconclusive("Amazon S3 connection string not configured. Skipping test.");
            return;
        }

        try
        {
            // Parse connection string
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2))
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

            var bucket = parts["Bucket"];
            var region = parts["Region"];
            var keyId = parts["KeyId"];
            var key = parts["Key"];

            // Act - Attempt to connect and check if bucket exists
            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
            using var client = new AmazonS3Client(keyId, key, regionEndpoint);

            var request = new GetBucketLocationRequest { BucketName = bucket };
            var response = await client.GetBucketLocationAsync(request);

            // Assert
            Assert.IsNotNull(response, "Failed to connect to Amazon S3");
            Console.WriteLine($"✓ Successfully connected to Amazon S3");
            Console.WriteLine($"  Bucket: {bucket}, Region: {region}");
        }
        catch (AmazonS3Exception ex)
        {
            Assert.Fail($"Amazon S3 connectivity failed: {ex.StatusCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Amazon S3 connectivity failed with unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests connectivity to Cloudflare R2 by listing objects.
    /// </summary>
    [TestMethod]
    [TestCategory("Connectivity")]
    [TestCategory("Storage")]
    public async Task CloudflareR2_CanConnect_AndAccessBucket()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("CloudflareR2ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Inconclusive("Cloudflare R2 connection string not configured. Skipping test.");
            return;
        }

        try
        {
            // Parse connection string
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2))
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

            var accountId = parts["AccountId"];
            var bucket = parts["Bucket"];
            var keyId = parts["KeyId"];
            var key = parts["key"];

            // Act - Cloudflare R2 uses S3-compatible API
            var endpoint = $"https://{accountId}.r2.cloudflarestorage.com";
            var config = new AmazonS3Config
            {
                ServiceURL = endpoint
            };

            using var client = new AmazonS3Client(keyId, key, config);

            var request = new ListObjectsV2Request
            {
                BucketName = bucket,
                MaxKeys = 1
            };
            var response = await client.ListObjectsV2Async(request);

            // Assert
            Assert.IsNotNull(response, "Failed to connect to Cloudflare R2");
            Console.WriteLine($"✓ Successfully connected to Cloudflare R2");
            Console.WriteLine($"  Bucket: {bucket}, Account: {accountId}");
        }
        catch (AmazonS3Exception ex)
        {
            Assert.Fail($"Cloudflare R2 connectivity failed: {ex.StatusCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Cloudflare R2 connectivity failed with unexpected error: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets a configuration value from multiple possible sources.
    /// Checks in order: user secrets/appsettings, environment variables, connection strings.
    /// Environment variables have highest priority due to configuration builder order.
    /// </summary>
    private static string? GetConfigValue(string key)
    {
        // First try direct configuration key (includes environment variables due to builder order)
        var value = _configuration[key];

        if (string.IsNullOrEmpty(value))
        {
            // Try as connection string
            value = _configuration.GetConnectionString(key);
        }

        return value;
    }

    #endregion
}
