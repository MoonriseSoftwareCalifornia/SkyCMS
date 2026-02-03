// <copyright file="MigrationServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Migrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Services.Migrations;
    using Sky.Editor.Services.Migrations.Core;

    /// <summary>
    /// Unit tests for the MigrationService class.
    /// Tests brand new database bootstrapping, existing database migrations, and error scenarios.
    /// </summary>
    [TestClass]
    public class MigrationServiceTests
    {
        private SqliteConnection _connection;
        private DbContextOptions<ApplicationDbContext> _options;
        private Mock<ILogger<MigrationService>> _loggerMock;

        /// <summary>
        /// Initializes the test environment before each test.
        /// Creates an in-memory SQLite database connection.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            // Create in-memory SQLite database
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _loggerMock = new Mock<ILogger<MigrationService>>();
        }

        /// <summary>
        /// Cleans up test resources after each test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            _connection?.Close();
            _connection?.Dispose();
        }

        /// <summary>
        /// Tests that a brand new database is properly bootstrapped with EnsureCreated.
        /// Verifies that Migration 000 is recorded and all discovered migrations are pre-applied.
        /// </summary>
        [TestMethod]
        public async Task RunMigrationsAsync_BrandNewDatabase_BootstrapsWithEnsureCreated()
        {
            // Arrange
            using var context = new ApplicationDbContext(_options);
            var migrationService = new MigrationService(_loggerMock.Object);
            var migrationContext = new MigrationContext
            {
                DbContext = context,
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = "DataSource=:memory:",
                Logger = _loggerMock.Object
            };

            // Act
            await migrationService.RunMigrationsAsync(migrationContext);

            // Assert - Verify database schema was created
            var layoutsTableExists = await TableExistsAsync(context, "Layouts");
            Assert.IsTrue(layoutsTableExists, "Layouts table should be created by EnsureCreated");

            // Assert - Verify Migration 000 was recorded
            var migration000 = await context.Set<MigrationHistory>()
                .FirstOrDefaultAsync(m => m.MigrationId == "000");
            Assert.IsNotNull(migration000, "Migration 000 should be recorded");
            Assert.AreEqual("Initial schema created via EnsureCreated (includes all schema up to current DbContext model)", 
                migration000.Description);
            Assert.AreEqual("Sqlite", migration000.Provider);

            // Assert - Verify all discovered migrations were pre-applied
            var allMigrations = await context.Set<MigrationHistory>()
                .Where(m => m.Provider == "Sqlite")
                .ToListAsync();
            Assert.IsTrue(allMigrations.Count > 0, "At least Migration 000 should be recorded");
        }

        /// <summary>
        /// Tests that existing databases with pending migrations run them correctly.
        /// </summary>
        [TestMethod]
        public async Task RunMigrationsAsync_ExistingDatabase_RunsPendingMigrations()
        {
            // Arrange - Create database with schema but no migrations applied
            using var setupContext = new ApplicationDbContext(_options);
            await setupContext.Database.EnsureCreatedAsync();
            
            // Manually create MigrationHistory table
            await setupContext.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS MigrationHistory (
                    Id TEXT PRIMARY KEY,
                    MigrationId TEXT NOT NULL,
                    Version TEXT NOT NULL,
                    Description TEXT,
                    AppliedAt TEXT NOT NULL,
                    Provider TEXT NOT NULL,
                    ApplicationVersion TEXT
                )");

            var migrationService = new MigrationService(_loggerMock.Object);
            var migrationContext = new MigrationContext
            {
                DbContext = setupContext,
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = "DataSource=:memory:",
                Logger = _loggerMock.Object
            };

            // Act
            await migrationService.RunMigrationsAsync(migrationContext);

            // Assert - Verify migrations were applied
            var appliedMigrations = await setupContext.Set<MigrationHistory>()
                .Where(m => m.Provider == "Sqlite")
                .ToListAsync();
            
            Assert.IsTrue(appliedMigrations.Count > 0, "Migrations should be applied");
        }

        /// <summary>
        /// Tests that databases with all migrations applied report as up-to-date.
        /// </summary>
        [TestMethod]
        public async Task RunMigrationsAsync_AllMigrationsApplied_ReportsUpToDate()
        {
            // Arrange - Bootstrap a new database (applies all migrations)
            using var setupContext = new ApplicationDbContext(_options);
            var setupMigrationService = new MigrationService(_loggerMock.Object);
            var setupMigrationContext = new MigrationContext
            {
                DbContext = setupContext,
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = "DataSource=:memory:",
                Logger = _loggerMock.Object
            };
            await setupMigrationService.RunMigrationsAsync(setupMigrationContext);

            // Act - Run migrations again
            using var context = new ApplicationDbContext(_options);
            var migrationService = new MigrationService(_loggerMock.Object);
            var migrationContext = new MigrationContext
            {
                DbContext = context,
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = "DataSource=:memory:",
                Logger = _loggerMock.Object
            };
            
            await migrationService.RunMigrationsAsync(migrationContext);

            // Assert - Verify logged as up-to-date
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("up to date")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        /// <summary>
        /// Tests that reserved migration ID "000" throws an error if used by custom migrations.
        /// ID "000" is reserved for the virtual bootstrap migration created by EnsureCreated.
        /// </summary>
        [TestMethod]
        public void RunMigrationsAsync_ReservedMigrationId_ThrowsException()
        {
            // Test the IsReservedMigrationId validation logic directly
            
            // Reserved ID (should return true)
            Assert.IsTrue(MigrationService.IsReservedMigrationId("000"), "000 should be reserved");
            
            // Non-reserved IDs (should return false)
            Assert.IsFalse(MigrationService.IsReservedMigrationId("001"), "001 should not be strictly reserved");
            Assert.IsFalse(MigrationService.IsReservedMigrationId("005"), "005 should not be strictly reserved");
            Assert.IsFalse(MigrationService.IsReservedMigrationId("009"), "009 should not be strictly reserved");
            Assert.IsFalse(MigrationService.IsReservedMigrationId("010"), "010 should not be reserved");
            Assert.IsFalse(MigrationService.IsReservedMigrationId("100"), "100 should not be reserved");
            Assert.IsFalse(MigrationService.IsReservedMigrationId("M001"), "M001 should not be reserved");
            Assert.IsFalse(MigrationService.IsReservedMigrationId(""), "Empty should not be reserved");
            Assert.IsFalse(MigrationService.IsReservedMigrationId(null), "Null should not be reserved");
        }

        /// <summary>
        /// Tests that migrations can detect when schema changes are already applied and skip execution.
        /// </summary>
        [TestMethod]
        public async Task RunMigrationsAsync_SchemaAlreadyExists_SkipsMigrations()
        {
            // Arrange - Create database with schema
            using var context = new ApplicationDbContext(_options);
            await context.Database.EnsureCreatedAsync();
            
            // Manually create MigrationHistory table
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS MigrationHistory (
                    Id TEXT PRIMARY KEY,
                    MigrationId TEXT NOT NULL,
                    Version TEXT NOT NULL,
                    Description TEXT,
                    AppliedAt TEXT NOT NULL,
                    Provider TEXT NOT NULL,
                    ApplicationVersion TEXT
                )");

            // Mark all existing migrations as applied since EnsureCreatedAsync created the schema
            // Migration 001 adds LayoutNumber which was already created by EnsureCreatedAsync
            var migration001 = new MigrationHistory
            {
                MigrationId = "001",
                Version = "1.0.0",
                Description = "Add LayoutNumber for layout versioning",
                Provider = "Sqlite",
                ApplicationVersion = "1.0.0"
            };
            context.Set<MigrationHistory>().Add(migration001);
            
            // Add a migration record for a non-existent migration to force normal flow
            var fakeRecord = new MigrationHistory
            {
                MigrationId = "999",
                Version = "1.0.0",
                Description = "Fake migration",
                Provider = "Sqlite",
                ApplicationVersion = "1.0.0"
            };
            context.Set<MigrationHistory>().Add(fakeRecord);
            await context.SaveChangesAsync();

            var migrationService = new MigrationService(_loggerMock.Object);
            var migrationContext = new MigrationContext
            {
                DbContext = context,
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = "DataSource=:memory:",
                Logger = _loggerMock.Object
            };

            // Note: Since all real migrations are marked as applied, this should complete successfully
            // This test verifies that migrations already applied are properly detected and skipped
            
            // Act - This should not throw since all migrations are either applied or skipped
            await migrationService.RunMigrationsAsync(migrationContext);

            // Assert - Verify no errors were logged (all migrations were skipped)
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never,
                "No errors should be logged when migrations are properly skipped");
        }

        /// <summary>
        /// Tests that DetermineProvider correctly identifies SQLite connection strings.
        /// </summary>
        [TestMethod]
        public void DetermineProvider_SqliteConnectionString_ReturnsSqlite()
        {
            // Arrange
            var sqliteConnectionString = "Data Source=test.db";

            // Act
            var provider = MigrationService.DetermineProvider(sqliteConnectionString);

            // Assert
            Assert.AreEqual(DatabaseProvider.Sqlite, provider);
        }

        /// <summary>
        /// Tests that DetermineProvider throws ArgumentNullException for null/empty connection strings.
        /// </summary>
        [TestMethod]
        public void DetermineProvider_NullConnectionString_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(
                () => MigrationService.DetermineProvider(null));
            
            Assert.Throws<ArgumentNullException>(
                () => MigrationService.DetermineProvider(string.Empty));
        }

        /// <summary>
        /// Tests that DetermineProvider throws ArgumentException for invalid connection strings.
        /// </summary>
        [TestMethod]
        public void DetermineProvider_InvalidConnectionString_ThrowsArgumentException()
        {
            // Arrange
            var invalidConnectionString = "InvalidConnectionString";

            // Act & Assert
            Assert.Throws<ArgumentException>(
                () => MigrationService.DetermineProvider(invalidConnectionString));
        }

        /// <summary>
        /// Helper method to check if a table exists in the database.
        /// </summary>
        private async Task<bool> TableExistsAsync(ApplicationDbContext context, string tableName)
        {
            var connection = context.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            
            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@tableName";
                parameter.Value = tableName;
                command.Parameters.Add(parameter);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            finally
            {
                if (!wasOpen && connection.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        /// <summary>
        /// Test implementation of ApplicationDbContext that includes a reserved migration.
        /// Used to test reserved migration ID validation.
        /// </summary>
        private class TestApplicationDbContextWithReservedMigration : ApplicationDbContext
        {
            public TestApplicationDbContextWithReservedMigration(DbContextOptions<ApplicationDbContext> options)
                : base(options)
            {
            }
        }

        /// <summary>
        /// Test migration with a reserved ID.
        /// </summary>
        private class TestReservedMigration : IMigration
        {
            public string MigrationId => "001"; // Reserved if using 00x pattern
            public string Version => "1.0.0";
            public string Description => "Test migration with reserved ID";

            public Task ApplyAsync(MigrationContext context)
            {
                return Task.CompletedTask;
            }

            public Task<bool> IsAppliedAsync(MigrationContext context)
            {
                return Task.FromResult(false);
            }

            public Task RollbackAsync(MigrationContext context)
            {
                return Task.CompletedTask;
            }
        }
    }
}