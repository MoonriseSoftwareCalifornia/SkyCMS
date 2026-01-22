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
                    "Ensure the connection string is valid for Cosmos DB, SQL Server, MySQL, or SQLite.",
                    nameof(connectionString));
            }

            // Map strategy ProviderName to DatabaseProvider enum
            return strategy.ProviderName switch
            {
                "Microsoft.EntityFrameworkCore.Cosmos" => DatabaseProvider.CosmosDb,
                "Microsoft.EntityFrameworkCore.SqlServer" => DatabaseProvider.SqlServer,
                "MySql.EntityFrameworkCore" => DatabaseProvider.MySql,
                "Microsoft.EntityFrameworkCore.Sqlite" => DatabaseProvider.Sqlite,
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
                         && !t.IsAbstract)
                .ToList();

            foreach (var type in migrationTypes)
            {
                try
                {
                    var migration = (IMigration)Activator.CreateInstance(type);
                    migrations.Add(migration);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to instantiate migration type: {TypeName}", type.Name);
                }
            }

            _logger.LogDebug("Discovered {Count} migration(s)", migrations.Count);
            return migrations.OrderBy(m => m.MigrationId).ToList();
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
                        applied.Add(reader.GetString(0));
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
        private static void AddParameter(DbCommand command, string name, string value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
    }
}