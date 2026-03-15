// <copyright file="SqlServerConfigurationStrategy.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace AspNetCore.Identity.FlexDb.Strategies
{
    using Microsoft.EntityFrameworkCore;
    using System;

    /// <summary>
    /// Configuration strategy for SQL Server.
    /// </summary>
    public class SqlServerConfigurationStrategy : IDatabaseConfigurationStrategy
    {
        /// <inheritdoc/>
        public string ProviderName => "SQL Server";

        /// <inheritdoc/>
        public int Priority => 20;

        /// <inheritdoc/>
        public bool CanHandle(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            // SQL Server connection strings can use different authentication methods:
            // - SQL Server Authentication: User ID/Password
            // - Windows Authentication: Trusted_Connection or Integrated Security
            return connectionString.Contains("User ID", StringComparison.InvariantCultureIgnoreCase) ||
                   connectionString.Contains("Trusted_Connection", StringComparison.InvariantCultureIgnoreCase) ||
                   connectionString.Contains("Integrated Security", StringComparison.InvariantCultureIgnoreCase);
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

            optionsBuilder.UseSqlServer(connectionString, sqlServerOptions =>
            {
                // Enable retry on failure for transient errors
                // This handles temporary network issues, database unavailability, etc.
                sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);

                // Set command timeout (optional, but recommended for CI/CD)
                sqlServerOptions.CommandTimeout(60); // 60 seconds
            });
        }
    }
}
