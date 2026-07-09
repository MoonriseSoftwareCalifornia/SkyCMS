// <copyright file="Extensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.DataProtection
{
    using System;
    using AspNetCore.Identity.FlexDb;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
    using Microsoft.AspNetCore.DataProtection.Repositories;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Service collection extensions for FlexDb data protection.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Adds FlexDb data protection services to the service collection.
        /// </summary>
        /// <param name="services">Services collection.</param>
        /// <param name="config">Configuration.</param>
        /// <exception cref="ArgumentNullException">DB connection not found.</exception>
        public static void AddFlexDbDataProtection(this IServiceCollection services, IConfiguration config)
        {
            var isMultiTenant = config.GetValue<bool?>("MultiTenantEditor") ?? false;

            var connectionString = isMultiTenant ? config.GetConnectionString("ConfigDbConnectionString") : config.GetConnectionString("ApplicationDbContextConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException("ApplicationDbContextConnection", "'ApplicationDbContextConnection' connection string is not set.");
            }

            var builder = CosmosDbOptionsBuilder.GetDbOptionsBuilder<DataProtectionDbContext>(connectionString);

            // Ensure database schema exists (one-time initialization)
            using (var initContext = new DataProtectionDbContext(builder.Options))
            {
                _ = initContext.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
            }

            // Register as singleton. Singleton is required by the data protection framework.
            var contextInstance = new DataProtectionDbContext(builder.Options);
            services.AddSingleton<DataProtectionDbContext>(contextInstance);

            // Configure data protection with the context AND register resilient XML repository
            // to handle Cosmos DB conflicts gracefully (409 errors when concurrent requests
            // try to create the same key).
            services.AddDataProtection()
                .SetApplicationName("SkyCMS")
                .PersistKeysToDbContext<DataProtectionDbContext>()
                .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

            // Override with resilient repository that handles 409 conflicts
            services.AddSingleton<IXmlRepository>(sp =>
                new ResilientEntityFrameworkCoreXmlRepository<DataProtectionDbContext>(
                    sp,
                    sp.GetRequiredService<ILoggerFactory>()));
        }
    }
}
