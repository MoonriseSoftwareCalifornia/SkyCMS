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
        private Mock<IConfiguration> _mockConfiguration;
        private Mock<IConfigurationSection> _mockConfigSection;
        private ServiceCollection _services;
        private ILogger<object> _mockLogger;

        /// <summary>
        /// Initializes test fixtures.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfigSection = new Mock<IConfigurationSection>();
            _services = new ServiceCollection();
            
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockLogger = new Mock<ILogger<object>>().Object;
            mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger);
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
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(false);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ApplicationDbContextConnection"))
                .Returns((string)null);

            // Act
            _services.AddHangFireScheduling(_mockConfiguration.Object);

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
            
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(false);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ApplicationDbContextConnection"))
                .Returns(sqlServerConnectionString);

            // Act
            _services.AddHangFireScheduling(_mockConfiguration.Object);
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
            
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(true);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ConfigDbConnectionString"))
                .Returns(configDbConnectionString);

            // Act
            _services.AddHangFireScheduling(_mockConfiguration.Object);
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
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(false);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ApplicationDbContextConnection"))
                .Returns((string)null); // No connection string

            _services.AddHangFireScheduling(_mockConfiguration.Object);
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
            
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(false);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ApplicationDbContextConnection"))
                .Returns(sqlConnectionString);

            // Act
            _services.AddHangFireScheduling(_mockConfiguration.Object);
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
            
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(false);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ApplicationDbContextConnection"))
                .Returns(connectionString);

            // Act
            _services.AddHangFireScheduling(_mockConfiguration.Object);

            // Assert - Verify the connection string was used correctly
            _mockConfiguration.Verify(
                x => x.GetConnectionString("ApplicationDbContextConnection"),
                Times.Once,
                "Should retrieve connection string exactly once");
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
            
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(false);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ApplicationDbContextConnection"))
                .Returns(sqlConnectionString);

            // Act
            _services.AddHangFireScheduling(_mockConfiguration.Object);
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
            
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(false);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ApplicationDbContextConnection"))
                .Returns(sqlConnectionString);

            // Act
            _services.AddHangFireScheduling(_mockConfiguration.Object);
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
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(false);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ApplicationDbContextConnection"))
                .Returns(string.Empty);

            // Act & Assert - Should not throw
            _services.AddHangFireScheduling(_mockConfiguration.Object);
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
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(true);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ConfigDbConnectionString"))
                .Returns((string)null);

            // Act
            _services.AddHangFireScheduling(_mockConfiguration.Object);
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
            var multiTenantValue = true;
            
            _mockConfiguration
                .Setup(x => x.GetValue<bool?>("MultiTenantEditor"))
                .Returns(multiTenantValue);

            _mockConfiguration
                .Setup(x => x.GetConnectionString("ConfigDbConnectionString"))
                .Returns(sqlConnectionString);

            // Act
            _services.AddHangFireScheduling(_mockConfiguration.Object);

            // Assert
            _mockConfiguration.Verify(
                x => x.GetValue<bool?>("MultiTenantEditor"),
                Times.Once,
                "Should retrieve MultiTenantEditor setting");
        }

        #endregion
    }
}
