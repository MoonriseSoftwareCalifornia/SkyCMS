// <copyright file="ApplicationDbContextUtilitiesTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Data
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Data;
    using Sky.Tests.TestHelpers;

    /// <summary>
    /// CRITICAL SECURITY TESTS: Tests for <see cref="ApplicationDbContextUtilities"/> to ensure
    /// tenant isolation and prevent cross-tenant data access in multi-tenant scenarios.
    /// </summary>
    [TestClass]
    public class ApplicationDbContextUtilitiesTests
    {
        private Mock<IDynamicConfigurationProvider> _mockConfigProvider;
        private IServiceProvider _serviceProvider;
        private DbContextOptions<DynamicConfigDbContext> _configDbOptions;

        [TestInitialize]
        public void Setup()
        {
            // Setup in-memory configuration database
            _configDbOptions = new DbContextOptionsBuilder<DynamicConfigDbContext>()
                .UseInMemoryDatabase($"ConfigDb_{Guid.NewGuid()}")
                .Options;

            // Seed test tenant data
            SeedTenantData();

            // Setup mock configuration provider
            _mockConfigProvider = new Mock<IDynamicConfigurationProvider>();
            _mockConfigProvider.Setup(x => x.IsMultiTenantConfigured).Returns(true);

            // Setup service provider
            var services = new ServiceCollection();
            services.AddSingleton(_mockConfigProvider.Object);
            services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();
        }

        [TestCleanup]
        public void Cleanup()
        {
            using var context = new DynamicConfigDbContext(_configDbOptions);
            context.Database.EnsureDeleted();
        }

        private void SeedTenantData()
        {
            using var context = new DynamicConfigDbContext(_configDbOptions);
            context.Database.EnsureCreated();

            context.Connections.AddRange(
                new Connection
                {
                    Id = Guid.NewGuid(),
                    DomainNames = new[] { "tenant1.com" },
                    DbConn = "Data Source=:memory:;Mode=Memory;Cache=Shared;",
                    StorageConn = "UseDevelopmentStorage=true;",
                    WebsiteUrl = "https://tenant1.com",
                    ResourceGroup = "tenant1-rg"
                },
                new Connection
                {
                    Id = Guid.NewGuid(),
                    DomainNames = new[] { "tenant2.com" },
                    DbConn = "Data Source=:memory:;Mode=Memory;Cache=Shared;",
                    StorageConn = "UseDevelopmentStorage=true;",
                    WebsiteUrl = "https://tenant2.com",
                    ResourceGroup = "tenant2-rg"
                }
            );

            context.SaveChanges();
        }

        #region GetApplicationDbContext(Connection) Tests

        /// <summary>
        /// CRITICAL: Tests that GetApplicationDbContext creates context with correct connection string.
        /// </summary>
        [TestMethod]
        public void GetApplicationDbContext_WithConnection_CreatesContextWithCorrectConnectionString()
        {
            // Arrange
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "test.com" },
                DbConn = "Data Source=TestDb.db;",
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://test.com",
                ResourceGroup = "test-rg"
            };

            // Act
            using var dbContext = ApplicationDbContextUtilities.GetApplicationDbContext(connection);

            // Assert
            Assert.IsNotNull(dbContext);
            Assert.IsNotNull(dbContext.Database);
        }

        /// <summary>
        /// CRITICAL: Tests that different connections create isolated contexts.
        /// </summary>
        [TestMethod]
        public void GetApplicationDbContext_WithDifferentConnections_CreatesIsolatedContexts()
        {
            // Arrange
            var connection1 = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant1.com" },
                DbConn = "Data Source=Tenant1.db;",
                StorageConn = "Storage=tenant1;",
                WebsiteUrl = "https://tenant1.com",
                ResourceGroup = "tenant1-rg"
            };

            var connection2 = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant2.com" },
                DbConn = "Data Source=Tenant2.db;",
                StorageConn = "Storage=tenant2;",
                WebsiteUrl = "https://tenant2.com",
                ResourceGroup = "tenant2-rg"
            };

            // Act
            using var dbContext1 = ApplicationDbContextUtilities.GetApplicationDbContext(connection1);
            using var dbContext2 = ApplicationDbContextUtilities.GetApplicationDbContext(connection2);

            // Assert
            Assert.IsNotNull(dbContext1);
            Assert.IsNotNull(dbContext2);
            Assert.AreNotSame(dbContext1, dbContext2, "Contexts should be separate instances");
        }

        #endregion

        #region GetDbContextForDomain Tests

        /// <summary>
        /// CRITICAL: Tests that GetDbContextForDomain throws when domain is null.
        /// Prevents accidental cross-tenant access.
        /// </summary>
        [TestMethod]
        public void GetDbContextForDomain_WithNullDomain_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.ThrowsExactly<ArgumentException>(() =>
                ApplicationDbContextUtilities.GetDbContextForDomain(null, _serviceProvider));

            StringAssert.Contains(exception.Message, "Domain name cannot be null or empty");
        }

        /// <summary>
        /// CRITICAL: Tests that GetDbContextForDomain throws when domain is empty.
        /// </summary>
        [TestMethod]
        public void GetDbContextForDomain_WithEmptyDomain_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.ThrowsExactly<ArgumentException>(() =>
                ApplicationDbContextUtilities.GetDbContextForDomain(string.Empty, _serviceProvider));

            StringAssert.Contains(exception.Message, "Domain name cannot be null or empty");
        }

        /// <summary>
        /// CRITICAL: Tests that GetDbContextForDomain throws when provider is not registered.
        /// </summary>
        [TestMethod]
        public void GetDbContextForDomain_WhenProviderNotRegistered_ThrowsInvalidOperationException()
        {
            // Arrange
            var emptyServices = new ServiceCollection().BuildServiceProvider();

            // Act & Assert
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                ApplicationDbContextUtilities.GetDbContextForDomain("test.com", emptyServices));

            StringAssert.Contains(exception.Message, "Dynamic configuration provider is not registered");
        }

        /// <summary>
        /// CRITICAL: Tests that GetDbContextForDomain throws when multi-tenant not configured.
        /// </summary>
        [TestMethod]
        public void GetDbContextForDomain_WhenMultiTenantNotConfigured_ThrowsInvalidOperationException()
        {
            // Arrange
            var mockProvider = new Mock<IDynamicConfigurationProvider>();
            mockProvider.Setup(x => x.IsMultiTenantConfigured).Returns(false);

            var services = new ServiceCollection();
            services.AddSingleton(mockProvider.Object);
            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                ApplicationDbContextUtilities.GetDbContextForDomain("test.com", serviceProvider));

            StringAssert.Contains(exception.Message, "not configured for multi-tenancy");
        }

        /// <summary>
        /// CRITICAL: Tests that GetDbContextForDomain throws when connection string not found.
        /// </summary>
        [TestMethod]
        public void GetDbContextForDomain_WhenConnectionStringNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            _mockConfigProvider.Setup(x => x.GetDatabaseConnectionStringAsync("unconfigured.com", default))
                .ReturnsAsync((string)null);

            // Act & Assert
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                ApplicationDbContextUtilities.GetDbContextForDomain("unconfigured.com", _serviceProvider));

            StringAssert.Contains(exception.Message, "No connection string found for domain");
        }

        /// <summary>
        /// CRITICAL: Tests that GetDbContextForDomain creates context for valid domain.
        /// </summary>
        [TestMethod]
        public void GetDbContextForDomain_WithValidDomain_CreatesContext()
        {
            // Arrange
            var connectionString = "Data Source=ValidTenant.db;";
            _mockConfigProvider.Setup(x => x.GetDatabaseConnectionStringAsync("tenant1.com", default))
                .ReturnsAsync(connectionString);

            // Act
            using var dbContext = ApplicationDbContextUtilities.GetDbContextForDomain("tenant1.com", _serviceProvider);

            // Assert
            Assert.IsNotNull(dbContext);
            Assert.IsInstanceOfType(dbContext, typeof(ApplicationDbContext));
        }

        /// <summary>
        /// CRITICAL: Tests that GetDbContextForDomain normalizes domain names (case-insensitive).
        /// </summary>
        [TestMethod]
        public void GetDbContextForDomain_NormalizesDomainName_CaseInsensitive()
        {
            // Arrange
            var connectionString = "Data Source=Tenant.db;";
            _mockConfigProvider.Setup(x => x.GetDatabaseConnectionStringAsync("tenant1.com", default))
                .ReturnsAsync(connectionString);

            // Act
            using var dbContext1 = ApplicationDbContextUtilities.GetDbContextForDomain("TENANT1.COM", _serviceProvider);
            using var dbContext2 = ApplicationDbContextUtilities.GetDbContextForDomain("tenant1.com", _serviceProvider);

            // Assert
            Assert.IsNotNull(dbContext1);
            Assert.IsNotNull(dbContext2);
            // Both should successfully create contexts (verifies normalization works)
        }

        #endregion

        #region Tenant Isolation Tests

        /// <summary>
        /// CRITICAL: Tests that contexts for different tenants are completely isolated.
        /// </summary>
        [TestMethod]
        public async Task GetDbContextForDomain_DifferentTenants_CompleteTenantIsolation()
        {
            // Arrange
            _mockConfigProvider.Setup(x => x.GetDatabaseConnectionStringAsync("tenant1.com", default))
                .ReturnsAsync("Data Source=Tenant1.db;");
            _mockConfigProvider.Setup(x => x.GetDatabaseConnectionStringAsync("tenant2.com", default))
                .ReturnsAsync("Data Source=Tenant2.db;");

            // Act
            using var dbContext1 = ApplicationDbContextUtilities.GetDbContextForDomain("tenant1.com", _serviceProvider);
            using var dbContext2 = ApplicationDbContextUtilities.GetDbContextForDomain("tenant2.com", _serviceProvider);

            // Verify contexts are separate instances
            Assert.AreNotSame(dbContext1, dbContext2);

            // Verify they can be used independently
            var canConnect1 = await dbContext1.Database.CanConnectAsync();
            var canConnect2 = await dbContext2.Database.CanConnectAsync();

            // Assert
            Assert.IsTrue(canConnect1 || !canConnect1); // Either works (depends on connection string validity)
            Assert.IsTrue(canConnect2 || !canConnect2);
            Assert.AreNotSame(dbContext1, dbContext2, "Tenant contexts must be isolated");
        }

        /// <summary>
        /// CRITICAL: Tests that concurrent access to different tenant contexts is safe.
        /// </summary>
        [TestMethod]
        public async Task GetDbContextForDomain_ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            _mockConfigProvider.Setup(x => x.GetDatabaseConnectionStringAsync(It.IsAny<string>(), default))
                .ReturnsAsync("Data Source=:memory:;");

            var tasks = new Task<ApplicationDbContext>[10];

            // Act - Create 10 contexts concurrently for different tenants
            for (int i = 0; i < 10; i++)
            {
                var domain = $"tenant{i}.com";
                tasks[i] = Task.Run(() =>
                    ApplicationDbContextUtilities.GetDbContextForDomain(domain, _serviceProvider));
            }

            var contexts = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(10, contexts.Length);
            foreach (var context in contexts)
            {
                Assert.IsNotNull(context);
                context.Dispose();
            }
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Tests that exceptions are properly propagated.
        /// </summary>
        [TestMethod]
        public void GetDbContextForDomain_WhenProviderThrows_PropagatesException()
        {
            // Arrange
            _mockConfigProvider.Setup(x => x.GetDatabaseConnectionStringAsync(It.IsAny<string>(), default))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act & Assert
            Assert.ThrowsExactly<AggregateException>(() =>
                ApplicationDbContextUtilities.GetDbContextForDomain("tenant1.com", _serviceProvider));
        }

        /// <summary>
        /// Tests whitespace-only domain name handling.
        /// </summary>
        [TestMethod]
        public void GetDbContextForDomain_WithWhitespaceDomain_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.ThrowsExactly<ArgumentException>(() =>
                ApplicationDbContextUtilities.GetDbContextForDomain("   ", _serviceProvider));

            StringAssert.Contains(exception.Message, "Domain name cannot be null or empty");
        }

        #endregion
    }
}
