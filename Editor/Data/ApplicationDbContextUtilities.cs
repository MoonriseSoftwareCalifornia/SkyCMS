// <copyright file="ApplicationDbContextUtilities.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Data
{
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Microsoft.Extensions.DependencyInjection;
    using System;

    /// <summary>
    /// Utilities for the ApplicationDbContext class.
    /// </summary>
    public static class ApplicationDbContextUtilities
    {
        /// <summary>
        /// Get an ApplicationDbContext from a connection.
        /// </summary>
        /// <param name="connection">Website connection.</param>
        /// <returns>ApplicationDbContext.</returns>
        public static ApplicationDbContext GetApplicationDbContext(Connection connection)
        {
            return new ApplicationDbContext(connection.DbConn);
        }

        /// <summary>
        /// Gets a new instance of the ApplicationDbContext for a specific domain name.
        /// </summary>
        /// <param name="domainName">Domain name.</param>
        /// <param name="services">Services provider.</param>
        /// <returns>ApplicationDbContext</returns>
        public static ApplicationDbContext GetDbContextForDomain(string domainName, IServiceProvider services)
        {
            if (string.IsNullOrWhiteSpace(domainName))
            {
                throw new ArgumentException("Domain name cannot be null or empty.", nameof(domainName));
            }

            // Normalize domain name to lowercase for consistent lookup
            domainName = domainName.Trim().ToLowerInvariant();

            IDynamicConfigurationProvider provider;
            try
            {
                provider = services.GetRequiredService<IDynamicConfigurationProvider>();
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("Dynamic configuration provider is not registered.", ex);
            }

            if (!provider.IsMultiTenantConfigured)
            {
                throw new InvalidOperationException("Dynamic configuration provider is not configured for multi-tenancy.");
            }

            var connectionString = provider.GetDatabaseConnectionStringAsync(domainName).Result;

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"No connection string found for domain '{domainName}'.");
            }

            // Create a new instance of ApplicationDbContext with the same options but for the specified domain.
            return new ApplicationDbContext(connectionString);
        }
    }
}
