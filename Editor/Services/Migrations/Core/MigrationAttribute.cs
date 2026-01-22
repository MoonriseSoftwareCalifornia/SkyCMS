// <copyright file="MigrationAttribute.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Migrations.Core
{
    using System;

    /// <summary>
    /// Marks a class as a database migration with metadata.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class MigrationAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MigrationAttribute"/> class.
        /// </summary>
        /// <param name="migrationId">Unique migration identifier (e.g., "001", "002").</param>
        /// <param name="version">Semantic version (e.g., "1.0.0").</param>
        /// <param name="description">Human-readable description of the migration.</param>
        public MigrationAttribute(string migrationId, string version, string description)
        {
            MigrationId = migrationId ?? throw new ArgumentNullException(nameof(migrationId));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        /// <summary>
        /// Gets the migration identifier.
        /// </summary>
        public string MigrationId { get; }

        /// <summary>
        /// Gets the version.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets or sets migrations that must be applied before this one.
        /// </summary>
        public string[] DependsOn { get; set; } = Array.Empty<string>();
    }
}