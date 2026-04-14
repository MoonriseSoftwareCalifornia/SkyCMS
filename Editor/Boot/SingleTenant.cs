// <copyright file="SingleTenant.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Boot
{
    using System;
    using System.Linq;
    using AspNetCore.Identity.FlexDb;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Single-tenant configuration for the application.
    /// </summary>
    public static class SingleTenant
    {
        /// <summary>
        /// Configures services for single-tenant mode.
        /// Ensures database schema exists before registering services.
        /// </summary>
        /// <param name="builder">Web application builder.</param>
        public static void Configure(WebApplicationBuilder builder)
        {
            var logger = LoggerFactory.Create(config => config.AddConsole())
                .CreateLogger("SingleTenant.Configure");

            var connectionString = builder.Configuration.GetConnectionString("ApplicationDbContextConnection");
            var allowSetup = builder.Configuration.GetValue<bool?>("CosmosAllowSetup") ?? false;

            // If no connection string is provided, don't configure database yet
            if (string.IsNullOrEmpty(connectionString))
            {
                logger.LogWarning("No connection string configured - database will be configured during setup");

                // Don't add DbContext here - it will be added after setup completes
                return;
            }

            // Ensure database schema exists if setup is allowed
            if (allowSetup)
            {
                try
                {
                    logger.LogInformation("Checking database schema for single-tenant mode...");
                    EnsureDatabaseSchemaExists(connectionString, logger);
                    logger.LogInformation("Database schema is ready");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Database schema initialization failed - application will continue but may have limited functionality");
                }
            }

            // Configure the ApplicationDbContext with the appropriate provider
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                CosmosDbOptionsBuilder.ConfigureDbOptions(options, connectionString);
            });

            // Register the IApplicationDbContext interface
            builder.Services.AddScoped(typeof(IApplicationDbContext), sp => sp.GetRequiredService<ApplicationDbContext>());
        }

        /// <summary>
        /// Ensures the database schema exists for the ApplicationDbContext.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="logger">Logger instance.</param>
        private static void EnsureDatabaseSchemaExists(string connectionString, ILogger logger)
        {
            try
            {
                using var dbContext = new ApplicationDbContext(CosmosDbOptionsBuilder.GetDbOptions<ApplicationDbContext>(connectionString));

                // Check if database is accessible
                if (!dbContext.Database.CanConnect())
                {
                    logger.LogInformation("Database not accessible - creating database and schema");
                    dbContext.Database.EnsureCreated();
                    logger.LogInformation("Database and schema created successfully");
                    return;
                }

                // Try to query a table to verify schema exists
                try
                {
                    var testQuery = dbContext.Settings.FirstOrDefault();
                    logger.LogInformation("Database schema verified - Settings table is accessible");
                }
                catch
                {
                    logger.LogInformation("Database schema incomplete or missing - creating schema");
                    dbContext.Database.EnsureCreated();
                    logger.LogInformation("Database schema created successfully");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure database schema exists");
                throw;
            }
        }
    }
}
