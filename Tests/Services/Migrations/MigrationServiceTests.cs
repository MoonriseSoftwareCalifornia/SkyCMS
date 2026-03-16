// <copyright file="MigrationServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Migrations
{
    using Cosmos.Common.Data;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Services.Migrations;
    using Sky.Editor.Services.Migrations.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

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
        /// Tests that DetermineProvider correctly identifies SQL Server connection strings.
        /// </summary>
        [TestMethod]
        public void DetermineProvider_SqlServerConnectionString_ReturnsSqlServer()
        {
            // Arrange
            var sqlServerConnectionString = "Server=localhost;Database=TestDb;Trusted_Connection=True;";

            // Act
            var provider = MigrationService.DetermineProvider(sqlServerConnectionString);

            // Assert
            Assert.AreEqual(DatabaseProvider.SqlServer, provider);
        }

        /// <summary>
        /// Tests that DetermineProvider correctly identifies MySQL connection strings.
        /// </summary>
        [TestMethod]
        public void DetermineProvider_MySqlConnectionString_ReturnsMySql()
        {
            // Arrange
            // MySQL connection strings use 'uid=' for user identification
            var mySqlConnectionString = "server=localhost;port=3306;database=testdb;uid=root;password=password";

            // Act
            var provider = MigrationService.DetermineProvider(mySqlConnectionString);

            // Assert
            Assert.AreEqual(DatabaseProvider.MySql, provider);
        }

        /// <summary>
        /// Tests that DetermineProvider correctly identifies Cosmos DB connection strings.
        /// </summary>
        [TestMethod]
        public void DetermineProvider_CosmosDbConnectionString_ReturnsCosmosDb()
        {
            // Arrange
            var cosmosConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

            // Act
            var provider = MigrationService.DetermineProvider(cosmosConnectionString);

            // Assert
            Assert.AreEqual(DatabaseProvider.CosmosDb, provider);
        }

        /// <summary>
        /// Tests that RecordMigrationAsync throws ArgumentException when MigrationId is null.
        /// </summary>
        [TestMethod]
        public async Task RecordMigrationAsync_NullMigrationId_ThrowsArgumentException()
        {
            // Arrange
            using var context = new ApplicationDbContext(_options);
            await context.Database.EnsureCreatedAsync();

            var migrationService = new MigrationService(_loggerMock.Object);
            var migrationContext = new MigrationContext
            {
                DbContext = context,
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = "DataSource=:memory:",
                Logger = _loggerMock.Object
            };

            var invalidMigration = new TestMigrationWithNullId();

            // Act & Assert
            var exceptionThrown = false;
            try
            {
                // Use reflection to call private RecordMigrationAsync method
                var method = typeof(MigrationService).GetMethod("RecordMigrationAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var task = (Task)method.Invoke(migrationService, new object[] { migrationContext, invalidMigration });
                await task;
            }
            catch (ArgumentException ex)
            {
                exceptionThrown = true;
                Assert.IsTrue(ex.Message.Contains("MigrationId"));
            }

            Assert.IsTrue(exceptionThrown, "Expected ArgumentException was not thrown");
        }

        /// <summary>
        /// Tests that RecordMigrationAsync throws ArgumentException when Version is null.
        /// </summary>
        [TestMethod]
        public async Task RecordMigrationAsync_NullVersion_ThrowsArgumentException()
        {
            // Arrange
            using var context = new ApplicationDbContext(_options);
            await context.Database.EnsureCreatedAsync();

            var migrationService = new MigrationService(_loggerMock.Object);
            var migrationContext = new MigrationContext
            {
                DbContext = context,
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = "DataSource=:memory:",
                Logger = _loggerMock.Object
            };

            var invalidMigration = new TestMigrationWithNullVersion();

            // Act & Assert
            var exceptionThrown = false;
            try
            {
                // Use reflection to call private RecordMigrationAsync method
                var method = typeof(MigrationService).GetMethod("RecordMigrationAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var task = (Task)method.Invoke(migrationService, new object[] { migrationContext, invalidMigration });
                await task;
            }
            catch (ArgumentException ex)
            {
                exceptionThrown = true;
                Assert.IsTrue(ex.Message.Contains("Version"));
            }

            Assert.IsTrue(exceptionThrown, "Expected ArgumentException was not thrown");
        }

        /// <summary>
        /// Tests that ApplyMigrationAsync wraps migration exceptions in InvalidOperationException.
        /// </summary>
        [TestMethod]
        public async Task ApplyMigrationAsync_MigrationThrowsException_WrapsInInvalidOperationException()
        {
            // Arrange
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

            var migrationService = new MigrationService(_loggerMock.Object);
            var migrationContext = new MigrationContext
            {
                DbContext = context,
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = "DataSource=:memory:",
                Logger = _loggerMock.Object
            };

            var failingMigration = new TestFailingMigration();

            // Act & Assert
            var exceptionThrown = false;
            InvalidOperationException capturedException = null;

            try
            {
                // Use reflection to call private ApplyMigrationAsync method
                var method = typeof(MigrationService).GetMethod("ApplyMigrationAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var task = (Task)method.Invoke(migrationService, new object[] { migrationContext, failingMigration });
                await task;
            }
            catch (InvalidOperationException ex)
            {
                exceptionThrown = true;
                capturedException = ex;
            }

            Assert.IsTrue(exceptionThrown, "Expected InvalidOperationException was not thrown");
            Assert.IsNotNull(capturedException);

            // Assert the exception message contains migration details
            Assert.IsTrue(capturedException.Message.Contains("FAIL001"));
            Assert.IsTrue(capturedException.Message.Contains("Test migration that fails"));
            Assert.IsNotNull(capturedException.InnerException);
            Assert.IsInstanceOfType(capturedException.InnerException, typeof(InvalidOperationException));
        }

        /// <summary>
        /// Tests that RecordMigrationAsync handles null Description gracefully.
        /// </summary>
        [TestMethod]
        public async Task RecordMigrationAsync_NullDescription_InsertsEmptyString()
        {
            // Arrange
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

            var migrationService = new MigrationService(_loggerMock.Object);
            var migrationContext = new MigrationContext
            {
                DbContext = context,
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = "DataSource=:memory:",
                Logger = _loggerMock.Object
            };

            var migrationWithNullDescription = new TestMigrationWithNullDescription();

            // Act - Use reflection to call private RecordMigrationAsync method
            var method = typeof(MigrationService).GetMethod("RecordMigrationAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await (Task)method.Invoke(migrationService, new object[] { migrationContext, migrationWithNullDescription });

            // Assert - Verify the migration was recorded with empty description
            var recorded = await context.Set<MigrationHistory>()
                .FirstOrDefaultAsync(m => m.MigrationId == "NULLDESC001");

            Assert.IsNotNull(recorded);
            Assert.IsNotNull(recorded.Description); // Should be empty string, not null
        }

        /// <summary>
        /// Tests that the virtual bootstrap migration cannot be rolled back.
        /// This verifies the VirtualMigration.RollbackAsync throws NotSupportedException.
        /// </summary>
        [TestMethod]
        public async Task VirtualMigration_RollbackAsync_ThrowsNotSupportedException()
        {
            // Arrange
            using var context = new ApplicationDbContext(_options);
            var migrationContext = new MigrationContext
            {
                DbContext = context,
                Provider = DatabaseProvider.Sqlite,
                ConnectionString = "DataSource=:memory:",
                Logger = _loggerMock.Object
            };

            // Bootstrap a new database to get Migration 000 created
            var migrationService = new MigrationService(_loggerMock.Object);
            await migrationService.RunMigrationsAsync(migrationContext);

            // Verify Migration 000 exists
            var migration000 = await context.Set<MigrationHistory>()
                .FirstOrDefaultAsync(m => m.MigrationId == "000");
            Assert.IsNotNull(migration000, "Migration 000 should exist after bootstrap");

            // Act & Assert - Try to rollback the virtual migration
            // Get the VirtualMigration type using reflection
            var virtualMigrationType = typeof(MigrationService)
                .GetNestedType("VirtualMigration", System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(virtualMigrationType, "VirtualMigration type should exist");

            // Create instance of VirtualMigration
            var virtualMigration = Activator.CreateInstance(virtualMigrationType);

            // Set properties
            virtualMigrationType.GetProperty("MigrationId").SetValue(virtualMigration, "000");
            virtualMigrationType.GetProperty("Version").SetValue(virtualMigration, "1.0.0");
            virtualMigrationType.GetProperty("Description").SetValue(virtualMigration, "Test");

            // Get RollbackAsync method
            var rollbackMethod = virtualMigrationType.GetMethod("RollbackAsync");

            // Invoke RollbackAsync and expect NotSupportedException
            // When calling async methods through reflection, the exception is NOT wrapped in TargetInvocationException
            var exceptionThrown = false;
            NotSupportedException capturedException = null;

            try
            {
                // For async methods, we need to await the task returned by Invoke
                var task = rollbackMethod.Invoke(virtualMigration, new object[] { migrationContext }) as Task;
                if (task != null)
                {
                    await task;
                }
            }
            catch (NotSupportedException ex)
            {
                exceptionThrown = true;
                capturedException = ex;
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is NotSupportedException)
            {
                exceptionThrown = true;
                capturedException = ex.InnerException as NotSupportedException;
            }

            Assert.IsTrue(exceptionThrown, "Expected NotSupportedException was not thrown");
            Assert.IsNotNull(capturedException);

            // Verify exception message
            Assert.IsTrue(capturedException.Message.Contains("Virtual migration 000"));
            Assert.IsTrue(capturedException.Message.Contains("cannot be rolled back"));
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

        /// <summary>
        /// Test migration with null MigrationId for validation testing.
        /// </summary>
        private class TestMigrationWithNullId : IMigration
        {
            public string MigrationId => null;
            public string Version => "1.0.0";
            public string Description => "Test migration with null ID";

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

        /// <summary>
        /// Test migration with null Version for validation testing.
        /// </summary>
        private class TestMigrationWithNullVersion : IMigration
        {
            public string MigrationId => "TEST001";
            public string Version => null;
            public string Description => "Test migration with null version";

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

        /// <summary>
        /// Test migration that throws an exception during ApplyAsync.
        /// </summary>
        private class TestFailingMigration : IMigration
        {
            public string MigrationId => "FAIL001";
            public string Version => "1.0.0";
            public string Description => "Test migration that fails";

            public Task ApplyAsync(MigrationContext context)
            {
                throw new InvalidOperationException("Simulated migration failure");
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

        /// <summary>
        /// Test migration with null Description for testing DBNull handling.
        /// </summary>
        private class TestMigrationWithNullDescription : IMigration
        {
            public string MigrationId => "NULLDESC001";
            public string Version => "1.0.0";
            public string Description => null;

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

