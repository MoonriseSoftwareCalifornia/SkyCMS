// <copyright file="MigrationService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Migrations
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Migrations.Core;

    /// <summary>
    /// Discovers and executes database migrations across all supported database providers.
    /// </summary>
    /// <remarks>
    /// This service automatically discovers all classes implementing <see cref="IMigration"/>,
    /// checks which migrations have been applied, and executes pending migrations in order.
    /// Migrations are provider-aware and can implement different logic for each database type.
    /// </remarks>
    public class MigrationService
    {
        private readonly ILogger<MigrationService> _logger;
        private readonly List<IMigration> _migrations;

        /// <summary>
        /// Initializes a new instance of the <see cref="MigrationService"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        public MigrationService(ILogger<MigrationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _migrations = DiscoverMigrations();
        }

        /// <summary>
        /// Determines the database provider from a connection string using CosmosDbOptionsBuilder strategies.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        /// <returns>The detected database provider.</returns>
        /// <exception cref="ArgumentNullException">Thrown when connectionString is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when provider cannot be determined.</exception>
        /// <exception cref="NotSupportedException">Thrown when provider is recognized but not supported.</exception>
        /// <remarks>
        /// This method leverages the existing CosmosDbOptionsBuilder infrastructure to detect
        /// database providers, ensuring consistent provider detection across the application.
        /// Supported providers: Cosmos DB, SQL Server, MySQL/MariaDB, SQLite.
        /// </remarks>
        public static DatabaseProvider DetermineProvider(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            // Use existing CosmosDbOptionsBuilder strategies to detect provider
            var strategies = AspNetCore.Identity.FlexDb.CosmosDbOptionsBuilder.GetDefaultStrategies();
            var strategy = strategies
                .OrderBy(s => s.Priority)
                .FirstOrDefault(s => s.CanHandle(connectionString));

            if (strategy == null)
            {
                throw new ArgumentException(
                    "Unable to determine database provider from connection string. " +
                    "Ensure the connection string is valid for Cosmos, SQL Server, MySQL, or SQLite.",
                    nameof(connectionString));
            }

            // Map strategy ProviderName to DatabaseProvider enum
            return strategy.ProviderName switch
            {
                "Cosmos" => DatabaseProvider.CosmosDb,
                "SQL Server" => DatabaseProvider.SqlServer,
                "MySQL" => DatabaseProvider.MySql,
                "SQLite" => DatabaseProvider.Sqlite,
                _ => throw new NotSupportedException(
                    $"Provider '{strategy.ProviderName}' is recognized but not supported by migration service")
            };
        }

        /// <summary>
        /// Runs all pending migrations for the specified database.
        /// </summary>
        /// <param name="context">Migration execution context.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public async Task RunMigrationsAsync(MigrationContext context)
        {
            _logger.LogInformation("🔄 Starting custom migration discovery and execution");

            try
            {
                // Check if this is a brand new database BEFORE creating migration history table
                bool isBrandNewDatabase = await IsBrandNewDatabaseAsync(context);
                
                if (isBrandNewDatabase)
                {
                    _logger.LogInformation("📦 Brand new database detected - bootstrapping with EnsureCreated");
                    
                    // Create complete schema from current DbContext model
                    await context.DbContext.Database.EnsureCreatedAsync();
                    _logger.LogInformation("✅ Database schema created");
                    
                    // Record virtual "000" migration as the bootstrap marker
                    await RecordVirtualInitialMigrationAsync(context);
                    
                    // Mark ALL currently discovered migrations as applied
                    // (their changes are included in the EnsureCreated schema)
                    _logger.LogInformation("📝 Marking {Count} existing migration(s) as pre-applied", _migrations.Count);
                    
                    foreach (var migration in _migrations.OrderBy(m => m.MigrationId))
                    {
                        await RecordMigrationAsync(context, migration);
                        _logger.LogInformation("   ✓ Pre-recorded migration {Id}", migration.MigrationId);
                    }
                    
                    _logger.LogInformation("✅ Initial schema setup complete");
                    return;
                }
                
                // Existing database - run normal migration flow
                _logger.LogInformation("🔍 Existing database detected - checking for pending migrations");
                
                // Ensure migration history table exists
                await EnsureMigrationHistoryTableAsync(context);

                // Get applied migrations
                var appliedMigrations = await GetAppliedMigrationsAsync(context);
                
                // Get pending migrations
                var pendingMigrations = _migrations
                    .Where(m => !appliedMigrations.Contains(m.MigrationId))
                    .OrderBy(m => m.MigrationId)
                    .ToList();

                if (!pendingMigrations.Any())
                {
                    _logger.LogInformation("✅ All custom migrations are up to date");
                    return;
                }

                _logger.LogInformation("📋 Found {Count} pending migration(s) to apply", pendingMigrations.Count);

                foreach (var migration in pendingMigrations)
                {
                    await ApplyMigrationAsync(context, migration);
                }

                _logger.LogInformation("✅ All custom migrations completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Custom migration execution failed");
                throw;
            }
        }

        /// <summary>
        /// Applies a single migration with error handling and tracking.
        /// </summary>
        private async Task ApplyMigrationAsync(MigrationContext context, IMigration migration)
        {
            _logger.LogInformation("Applying migration {Id}: {Description}", 
                migration.MigrationId, migration.Description);

            try
            {
                // Check if migration is already applied at the schema level (defensive check)
                bool isAlreadyApplied = await migration.IsAppliedAsync(context);
                if (isAlreadyApplied)
                {
                    _logger.LogInformation(
                        "⚠️ Migration {Id} appears to be already applied at schema level - skipping execution but recording it",
                        migration.MigrationId);
                    await RecordMigrationAsync(context, migration);
                    return;
                }

                await migration.ApplyAsync(context);
                await RecordMigrationAsync(context, migration);

                _logger.LogInformation("✅ Migration {Id} completed successfully", 
                    migration.MigrationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "❌ Migration {Id} failed: {Message}", 
                    migration.MigrationId, ex.Message);
                throw new InvalidOperationException(
                    $"Migration {migration.MigrationId} ({migration.Description}) failed. " +
                    $"See inner exception for details.", ex);
            }
        }

        /// <summary>
        /// Discovers all migration classes in the current assembly.
        /// </summary>
        private List<IMigration> DiscoverMigrations()
        {
            var migrations = new List<IMigration>();
            var migrationTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IMigration).IsAssignableFrom(t) 
                         && !t.IsInterface 
                         && !t.IsAbstract
                         && !t.IsNested) // Exclude nested types like VirtualMigration
                .ToList();

            foreach (var type in migrationTypes)
            {
                try
                {
                    var migration = (IMigration)Activator.CreateInstance(type);
                    
                    // Validate migration ID is not "000" (reserved for virtual bootstrap migration)
                    if (migration.MigrationId == "000")
                    {
                        throw new InvalidOperationException(
                            $"Migration '{type.FullName}' uses reserved migration ID '000'. " +
                            "Migration ID '000' is reserved for the virtual bootstrap migration created by EnsureCreated.");
                    }
                    
                    migrations.Add(migration);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to instantiate migration type: {TypeName}", type.Name);
                    throw; // Re-throw to ensure validation failures are not silently ignored
                }
            }

            _logger.LogDebug("Discovered {Count} migration(s)", migrations.Count);
            return migrations.OrderBy(m => m.MigrationId).ToList();
        }

        /// <summary>
        /// Determines if a migration ID is reserved.
        /// Currently only "000" is strictly reserved for the virtual bootstrap migration.
        /// Note: IDs 001-009 are conventionally reserved for system migrations shipped with SkyCMS.
        /// </summary>
        /// <param name="migrationId">The migration ID to check.</param>
        /// <returns>True if the ID is reserved; otherwise, false.</returns>
        internal static bool IsReservedMigrationId(string migrationId)
        {
            if (string.IsNullOrWhiteSpace(migrationId))
            {
                return false;
            }

            // Only "000" is strictly reserved for the virtual bootstrap migration
            return migrationId == "000";
        }

        /// <summary>
        /// Ensures the MigrationHistory table exists in the database.
        /// </summary>
        private async Task EnsureMigrationHistoryTableAsync(MigrationContext context)
        {
            var sql = context.Provider switch
            {
                DatabaseProvider.MySql => @"
                    CREATE TABLE IF NOT EXISTS MigrationHistory (
                        Id CHAR(36) PRIMARY KEY,
                        MigrationId VARCHAR(50) NOT NULL,
                        Version VARCHAR(20) NOT NULL,
                        Description VARCHAR(500),
                        AppliedAt DATETIME(6) NOT NULL,
                        Provider VARCHAR(20) NOT NULL,
                        ApplicationVersion VARCHAR(50),
                        INDEX idx_migration_id (MigrationId),
                        INDEX idx_provider (Provider)
                    )",
                
                DatabaseProvider.SqlServer => @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MigrationHistory')
                    CREATE TABLE MigrationHistory (
                        Id UNIQUEIDENTIFIER PRIMARY KEY,
                        MigrationId NVARCHAR(50) NOT NULL,
                        Version NVARCHAR(20) NOT NULL,
                        Description NVARCHAR(500),
                        AppliedAt DATETIMEOFFSET NOT NULL,
                        Provider NVARCHAR(20) NOT NULL,
                        ApplicationVersion NVARCHAR(50),
                        INDEX idx_migration_id NONCLUSTERED (MigrationId),
                        INDEX idx_provider NONCLUSTERED (Provider)
                    )",
                
                DatabaseProvider.Sqlite => @"
                    CREATE TABLE IF NOT EXISTS MigrationHistory (
                        Id TEXT PRIMARY KEY,
                        MigrationId TEXT NOT NULL,
                        Version TEXT NOT NULL,
                        Description TEXT,
                        AppliedAt TEXT NOT NULL,
                        Provider TEXT NOT NULL,
                        ApplicationVersion TEXT
                    );
                    CREATE INDEX IF NOT EXISTS idx_migration_id ON MigrationHistory(MigrationId);
                    CREATE INDEX IF NOT EXISTS idx_provider ON MigrationHistory(Provider);",
                
                DatabaseProvider.CosmosDb => 
                    null, // Cosmos DB doesn't need explicit table creation
                
                _ => throw new NotSupportedException($"Provider {context.Provider} not supported")
            };

            if (sql != null)
            {
                await context.DbContext.Database.ExecuteSqlRawAsync(sql);
            }
        }

        /// <summary>
        /// Gets the list of migration IDs that have already been applied.
        /// </summary>
        private async Task<HashSet<string>> GetAppliedMigrationsAsync(MigrationContext context)
        {
            if (context.Provider == DatabaseProvider.CosmosDb)
            {
                // Cosmos DB: Query the MigrationHistory container using LINQ
                try
                {
                    var applied = await context.DbContext
                        .Set<MigrationHistory>()
                        .Where(m => m.Provider == context.Provider.ToString())
                        .Select(m => m.MigrationId)
                        .ToListAsync();

                    return new HashSet<string>(applied);
                }
                catch (Exception ex)
                {
                    // Container might not exist yet - that's okay
                    _logger.LogDebug(ex, "MigrationHistory container not found or query failed - assuming no migrations applied");
                    return new HashSet<string>();
                }
            }

            // Relational databases: Use parameterized SQL
            var connection = context.DbContext.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            
            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT MigrationId FROM MigrationHistory WHERE Provider = @Provider";
                
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@Provider";
                parameter.Value = context.Provider.ToString();
                command.Parameters.Add(parameter);

                var applied = new HashSet<string>();
                
                try
                {
                    using var reader = await command.ExecuteReaderAsync();
                    
                    while (await reader.ReadAsync())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            applied.Add(reader.GetString(0));
                        }
                        else
                        {
                            _logger.LogWarning("Found migration record with NULL MigrationId - skipping");
                        }
                    }
                }
                catch (DbException)
                {
                    // Table might not exist yet - that's okay
                    _logger.LogDebug("MigrationHistory table not found or query failed - assuming no migrations applied");
                }

                return applied;
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
        /// Records a migration as applied in the MigrationHistory table.
        /// </summary>
        private async Task RecordMigrationAsync(MigrationContext context, IMigration migration)
        {
            if (string.IsNullOrWhiteSpace(migration.MigrationId))
            {
                throw new ArgumentException("Migration must have a non-null, non-empty MigrationId", nameof(migration));
            }

            if (string.IsNullOrWhiteSpace(migration.Version))
            {
                throw new ArgumentException("Migration must have a non-null, non-empty Version", nameof(migration));
            }

            var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

            if (context.Provider == DatabaseProvider.CosmosDb)
            {
                // Cosmos DB: Use EF Core entities
                var record = new MigrationHistory
                {
                    MigrationId = migration.MigrationId,
                    Version = migration.Version,
                    Description = migration.Description,
                    Provider = context.Provider.ToString(),
                    ApplicationVersion = appVersion
                };

                context.DbContext.Set<MigrationHistory>().Add(record);
                await context.DbContext.SaveChangesAsync();
                return;
            }

            // Relational databases: Use parameterized SQL
            var id = Guid.NewGuid();
            var connection = context.DbContext.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;

            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                
                // Provider-specific SQL with placeholders
                command.CommandText = context.Provider switch
                {
                    DatabaseProvider.MySql =>
                        "INSERT INTO MigrationHistory (Id, MigrationId, Version, Description, AppliedAt, Provider, ApplicationVersion) " +
                        "VALUES (@Id, @MigrationId, @Version, @Description, UTC_TIMESTAMP(6), @Provider, @ApplicationVersion)",
                    
                    DatabaseProvider.SqlServer =>
                        "INSERT INTO MigrationHistory (Id, MigrationId, Version, Description, AppliedAt, Provider, ApplicationVersion) " +
                        "VALUES (@Id, @MigrationId, @Version, @Description, SYSDATETIMEOFFSET(), @Provider, @ApplicationVersion)",
                    
                    DatabaseProvider.Sqlite =>
                        "INSERT INTO MigrationHistory (Id, MigrationId, Version, Description, AppliedAt, Provider, ApplicationVersion) " +
                        "VALUES (@Id, @MigrationId, @Version, @Description, datetime('now'), @Provider, @ApplicationVersion)",
                    
                    _ => throw new NotSupportedException($"Provider {context.Provider} not supported")
                };

                // Add parameters
                AddParameter(command, "@Id", id.ToString());
                AddParameter(command, "@MigrationId", migration.MigrationId);
                AddParameter(command, "@Version", migration.Version);
                AddParameter(command, "@Description", migration.Description ?? string.Empty);
                AddParameter(command, "@Provider", context.Provider.ToString());
                AddParameter(command, "@ApplicationVersion", appVersion);

                await command.ExecuteNonQueryAsync();
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
        /// Helper method to add a parameter to a database command.
        /// </summary>
        /// <remarks>
        /// Handles null values by converting them to DBNull.Value for proper database parameter binding.
        /// </remarks>
        private static void AddParameter(DbCommand command, string name, string value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? (object)DBNull.Value; // Handle null values for database
            command.Parameters.Add(parameter);
        }

        /// <summary>
        /// Records a virtual "000" migration to mark database schema as bootstrapped via EnsureCreated.
        /// </summary>
        /// <param name="context">Migration execution context.</param>
        private async Task RecordVirtualInitialMigrationAsync(MigrationContext context)
        {
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
    
            // Create a virtual migration marker
            var virtualMigration = new VirtualMigration
            {
                MigrationId = "000",
                Version = "1.0.0",
                Description = "Initial schema created via EnsureCreated (includes all schema up to current DbContext model)"
            };

            await RecordMigrationAsync(context, virtualMigration);
        }

        /// <summary>
        /// Determines if this is a brand new database with no application tables.
        /// </summary>
        /// <param name="context">Migration execution context.</param>
        /// <returns>True if the database has no application tables; otherwise, false.</returns>
        /// <remarks>
        /// Uses the Layouts table as a sentinel to detect brand new databases.
        /// Layouts is a core table that should exist in any initialized SkyCMS database.
        /// </remarks>
        private async Task<bool> IsBrandNewDatabaseAsync(MigrationContext context)
        {
            if (context.Provider == DatabaseProvider.CosmosDb)
            {
                // Cosmos DB: Check if core containers exist by attempting a query
                try
                {
                    await context.DbContext.Set<Layout>().AnyAsync();
                    return false; // If query succeeds, containers exist
                }
                catch
                {
                    return true; // Containers don't exist yet
                }
            }

            // Relational databases: Check if Layouts table exists
            // (Layouts is a core table that should exist in any initialized database)
            var connection = context.DbContext.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            
            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                
                command.CommandText = context.Provider switch
                {
                    DatabaseProvider.Sqlite => 
                        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Layouts'",
                    DatabaseProvider.MySql => 
                        "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'Layouts'",
                    DatabaseProvider.SqlServer => 
                        "SELECT COUNT(*) FROM sys.tables WHERE name = 'Layouts'",
                    _ => throw new NotSupportedException($"Provider {context.Provider} not supported")
                };

                var result = await command.ExecuteScalarAsync();
                var tableCount = Convert.ToInt32(result);
                
                _logger.LogDebug("Layouts table existence check: {Count} table(s) found", tableCount);
                
                return tableCount == 0; // Brand new if Layouts table doesn't exist
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking for Layouts table - assuming brand new database");
                return true; // If we can't check, assume it's new
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
        /// Internal class representing a virtual migration marker.
        /// Used to record the initial schema bootstrap without executing any actual migration logic.
        /// </summary>
        private class VirtualMigration : IMigration
        {
            public string MigrationId { get; init; }

            public string Version { get; init; }

            public string Description { get; init; }

            public Task ApplyAsync(MigrationContext context)
            {
                // Virtual migration - no actual work to do
                // Schema was already created by EnsureCreated()
                return Task.CompletedTask;
            }

            public Task<bool> IsAppliedAsync(MigrationContext context)
            {
                // Virtual migration is always considered "not yet applied" when checked
                // This method is not used in the bootstrap flow
                return Task.FromResult(false);
            }

            public Task RollbackAsync(MigrationContext context)
            {
                // Virtual migrations cannot be rolled back
                throw new NotSupportedException("Virtual migration 000 cannot be rolled back. It represents the initial schema created via EnsureCreated.");
            }
        }
    }
}