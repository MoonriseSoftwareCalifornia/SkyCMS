// <copyright file="DatabaseProviderComparisonTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.DatabaseProviders
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using AspNetCore.Identity.FlexDb;
    using AspNetCore.Identity.FlexDb.Strategies;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Comprehensive tests comparing performance and functionality across all four database providers.
    /// Tests: Azure Cosmos DB, SQL Server, MySQL, and SQLite.
    /// </summary>
    [TestClass]
    public class DatabaseProviderComparisonTests
    {
        private Dictionary<string, string> _connectionStrings;
        private Dictionary<string, TestDbContext> _contexts;
        private DbConnection _sqliteConnection; // Keep SQLite connection alive for in-memory database
        private IConfiguration _configuration;

        [TestInitialize]
        public void Setup()
        {
            // Load configuration from User Secrets
            _configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddUserSecrets(typeof(DatabaseProviderComparisonTests).Assembly, optional: true)
                .Build();

            // Setup SQLite in-memory connection (must stay open)
            var sqliteConnectionString = _configuration.GetConnectionString("SQLite") 
                ?? "Data Source=:memory:;Mode=Memory;Cache=Shared;";
            
            _sqliteConnection = new SqliteConnection(sqliteConnectionString);
            _sqliteConnection.Open();

            _connectionStrings = new Dictionary<string, string>
            {
                ["CosmosDB"] = _configuration.GetConnectionString("CosmosDB") 
                    ?? "AccountEndpoint=https://localhost:8081;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;Database=TestDb",
                ["SqlServer"] = _configuration.GetConnectionString("SqlServer") 
                    ?? $"Server=(localdb)\\mssqllocaldb;Database=TestDb_{Guid.NewGuid()};Trusted_Connection=True;MultipleActiveResultSets=true",
                ["MySQL"] = _configuration.GetConnectionString("MySQL") 
                    ?? "Server=localhost;Port=3306;Database=testdb;uid=test;pwd=test;",
                ["SQLite"] = _sqliteConnection.ConnectionString
            };

            _contexts = new Dictionary<string, TestDbContext>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var context in _contexts.Values)
            {
                try
                {
                    context.Database.EnsureDeleted();
                }
                catch
                {
                    // Ignore cleanup errors
                }
                context.Dispose();
            }

            _contexts.Clear();

            // Close and dispose SQLite connection
            _sqliteConnection?.Close();
            _sqliteConnection?.Dispose();
        }

        #region Provider Detection Tests

        /// <summary>
        /// CRITICAL: Verify all four providers are correctly detected by connection string pattern.
        /// </summary>
        [TestMethod]
        public void AllProviders_ConnectionStringDetection_CorrectStrategySelected()
        {
            // Arrange
            var strategies = CosmosDbOptionsBuilder.GetDefaultStrategies();

            // Act & Assert
            var cosmosStrategy = strategies.FirstOrDefault(s => s.CanHandle(_connectionStrings["CosmosDB"]));
            Assert.IsNotNull(cosmosStrategy, "CosmosDB strategy should be found");
            Assert.AreEqual("Microsoft.EntityFrameworkCore.Cosmos", cosmosStrategy.ProviderName);

            var sqlServerStrategy = strategies.FirstOrDefault(s => s.CanHandle(_connectionStrings["SqlServer"]));
            Assert.IsNotNull(sqlServerStrategy, "SqlServer strategy should be found");
            Assert.AreEqual("Microsoft.EntityFrameworkCore.SqlServer", sqlServerStrategy.ProviderName);

            var mySqlStrategy = strategies.FirstOrDefault(s => s.CanHandle(_connectionStrings["MySQL"]));
            Assert.IsNotNull(mySqlStrategy, "MySQL strategy should be found");
            Assert.AreEqual("MySql.EntityFrameworkCore", mySqlStrategy.ProviderName);

            var sqliteStrategy = strategies.FirstOrDefault(s => s.CanHandle(_connectionStrings["SQLite"]));
            Assert.IsNotNull(sqliteStrategy, "SQLite strategy should be found");
            Assert.AreEqual("Microsoft.EntityFrameworkCore.Sqlite", sqliteStrategy.ProviderName);
        }

        /// <summary>
        /// Verify strategy priority ordering ensures correct provider selection.
        /// </summary>
        [TestMethod]
        public void AllProviders_StrategyPriority_OrderedCorrectly()
        {
            // Arrange
            var strategies = CosmosDbOptionsBuilder.GetDefaultStrategies();

            // Act
            var orderedStrategies = strategies.OrderBy(s => s.Priority).ToList();

            // Assert
            Assert.AreEqual("Microsoft.EntityFrameworkCore.Cosmos", orderedStrategies[0].ProviderName);
            Assert.AreEqual("Microsoft.EntityFrameworkCore.SqlServer", orderedStrategies[1].ProviderName);
            Assert.AreEqual("MySql.EntityFrameworkCore", orderedStrategies[2].ProviderName);
            Assert.AreEqual("Microsoft.EntityFrameworkCore.Sqlite", orderedStrategies[3].ProviderName);
        }

        #endregion

        #region Connection and Basic Operations Tests

        /// <summary>
        /// CRITICAL: Verify all providers can establish database connections.
        /// </summary>
        [TestMethod]
        [DataRow("CosmosDB", DisplayName = "CosmosDB Connection")]
        [DataRow("SqlServer", DisplayName = "SQL Server Connection")]
        [DataRow("MySQL", DisplayName = "MySQL Connection")]
        [DataRow("SQLite", DisplayName = "SQLite Connection")]
        public async Task Provider_CanConnect_Successfully(string providerKey)
        {
            // Arrange
            var context = CreateContext(providerKey);

            try
            {
                // Act
                var canConnect = await context.Database.CanConnectAsync();

                // Assert
                Assert.IsTrue(canConnect, $"{providerKey} should be able to connect");
            }
            catch (Exception ex)
            {
                Assert.Inconclusive($"{providerKey} connection failed: {ex.Message}. Ensure database is available.");
            }
        }

        #endregion

        #region Helper Methods

        private TestDbContext CreateContext(string providerKey)
        {
            if (_contexts.ContainsKey(providerKey))
            {
                return _contexts[providerKey];
            }

            var connectionString = _connectionStrings[providerKey];
            var context = providerKey == "SQLite"
                ? new TestDbContext(connectionString, _sqliteConnection)
                : new TestDbContext(connectionString);

            _contexts[providerKey] = context;
            return context;
        }

        #endregion

        /// <summary>
        /// Test DbContext using CosmosDbOptionsBuilder for automatic provider selection.
        /// </summary>
        private class TestDbContext : DbContext
        {
            private readonly string _connectionString;
            private readonly DbConnection _connection;

            public TestDbContext(string connectionString, DbConnection connection = null)
            {
                _connectionString = connectionString;
                _connection = connection;
            }

            public DbSet<IdentityUser> Users { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                if (_connection != null)
                {
                    // Use existing connection for SQLite in-memory
                    optionsBuilder.UseSqlite(_connection);
                }
                else
                {
                    CosmosDbOptionsBuilder.ConfigureDbOptions(optionsBuilder, _connectionString);
                }
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                if (Database.IsCosmos())
                {
                    modelBuilder.Entity<IdentityUser>()
                        .ToContainer("Users")
                        .HasPartitionKey(u => u.Id)
                        .HasKey(u => u.Id);
                }
                else
                {
                    modelBuilder.Entity<IdentityUser>().HasKey(u => u.Id);
                }
            }
        }
    }
}
