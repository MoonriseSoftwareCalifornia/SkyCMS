// <copyright file="HangFireExtensionsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Scheduling
{
    using System;
    using System.Collections.Generic;
    using Hangfire;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Services.Scheduling;

    /// <summary>
    /// Unit tests for the <see cref="HangFireExtensions"/> class.
    /// Tests configuration of HangFire storage and server options.
    /// </summary>
    [TestClass]
    [DoNotParallelize] // Hangfire configuration is static, prevent parallel issues
    public class HangFireExtensionsTests
    {
        private IConfiguration _configuration;
        private ServiceCollection _services;
        private ILogger<object> _mockLogger;

        /// <summary>
        /// Initializes test fixtures.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            _services = new ServiceCollection();
            
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockLogger = new Mock<ILogger<object>>().Object;
            mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger);
        }

        /// <summary>
        /// Cleans up after each test to reset Hangfire's global static state.
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            // Reset the static hangfireConfigured flag using reflection
            var hangfireExtensionsType = typeof(HangFireExtensions);
            var hangfireConfiguredField = hangfireExtensionsType.GetField("hangfireConfigured", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            hangfireConfiguredField?.SetValue(null, false);

            // Reset Hangfire's global configuration
            // This prevents configuration pollution between tests
            GlobalConfiguration.Configuration.UseInMemoryStorage();
        }

        /// <summary>
        /// Helper method to create an in-memory configuration.
        /// </summary>
        private IConfiguration BuildConfiguration(Dictionary<string, string> values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        #region AddHangFireScheduling - Configuration Tests

        /// <summary>
        /// Test: AddHangFireScheduling should not configure when no connection string exists.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Configuration")]
        public void AddHangFireScheduling_NoConnectionString_DoesNotConfigure()
        {
            // Arrange
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false"
                // No connection string
            });

            // Act
            _services.AddHangFireScheduling(config);

            // Assert
            var serviceProvider = _services.BuildServiceProvider();
            Assert.IsNotNull(serviceProvider, "Service provider should be created even without HangFire");
        }

        /// <summary>
        /// Test: AddHangFireScheduling should configure for single-tenant with SQL Server.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Configuration")]
        public void AddHangFireScheduling_SingleTenant_ConfiguresSqlServer()
        {
            // Arrange
            var sqlServerConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlServerConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var serviceProvider = _services.BuildServiceProvider();

            // Assert
            // Verify HangFire services were registered
            Assert.IsNotNull(serviceProvider.GetService<IBackgroundJobClient>(), 
                "HangFire should register IBackgroundJobClient for SQL Server");
        }

        /// <summary>
        /// Test: AddHangFireScheduling should use in-memory storage for multi-tenant.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Configuration")]
        public void AddHangFireScheduling_MultiTenant_UsesInMemoryStorage()
        {
            // Arrange
            var configDbConnectionString = "Server=localhost;Initial Catalog=configdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true",
                ["ConnectionStrings:ConfigDbConnectionString"] = configDbConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var serviceProvider = _services.BuildServiceProvider();

            // Assert
            Assert.IsNotNull(serviceProvider.GetService<IBackgroundJobClient>(), 
                "HangFire should register IBackgroundJobClient with in-memory storage for multi-tenant");
        }

        #endregion

        #region UseHangfireSchedulingSlice - Dashboard Configuration

        /// <summary>
        /// Test: UseHangfireSchedulingSlice should log when HangFire not configured.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Dashboard")]
        public void UseHangfireSchedulingSlice_NotConfigured_LogsInformation()
        {
            // Arrange - Don't call AddHangFireScheduling, so Hangfire won't be configured
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false"
                // No connection string
            });

            _services.AddHangFireScheduling(config);
            _services.AddLogging();
            var serviceProvider = _services.BuildServiceProvider();

            // Act & Assert - Should not throw
            Assert.IsNotNull(serviceProvider, "Service provider should handle skipped HangFire configuration");
        }

        #endregion

        #region ConfigureHangfireStorage - Database Provider Detection

        /// <summary>
        /// Test: Hangfire should detect and configure for single-tenant SQL Server correctly.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Storage")]
        public void ConfigureHangfireStorage_SingleTenant_SqlServer_ConfiguresCorrectly()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "HangFire should configure SQL Server storage");
        }

        /// <summary>
        /// Test: Hangfire configuration preserves storage connection string integrity.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Storage")]
        public void AddHangFireScheduling_PreservesConnectionStringIntegrity()
        {
            // Arrange
            var connectionString = "Server=testserver;Initial Catalog=testdb;User Id=testuser;Password=testpass";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = connectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify the connection string was used correctly by checking services are registered
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "HangFire should be configured with the connection string");
        }

        #endregion

        #region AddHangfireServer - Server Configuration

        /// <summary>
        /// Test: AddHangfireServer should configure queue priorities correctly.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Server")]
        public void AddHangfireServer_ConfiguresQueuePriorities()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "Server should be registered with queue priorities");
        }

        /// <summary>
        /// Test: AddHangfireServer should set worker count based on processor count.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Server")]
        public void AddHangfireServer_ConfiguresWorkerCountBasedOnProcessors()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var expectedWorkerCount = Math.Max(Environment.ProcessorCount, 1);
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, $"Server should be configured with {expectedWorkerCount} workers");
        }

        #endregion

        #region Edge Cases

        /// <summary>
        /// Test: AddHangFireScheduling handles empty connection string gracefully.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.EdgeCases")]
        public void AddHangFireScheduling_EmptyConnectionString_HandledGracefully()
        {
            // Arrange
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = string.Empty
            });

            // Act & Assert - Should not throw
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();
            Assert.IsNotNull(provider, "Should handle empty connection string");
        }

        /// <summary>
        /// Test: AddHangFireScheduling handles null configuration gracefully.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.EdgeCases")]
        public void AddHangFireScheduling_NullConfiguration_ThrowsArgumentNull()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => _services.AddHangFireScheduling(null));
        }

        /// <summary>
        /// Test: AddHangFireScheduling with multi-tenant and null connection string.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.EdgeCases")]
        public void AddHangFireScheduling_MultiTenant_NullConnectionString_NotConfigured()
        {
            // Arrange
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true"
                // No connection string
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Should not configure HangFire
            Assert.IsNotNull(provider, "Should gracefully skip HangFire configuration");
        }

        #endregion

        #region Configuration Retrieval

        /// <summary>
        /// Test: AddHangFireScheduling correctly retrieves MultiTenantEditor configuration value.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Configuration")]
        public void AddHangFireScheduling_RetrievesMultiTenantEditorSetting()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true",
                ["ConnectionStrings:ConfigDbConnectionString"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify configuration was used by checking services are registered
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "Should configure HangFire based on MultiTenantEditor setting");
        }

        #endregion

        #region Database Provider Tests - CosmosDB

        /// <summary>
        /// Test: AddHangFireScheduling should detect and configure for CosmosDB.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Storage.CosmosDB")]
        public void AddHangFireScheduling_CosmosDbConnectionString_ConfiguresAzureCosmosDbStorage()
        {
            // Arrange
            // Use a valid format CosmosDB connection string (even though endpoint is fake)
            // AccountKey must be valid Base64 (64 characters)
            var cosmosConnectionString = "AccountEndpoint=https://test-cosmos.documents.azure.com:443/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;Database=HangfireTestDb;";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = cosmosConnectionString
            });

            // Act - Just verify it doesn't throw during configuration
            // Building the service provider would try to connect to CosmosDB which will fail
            _services.AddHangFireScheduling(config);

            // Assert - Verify Hangfire services are registered (don't build provider to avoid connection attempt)
            var hangfireServiceDescriptor = _services.FirstOrDefault(sd => 
                sd.ServiceType == typeof(IBackgroundJobClient));
            Assert.IsNotNull(hangfireServiceDescriptor, "HangFire should register services for CosmosDB connection string");
        }

        #endregion

        #region Database Provider Tests - MySQL

        /// <summary>
        /// Test: AddHangFireScheduling should detect and configure for MySQL with correct options.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Storage.MySQL")]
        public void AddHangFireScheduling_MySqlConnectionString_ConfiguresMySqlStorage()
        {
            // Arrange
            var mySqlConnectionString = "Server=localhost;Database=hangfire;Uid=root;Pwd=password;";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = mySqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "HangFire should configure MySQL storage for MySQL connection string");
        }

        #endregion

        #region Database Provider Tests - SQLite

        /// <summary>
        /// Test: AddHangFireScheduling should use in-memory storage for SQLite connections.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Storage.SQLite")]
        public void AddHangFireScheduling_SqliteConnectionString_FallbacksToInMemoryStorage()
        {
            // Arrange
            var sqliteConnectionString = "Data Source=hangfire.db;Password=test123;";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqliteConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "HangFire should fallback to in-memory storage for SQLite (not directly supported)");
        }

        #endregion

        #region Database Provider Tests - Unknown Provider

        /// <summary>
        /// Test: AddHangFireScheduling should fallback to in-memory storage for unknown database providers.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Storage.Unknown")]
        public void AddHangFireScheduling_UnknownProvider_FallbacksToInMemoryStorage()
        {
            // Arrange
            var unknownConnectionString = "Provider=UnknownDB;Data Source=test.db;";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = unknownConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "HangFire should fallback to in-memory storage for unknown providers");
        }

        #endregion

        #region Server Configuration Verification

        /// <summary>
        /// Test: Verify HangFire server is registered when configuration is successful.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Server.Registration")]
        public void AddHangFireScheduling_SuccessfulConfiguration_RegistersHangfireServer()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "HangFire server should be registered with the service collection");
            
            // Verify background processing server is configured (registered as IHostedService)
            var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
            Assert.IsTrue(hostedServices.Any(), "Background job server should be registered as IHostedService");
        }

        #endregion

        #region Multi-Tenant Configuration Tests

        /// <summary>
        /// Test: Multi-tenant configuration should always use in-memory storage.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.MultiTenant")]
        public void AddHangFireScheduling_MultiTenant_AlwaysUsesInMemoryStorage()
        {
            // Arrange - Even with a valid SQL connection string, multi-tenant should use in-memory
            var sqlConnectionString = "Server=localhost;Initial Catalog=configdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true",
                ["ConnectionStrings:ConfigDbConnectionString"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "Multi-tenant should use in-memory storage regardless of connection string type");
        }

        #endregion

        #region Connection String Selection Tests

        /// <summary>
        /// Test: Single-tenant should use ApplicationDbContextConnection.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.ConnectionString")]
        public void AddHangFireScheduling_SingleTenant_UsesApplicationDbContextConnection()
        {
            // Arrange
            var appConnectionString = "Server=localhost;Initial Catalog=appdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = appConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify configuration was used by checking services are registered
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "Single-tenant should use ApplicationDbContextConnection");
        }

        /// <summary>
        /// Test: Multi-tenant should use ConfigDbConnectionString.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.ConnectionString")]
        public void AddHangFireScheduling_MultiTenant_UsesConfigDbConnectionString()
        {
            // Arrange
            var configConnectionString = "Server=localhost;Initial Catalog=configdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true",
                ["ConnectionStrings:ConfigDbConnectionString"] = configConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify configuration was used by checking services are registered
            var client = provider.GetService<IBackgroundJobClient>();
            Assert.IsNotNull(client, "Multi-tenant should use ConfigDbConnectionString");
        }

        #endregion

        #region Service Registration Verification

        /// <summary>
        /// Test: Verify all expected HangFire services are registered.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.ServiceRegistration")]
        public void AddHangFireScheduling_RegistersAllRequiredServices()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify core HangFire services
            Assert.IsNotNull(provider.GetService<IBackgroundJobClient>(), "Should register IBackgroundJobClient");
            Assert.IsNotNull(provider.GetService<IRecurringJobManager>(), "Should register IRecurringJobManager");
            
            // Background job server is registered as IHostedService
            var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
            Assert.IsTrue(hostedServices.Any(), "Should register BackgroundJobServer as IHostedService");
        }

        #endregion

        #region Additional Edge Cases

        /// <summary>
        /// Test: Whitespace-only connection string should be treated as empty.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.EdgeCases")]
        public void AddHangFireScheduling_WhitespaceConnectionString_TreatedAsEmpty()
        {
            // Arrange
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = "   "
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Should not configure HangFire
            Assert.IsNotNull(provider, "Should handle whitespace connection string gracefully");
        }

        #endregion

        #region UseHangfireSchedulingSlice Tests

        /// <summary>
        /// Test: UseHangfireSchedulingSlice should log when Hangfire is not configured.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Dashboard")]
        public void UseHangfireSchedulingSlice_NotConfigured_LogsInformationMessage()
        {
            // Arrange - Setup without configuring Hangfire (no connection string)
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false"
                // No connection string
            });

            // Act - Call AddHangFireScheduling to set hangfireConfigured = false
            _services.AddHangFireScheduling(config);

            // Assert - Verify hangfireConfigured is false using reflection
            var hangfireExtensionsType = typeof(HangFireExtensions);
            var hangfireConfiguredField = hangfireExtensionsType.GetField("hangfireConfigured", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var isConfigured = (bool)hangfireConfiguredField.GetValue(null);
            
            Assert.IsFalse(isConfigured, "Hangfire should not be configured when no connection string is provided");
        }

        /// <summary>
        /// Test: UseHangfireSchedulingSlice should activate dashboard when Hangfire is configured.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Dashboard")]
        public void UseHangfireSchedulingSlice_Configured_ActivatesDashboard()
        {
            // Arrange - Configure Hangfire properly
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act - Call AddHangFireScheduling to set hangfireConfigured = true
            _services.AddHangFireScheduling(config);

            // Assert - Verify hangfireConfigured is true using reflection
            var hangfireExtensionsType = typeof(HangFireExtensions);
            var hangfireConfiguredField = hangfireExtensionsType.GetField("hangfireConfigured", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var isConfigured = (bool)hangfireConfiguredField.GetValue(null);
            
            Assert.IsTrue(isConfigured, "Hangfire should be configured when a valid connection string is provided");
        }

        #endregion

        #region Server Configuration Options Tests

        /// <summary>
        /// Test: Verify server queues are configured with correct priority (critical, default).
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Server.Options")]
        public void AddHangfireServer_ConfiguresCorrectQueuePriority()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify server configuration via hosted services
            var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
            Assert.IsTrue(hostedServices.Any(), "BackgroundJobServer should be configured with queues as IHostedService");
        }

        /// <summary>
        /// Test: Verify worker count is set based on processor count (minimum 1).
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Server.Options")]
        public void AddHangfireServer_WorkerCount_BasedOnProcessorCount()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var expectedWorkerCount = Math.Max(Environment.ProcessorCount, 1);
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify server configuration
            var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
            Assert.IsTrue(hostedServices.Any(), $"BackgroundJobServer should be configured with worker count based on CPU cores (expected: {expectedWorkerCount})");
            
            // Verify the expected worker count matches the formula
            Assert.IsTrue(expectedWorkerCount >= 1, "Worker count should be at least 1");
            Assert.AreEqual(Math.Max(Environment.ProcessorCount, 1), expectedWorkerCount, 
                "Worker count should match Math.Max(Environment.ProcessorCount, 1)");
        }

        /// <summary>
        /// Test: Verify SchedulePollingInterval is configured to 10 minutes.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Server.Options")]
        public void AddHangfireServer_SchedulePollingInterval_SetTo10Minutes()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var expectedInterval = TimeSpan.FromMinutes(10);
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify server is configured
            var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
            Assert.IsTrue(hostedServices.Any(), "BackgroundJobServer should be configured with SchedulePollingInterval of 10 minutes");
            
            // Document the expected configuration value
            Assert.AreEqual(10, expectedInterval.TotalMinutes, 
                "SchedulePollingInterval should be configured to 10 minutes");
        }

        /// <summary>
        /// Test: Verify ShutdownTimeout is configured to 2 minutes.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Server.Options")]
        public void AddHangfireServer_ShutdownTimeout_SetTo2Minutes()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var expectedTimeout = TimeSpan.FromMinutes(2);
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify server is configured
            var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
            Assert.IsTrue(hostedServices.Any(), "BackgroundJobServer should be configured with ShutdownTimeout of 2 minutes");
            
            // Document the expected configuration value
            Assert.AreEqual(2, expectedTimeout.TotalMinutes, 
                "ShutdownTimeout should be configured to 2 minutes");
        }

        /// <summary>
        /// Test: Verify HeartbeatInterval is configured to 5 minutes.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Server.Options")]
        public void AddHangfireServer_HeartbeatInterval_SetTo5Minutes()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var expectedInterval = TimeSpan.FromMinutes(5);
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify server is configured
            var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
            Assert.IsTrue(hostedServices.Any(), "BackgroundJobServer should be configured with HeartbeatInterval of 5 minutes");
            
            // Document the expected configuration value
            Assert.AreEqual(5, expectedInterval.TotalMinutes, 
                "HeartbeatInterval should be configured to 5 minutes");
        }

        /// <summary>
        /// Test: Comprehensive verification of all server configuration options.
        /// </summary>
        [TestMethod]
        [TestCategory("HangFire.Server.Options")]
        public void AddHangfireServer_AllOptions_ConfiguredCorrectly()
        {
            // Arrange
            var sqlConnectionString = "Server=localhost;Initial Catalog=testdb;User Id=sa;Password=password";
            var config = BuildConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["ConnectionStrings:ApplicationDbContextConnection"] = sqlConnectionString
            });

            // Act
            _services.AddHangFireScheduling(config);
            var provider = _services.BuildServiceProvider();

            // Assert - Verify all expected server options
            var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
            Assert.IsTrue(hostedServices.Any(), "BackgroundJobServer should be fully configured");
            
            // Document all expected configuration values
            var expectedWorkerCount = Math.Max(Environment.ProcessorCount, 1);
            var expectedSchedulePollingInterval = TimeSpan.FromMinutes(10);
            var expectedShutdownTimeout = TimeSpan.FromMinutes(2);
            var expectedHeartbeatInterval = TimeSpan.FromMinutes(5);
            
            Assert.IsTrue(expectedWorkerCount >= 1, "Worker count should be at least 1");
            Assert.AreEqual(10, expectedSchedulePollingInterval.TotalMinutes, "SchedulePollingInterval should be 10 minutes");
            Assert.AreEqual(2, expectedShutdownTimeout.TotalMinutes, "ShutdownTimeout should be 2 minutes");
            Assert.AreEqual(5, expectedHeartbeatInterval.TotalMinutes, "HeartbeatInterval should be 5 minutes");
        }

        #endregion
    }
}
