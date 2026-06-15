// <copyright file="CatalogEntry.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    /// <summary>
    /// Represents the canonical catalog entry (metadata record) for a published or draft article.
    /// </summary>
    /// <remarks>
    /// This class is the single authoritative source for:
    /// <list type="bullet">
    ///   <item><description>Article identity (ArticleNumber)</description></item>
    ///   <item><description>Title and introductory teaser text</description></item>
    ///   <item><description>Lifecycle state (<see cref="Status"/>)</description></item>
    ///   <item><description>Last content update timestamp (<see cref="Updated"/>)</description></item>
    ///   <item><description>Optional publication timestamp (<see cref="Published"/>)</description></item>
    ///   <item><description>Resolved URL path slug (<see cref="UrlPath"/>)</description></item>
    ///   <item><description>Template selection (<see cref="TemplateId"/>)</description></item>
    ///   <item><description>Per-identity or role permissions (<see cref="ArticlePermissions"/>)</description></item>
    /// </list>
    /// Concurrency: <see cref="RowVersion"/> is used as an optimistic concurrency token.
    /// </remarks>
    public class CatalogEntry
    {
        /// <summary>
        /// Gets or sets the numeric identifier (primary key) for the article.
        /// </summary>
        /// <remarks>
        /// This value is immutable once persisted and is used as the foreign key for related
        /// content (e.g., permissions, article body storage, assets).
        /// </remarks>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Display(Name = "Article#")]
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets serialized author information (e.g., display name or structured JSON).
        /// </summary>
        /// <remarks>
        /// Stored as a string to allow flexibility (simple name, composite author, etc.).
        /// Empty string when author not explicitly assigned.
        /// </remarks>
        [Display(Name = "Author")]
        public string AuthorInfo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path or identifier of the banner/hero image associated with the article.
        /// </summary>
        /// <remarks>
        /// May be a relative path, CDN URL, or media storage key. Empty string if none.
        /// </remarks>
        [Display(Name = "Banner Image")]
        public string BannerImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable article title.
        /// </summary>
        /// <remarks>
        /// Also serves as the basis for generating a slug for <see cref="UrlPath"/> if not explicitly set.
        /// </remarks>
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a short summary or teaser for the article.
        /// </summary>
        /// <remarks>
        /// Intended for list views, search previews, and meta descriptions.
        /// </remarks>
        [Display(Name = "Introduction")]
        public string Introduction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the canonical lifecycle status code aligned with <c>StatusCodeEnum</c>.
        /// </summary>
        /// <remarks>
        /// This numeric status is the authoritative lifecycle indicator for catalog filtering
        /// across providers. It is kept in sync with <see cref="Status"/> during transition.
        /// </remarks>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Gets or sets the lifecycle status of the article as a compatibility string label.
        /// </summary>
        /// <remarks>
        /// Legacy field retained for backward compatibility. Prefer <see cref="StatusCode"/> for
        /// lifecycle decisions and filtering.
        /// </remarks>
        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp (UTC) when the article content or metadata was last modified.
        /// </summary>
        /// <remarks>
        /// This should be updated whenever any material change occurs (content or metadata affecting rendering or permissions).
        /// </remarks>
        [Display(Name = "Updated")]
        public DateTimeOffset Updated { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets or sets the publication timestamp (UTC) if the article is published and publicly visible.
        /// </summary>
        /// <remarks>
        /// Null indicates the article has not yet been published (e.g., still Draft or internal only).
        /// </remarks>
        [Display(Name = "Publish date/time")]
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the resolved URL path (slug) for routing to this article.
        /// </summary>
        /// <remarks>
        /// Should be unique within its routing scope. Not guaranteed to reflect <see cref="Title"/> if manually overridden.
        /// Stored without protocol/host (application-relative path component).
        /// </remarks>
        [MaxLength(1999)]
        [Display(Name = "Url")]
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the rendering template associated with this article, if any.
        /// </summary>
        /// <remarks>
        /// Null indicates that the default template should be applied at render time.
        /// </remarks>
        [Display(Name = "Template ID")]
        public Guid? TemplateId { get; set; }

        /// <summary>
        /// Gets or sets the collection of explicit permissions controlling access to this article.
        /// </summary>
        /// <remarks>
        /// When empty, default/global access rules apply. Each item may represent either a role or a user
        /// (see <c>ArticlePermission.IsRoleObject</c>). This list is authoritative for fine-grained access control.
        /// </remarks>
        public List<ArticlePermission> ArticlePermissions { get; set; } = new List<ArticlePermission>();

        /// <summary>
        /// Gets or sets the optimistic concurrency token (row version) maintained by the data store.
        /// </summary>
        /// <remarks>
        /// For SQL databases, this value is automatically generated/updated by the underlying persistence provider (e.g., EF Core).
        /// Clients must supply the last known token when updating to prevent lost updates.
        /// </remarks>
        public long? RowVersion { get; set; }

        /// <summary>
        /// Gets or sets the blog key associated with the article.
        /// </summary>
        /// <remarks>
        /// This value is used to associate the article with a specific blog or section
        /// within the application. Default is "default" if not explicitly set.
        /// </remarks>
        [MaxLength(128)]
        [Display(Name = "Blog Key")]
        public string BlogKey { get; set; } = "default";
    }
}
