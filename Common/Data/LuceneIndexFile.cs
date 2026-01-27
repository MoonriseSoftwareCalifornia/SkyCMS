// <copyright file="LuceneIndexFile.cs" company="Moonrise Software, LLC">
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
    /// Represents a Lucene index file stored in the database.
    /// Stores the actual index segments and files for the Lucene Directory implementation.
    /// </summary>
    [Table("LuceneIndexFiles")]
    [Index(nameof(TenantDomain), nameof(IndexName), nameof(FileName), IsUnique = true)]
    [Index(nameof(TenantDomain), nameof(IndexName))]
    [Index(nameof(LastModified))]
    [Index(nameof(FileSize))]
    public class LuceneIndexFile
    {
        /// <summary>
        /// Gets or sets the unique identifier for this file entry.
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
        /// Gets or sets the file name within the Lucene index.
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file content as binary data.
        /// </summary>
        [Required]
        public byte[] FileContent { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Gets or sets the file checksum for integrity verification.
        /// </summary>
        [MaxLength(64)]
        public string? Checksum { get; set; }

        /// <summary>
        /// Gets or sets when this file was created.
        /// </summary>
        [Required]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets when this file was last modified.
        /// </summary>
        [Required]
        public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets whether this file is marked for deletion.
        /// Used for cleanup operations.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets the file version for optimistic concurrency.
        /// </summary>
        [Timestamp]
        public byte[]? Version { get; set; }
    }
}