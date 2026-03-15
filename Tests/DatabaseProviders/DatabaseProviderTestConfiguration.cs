// <copyright file="DatabaseProviderTestConfiguration.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.DatabaseProviders
{
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Configuration helper for database provider testing.
    /// Supports both local emulators and actual Azure cloud databases.
    /// </summary>
    public static class DatabaseProviderTestConfiguration
    {
        /// <summary>
        /// Gets test connection strings for all supported providers.
        /// Checks environment variables and configuration files first, falls back to local emulators.
        /// </summary>
        /// <param name="configuration">Optional configuration provider.</param>
        /// <returns>Dictionary of provider names and connection strings.</returns>
        public static Dictionary<string, string> GetTestConnectionStrings(IConfiguration configuration = null)
        {
            // Try to load from configuration or environment variables first
            var cosmosDb = GetConnectionString("CosmosDB", configuration)
                ?? GetConnectionString("TEST_COSMOSDB_CONNECTION", configuration)
                ?? "AccountEndpoint=https://localhost:8081;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;Database=TestDb";

            var sqlServer = GetConnectionString("SqlServer", configuration)
                ?? GetConnectionString("TEST_SQLSERVER_CONNECTION", configuration)
                ?? "Server=(localdb)\\mssqllocaldb;Database=SkyCmsTest;Trusted_Connection=True;MultipleActiveResultSets=true";

            var mySql = GetConnectionString("MySQL", configuration)
                ?? GetConnectionString("TEST_MYSQL_CONNECTION", configuration)
                ?? "Server=localhost;Port=3306;Database=skycmstest;uid=root;pwd=password;";

            var sqlite = GetConnectionString("SQLite", configuration)
                ?? GetConnectionString("TEST_SQLITE_CONNECTION", configuration)
                ?? "Data Source=:memory:;Mode=Memory;Cache=Shared;";

            return new Dictionary<string, string>
            {
                ["CosmosDB"] = cosmosDb,
                ["SqlServer"] = sqlServer,
                ["MySQL"] = mySql,
                ["SQLite"] = sqlite
            };
        }

        /// <summary>
        /// Gets whether a provider is using a cloud/production connection.
        /// </summary>
        /// <param name="providerKey">Provider key (CosmosDB, SqlServer, MySQL, SQLite).</param>
        /// <param name="configuration">Optional configuration provider.</param>
        /// <returns>True if using cloud connection.</returns>
        public static bool IsUsingCloudConnection(string providerKey, IConfiguration configuration = null)
        {
            var connectionStrings = GetTestConnectionStrings(configuration);
            var connectionString = connectionStrings[providerKey];

            return providerKey switch
            {
                "CosmosDB" => !connectionString.Contains("localhost:8081"),
                "SqlServer" => !connectionString.Contains("localdb") && !connectionString.Contains("localhost"),
                "MySQL" => !connectionString.Contains("localhost") && !connectionString.Contains("127.0.0.1"),
                "SQLite" => !connectionString.Contains(":memory:"),
                _ => false
            };
        }

        /// <summary>
        /// Gets expected performance characteristics for each provider.
        /// </summary>
        /// <returns>Dictionary of provider characteristics.</returns>
        public static Dictionary<string, ProviderCharacteristics> GetProviderCharacteristics()
        {
            return new Dictionary<string, ProviderCharacteristics>
            {
                ["CosmosDB"] = new ProviderCharacteristics
                {
                    SupportsTransactions = false,
                    OptimalForWrites = true,
                    OptimalForReads = true,
                    ScalesHorizontally = true,
                    RequiresPartitionKey = true,
                    RecommendedForProduction = true
                },
                ["SqlServer"] = new ProviderCharacteristics
                {
                    SupportsTransactions = true,
                    OptimalForWrites = true,
                    OptimalForReads = true,
                    ScalesHorizontally = false,
                    RequiresPartitionKey = false,
                    RecommendedForProduction = true
                },
                ["MySQL"] = new ProviderCharacteristics
                {
                    SupportsTransactions = true,
                    OptimalForWrites = true,
                    OptimalForReads = true,
                    ScalesHorizontally = false,
                    RequiresPartitionKey = false,
                    RecommendedForProduction = true
                },
                ["SQLite"] = new ProviderCharacteristics
                {
                    SupportsTransactions = true,
                    OptimalForWrites = false,
                    OptimalForReads = true,
                    ScalesHorizontally = false,
                    RequiresPartitionKey = false,
                    RecommendedForProduction = false
                }
            };
        }

        /// <summary>
        /// Gets example connection strings for cloud providers.
        /// </summary>
        /// <returns>Dictionary of provider examples.</returns>
        public static Dictionary<string, string> GetCloudConnectionStringExamples()
        {
            return new Dictionary<string, string>
            {
                ["CosmosDB"] = "AccountEndpoint=https://YOUR-ACCOUNT.documents.azure.com:443/;AccountKey=YOUR-KEY;Database=YOUR-DATABASE",
                ["CosmosDB_ManagedIdentity"] = "AccountEndpoint=https://YOUR-ACCOUNT.documents.azure.com:443/;AccountKey=AccessToken;Database=YOUR-DATABASE",
                ["SqlServer"] = "Server=tcp:YOUR-SERVER.database.windows.net,1433;Initial Catalog=YOUR-DATABASE;User ID=YOUR-USER;Password=YOUR-PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
                ["SqlServer_ManagedIdentity"] = "Server=tcp:YOUR-SERVER.database.windows.net,1433;Initial Catalog=YOUR-DATABASE;Authentication=Active Directory Default;Encrypt=True;",
                ["MySQL"] = "Server=YOUR-SERVER.mysql.database.azure.com;Port=3306;Database=YOUR-DATABASE;Uid=YOUR-USER@YOUR-SERVER;Pwd=YOUR-PASSWORD;SslMode=Required;"
            };
        }

        private static string GetConnectionString(string key, IConfiguration configuration)
        {
            // Try configuration first
            if (configuration != null)
            {
                var value = configuration.GetConnectionString(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            // Try environment variable
            var envValue = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue;
            }

            return null;
        }
    }

    /// <summary>
    /// Provider characteristics for testing and comparison.
    /// </summary>
    public class ProviderCharacteristics
    {
        /// <summary>
        /// Gets or sets a value indicating whether the provider supports transactions.
        /// </summary>
        public bool SupportsTransactions { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the provider is optimized for write operations.
        /// </summary>
        public bool OptimalForWrites { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the provider is optimized for read operations.
        /// </summary>
        public bool OptimalForReads { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the provider scales horizontally.
        /// </summary>
        public bool ScalesHorizontally { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the provider requires partition keys.
        /// </summary>
        public bool RequiresPartitionKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the provider is recommended for production use.
        /// </summary>
        public bool RecommendedForProduction { get; set; }
    }
}
