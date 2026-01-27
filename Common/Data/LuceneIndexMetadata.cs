// <copyright file="LuceneIndexMetadata.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Represents metadata for a Lucene search index stored in the database.
    /// Tracks index configuration, statistics, and tenant isolation.
    /// </summary>
    [Table("LuceneIndexMetadata")]
    [Index(nameof(TenantDomain), nameof(IndexName), IsUnique = true)]
    [Index(nameof(TenantDomain))]
    [Index(nameof(LastOptimized))]
    public class LuceneIndexMetadata
    {
        /// <summary>
        /// Gets or sets the unique identifier for this index metadata.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the tenant domain for multi-tenant isolation.
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string TenantDomain { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the search index.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string IndexName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the index configuration (JSON).
        /// Stores analyzer settings, field mappings, etc.
        /// </summary>
        public string? Configuration { get; set; }

        /// <summary>
        /// Gets or sets the total number of documents in the index.
        /// </summary>
        public long DocumentCount { get; set; }

        /// <summary>
        /// Gets or sets the index size in bytes (for monitoring).
        /// </summary>
        public long IndexSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets when this index was created.
        /// </summary>
        [Required]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets when this index was last modified.
        /// </summary>
        [Required]
        public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets when this index was last optimized.
        /// </summary>
        public DateTimeOffset? LastOptimized { get; set; }

        /// <summary>
        /// Gets or sets the index version for compatibility tracking.
        /// </summary>
        [MaxLength(20)]
        public string? IndexVersion { get; set; }

        /// <summary>
        /// Gets or sets whether the index is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the metadata version for optimistic concurrency.
        /// </summary>
        [Timestamp]
        public byte[]? Version { get; set; }
    }
}