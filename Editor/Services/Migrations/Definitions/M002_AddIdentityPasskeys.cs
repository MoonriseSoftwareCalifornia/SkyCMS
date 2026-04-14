// <copyright file="M002_AddIdentityPasskeys.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Migrations.Definitions
{
    using System;
    using System.Data.Common;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Migrations.Core;

    /// <summary>
    /// Adds ASP.NET Core Identity passkey persistence schema for relational databases.
    /// </summary>
    [Migration("002", "1.1.0", "Add Identity passkey persistence schema")]
    public class M002_AddIdentityPasskeys : IMigration
    {
        /// <inheritdoc/>
        public string MigrationId => "002";

        /// <inheritdoc/>
        public string Description => "Add AspNetUserPasskeys table and index for passkey support";

        /// <inheritdoc/>
        public string Version => "1.1.0";

        /// <inheritdoc/>
        public async Task<bool> IsAppliedAsync(MigrationContext context)
        {
            if (context.Provider == DatabaseProvider.CosmosDb)
            {
                return true;
            }

            var tableExists = await TableExistsAsync(context).ConfigureAwait(false);
            if (!tableExists)
            {
                return false;
            }

            return await IndexExistsAsync(context).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ApplyAsync(MigrationContext context)
        {
            context.Logger.LogInformation(
                "Applying migration {MigrationId}: {Description} for provider {Provider}",
                MigrationId,
                Description,
                context.Provider);

            switch (context.Provider)
            {
                case DatabaseProvider.CosmosDb:
                    context.Logger.LogInformation("Cosmos DB passkey schema is already handled by provider mapping.");
                    break;

                case DatabaseProvider.SqlServer:
                    await ApplySqlServerAsync(context).ConfigureAwait(false);
                    break;

                case DatabaseProvider.Sqlite:
                    await ApplySqliteAsync(context).ConfigureAwait(false);
                    break;

                case DatabaseProvider.MySql:
                    await ApplyMySqlAsync(context).ConfigureAwait(false);
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
            switch (context.Provider)
            {
                case DatabaseProvider.CosmosDb:
                    throw new NotSupportedException("Cosmos DB does not support schema rollback.");

                case DatabaseProvider.SqlServer:
                    await context.DbContext.Database.ExecuteSqlRawAsync(@"
                        IF OBJECT_ID(N'[AspNetUserPasskeys]', N'U') IS NOT NULL
                        BEGIN
                            DROP TABLE [AspNetUserPasskeys];
                        END").ConfigureAwait(false);
                    break;

                case DatabaseProvider.Sqlite:
                    await context.DbContext.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS AspNetUserPasskeys;")
                        .ConfigureAwait(false);
                    break;

                case DatabaseProvider.MySql:
                    await context.DbContext.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS AspNetUserPasskeys;")
                        .ConfigureAwait(false);
                    break;
            }
        }

        private static async Task ApplySqlServerAsync(MigrationContext context)
        {
            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID(N'[AspNetUserPasskeys]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AspNetUserPasskeys] (
                        [CredentialId] VARBINARY(1024) NOT NULL,
                        [UserId] NVARCHAR(450) NOT NULL,
                        [Data] NVARCHAR(MAX) NOT NULL,
                        CONSTRAINT [PK_AspNetUserPasskeys] PRIMARY KEY ([CredentialId]),
                        CONSTRAINT [FK_AspNetUserPasskeys_AspNetUsers_UserId]
                            FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                    );
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_AspNetUserPasskeys_UserId'
                      AND object_id = OBJECT_ID(N'[AspNetUserPasskeys]'))
                BEGIN
                    CREATE INDEX [IX_AspNetUserPasskeys_UserId]
                    ON [AspNetUserPasskeys] ([UserId]);
                END;")
                .ConfigureAwait(false);
        }

        private static async Task ApplySqliteAsync(MigrationContext context)
        {
            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS AspNetUserPasskeys (
                    CredentialId BLOB NOT NULL,
                    UserId TEXT NOT NULL,
                    Data TEXT NOT NULL,
                    CONSTRAINT PK_AspNetUserPasskeys PRIMARY KEY (CredentialId),
                    CONSTRAINT FK_AspNetUserPasskeys_AspNetUsers_UserId
                        FOREIGN KEY (UserId) REFERENCES AspNetUsers (Id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS IX_AspNetUserPasskeys_UserId
                ON AspNetUserPasskeys (UserId);")
                .ConfigureAwait(false);
        }

        private static async Task ApplyMySqlAsync(MigrationContext context)
        {
            await context.DbContext.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS AspNetUserPasskeys (
                    CredentialId VARBINARY(1024) NOT NULL,
                    UserId VARCHAR(255) NOT NULL,
                    Data LONGTEXT NOT NULL,
                    CONSTRAINT PK_AspNetUserPasskeys PRIMARY KEY (CredentialId),
                    CONSTRAINT FK_AspNetUserPasskeys_AspNetUsers_UserId
                        FOREIGN KEY (UserId) REFERENCES AspNetUsers (Id) ON DELETE CASCADE
                );")
                .ConfigureAwait(false);

            var hasIndex = await ExecuteScalarIntAsync(
                context,
                @"SELECT COUNT(*)
                  FROM information_schema.statistics
                  WHERE table_schema = DATABASE()
                    AND table_name = 'AspNetUserPasskeys'
                    AND index_name = 'IX_AspNetUserPasskeys_UserId';")
                .ConfigureAwait(false);

            if (hasIndex == 0)
            {
                await context.DbContext.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IX_AspNetUserPasskeys_UserId ON AspNetUserPasskeys (UserId);")
                    .ConfigureAwait(false);
            }
        }

        private static async Task<bool> TableExistsAsync(MigrationContext context)
        {
            var count = context.Provider switch
            {
                DatabaseProvider.SqlServer => await ExecuteScalarIntAsync(
                    context,
                    "SELECT COUNT(*) FROM sys.tables WHERE name = 'AspNetUserPasskeys';").ConfigureAwait(false),

                DatabaseProvider.Sqlite => await ExecuteScalarIntAsync(
                    context,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AspNetUserPasskeys';").ConfigureAwait(false),

                DatabaseProvider.MySql => await ExecuteScalarIntAsync(
                    context,
                    @"SELECT COUNT(*)
                      FROM information_schema.tables
                      WHERE table_schema = DATABASE()
                        AND table_name = 'AspNetUserPasskeys';").ConfigureAwait(false),

                _ => 0
            };

            return count > 0;
        }

        private static async Task<bool> IndexExistsAsync(MigrationContext context)
        {
            var count = context.Provider switch
            {
                DatabaseProvider.SqlServer => await ExecuteScalarIntAsync(
                    context,
                    @"SELECT COUNT(*)
                      FROM sys.indexes
                      WHERE name = 'IX_AspNetUserPasskeys_UserId'
                        AND object_id = OBJECT_ID('AspNetUserPasskeys');").ConfigureAwait(false),

                DatabaseProvider.Sqlite => await ExecuteScalarIntAsync(
                    context,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_AspNetUserPasskeys_UserId';").ConfigureAwait(false),

                DatabaseProvider.MySql => await ExecuteScalarIntAsync(
                    context,
                    @"SELECT COUNT(*)
                      FROM information_schema.statistics
                      WHERE table_schema = DATABASE()
                        AND table_name = 'AspNetUserPasskeys'
                        AND index_name = 'IX_AspNetUserPasskeys_UserId';").ConfigureAwait(false),

                _ => 0
            };

            return count > 0;
        }

        private static async Task<int> ExecuteScalarIntAsync(MigrationContext context, string sql)
        {
            var connection = context.DbContext.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;

            if (!wasOpen)
            {
                await connection.OpenAsync().ConfigureAwait(false);
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;

                var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
                return Convert.ToInt32(result);
            }
            finally
            {
                if (!wasOpen && connection.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
