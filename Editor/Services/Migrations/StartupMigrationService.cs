// <copyright file="StartupMigrationService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Migrations
{
    using AspNetCore.Identity.FlexDb;
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Layout;
    using Sky.Editor.Services.Migrations.Core;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Service for running database migrations during application startup.
    /// Supports both single-tenant and multi-tenant modes.
    /// </summary>
    public static class StartupMigrationService
    {
        /// <summary>
        /// Runs database migrations based on the deployment mode.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="isMultiTenant">Whether the application is running in multi-tenant mode.</param>
        /// <returns>A summary of migration results.</returns>
        public static async Task<MigrationSummary> RunMigrationsAsync(
            IConfiguration configuration,
            bool isMultiTenant)
        {
            if (isMultiTenant)
            {
                return await RunMultiTenantMigrationsAsync(configuration);
            }
            else
            {
                return await RunSingleTenantMigrationAsync(configuration);
            }
        }

        /// <summary>
        /// Runs migrations for a single-tenant deployment.
        /// </summary>
        private static async Task<MigrationSummary> RunSingleTenantMigrationAsync(IConfiguration configuration)
        {
            System.Console.WriteLine("🔄 Running custom migration service (single-tenant mode)...");

            var connectionString = configuration.GetConnectionString("ApplicationDbContextConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                System.Console.WriteLine("⚠️ No connection string found. Skipping migration check.");
                System.Console.WriteLine("   This is normal during initial setup.");

                return new MigrationSummary
                {
                    IsSuccess = true,
                    SkippedCount = 1
                };
            }

            try
            {
                // Create logger for migration service
                var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
                var migrationLogger = loggerFactory.CreateLogger<MigrationService>();

                // Determine database provider
                var provider = MigrationService.DetermineProvider(connectionString);
                System.Console.WriteLine($"   Detected provider: {provider}");

                // Create temporary service scope for migration
                var tempServices = new ServiceCollection();
                tempServices.AddLogging(config => config.AddConsole());

                // Configure DbContext
                tempServices.AddDbContext<ApplicationDbContext>(options =>
                {
                    CosmosDbOptionsBuilder.ConfigureDbOptions(options, connectionString);
                });

                var tempServiceProvider = tempServices.BuildServiceProvider();

                // Phase 1: Run schema migrations
                using (var scope = tempServiceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var migrationContext = new MigrationContext
                    {
                        DbContext = dbContext,
                        Provider = provider,
                        ConnectionString = connectionString,
                        Logger = migrationLogger,
                        ServiceProvider = scope.ServiceProvider
                    };

                    var migrationService = new MigrationService(migrationLogger);
                    await migrationService.RunMigrationsAsync(migrationContext);

                    System.Console.WriteLine("✅ Custom schema migrations completed successfully");
                }

                // Phase 2: Run data migrations
                System.Console.WriteLine("🔄 Checking for layout versioning data migration...");

                int layoutCount = 0;
                using (var scope = tempServiceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var layoutMigrationLogger = loggerFactory.CreateLogger<LayoutMigrationService>();

                    var layoutMigrationService = new LayoutMigrationService(
                        dbContext,
                        layoutMigrationLogger);

                    if (await layoutMigrationService.NeedsMigrationAsync())
                    {
                        System.Console.WriteLine("📦 Layout data migration required - starting migration...");

                        layoutCount = await layoutMigrationService.MigrateLayoutNumbersAsync();
                        System.Console.WriteLine($"✅ Migrated {layoutCount} layouts to versioned families");

                        await layoutMigrationService.MigrateTemplateLayoutNumbersAsync();
                        System.Console.WriteLine("✅ Template LayoutNumbers updated");

                        System.Console.WriteLine("✅ Layout data migration completed successfully");
                    }
                    else
                    {
                        System.Console.WriteLine("✓ Layout versioning already configured - no data migration needed");
                    }
                }

                return new MigrationSummary
                {
                    IsSuccess = true,
                    SuccessCount = 1,
                    TenantResults = new System.Collections.Generic.List<TenantMigrationResult>
                    {
                        new TenantMigrationResult
                        {
                            DomainName = "Single-Tenant",
                            IsSuccess = true,
                            LayoutsMigrated = layoutCount
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ FATAL ERROR: Migration failed: {ex.Message}");
                System.Console.WriteLine($"   {ex.StackTrace}");
                System.Console.WriteLine("Application startup halted. Please fix the database configuration and restart.");

                return new MigrationSummary
                {
                    IsSuccess = false,
                    FailureCount = 1,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Runs migrations for all tenants in a multi-tenant deployment.
        /// </summary>
        private static async Task<MigrationSummary> RunMultiTenantMigrationsAsync(IConfiguration configuration)
        {
            System.Console.WriteLine("🔄 Running custom migration service (multi-tenant mode)...");

            var summary = new MigrationSummary { IsSuccess = true };

            try
            {
                var configDbConnectionString = configuration.GetConnectionString("ConfigDbConnectionString");

                // Create temporary services for multi-tenant migration
                var tempServices = new ServiceCollection();
                var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
                tempServices.AddSingleton<ILoggerFactory>(loggerFactory);
                tempServices.AddLogging(config => config.AddConsole());

                // Register DynamicConfigurationProvider for tenant discovery
                tempServices.AddSingleton<IConfiguration>(configuration);
                tempServices.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                tempServices.AddMemoryCache();
                tempServices.AddSingleton<IDynamicConfigurationProvider, DynamicConfigurationProvider>();

                var tempServiceProvider = tempServices.BuildServiceProvider();

                using (var scope = tempServiceProvider.CreateScope())
                {
                    var configProvider = scope.ServiceProvider.GetRequiredService<IDynamicConfigurationProvider>();

                    // Discover all tenant domain names from configuration database
                    var domainNames = await configProvider.GetAllDomainNamesAsync();

                    if (domainNames.Count == 0)
                    {
                        System.Console.WriteLine("⚠️ No tenant configurations found - skipping multi-tenant migration");
                        return summary;
                    }

                    System.Console.WriteLine($"   Found {domainNames.Count} tenant(s) to migrate");
                    summary.TotalProcessed = domainNames.Count;

                    // Process each tenant independently
                    foreach (var domainName in domainNames)
                    {
                        var tenantResult = await MigrateTenantAsync(
                            domainName,
                            configProvider,
                            loggerFactory);

                        summary.TenantResults.Add(tenantResult);

                        if (tenantResult.IsSuccess)
                        {
                            summary.SuccessCount++;
                        }
                        else if (tenantResult.WasSkipped)
                        {
                            summary.SkippedCount++;
                        }
                        else
                        {
                            summary.FailureCount++;
                        }
                    }
                }

                return summary;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"⚠️ WARNING: Multi-tenant migration failed: {ex.Message}");
                System.Console.WriteLine($"   {ex.StackTrace}");
                System.Console.WriteLine("   Application will continue, but migrations may not be applied to all tenants.");

                summary.IsSuccess = false;
                summary.ErrorMessage = ex.Message;
                summary.Exception = ex;

                return summary;
            }
        }

        /// <summary>
        /// Migrates a single tenant's database.
        /// </summary>
        private static async Task<TenantMigrationResult> MigrateTenantAsync(
            string domainName,
            IDynamicConfigurationProvider configProvider,
            ILoggerFactory loggerFactory)
        {
            var result = new TenantMigrationResult { DomainName = domainName };

            try
            {
                System.Console.WriteLine($"   Processing tenant: {domainName}");

                // Get connection string for this tenant
                var tenantConnectionString = await configProvider.GetDatabaseConnectionStringAsync(domainName);

                if (string.IsNullOrWhiteSpace(tenantConnectionString))
                {
                    System.Console.WriteLine($"      ⚠️ No connection string found for {domainName} - skipping");
                    result.WasSkipped = true;
                    return result;
                }

                // Determine provider for this tenant
                var provider = MigrationService.DetermineProvider(tenantConnectionString);

                // Create tenant-specific services
                var tenantServices = new ServiceCollection();
                tenantServices.AddLogging(config =>
                {
                    config.AddConsole();
                    config.SetMinimumLevel(LogLevel.Warning); // Reduce noise
                });

                tenantServices.AddDbContext<ApplicationDbContext>(options =>
                {
                    CosmosDbOptionsBuilder.ConfigureDbOptions(options, tenantConnectionString);
                });

                var tenantServiceProvider = tenantServices.BuildServiceProvider();

                using (var tenantScope = tenantServiceProvider.CreateScope())
                {
                    var tenantDbContext = tenantScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var tenantLogger = loggerFactory.CreateLogger<MigrationService>();

                    // Create migration context for this tenant
                    var migrationContext = new MigrationContext
                    {
                        DbContext = tenantDbContext,
                        Provider = provider,
                        ConnectionString = tenantConnectionString,
                        Logger = tenantLogger,
                        ServiceProvider = tenantScope.ServiceProvider
                    };

                    // Run custom schema migrations
                    var migrationService = new MigrationService(tenantLogger);
                    await migrationService.RunMigrationsAsync(migrationContext);

                    // Run data migrations (LayoutMigrationService)
                    var layoutMigrationLogger = loggerFactory.CreateLogger<LayoutMigrationService>();
                    var layoutMigrationService = new LayoutMigrationService(
                        tenantDbContext,
                        layoutMigrationLogger);

                    if (await layoutMigrationService.NeedsMigrationAsync())
                    {
                        var layoutCount = await layoutMigrationService.MigrateLayoutNumbersAsync();
                        await layoutMigrationService.MigrateTemplateLayoutNumbersAsync();
                        System.Console.WriteLine($"      ✅ {domainName}: Migrated {layoutCount} layouts");
                        result.LayoutsMigrated = layoutCount;
                    }
                    else
                    {
                        System.Console.WriteLine($"      ✓ {domainName}: Already migrated");
                    }

                    result.IsSuccess = true;
                }
            }
            catch (Exception tenantEx)
            {
                System.Console.WriteLine($"      ❌ {domainName}: Migration failed - {tenantEx.Message}");
                result.IsSuccess = false;
                result.ErrorMessage = tenantEx.Message;
                // Continue with next tenant - don't halt startup
            }

            return result;
        }
    }
}
