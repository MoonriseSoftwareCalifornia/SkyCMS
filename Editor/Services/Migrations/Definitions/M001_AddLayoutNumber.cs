// <copyright file="M001_AddLayoutNumber.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Migrations.Definitions
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Migrations.Core;

    /// <summary>
    /// Adds LayoutNumber column to Layouts and Templates tables to support layout versioning.
    /// </summary>
    /// <remarks>
    /// This migration enables the new layout versioning system where multiple layout versions
    /// can belong to the same layout family, identified by LayoutNumber. This replaces the
    /// previous LayoutId-based relationship with a more flexible versioning approach.
    /// </remarks>
    [Migration("001", "1.0.0", "Add LayoutNumber for layout versioning")]
    public class M001_AddLayoutNumber : IMigration
    {
        /// <inheritdoc/>
        public string MigrationId => "001";

        /// <inheritdoc/>
        public string Description => "Add LayoutNumber to Layouts and Templates for layout versioning";

        /// <inheritdoc/>
        public string Version => "1.0.0";

        /// <inheritdoc/>
        public async Task<bool> IsAppliedAsync(MigrationContext context)
        {
            try
            {
                // Try to query LayoutNumber column - if it exists, migration is applied
                if (context.Provider == DatabaseProvider.CosmosDb)
                {
                    // Cosmos DB is schema-less, check if any document has the property
                    var hasProperty = await context.DbContext.Layouts
                        .AnyAsync(l => l.LayoutNumber >= 0);
                    return true; // If query succeeds, property exists
                }
                else
                {
                    // Relational databases: Try to select the column
                    await context.DbContext.Layouts
                        .Select(l => l.LayoutNumber)
                        .Take(1)
                        .ToListAsync();
                    return true; // Column exists
                }
            }
            catch
            {
                return false; // Column doesn't exist
            }
        }

        /// <inheritdoc/>
        public async Task ApplyAsync(MigrationContext context)
        {
            context.Logger.LogInformation(
                "Applying migration {MigrationId}: {Description} for provider {Provider}",
                MigrationId, Description, context.Provider);

            switch (context.Provider)
            {
                case DatabaseProvider.CosmosDb:
                    await ApplyCosmosDbAsync(context);
                    break;

                case DatabaseProvider.MySql:
                    await ApplyMySqlAsync(context);
                    break;

                case DatabaseProvider.SqlServer:
                    await ApplySqlServerAsync(context);
                    break;

                case DatabaseProvider.Sqlite:
                    await ApplySqliteAsync(context);
                    break;

                default:
                    throw new NotSupportedException($"Provider {context.Provider} is not supported by this migration");
            }

            context.Logger.LogInformation(
                "✅ Migration {MigrationId} applied successfully to {Provider}",
                MigrationId, context.Provider);
        }

        /// <inheritdoc/>
        public async Task RollbackAsync(MigrationContext context)
        {
            context.Logger.LogWarning(
                "Rolling back migration {MigrationId} for {Provider}",
                MigrationId, context.Provider);

            switch (context.Provider)
            {
                case DatabaseProvider.CosmosDb:
                    throw new NotSupportedException(
                        "Cosmos DB doesn't support schema rollback. Manual data cleanup required.");

                case DatabaseProvider.MySql:
                case DatabaseProvider.Sqlite:
                    await context.DbContext.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE Layouts DROP COLUMN LayoutNumber");
                    await context.DbContext.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE Templates DROP COLUMN LayoutNumber");
                    break;

                case DatabaseProvider.SqlServer:
                    await context.DbContext.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE [Layouts] DROP COLUMN [LayoutNumber]");
                    await context.DbContext.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE [Templates] DROP COLUMN [LayoutNumber]");
                    break;
            }

            context.Logger.LogWarning("Migration {MigrationId} rolled back", MigrationId);
        }

        /// <summary>
        /// Applies migration to Cosmos DB.
        /// </summary>
        private async Task ApplyCosmosDbAsync(MigrationContext context)
        {
            // Cosmos DB is schema-less - no ALTER TABLE needed
            // The property will be available once the entity model is updated
            context.Logger.LogInformation(
                "Cosmos DB is schema-less - LayoutNumber property will be available immediately");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Applies migration to MySQL/MariaDB.
        /// </summary>
        private async Task ApplyMySqlAsync(MigrationContext context)
        {
            context.Logger.LogDebug("Adding LayoutNumber column to Layouts table");
            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Layouts 
                ADD COLUMN LayoutNumber INT NOT NULL DEFAULT 0");

            context.Logger.LogDebug("Adding LayoutNumber column to Templates table");
            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Templates 
                ADD COLUMN LayoutNumber INT NOT NULL DEFAULT 0");

            context.Logger.LogDebug("Creating indexes for LayoutNumber");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IX_Layout_LayoutNumber 
                ON Layouts(LayoutNumber)");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IX_Layout_LayoutNumber_Version 
                ON Layouts(LayoutNumber, Version)");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IX_Layout_LayoutNumber_IsDefault_Published 
                ON Layouts(LayoutNumber, IsDefault, Published)");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IX_Template_LayoutNumber 
                ON Templates(LayoutNumber)");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IX_Template_LayoutNumber_LayoutId 
                ON Templates(LayoutNumber, LayoutId)");

            context.Logger.LogDebug("MySQL migration completed - 2 columns and 5 indexes created");
        }

        /// <summary>
        /// Applies migration to SQL Server.
        /// </summary>
        private async Task ApplySqlServerAsync(MigrationContext context)
        {
            context.Logger.LogDebug("Adding LayoutNumber column to Layouts table");
            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE [Layouts] 
                ADD [LayoutNumber] INT NOT NULL DEFAULT 0");

            context.Logger.LogDebug("Adding LayoutNumber column to Templates table");
            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE [Templates] 
                ADD [LayoutNumber] INT NOT NULL DEFAULT 0");

            context.Logger.LogDebug("Creating indexes for LayoutNumber");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX [IX_Layout_LayoutNumber] 
                ON [Layouts]([LayoutNumber])");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX [IX_Layout_LayoutNumber_Version] 
                ON [Layouts]([LayoutNumber], [Version])");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX [IX_Layout_LayoutNumber_IsDefault_Published] 
                ON [Layouts]([LayoutNumber], [IsDefault], [Published])");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX [IX_Template_LayoutNumber] 
                ON [Templates]([LayoutNumber])");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX [IX_Template_LayoutNumber_LayoutId] 
                ON [Templates]([LayoutNumber], [LayoutId])");

            context.Logger.LogDebug("SQL Server migration completed - 2 columns and 5 indexes created");
        }

        /// <summary>
        /// Applies migration to SQLite.
        /// </summary>
        private async Task ApplySqliteAsync(MigrationContext context)
        {
            // SQLite uses similar syntax to MySQL for ALTER TABLE
            context.Logger.LogDebug("Adding LayoutNumber column to Layouts table");
            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Layouts 
                ADD COLUMN LayoutNumber INTEGER NOT NULL DEFAULT 0");

            context.Logger.LogDebug("Adding LayoutNumber column to Templates table");
            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Templates 
                ADD COLUMN LayoutNumber INTEGER NOT NULL DEFAULT 0");

            context.Logger.LogDebug("Creating indexes for LayoutNumber");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IX_Layout_LayoutNumber 
                ON Layouts(LayoutNumber)");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IX_Layout_LayoutNumber_Version 
                ON Layouts(LayoutNumber, Version)");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IX_Layout_LayoutNumber_IsDefault_Published 
                ON Layouts(LayoutNumber, IsDefault, Published)");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IX_Template_LayoutNumber 
                ON Templates(LayoutNumber)");

            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IX_Template_LayoutNumber_LayoutId 
                ON Templates(LayoutNumber, LayoutId)");

            context.Logger.LogDebug("SQLite migration completed - 2 columns and 5 indexes created");
        }
    }
}