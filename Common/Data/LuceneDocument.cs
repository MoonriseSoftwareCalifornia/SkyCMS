// <copyright file="LuceneDocument.cs" company="Moonrise Software, LLC">
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
    /// Represents a document in the Lucene search index stored in the database.
    /// Provides tenant isolation and optimized storage for search operations.
    /// </summary>
    [Table("LuceneDocuments")]
    [Index(nameof(TenantDomain), nameof(IndexName), nameof(DocumentId), IsUnique = true)]
    [Index(nameof(TenantDomain), nameof(IndexName))]
    [Index(nameof(LastModified))]
    public class LuceneDocument
    {
        /// <summary>
        /// Gets or sets the unique identifier for this document entry.
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
        /// Gets or sets the unique document identifier within the index.
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized document content (JSON).
        /// </summary>
        [Required]
        public string DocumentContent { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the document boost value for ranking.
        /// </summary>
        public float Boost { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets when this document was created.
        /// </summary>
        [Required]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets when this document was last modified.
        /// </summary>
        [Required]
        public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the document version for optimistic concurrency.
        /// </summary>
        [Timestamp]
        public byte[]? Version { get; set; }
    }
}