// <copyright file="SqliteConfigurationStrategy.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace AspNetCore.Identity.FlexDb.Strategies
{
    using Microsoft.EntityFrameworkCore;
    using SQLitePCL;
    using System;

    /// <summary>
    /// Configuration strategy for SQLite.
    /// </summary>
    public class SqliteConfigurationStrategy : IDatabaseConfigurationStrategy
    {
        /// <inheritdoc/>
        public string ProviderName => "SQLite";

        /// <inheritdoc/>
        public int Priority => 40;

        /// <inheritdoc/>
        public bool CanHandle(string connectionString)
        {
            return !string.IsNullOrWhiteSpace(connectionString) &&
                   connectionString.Contains("Data Source=", StringComparison.InvariantCultureIgnoreCase) &&
                   (connectionString.Contains(":memory:", StringComparison.InvariantCultureIgnoreCase) ||
                    connectionString.Contains(".db", StringComparison.InvariantCultureIgnoreCase) ||
                    connectionString.Contains(".sqlite", StringComparison.InvariantCultureIgnoreCase));
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

            if (connectionString.Contains("Password=", StringComparison.InvariantCultureIgnoreCase))
            {
                // Initialize SQLite encryption support.
                Batteries_V2.Init();

                optionsBuilder.UseSqlite(connectionString, options =>
                {
                    options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
            }
            else
            {
                optionsBuilder.UseSqlite(connectionString);
            }
        }
    }
}
