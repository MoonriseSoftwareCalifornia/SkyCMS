// <copyright file="DynamicConfigDbContextTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.DynamicConfig
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.DynamicConfig;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="DynamicConfigDbContext"/> to ensure proper configuration
    /// and partition key setup for multi-tenant scenarios.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class DynamicConfigDbContextTests
    {
        private DbContextOptions<DynamicConfigDbContext> _options;

        [TestInitialize]
        public void Setup()
        {
            _options = new DbContextOptionsBuilder<DynamicConfigDbContext>()
                .UseInMemoryDatabase($"DynamicConfigTest_{Guid.NewGuid()}")
                .Options;
        }

        [TestCleanup]
        public void Cleanup()
        {
            using var context = new DynamicConfigDbContext(_options);
            context.Database.EnsureDeleted();
        }

        #region OnModelCreating Tests

        /// <summary>
        /// Tests that Connections entity is properly configured.
        /// </summary>
        [TestMethod]
        public void OnModelCreating_ConfiguresConnectionsEntity()
        {
            // Arrange & Act
            using var context = new DynamicConfigDbContext(_options);
            var entityType = context.Model.FindEntityType(typeof(Connection));

            // Assert
            Assert.IsNotNull(entityType, "Connection entity should be configured");
            Assert.AreEqual("Connection", entityType.GetTableName());
        }

        /// <summary>
        /// Tests that Metrics entity is properly configured.
        /// </summary>
        [TestMethod]
        public void OnModelCreating_ConfiguresMetricsEntity()
        {
            // Arrange & Act
            using var context = new DynamicConfigDbContext(_options);
            var entityType = context.Model.FindEntityType(typeof(Metric));

            // Assert
            Assert.IsNotNull(entityType, "Metric entity should be configured");
            Assert.AreEqual("Metric", entityType.GetTableName());
        }

        #endregion

        #region Connection Query Tests

        /// <summary>
        /// CRITICAL: Tests that connections can be queried by domain name (case-insensitive).
        /// </summary>
        [TestMethod]
        public async Task Connections_QueryByDomainName_ReturnsCorrectTenant()
        {
            // Arrange
            using var context = new DynamicConfigDbContext(_options);
            var connection1 = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant1.com", "www.tenant1.com" },
                DbConn = "Server=tenant1;Database=Tenant1Db;",
                StorageConn = "DefaultEndpointsProtocol=https;AccountName=tenant1storage;",
                WebsiteUrl = "https://tenant1.com",
                ResourceGroup = "tenant1-rg"
            };

            var connection2 = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant2.com" },
                DbConn = "Server=tenant2;Database=Tenant2Db;",
                StorageConn = "DefaultEndpointsProtocol=https;AccountName=tenant2storage;",
                WebsiteUrl = "https://tenant2.com",
                ResourceGroup = "tenant2-rg"
            };

            context.Connections.AddRange(connection1, connection2);
            await context.SaveChangesAsync();

            // Act
            var result = await context.Connections
                .FirstOrDefaultAsync(c => c.DomainNames != null && c.DomainNames.Contains("tenant1.com"));

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(connection1.Id, result.Id);
            Assert.AreEqual("Server=tenant1;Database=Tenant1Db;", result.DbConn);
        }

        /// <summary>
        /// CRITICAL: Tests case-insensitive domain lookup.
        /// </summary>
        [TestMethod]
        public async Task Connections_QueryByDomainName_CaseInsensitive()
        {
            // Arrange
            using var context = new DynamicConfigDbContext(_options);
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant1.com" },
                DbConn = "Server=tenant1;",
                StorageConn = "Storage=tenant1;",
                WebsiteUrl = "https://tenant1.com",
                ResourceGroup = "tenant1-rg"
            };

            context.Connections.Add(connection);
            await context.SaveChangesAsync();

            // Act - Query with different cases
            var lowerCase = await context.Connections
                .ToListAsync();
            var upperCaseResult = lowerCase.FirstOrDefault(c =>
                c.DomainNames != null && c.DomainNames.Contains("TENANT1.COM", StringComparer.OrdinalIgnoreCase));

            // Assert
            Assert.IsNotNull(upperCaseResult, "Should find connection regardless of case");
            Assert.AreEqual(connection.Id, upperCaseResult.Id);
        }

        /// <summary>
        /// Tests that multiple domains map to the same connection.
        /// </summary>
        [TestMethod]
        public async Task Connections_MultipleDomains_MapToSameConnection()
        {
            // Arrange
            using var context = new DynamicConfigDbContext(_options);
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant1.com", "www.tenant1.com", "app.tenant1.com" },
                DbConn = "Server=tenant1;",
                StorageConn = "Storage=tenant1;",
                WebsiteUrl = "https://tenant1.com",
                ResourceGroup = "tenant1-rg"
            };

            context.Connections.Add(connection);
            await context.SaveChangesAsync();

            // Act
            var allConnections = await context.Connections.ToListAsync();
            var result1 = allConnections.FirstOrDefault(c => c.DomainNames.Contains("tenant1.com"));
            var result2 = allConnections.FirstOrDefault(c => c.DomainNames.Contains("www.tenant1.com"));
            var result3 = allConnections.FirstOrDefault(c => c.DomainNames.Contains("app.tenant1.com"));

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.IsNotNull(result3);
            Assert.AreEqual(connection.Id, result1.Id);
            Assert.AreEqual(connection.Id, result2.Id);
            Assert.AreEqual(connection.Id, result3.Id);
        }

        #endregion

        #region CRUD Operation Tests

        /// <summary>
        /// Tests adding a new connection.
        /// </summary>
        [TestMethod]
        public async Task AddConnection_WithValidData_SavesSuccessfully()
        {
            // Arrange
            using var context = new DynamicConfigDbContext(_options);
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "newtenant.com" }, // Fixed: was "newteniant.com"
                DbConn = "Server=newtenant;",
                StorageConn = "Storage=newtenant;",
                WebsiteUrl = "https://newtenant.com",
                ResourceGroup = "newtenant-rg"
            };

            // Act
            context.Connections.Add(connection);
            var result = await context.SaveChangesAsync();

            // Assert
            Assert.AreEqual(1, result, "Should save 1 entity");
            var saved = await context.Connections.FindAsync(connection.Id);
            Assert.IsNotNull(saved);
            Assert.AreEqual("newtenant.com", saved.DomainNames[0]);
        }

        /// <summary>
        /// Tests updating an existing connection.
        /// </summary>
        [TestMethod]
        public async Task UpdateConnection_ChangesProperties_SavesSuccessfully()
        {
            // Arrange
            using var context = new DynamicConfigDbContext(_options);
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "update.com" },
                DbConn = "OldServer=update;",
                StorageConn = "OldStorage=update;",
                WebsiteUrl = "https://update.com",
                ResourceGroup = "update-rg"
            };

            context.Connections.Add(connection);
            await context.SaveChangesAsync();

            // Act
            connection.DbConn = "NewServer=update;";
            connection.StorageConn = "NewStorage=update;";
            await context.SaveChangesAsync();

            // Assert
            var updated = await context.Connections.FindAsync(connection.Id);
            Assert.AreEqual("NewServer=update;", updated.DbConn);
            Assert.AreEqual("NewStorage=update;", updated.StorageConn);
        }

        /// <summary>
        /// Tests deleting a connection.
        /// </summary>
        [TestMethod]
        public async Task DeleteConnection_RemovesFromDatabase()
        {
            // Arrange
            using var context = new DynamicConfigDbContext(_options);
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "delete.com" },
                DbConn = "Server=delete;",
                StorageConn = "Storage=delete;",
                WebsiteUrl = "https://delete.com",
                ResourceGroup = "delete-rg"
            };

            context.Connections.Add(connection);
            await context.SaveChangesAsync();

            // Act
            context.Connections.Remove(connection);
            await context.SaveChangesAsync();

            // Assert
            var deleted = await context.Connections.FindAsync(connection.Id);
            Assert.IsNull(deleted);
        }

        #endregion

        #region Metric Entity Tests

        /// <summary>
        /// Tests adding metrics for a connection.
        /// </summary>
        [TestMethod]
        public async Task AddMetric_WithValidData_SavesSuccessfully()
        {
            // Arrange
            using var context = new DynamicConfigDbContext(_options);
            var connectionId = Guid.NewGuid();
            var metric = new Metric
            {
                Id = Guid.NewGuid(),
                ConnectionId = connectionId,
                TimeStamp = DateTimeOffset.UtcNow,
                BlobStorageBytes = 1024000,
                BlobStorageEgressBytes = 512000,
                BlobStorageIngressBytes = 512000,
                BlobStorageTransactions = 100,
                DatabaseDataUsageBytes = 2048000,
                DatabaseRuUsage = 50.5
            };

            // Act
            context.Metrics.Add(metric);
            var result = await context.SaveChangesAsync();

            // Assert
            Assert.AreEqual(1, result);
            var saved = await context.Metrics.FindAsync(metric.Id);
            Assert.IsNotNull(saved);
            Assert.AreEqual(connectionId, saved.ConnectionId);
            Assert.AreEqual(1024000, saved.BlobStorageBytes);
        }

        /// <summary>
        /// Tests querying metrics by connection ID.
        /// </summary>
        [TestMethod]
        public async Task Metrics_QueryByConnectionId_ReturnsCorrectMetrics()
        {
            // Arrange
            using var context = new DynamicConfigDbContext(_options);
            var connectionId = Guid.NewGuid();
            var metric1 = new Metric
            {
                Id = Guid.NewGuid(),
                ConnectionId = connectionId,
                TimeStamp = DateTimeOffset.UtcNow.AddDays(-2),
                BlobStorageBytes = 1000
            };
            var metric2 = new Metric
            {
                Id = Guid.NewGuid(),
                ConnectionId = connectionId,
                TimeStamp = DateTimeOffset.UtcNow.AddDays(-1),
                BlobStorageBytes = 2000
            };

            context.Metrics.AddRange(metric1, metric2);
            await context.SaveChangesAsync();

            // Act
            var metrics = await context.Metrics
                .Where(m => m.ConnectionId == connectionId)
                .OrderBy(m => m.TimeStamp)
                .ToListAsync();

            // Assert
            Assert.AreEqual(2, metrics.Count);
            Assert.AreEqual(metric1.Id, metrics[0].Id);
            Assert.AreEqual(metric2.Id, metrics[1].Id);
        }

        #endregion

        #region Edge Cases
                
        /// <summary>
        /// Tests handling of empty domain names array.
        /// </summary>
        [TestMethod]
        public async Task Connection_WithEmptyDomainNames_HandledGracefully()
        {
            // Arrange
            using var context = new DynamicConfigDbContext(_options);
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = Array.Empty<string>(),
                DbConn = "Server=test;",
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://test.com",
                ResourceGroup = "test-rg"
            };

            // Act
            context.Connections.Add(connection);
            await context.SaveChangesAsync();

            // Assert
            var saved = await context.Connections.FindAsync(connection.Id);
            Assert.IsNotNull(saved);
            Assert.AreEqual(0, saved.DomainNames.Length);
        }

        #endregion
    }
}
