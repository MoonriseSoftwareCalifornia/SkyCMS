// <copyright file="CosmosDbConfigurationStrategy.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace AspNetCore.Identity.FlexDb.Strategies
{
    using Azure.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using System;
    using System.Linq;

    /// <summary>
    /// Configuration strategy for Azure Cosmos DB.
    /// </summary>
    public class CosmosDbConfigurationStrategy : IDatabaseConfigurationStrategy
    {
        /// <inheritdoc/>
        public string ProviderName => "Cosmos";

        /// <inheritdoc/>
        public int Priority => 10;

        /// <inheritdoc/>
        public bool CanHandle(string connectionString)
        {
            return !string.IsNullOrWhiteSpace(connectionString) &&
                   connectionString.Contains("AccountEndpoint=", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <inheritdoc/>
        public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
        {
            if (optionsBuilder == null)
            {
                throw new ArgumentNullException(nameof(optionsBuilder));
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            var accountProperties = GetAccountProperties(connectionString);

            ValidateRequiredParts(accountProperties.DatabaseName, accountProperties.AccountEndpoint, accountProperties.AccountKey);

            if (IsTokenAuthentication(accountProperties.AccountKey))
            {
                optionsBuilder.UseCosmos(
                    accountEndpoint: accountProperties.AccountEndpoint,
                    tokenCredential: new DefaultAzureCredential(),
                    databaseName: accountProperties.DatabaseName);
            }
            else
            {
                optionsBuilder.UseCosmos(
                    accountEndpoint: accountProperties.AccountEndpoint,
                    accountKey: accountProperties.AccountKey,
                    databaseName: accountProperties.DatabaseName);
            }

            // Data protection sync operations are not supported in Cosmos DB.  TODO remove in .Net 11
            optionsBuilder.ConfigureWarnings(w => w.Ignore(CosmosEventId.SyncNotSupported));
        }

        /// <summary>
        /// Gets the Cosmos DB account properties from the connection string.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns>Account properties</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static CosmosDbAccountProperties GetAccountProperties(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var databaseName = GetConnectionStringPart(parts, "Database=");
            var accountEndpoint = GetConnectionStringPart(parts, "AccountEndpoint=");
            var accountKey = GetConnectionStringPart(parts, "AccountKey=");

            return new CosmosDbAccountProperties
            {
                DatabaseName = databaseName,
                AccountEndpoint = accountEndpoint,
                AccountKey = accountKey
            };
        }

        private static string GetConnectionStringPart(string[] parts, string prefix)
        {
            var part = parts.FirstOrDefault(p => p.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase));

            if (part == null)
            {
                return null;
            }

            var indexOfEquals = part.IndexOf('=');
            return indexOfEquals >= 0 ? part.Substring(indexOfEquals + 1) : null;
        }

        private static bool IsTokenAuthentication(string accountKey)
        {
            return accountKey?.Equals("AccessToken", StringComparison.InvariantCultureIgnoreCase) ?? false;
        }

        private static void ValidateRequiredParts(string databaseName, string accountEndpoint, string accountKey)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                string.IsNullOrWhiteSpace(accountEndpoint) ||
                string.IsNullOrWhiteSpace(accountKey))
            {
                throw new ArgumentException(
                    "The provided Cosmos DB connection string is missing required components. " +
                    "Required: AccountEndpoint, AccountKey (or 'AccessToken' for managed identity), and Database.");
            }
        }
    }

    /// <summary>
    /// Cosmos DB account properties.
    /// </summary>
    public class CosmosDbAccountProperties
    {
        /// <summary>
        /// Gets or sets the database name.
        /// </summary>
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the account endpoint.
        /// </summary>
        public string AccountEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the account key.
        /// </summary>
        public string AccountKey { get; set; } = string.Empty;
    }
}
