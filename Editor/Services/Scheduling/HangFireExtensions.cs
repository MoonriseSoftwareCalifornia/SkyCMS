// <copyright file="HangFireExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Scheduling
{
    using System;
    using System.Linq;
    using AspNetCore.Identity.FlexDb;
    using AspNetCore.Identity.FlexDb.Strategies;
    using Hangfire;
    using Hangfire.MySql;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///  HangFire extensions for service collection and application configuration.
    /// </summary>
    public static class HangFireExtensions
    {
        private static bool hangfireConfigured = false;

        /// <summary>
        ///  Adds HangFire scheduling services to the service collection.
        ///  Database schema is guaranteed to exist by SingleTenant.Configure() or MultiTenant.Configure().
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="config">Configuration.</param>
        public static void AddHangFireScheduling(this IServiceCollection services, IConfiguration config)
        {
            var multi = config.GetValue<bool?>("MultiTenantEditor") ?? false;
            var connectionStringName = multi ? "ConfigDbConnectionString" : "ApplicationDbContextConnection";
            var connectionString = config.GetConnectionString(connectionStringName);

            // If no connection string, setup is not complete - don't configure Hangfire
            if (string.IsNullOrEmpty(connectionString))
            {
                hangfireConfigured = false;
                return;
            }

            // Database schema is already initialized by SingleTenant.Configure() or MultiTenant.Configure()
            // Safe to configure Hangfire
            ConfigureHangfireStorage(services, config, multi);
            AddHangfireServer(services);
            hangfireConfigured = true;
        }

        /// <summary>
        ///  Maps the HangFire dashboard (only if Hangfire was configured).
        /// </summary>
        /// <param name="app">Web application.</param>
        public static void UseHangfireSchedulingSlice(this WebApplication app)
        {
            var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();

            // Only activate dashboard if Hangfire was configured during service registration
            if (hangfireConfigured)
            {
                //logger.LogInformation("Activating Hangfire dashboard at /Editor/CCMS___PageScheduler");
                app.UseHangfireDashboard("/Editor/CCMS___PageScheduler", new DashboardOptions()
                {
                    DashboardTitle = "SkyCMS - Page Scheduler",
                    Authorization = new[] { new HangfireAuthorizationFilter() },
                });
            }
            else
            {
                logger.LogInformation("Hangfire not configured - setup not complete");
            }
        }

        /// <summary>
        ///  Configures Hangfire storage based on the database provider.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="config">Configuration.</param>
        /// <param name="multi">Multi-tenant flag.</param>
        private static void ConfigureHangfireStorage(IServiceCollection services, IConfiguration config, bool multi)
        {
            if (multi)
            {
                services.AddHangfire(hangfireConfig =>
                {
                    // Do this for now until I get the multi-tenant storage figured out.
                    hangfireConfig.UseInMemoryStorage();
                });
                return;
            }

            var connectionString = config.GetConnectionString("ApplicationDbContextConnection");

            // Determine database provider
            var isCosmosDb = CosmosDbOptionsBuilder
                .GetDefaultStrategies()
                .OfType<CosmosDbConfigurationStrategy>()
                .Any(strategy => strategy.CanHandle(connectionString));

            var isMsSql = CosmosDbOptionsBuilder
                .GetDefaultStrategies()
                .OfType<SqlServerConfigurationStrategy>()
                .Any(strategy => strategy.CanHandle(connectionString));

            var isMySql = CosmosDbOptionsBuilder
                .GetDefaultStrategies()
                .OfType<MySqlConfigurationStrategy>()
                .Any(strategy => strategy.CanHandle(connectionString));

            var isSqlite = CosmosDbOptionsBuilder
                .GetDefaultStrategies()
                .OfType<SqliteConfigurationStrategy>()
                .Any(strategy => strategy.CanHandle(connectionString));

            if (isCosmosDb)
            {
                services.AddHangfire(hangfireConfig =>
                {
                    var accountProperties = CosmosDbConfigurationStrategy.GetAccountProperties(connectionString);
                    hangfireConfig.UseAzureCosmosDbStorage(
                        accountProperties.AccountEndpoint,
                        accountProperties.AccountKey,
                        accountProperties.DatabaseName,
                        "hangfire",
                        new CosmosClientOptions());
                });
            }
            else if (isMsSql)
            {
                services.AddHangfire(hangfireConfig =>
                {
                    hangfireConfig.UseSqlServerStorage(connectionString);
                });
            }
            else if (isMySql)
            {
                services.AddHangfire(hangfireConfig =>
                {
                    hangfireConfig.UseStorage(
                        new MySqlStorage(
                            connectionString + "Allow User Variables=true;", 
                            new MySqlStorageOptions
                            {
                                // Increase timeout for maintenance operations (default is 30s)
                                TransactionTimeout = TimeSpan.FromMinutes(2),
                                // Additional optimizations for better performance
                                QueuePollInterval = TimeSpan.FromSeconds(15),
                                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                                CountersAggregateInterval = TimeSpan.FromMinutes(5),
                                PrepareSchemaIfNecessary = false // Schema already created
                            }));
                });
            }
            else if (isSqlite)
            {
                services.AddHangfire(hangfireConfig =>
                {
                    hangfireConfig.UseInMemoryStorage(); // SQLite with password is not supported directly by Hangfire.SQLite
                });
            }
            else
            {
                services.AddHangfire(hangfireConfig =>
                {
                    hangfireConfig.UseInMemoryStorage();
                });
            }
        }

        /// <summary>
        ///  Adds Hangfire server to the service collection.
        /// </summary>
        /// <param name="services">Service collection.</param>
        private static void AddHangfireServer(IServiceCollection services)
        {
            services.AddHangfireServer(options =>
            {
                options.Queues = new[] { "critical", "default" };
                options.WorkerCount = Math.Max(Environment.ProcessorCount, 1);
                options.SchedulePollingInterval = TimeSpan.FromMinutes(10);
                options.ShutdownTimeout = TimeSpan.FromMinutes(2);
                options.HeartbeatInterval = TimeSpan.FromMinutes(5);
            });
        }
    }
}
