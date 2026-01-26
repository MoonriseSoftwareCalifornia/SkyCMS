// <copyright file="MigrationHelperTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Data
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Data;

    [TestClass]
    public class MigrationHelperTests
    {
        private ILogger<MigrationHelperTests> logger = null!;
        private string testDbPath = null!;

        [TestInitialize]
        public void Setup()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            logger = loggerFactory.CreateLogger<MigrationHelperTests>();

            // Create unique temp database path for each test
            testDbPath = Path.Combine(Path.GetTempPath(), $"migration_test_{Guid.NewGuid()}.db");
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test database files
            if (File.Exists(testDbPath))
            {
                try
                {
                    File.Delete(testDbPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Connection String Detection Tests

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("ProviderDetection")]
        public async Task ApplyMigrationsAsync_WithSqliteConnectionString_DetectsCorrectProvider()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Act
            await MigrationHelper.ApplyMigrationsAsync(connectionString, logger);

            // Assert
            Assert.IsTrue(File.Exists(testDbPath), "SQLite database file should be created");
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("ProviderDetection")]
        public async Task ApplyMigrationsAsync_WithMySqlConnectionString_DetectsCorrectProvider()
        {
            // Arrange
            var connectionString = "Server=localhost;Port=3306;Database=testdb;Uid=user;Pwd=password;";

            // Act & Assert - Should fail on connection, not provider detection
            Exception? exception = null;
            try
            {
                await MigrationHelper.ApplyMigrationsAsync(connectionString, logger);
                Assert.Fail("Expected an exception to be thrown");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Verify it attempted MySQL connection (not a provider detection error)
            Assert.IsNotNull(exception);
            Assert.IsTrue(
                exception.Message.Contains("Unable to connect") ||
                exception.Message.Contains("connection") ||
                exception is MySqlConnector.MySqlException,
                $"Expected connection error but got: {exception.Message}");
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("ProviderDetection")]
        public async Task ApplyMigrationsAsync_WithSqlServerConnectionString_DetectsCorrectProvider()
        {
            // Arrange
            var connectionString = "Server=localhost;Database=test;User ID=sa;Password=Test123;TrustServerCertificate=True;";

            // Act & Assert - Should fail on connection, not provider detection
            Exception? exception = null;
            try
            {
                await MigrationHelper.ApplyMigrationsAsync(connectionString, logger);
                Assert.Fail("Expected an exception to be thrown");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Verify it attempted SQL Server connection
            Assert.IsNotNull(exception);
            Assert.IsTrue(
                exception.Message.Contains("network-related") ||
                exception.Message.Contains("SQL Server") ||
                exception.InnerException?.Message.Contains("login") == true,
                $"Expected SQL Server connection error but got: {exception.Message}");
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("ProviderDetection")]
        public async Task ApplyMigrationsAsync_WithCosmosDbConnectionString_DetectsCorrectProvider()
        {
            // Arrange
            var connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdGtleQ==;Database=testdb;";

            // Act & Assert - Should fail on connection (invalid Cosmos DB endpoint)
            var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
                await MigrationHelper.ApplyMigrationsAsync(connectionString, logger));

            // Verify it attempted Cosmos DB operation
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception.Message.Contains("Failed to ensure Cosmos DB creation"));
        }

        #endregion

        #region SQLite Migration Tests

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("SQLite")]
        public async Task ApplyMigrationsAsync_WithNewSqliteDatabase_AppliesMigrations()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Act
            await MigrationHelper.ApplyMigrationsAsync(connectionString, logger);

            // Assert
            Assert.IsTrue(File.Exists(testDbPath));

            // Verify migrations were applied
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString);

            using var context = new ApplicationDbContext(optionsBuilder.Options);
            var canConnect = await context.Database.CanConnectAsync();
            Assert.IsTrue(canConnect);

            // Check migrations history table exists
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            Assert.IsTrue(appliedMigrations.Any(), "Migrations should have been applied");
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("SQLite")]
        public async Task ApplyMigrationsAsync_WithExistingSqliteDatabase_IsIdempotent()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Act - Apply migrations twice
            await MigrationHelper.ApplyMigrationsAsync(connectionString, logger);
            await MigrationHelper.ApplyMigrationsAsync(connectionString, logger);

            // Assert - Should succeed without errors
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString);

            using var context = new ApplicationDbContext(optionsBuilder.Options);
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            Assert.IsTrue(appliedMigrations.Any());
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("SQLite")]
        public async Task DatabaseExistsWithoutMigrationsAsync_WithNewDatabase_ReturnsFalse()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Create database with migrations
            await MigrationHelper.ApplyMigrationsAsync(connectionString, logger);

            // Act
            var result = await MigrationHelper.DatabaseExistsWithoutMigrationsAsync(connectionString, logger);

            // Assert
            Assert.IsFalse(result, "Database with migrations should return false");
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("SQLite")]
        public async Task DatabaseExistsWithoutMigrationsAsync_WithNonExistentDatabase_ReturnsFalse()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Act
            var result = await MigrationHelper.DatabaseExistsWithoutMigrationsAsync(connectionString, logger);

            // Assert
            Assert.IsFalse(result, "Non-existent database should return false");
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("SQLite")]
        public async Task DatabaseExistsWithoutMigrationsAsync_WithDatabaseCreatedWithoutMigrations_ReturnsTrue()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Create database without migrations (using EnsureCreated)
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString);

            using (var context = new ApplicationDbContext(optionsBuilder.Options))
            {
                await context.Database.EnsureCreatedAsync();
            }

            // Act
            var result = await MigrationHelper.DatabaseExistsWithoutMigrationsAsync(connectionString, logger);

            // Assert
            Assert.IsTrue(result, "Database created without migrations should return true");
        }

        #endregion

        #region Mark Migration Tests

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("SQLite")]
        public async Task MarkMigrationAsAppliedAsync_WithValidMigrationId_MarksMigration()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Create database
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString);

            using (var context = new ApplicationDbContext(optionsBuilder.Options))
            {
                await context.Database.EnsureCreatedAsync();
            }

            var migrationId = "20250105120000_TestMigration_Sqlite";

            // Act
            await MigrationHelper.MarkMigrationAsAppliedAsync(connectionString, migrationId, logger);

            // Assert
            using (var context = new ApplicationDbContext(optionsBuilder.Options))
            {
                var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
                Assert.IsTrue(appliedMigrations.Contains(migrationId), "Migration should be marked as applied");
            }
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("SQLite")]
        public async Task MarkMigrationAsAppliedAsync_CalledTwice_IsIdempotent()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString);

            using (var context = new ApplicationDbContext(optionsBuilder.Options))
            {
                await context.Database.EnsureCreatedAsync();
            }

            var migrationId = "20250105120000_TestMigration_Sqlite";

            // Act - Mark twice
            await MigrationHelper.MarkMigrationAsAppliedAsync(connectionString, migrationId, logger);
            await MigrationHelper.MarkMigrationAsAppliedAsync(connectionString, migrationId, logger);

            // Assert - Should succeed without errors
            using (var context = new ApplicationDbContext(optionsBuilder.Options))
            {
                var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
                var count = appliedMigrations.Count(m => m == migrationId);
                Assert.AreEqual(1, count, "Migration should only be marked once");
            }
        }

        #endregion

        #region Validation Tests

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("Validation")]
        public async Task ApplyMigrationsAsync_WithNullConnectionString_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                await MigrationHelper.ApplyMigrationsAsync(null!, logger));
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("Validation")]
        public async Task ApplyMigrationsAsync_WithEmptyConnectionString_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                await MigrationHelper.ApplyMigrationsAsync(string.Empty, logger));
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("Validation")]
        public async Task MarkMigrationAsAppliedAsync_WithNullMigrationId_ThrowsArgumentException()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                await MigrationHelper.MarkMigrationAsAppliedAsync(connectionString, null!, logger));
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("Validation")]
        public async Task MarkMigrationAsAppliedAsync_WithCosmosDb_ThrowsInvalidOperationException()
        {
            // Arrange
            var connectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdGtleQ==;Database=testdb;";
            var migrationId = "20250105120000_TestMigration";

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
                await MigrationHelper.MarkMigrationAsAppliedAsync(connectionString, migrationId, logger));

            Assert.IsTrue(exception.Message.Contains("Cosmos DB does not support migrations"));
        }

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("Validation")]
        public async Task ApplyMigrationsAsync_WithInvalidConnectionString_ThrowsException()
        {
            // Arrange
            var connectionString = "InvalidConnectionString";

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
                await MigrationHelper.ApplyMigrationsAsync(connectionString, logger));
        }

        #endregion

        #region Custom Strategy Tests

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("Strategies")]
        public async Task ApplyMigrationsAsync_UsesCustomMigrationStrategies()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Act
            await MigrationHelper.ApplyMigrationsAsync(connectionString, logger);

            // Assert - Verify migrations assembly is correctly applied
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString, options =>
                {
                    options.MigrationsAssembly(typeof(MigrationHelper).Assembly.FullName);
                });

            using var context = new ApplicationDbContext(optionsBuilder.Options);
            var migrations = context.Database.GetMigrations().ToList();

            // If there are migrations, they should be from the correct assembly
            if (migrations.Any())
            {
                Assert.IsTrue(migrations.Any(m => m.Contains("Sqlite")),
                    "Migrations should include SQLite-specific migrations");
            }
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        [TestCategory("MigrationHelper")]
        [TestCategory("Integration")]
        public async Task ApplyMigrationsAsync_CompleteWorkflow_CreatesWorkingDatabase()
        {
            // Arrange
            var connectionString = $"Data Source={testDbPath}";

            // Act - Apply migrations
            await MigrationHelper.ApplyMigrationsAsync(connectionString, logger);

            // Assert - Verify database is functional
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString);

            using var context = new ApplicationDbContext(optionsBuilder.Options);

            // Add a test layout
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = true,
                Head = string.Empty,
                HtmlHeader = string.Empty,
                FooterHtmlContent = string.Empty
            };

            context.Layouts.Add(layout);
            await context.SaveChangesAsync();

            // Verify it was saved
            var savedLayout = await context.Layouts.FirstOrDefaultAsync(l => l.LayoutName == "Test Layout");
            Assert.IsNotNull(savedLayout);
            Assert.AreEqual("Test Layout", savedLayout.LayoutName);
        }

        #endregion
    }
}