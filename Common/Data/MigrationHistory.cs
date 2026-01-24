// <copyright file="MigrationHistory.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using System;

    /// <summary>
    /// Tracks applied migrations across all database providers.
    /// </summary>
    public class MigrationHistory
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MigrationHistory"/> class.
        /// </summary>
        public MigrationHistory()
        {
            Id = Guid.NewGuid().ToString();
            AppliedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Gets or sets unique identifier for this migration record.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets migration identifier (e.g., "M001_AddLayoutNumber").
        /// </summary>
        public string MigrationId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets migration version (e.g., "1.0.0").
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets human-readable description of the migration.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets when the migration was applied.
        /// </summary>
        public DateTimeOffset AppliedAt { get; set; }

        /// <summary>
        /// Gets or sets database provider (e.g., "CosmosDb", "MySql", "SqlServer", "Sqlite").
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets application version that applied the migration.
        /// </summary>
        public string ApplicationVersion { get; set; }
    }
}