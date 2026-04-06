// <copyright file="MigrationHelper.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Data
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AspNetCore.Identity.FlexDb;
    using AspNetCore.Identity.FlexDb.Strategies;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Helper service for applying database migrations across different providers.
    /// Supports both single-tenant and multi-tenant scenarios.
    /// </summary>
    public static class MigrationHelper
    {
        /// <summary>
        /// Applies migrations for a single database connection.
        /// This method can be called from single-tenant or multi-tenant contexts.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when migration fails.</exception>
        public static async Task ApplyMigrationsAsync(string connectionString, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
            }

            logger.LogInformation("Analyzing connection string to determine database provider...");

            var provider = DetermineProvider(connectionString);
            logger.LogInformation("Detected provider: {Provider}", provider);

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            // Use custom strategies that include migrations assembly
            var migrationsAssembly = typeof(MigrationHelper).Assembly.FullName;
            var strategies = CreateMigrationStrategies(migrationsAssembly);

            CosmosDbOptionsBuilder.ConfigureDbOptions(optionsBuilder, connectionString, strategies);

            switch (provider)
            {
                case DatabaseProvider.CosmosDb:
                    logger.LogInformation("Cosmos DB detected. Migrations are not supported. Using EnsureCreatedAsync() instead.");
                    await EnsureCosmosDbCreatedAsync(optionsBuilder.Options, logger);
                    break;

                case DatabaseProvider.SqlServer:
                case DatabaseProvider.MySql:
                case DatabaseProvider.Sqlite:
                    await ApplyRelationalMigrationsAsync(optionsBuilder.Options, logger, provider);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported database provider: {provider}");
            }

            logger.LogInformation("Database initialization completed successfully.");
        }

        /// <summary>
        /// Marks a migration as applied without executing it.
        /// Useful for existing databases that were created without migrations.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="migrationId">The migration ID (e.g., "20250105120000_InitialCreate_SqlServer").</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when connectionString or migrationId is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when marking migration fails or provider doesn't support migrations.</exception>
        public static async Task MarkMigrationAsAppliedAsync(string connectionString, string migrationId, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
            }

            if (string.IsNullOrWhiteSpace(migrationId))
            {
                throw new ArgumentException("Migration ID cannot be null or empty", nameof(migrationId));
            }

            logger.LogInformation("Marking migration as applied: {MigrationId}", migrationId);

            var provider = DetermineProvider(connectionString);
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            // Use custom strategies that include migrations assembly
            var migrationsAssembly = typeof(MigrationHelper).Assembly.FullName;
            var strategies = CreateMigrationStrategies(migrationsAssembly);

            CosmosDbOptionsBuilder.ConfigureDbOptions(optionsBuilder, connectionString, strategies);

            if (provider == DatabaseProvider.CosmosDb)
            {
                throw new InvalidOperationException("Cosmos DB does not support migrations.");
            }

            var providerName = provider switch
            {
                DatabaseProvider.SqlServer => "SQL Server",
                DatabaseProvider.MySql => "MySQL",
                DatabaseProvider.Sqlite => "SQLite",
                _ => provider.ToString()
            };

            await MarkMigrationAsAppliedInternalAsync(optionsBuilder.Options, migrationId, logger, providerName);
        }

        /// <summary>
        /// Checks if a database exists and has been created without migrations.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <returns>True if database exists without migrations history; otherwise false.</returns>
        public static async Task<bool> DatabaseExistsWithoutMigrationsAsync(string connectionString, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
            }

            var provider = DetermineProvider(connectionString);

            if (provider == DatabaseProvider.CosmosDb)
            {
                logger.LogInformation("Cosmos DB does not support migrations - skipping check.");
                return false;
            }

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            // Use custom strategies that include migrations assembly
            var migrationsAssembly = typeof(MigrationHelper).Assembly.FullName;
            var strategies = CreateMigrationStrategies(migrationsAssembly);

            CosmosDbOptionsBuilder.ConfigureDbOptions(optionsBuilder, connectionString, strategies);

            using var context = new ApplicationDbContext(optionsBuilder.Options);

            try
            {
                // Check if database can be connected to
                var canConnect = await context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return false;
                }

                // Check if migrations history table exists
                var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

                // If no migrations applied but database exists, it was created without migrations
                return !appliedMigrations.Any();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error checking database migration status: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Creates custom database configuration strategies with migrations assembly support.
        /// </summary>
        /// <param name="migrationsAssembly">The assembly containing migrations.</param>
        /// <returns>List of configuration strategies.</returns>
        private static System.Collections.Generic.List<IDatabaseConfigurationStrategy> CreateMigrationStrategies(string migrationsAssembly)
        {
            return new System.Collections.Generic.List<IDatabaseConfigurationStrategy>
            {
                new CosmosDbConfigurationStrategy(),
                new SqlServerMigrationStrategy(migrationsAssembly),
                new MySqlMigrationStrategy(migrationsAssembly),
                new SqliteMigrationStrategy(migrationsAssembly),
            };
        }

        /// <summary>
        /// Applies migrations to a relational database (SQL Server, MySQL, SQLite).
        /// Automatically detects and handles existing databases created without migrations.
        /// For test scenarios with new databases, uses EnsureCreated for simplicity.
        /// </summary>
        /// <param name="options">The database context options.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="provider">The database provider type.</param>
        private static async Task ApplyRelationalMigrationsAsync(
            DbContextOptions<ApplicationDbContext> options,
            ILogger logger,
            DatabaseProvider provider)
        {
            using var context = new ApplicationDbContext(options);

            try
            {
                // Check if database exists and can be connected to
                var canConnect = await context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    // Database doesn't exist - use EnsureCreated for new databases
                    // This is simpler than migrations for test/dev scenarios
                    logger.LogInformation("Database does not exist. Creating new database with EnsureCreated()...");
                    await context.Database.EnsureCreatedAsync();
                    logger.LogInformation("✅ Database created successfully.");

                    // Mark all migrations as applied for proper migration tracking
                    var migrations = context.Database.GetMigrations().ToList();

                    if (migrations.Any())
                    {
                        logger.LogInformation("📋 Marking {Count} migration(s) as applied for tracking...", migrations.Count);

                        var providerName = provider switch
                        {
                            DatabaseProvider.SqlServer => "SQL Server",
                            DatabaseProvider.MySql => "MySQL",
                            DatabaseProvider.Sqlite => "SQLite",
                            _ => provider.ToString()
                        };

                        foreach (var migration in migrations.OrderBy(m => m))
                        {
                            logger.LogInformation("  - {Migration}", migration);
                            await MarkMigrationAsAppliedInternalAsync(options, migration, logger, providerName);
                        }

                        logger.LogInformation("✅ All migrations marked as applied.");
                    }

                    return;
                }

                // Database exists - check migration history
                var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
                var appliedList = appliedMigrations.ToList();

                // Get all migrations defined in code
                var allMigrations = context.Database.GetMigrations().ToList();

                // SCENARIO 1: Database exists but NO migration history
                if (!appliedList.Any() && allMigrations.Any())
                {
                    logger.LogWarning("⚠️ Database exists without migration history. This appears to be an existing database.");
                    logger.LogInformation("🔄 Automatically marking all migrations as applied to prevent schema conflicts...");

                    // Get ALL migrations for this provider
                    var providerSuffix = $"_{provider}";
                    var providerMigrations = allMigrations
                        .Where(m => m.EndsWith(providerSuffix, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(m => m)
                        .ToList();

                    var providerName = provider switch
                    {
                        DatabaseProvider.SqlServer => "SQL Server",
                        DatabaseProvider.MySql => "MySQL",
                        DatabaseProvider.Sqlite => "SQLite",
                        _ => provider.ToString()
                    };

                    if (providerMigrations.Any())
                    {
                        logger.LogInformation("📋 Marking {Count} migration(s) for {Provider} as applied:",
                            providerMigrations.Count, provider);

                        // Mark all provider-specific migrations as applied
                        foreach (var migration in providerMigrations)
                        {
                            logger.LogInformation("  - {Migration}", migration);
                            await MarkMigrationAsAppliedInternalAsync(options, migration, logger, providerName);
                        }

                        logger.LogInformation("✅ All {Provider} migrations marked as applied.", provider);
                    }
                    else
                    {
                        logger.LogWarning("⚠️ No migrations found for {Provider}. Database state may be inconsistent.", provider);
                    }

                    // Also mark migrations for other providers as applied
                    var otherProviderMigrations = allMigrations
                        .Where(m => !m.EndsWith(providerSuffix, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(m => m)
                        .ToList();

                    if (otherProviderMigrations.Any())
                    {
                        logger.LogInformation("ℹ️ Marking {Count} migration(s) for other providers as applied:",
                            otherProviderMigrations.Count);

                        foreach (var migration in otherProviderMigrations)
                        {
                            logger.LogInformation("  - {Migration}", migration);
                            await MarkMigrationAsAppliedInternalAsync(options, migration, logger, providerName);
                        }
                    }
                }

                // Check for pending migrations
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                var pendingList = pendingMigrations.ToList();

                if (pendingList.Any())
                {
                    // Filter to only include migrations for the current provider
                    var providerSuffix = $"_{provider}";
                    var providerSpecificMigrations = pendingList
                        .Where(m => m.EndsWith(providerSuffix, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(m => m)
                        .ToList();

                    // Mark other provider migrations as applied
                    var otherProviderMigrations = pendingList.Except(providerSpecificMigrations).ToList();
                    if (otherProviderMigrations.Any())
                    {
                        logger.LogInformation("ℹ️ Marking {Count} migration(s) for other providers as applied (not applicable to {Provider}):",
                            otherProviderMigrations.Count, provider);

                        var providerName = provider switch
                        {
                            DatabaseProvider.SqlServer => "SQL Server",
                            DatabaseProvider.MySql => "MySQL",
                            DatabaseProvider.Sqlite => "SQLite",
                            _ => provider.ToString()
                        };

                        foreach (var migration in otherProviderMigrations)
                        {
                            logger.LogInformation("  - {Migration} (marking as applied, not executing)", migration);
                            await MarkMigrationAsAppliedInternalAsync(options, migration, logger, providerName);
                        }
                    }

                    // Re-check pending migrations
                    pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                    pendingList = pendingMigrations.ToList();

                    if (pendingList.Any())
                    {
                        logger.LogInformation("Found {Count} pending {Provider} migration(s):", pendingList.Count, provider);
                        foreach (var migration in pendingList)
                        {
                            logger.LogInformation("  - {Migration}", migration);
                        }

                        logger.LogInformation("Applying {Provider} migrations...", provider);
                        await context.Database.MigrateAsync();
                        logger.LogInformation("✅ Migrations applied successfully.");
                    }
                    else
                    {
                        logger.LogInformation("✅ Database is up to date. No pending {Provider} migrations.", provider);
                    }
                }
                else
                {
                    logger.LogInformation("✅ Database is up to date. No pending migrations.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Migration failed: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Marks a migration as applied in the database.
        /// </summary>
        private static async Task MarkMigrationAsAppliedInternalAsync(
            DbContextOptions<ApplicationDbContext> options,
            string migrationId,
            ILogger logger,
            string providerName)
        {
            using var context = new ApplicationDbContext(options);

            try
            {
                // Get EF version
                var productVersion = typeof(DbContext).Assembly
                    .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
                    .FirstOrDefault()?.InformationalVersion?.Split('+')[0] ?? "9.0.0";

                // Use provider-specific SQL syntax
                string sql;

                switch (providerName)
                {
                    case "SQL Server":
                        // SQL Server requires IF NOT EXISTS with different syntax
                        sql = @"
                            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[__EFMigrationsHistory]') AND type = 'U')
                            BEGIN
                                CREATE TABLE [__EFMigrationsHistory] (
                                    [MigrationId] NVARCHAR(150) NOT NULL PRIMARY KEY,
                                    [ProductVersion] NVARCHAR(32) NOT NULL
                                );
                            END
                            
                            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = @p0)
                            BEGIN
                                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) 
                                VALUES (@p0, @p1);
                            END";
                        break;

                    case "MySQL":
                    case "SQLite":
                        // MySQL and SQLite support CREATE TABLE IF NOT EXISTS
                        sql = @"
                            CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
                                MigrationId TEXT NOT NULL PRIMARY KEY,
                                ProductVersion TEXT NOT NULL
                            );
                            
                            INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) 
                            SELECT @p0, @p1
                            WHERE NOT EXISTS (
                                SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = @p0
                            );";
                        break;

                    default:
                        throw new InvalidOperationException($"Unsupported database provider: {providerName}");
                }

                var rowsAffected = await context.Database.ExecuteSqlRawAsync(sql, migrationId, productVersion);

                if (rowsAffected > 0)
                {
                    logger.LogInformation("✅ Migration '{MigrationId}' marked as applied in {Provider}", migrationId, providerName);
                }
                else
                {
                    logger.LogInformation("ℹ️ Migration '{MigrationId}' was already marked as applied in {Provider}", migrationId, providerName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Failed to mark migration as applied: {Message}", ex.Message);
                throw new InvalidOperationException($"Failed to mark migration as applied: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Ensures Cosmos DB database and containers are created.
        /// </summary>
        private static async Task EnsureCosmosDbCreatedAsync(DbContextOptions<ApplicationDbContext> options, ILogger logger)
        {
            using var context = new ApplicationDbContext(options);

            try
            {
                logger.LogInformation("Ensuring Cosmos DB database and containers exist...");
                var created = await context.Database.EnsureCreatedAsync();

                if (created)
                {
                    logger.LogInformation("✅ Cosmos DB database and containers created successfully.");
                }
                else
                {
                    logger.LogInformation("✅ Cosmos DB database already exists.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Failed to ensure Cosmos DB creation: {Message}", ex.Message);
                throw new InvalidOperationException($"Failed to ensure Cosmos DB creation: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Determines the database provider from a connection string.
        /// </summary>
        private static DatabaseProvider DetermineProvider(string connectionString)
        {
            if (connectionString.Contains("AccountEndpoint", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProvider.CosmosDb;
            }

            // Check for SQL Server-specific keywords BEFORE checking for "Data Source="
            // SQL Server uses: Initial Catalog, Integrated Security, User ID, TrustServerCertificate, etc.
            if (connectionString.Contains("Initial Catalog", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Integrated Security", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Trust Server Certificate", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProvider.SqlServer;
            }

            // Check for SQL Server named instances (e.g., "Data Source=SERVER\INSTANCE")
            // Be more specific: check if backslash appears in the Data Source value itself
            // by looking for pattern like "Data Source=...\" but NOT file paths ending with .db
            if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &&
                connectionString.Contains("\\", StringComparison.OrdinalIgnoreCase) &&
                !connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) &&
                !connectionString.Contains(".sqlite", StringComparison.OrdinalIgnoreCase))
            {
                // Additional validation: SQL Server connection strings with named instances
                // typically don't have file extensions
                return DatabaseProvider.SqlServer;
            }

            // Check for MySQL BEFORE generic Server= check
            if (connectionString.Contains("Port=3306", StringComparison.OrdinalIgnoreCase) ||
                (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) &&
                 connectionString.Contains("Uid=", StringComparison.OrdinalIgnoreCase)))
            {
                return DatabaseProvider.MySql;
            }

            // Generic Server= defaults to SQL Server
            if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProvider.SqlServer;
            }

            // SQLite check LAST (since it only uses "Data Source=" without SQL Server keywords)
            if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProvider.Sqlite;
            }

            throw new InvalidOperationException("Unable to determine database provider from connection string");
        }

        /// <summary>
        /// Database provider types.
        /// </summary>
        private enum DatabaseProvider
        {
            CosmosDb,
            SqlServer,
            MySql,
            Sqlite
        }

        /// <summary>
        /// SQL Server strategy with migrations assembly support.
        /// </summary>
        private class SqlServerMigrationStrategy : IDatabaseConfigurationStrategy
        {
            private readonly string migrationsAssembly;

            public SqlServerMigrationStrategy(string migrationsAssembly)
            {
                this.migrationsAssembly = migrationsAssembly;
            }

            public string ProviderName => "SQL Server";

            public int Priority => 10;

            public bool CanHandle(string connectionString)
            {
                return new SqlServerConfigurationStrategy().CanHandle(connectionString);
            }

            public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
            {
                optionsBuilder.UseSqlServer(connectionString, options =>
                {
                    options.MigrationsAssembly(migrationsAssembly);
                });
            }
        }

        /// <summary>
        /// MySQL strategy with migrations assembly support.
        /// </summary>
        private class MySqlMigrationStrategy : IDatabaseConfigurationStrategy
        {
            private readonly string migrationsAssembly;

            public MySqlMigrationStrategy(string migrationsAssembly)
            {
                this.migrationsAssembly = migrationsAssembly;
            }

            public string ProviderName => "MySQL";

            public int Priority => 20;

            public bool CanHandle(string connectionString)
            {
                // MySQL provider disabled for .NET 10 upgrade - not compatible with Pomelo/EF Core 10
                return false; // new MySqlConfigurationStrategy().CanHandle(connectionString);
            }

            public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
            {
                throw new NotSupportedException("MySQL provider is not supported in .NET 10 upgrade. Use Cosmos DB, SQL Server, or SQLite.");
                // MySQL provider configuration disabled
                /*
                var serverVersion = ServerVersion.AutoDetect(connectionString);
                optionsBuilder.UseMySql(connectionString, serverVersion, options =>
                {
                    options.MigrationsAssembly(migrationsAssembly);
                });
                */
            }
        }

        /// <summary>
        /// SQLite strategy with migrations assembly support.
        /// </summary>
        private class SqliteMigrationStrategy : IDatabaseConfigurationStrategy
        {
            private readonly string migrationsAssembly;

            public SqliteMigrationStrategy(string migrationsAssembly)
            {
                this.migrationsAssembly = migrationsAssembly;
            }

            public string ProviderName => "SQLite";

            public int Priority => 30;

            public bool CanHandle(string connectionString)
            {
                return new SqliteConfigurationStrategy().CanHandle(connectionString);
            }

            public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
            {
                optionsBuilder.UseSqlite(connectionString, options =>
                {
                    options.MigrationsAssembly(migrationsAssembly);
                });
            }
        }
    }
}