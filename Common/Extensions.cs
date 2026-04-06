// <copyright file="Extensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common
{
    using System;
    using AspNetCore.Identity.FlexDb;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

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
            var dbContext = new DataProtectionDbContext(builder.Options);
            _ = dbContext.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
            services.AddSingleton<DataProtectionDbContext>(dbContext);
            services.AddDataProtection()
                .PersistKeysToDbContext<DataProtectionDbContext>();
        }
    }
}
