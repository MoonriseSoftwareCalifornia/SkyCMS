// <copyright file="MySqlConfigurationStrategy.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace AspNetCore.Identity.FlexDb.Strategies
{
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Configuration strategy for MySQL.
    /// </summary>
    public class MySqlConfigurationStrategy : IDatabaseConfigurationStrategy
    {
        private static readonly ConcurrentDictionary<string, ServerVersion> ServerVersionCache = new();

        /// <inheritdoc/>
        public string ProviderName => "MySQL";

        /// <inheritdoc/>
        public int Priority => 30;

        /// <inheritdoc/>
        public bool CanHandle(string connectionString)
        {
            return !string.IsNullOrWhiteSpace(connectionString) &&
                   connectionString.Contains("uid=", StringComparison.InvariantCultureIgnoreCase);
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

            // Add connection timeout if not specified
            var connectionStringWithTimeout = EnsureConnectionTimeout(connectionString);

            // Cache server version per connection string to avoid repeated auto-detection
            var serverVersion = ServerVersionCache.GetOrAdd(
                connectionStringWithTimeout,
                cs => ServerVersion.AutoDetectAsync(cs).GetAwaiter().GetResult());

            optionsBuilder.UseMySql(
                connectionStringWithTimeout,
                serverVersion,
                options => options.EnableRetryOnFailure());
        }

        private static string EnsureConnectionTimeout(string connectionString)
        {
            // Check if connection timeout is already specified
            if (connectionString.Contains("Connection Timeout=", StringComparison.InvariantCultureIgnoreCase) ||
                connectionString.Contains("ConnectionTimeout=", StringComparison.InvariantCultureIgnoreCase))
            {
                return connectionString;
            }

            // Add a reasonable connection timeout (30 seconds)
            return connectionString.TrimEnd(';') + ";Connection Timeout=30;";
        }
    }
}
