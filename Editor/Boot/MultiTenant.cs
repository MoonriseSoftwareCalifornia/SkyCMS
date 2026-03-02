// <copyright file="MultiTenant.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Boot
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AspNetCore.Identity.FlexDb;
    using Azure.Identity;
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Multi-tenant configuration for the application.
    /// </summary>
    public static class MultiTenant
    {
        /// <summary>
        /// Configures services for multi-tenant mode.
        /// Ensures database schemas exist for config database and all tenants.
        /// </summary>
        /// <param name="builder">Web application builder.</param>
        /// <param name="credential">Azure credential.</param>
        public static void Configure(WebApplicationBuilder builder, DefaultAzureCredential credential)
        {
            var logger = LoggerFactory.Create(config => config.AddConsole())
                .CreateLogger("MultiTenant.Configure");

            var configConnectionString = builder.Configuration.GetConnectionString("ConfigDbConnectionString");
            var allowSetup = builder.Configuration.GetValue<bool?>("CosmosAllowSetup") ?? false;

            // If no config connection string is provided, don't configure yet
            if (string.IsNullOrEmpty(configConnectionString))
            {
                logger.LogWarning("No config connection string - database will be configured during setup");
                return;
            }

            // Ensure config database schema exists if setup is allowed
            if (allowSetup)
            {
                try
                {
                    logger.LogInformation("Checking config database schema for multi-tenant mode...");
                    EnsureConfigDatabaseSchemaExists(configConnectionString, logger).Wait();
                    logger.LogInformation("Config database schema is ready");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Database schema initialization failed - application will continue but may have limited functionality");
                }
            }

            // Configure the DynamicConfigDbContext
            builder.Services.AddDbContext<DynamicConfigDbContext>(options =>
            {
                CosmosDbOptionsBuilder.ConfigureDbOptions(options, configConnectionString);
            });

            // Register ApplicationDbContext with dynamic tenant resolution
            builder.Services.AddScoped<ApplicationDbContext>(serviceProvider =>
            {
                var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var contextLogger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

                // Get tenant domain from configuration (set by DomainMiddleware)
                var currentTenantDomain = configuration.GetValue<string>("CurrentTenantDomain");

                if (string.IsNullOrEmpty(currentTenantDomain))
                {
                    // Fallback: try to get from request headers
                    var xOriginHostname = httpContextAccessor.HttpContext?.Request.Headers["x-origin-hostname"].ToString();
                    currentTenantDomain = !string.IsNullOrWhiteSpace(xOriginHostname)
                        ? xOriginHostname.ToLowerInvariant()
                        : httpContextAccessor.HttpContext?.Request.Host.Host.ToLowerInvariant();
                }

                if (string.IsNullOrEmpty(currentTenantDomain))
                {
                    // Provide helpful error message for common scenarios
                    var errorMessage = "Unable to determine tenant domain for ApplicationDbContext resolution. ";
                    
                    if (httpContextAccessor.HttpContext == null)
                    {
                        errorMessage += "No HTTP context is available (this is expected for background jobs like Hangfire). " +
                                      "Background jobs should create ApplicationDbContext directly using 'new ApplicationDbContext(connectionString)' " +
                                      "instead of resolving it from the DI container.";
                    }
                    else
                    {
                        errorMessage += "HTTP context is available but tenant domain could not be determined from headers or host. " +
                                      "Ensure DomainMiddleware is properly configured and running before this request.";
                    }
                    
                    contextLogger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                // Get the tenant's connection string from DynamicConfigDbContext
                var configDbContext = serviceProvider.GetRequiredService<DynamicConfigDbContext>();
                var connection = configDbContext.Connections
                    .FirstOrDefault(c => c.DomainNames.Contains(currentTenantDomain));

                if (connection == null)
                {
                    throw new InvalidOperationException($"No tenant configuration found for domain: {currentTenantDomain}");
                }

                if (string.IsNullOrEmpty(connection.DbConn))
                {
                    throw new InvalidOperationException($"Tenant {currentTenantDomain} has no database connection string configured");
                }

                // Create ApplicationDbContext with tenant-specific connection string
                var options = CosmosDbOptionsBuilder.GetDbOptions<ApplicationDbContext>(connection.DbConn);
                return new ApplicationDbContext(options);
            });

            // Ensure all tenant database schemas exist if setup is allowed
            if (allowSetup)
            {
                try
                {
                    logger.LogInformation("Checking tenant database schemas...");
                    var task = EnsureTenantSchemasExist(configConnectionString, logger);
                    task.Wait(); // Wait for the task to complete
                    logger.LogInformation("All tenant database schemas are ready");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Tenant schema initialization failed - some tenants may have limited functionality");
                }
            }
        }

        /// <summary>
        /// Ensures the config database schema exists.
        /// </summary>
        /// <param name="connectionString">Config database connection string.</param>
        /// <param name="logger">Logger instance.</param>
        private static async Task EnsureConfigDatabaseSchemaExists(string connectionString, ILogger logger)
        {
            try
            {
                using var context = new ApplicationDbContext(connectionString);
                
                // Replace: if (!context.Database.CanConnect())
                if (!await CanConnectToCosmosAsync(context, logger))
                {
                    logger.LogWarning("Cannot connect to config database");
                    return;
                }
                
                // Cosmos DB automatically creates containers on first access
                // No explicit schema creation needed like SQL databases
                await context.Database.EnsureCreatedAsync();
                
                logger.LogInformation("Config database schema verified");
            }
            catch (NotSupportedException ex)
            {
                logger.LogWarning(ex, "Cosmos DB does not support schema operations - skipping");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure config database schema exists");
                throw;
            }
        }

        /// <summary>
        /// Ensures database schemas exist for all tenants.
        /// </summary>
        /// <param name="configConnectionString">Config database connection string.</param>
        /// <param name="logger">Logger instance.</param>
        private static async Task EnsureTenantSchemasExist(string configConnectionString, ILogger logger)
        {
            try
            {
                using var configDbContext = new DynamicConfigDbContext(CosmosDbOptionsBuilder.GetDbOptions<DynamicConfigDbContext>(configConnectionString));
                
                // Get all tenant configurations from the config database
                var tenantConfigs = configDbContext.Connections.ToList();

                if (!tenantConfigs.Any())
                {
                    logger.LogInformation("No tenants found in config database - skipping tenant schema initialization");
                    return;
                }

                logger.LogInformation("Found {TenantCount} tenant(s) - checking schemas", tenantConfigs.Count);

                foreach (var tenantConfig in tenantConfigs)
                {
                    try
                    {
                        logger.LogInformation("Checking schema for tenant: {TenantName} (ID: {TenantId})", 
                            tenantConfig.DomainNames, tenantConfig.Id);
                        
                        // Get the tenant's database connection string
                        var tenantConnectionString = tenantConfig.DbConn;
                        
                        if (string.IsNullOrEmpty(tenantConnectionString))
                        {
                            logger.LogWarning("Tenant {TenantName} has no connection string - skipping", tenantConfig.DomainNames);
                            continue;
                        }

                        using var tenantDbContext = new ApplicationDbContext(
                            CosmosDbOptionsBuilder.GetDbOptions<ApplicationDbContext>(tenantConnectionString));
                        
                        // Replace: if (!tenantDbContext.Database.CanConnect())
                        if (!await CanConnectToCosmosAsync(tenantDbContext, logger))
                        {
                            logger.LogWarning($"Cannot connect to tenant database: {tenantConfig.DomainNames}");
                            continue;
                        }
                        
                        await tenantDbContext.Database.EnsureCreatedAsync();
                        logger.LogInformation($"Tenant schema verified for {tenantConfig.DomainNames}");
                    }
                    catch (NotSupportedException ex)
                    {
                        logger.LogWarning(ex, $"Cosmos DB does not support schema operations for tenant {tenantConfig.DomainNames} - skipping");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"Failed to ensure schema for tenant: {tenantConfig.DomainNames}");
                    }
                }

                logger.LogInformation("Completed tenant schema initialization for all tenants");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed during tenant schema initialization");
                throw;
            }
        }

        private static async Task<bool> CanConnectToCosmosAsync(DbContext context, ILogger logger)
        {
            try
            {
                // For Cosmos DB, attempt to ensure the database exists
                // This validates connectivity without using CanConnect
                await context.Database.EnsureCreatedAsync();
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to Cosmos DB");
                return false;
            }
        }
    }
}
