// <copyright file="IMigration.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Migrations.Core
{
    using Cosmos.Common.Data;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a database migration that can be applied to multiple database providers.
    /// </summary>
    public interface IMigration
    {
        /// <summary>
        /// Gets the unique migration identifier (e.g., "001", "002").
        /// </summary>
        string MigrationId { get; }

        /// <summary>
        /// Gets the migration description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the migration version (semantic versioning recommended).
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Checks if this migration has already been applied to the database.
        /// </summary>
        /// <param name="context">Migration execution context.</param>
        /// <returns>True if migration is already applied.</returns>
        Task<bool> IsAppliedAsync(MigrationContext context);

        /// <summary>
        /// Applies the migration to the database.
        /// </summary>
        /// <param name="context">Migration execution context.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        Task ApplyAsync(MigrationContext context);

        /// <summary>
        /// Rolls back the migration (optional, for development/testing).
        /// </summary>
        /// <param name="context">Migration execution context.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        Task RollbackAsync(MigrationContext context);
    }

    /// <summary>
    /// Provides context for migration execution.
    /// </summary>
    public class MigrationContext
    {
        /// <summary>
        /// Gets or sets the database context.
        /// </summary>
        public ApplicationDbContext DbContext { get; set; }

        /// <summary>
        /// Gets or sets the database provider type.
        /// </summary>
        public DatabaseProvider Provider { get; set; }

        /// <summary>
        /// Gets or sets the connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Gets or sets the service provider for dependency injection.
        /// </summary>
        public IServiceProvider ServiceProvider { get; set; }
    }

    /// <summary>
    /// Database provider types.
    /// </summary>
    public enum DatabaseProvider
    {
        /// <summary>
        /// Azure Cosmos DB NoSQL.
        /// </summary>
        CosmosDb,

        /// <summary>
        /// Microsoft SQL Server.
        /// </summary>
        SqlServer,

        /// <summary>
        /// MySQL or MariaDB.
        /// </summary>
        MySql,

        /// <summary>
        /// SQLite.
        /// </summary>
        Sqlite
    }
}