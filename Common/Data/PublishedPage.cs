// <copyright file="PublishedPage.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Cosmos.Common.Data.Logic;

    /*
        PSEUDOCODE (Documentation enhancement only — no runtime logic changes):
        - Maintain existing properties and defaults.
        - Expand class summary to clarify purpose, lifecycle, and usage.
        - For each property:
            * Add/expand <summary> for clarity.
            * Add <remarks> where useful (e.g., publishing workflow, constraints).
            * Reference related enums (StatusCodeEnum) where applicable.
            * Clarify nullability usage (e.g., Published / Expires).
            * Explain concurrency (RowVersion).
            * Document formatting/length expectations and semantic roles.
        - Avoid changing signatures or defaults to prevent migrations.
        - Keep default initializers to avoid null reference issues.
    */

    /// <summary>
    /// Represents a published (or publishable) content artifact (page, article, blog post) in the CMS.
    /// </summary>
    /// <remarks>
    /// This entity stores the immutable published snapshot plus metadata required for:
    /// versioning, scheduling (publish / expire windows), templating, categorization, and rendering.
    /// Draft vs published state is inferred primarily via <see cref="StatusCode"/> and <see cref="Published"/>.
    /// Concurrency control is handled via <see cref="RowVersion"/>.
    /// </remarks>
    public class PublishedPage
    {
        /// <summary>
        /// Gets or sets the immutable primary key identifier (database identity) for this published page record.
        /// </summary>
        /// <remarks>
        /// This is distinct from <see cref="ArticleNumber"/>, which is a domain-level sequential or logical identifier.
        /// </remarks>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the logical/business article number.
        /// </summary>
        /// <remarks>
        /// Typically stable across versions of the same logical article. Multiple <see cref="VersionNumber"/> entries
        /// may share the same article number. Useful for friendly lookups or grouping revisions.
        /// </remarks>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the workflow/status code representing the state of this content.
        /// </summary>
        /// <remarks>
        /// See <see cref="StatusCodeEnum"/> for defined values (e.g., Draft, Published, Archived).
        /// This value governs visibility and editorial state. A "Published" status should normally
        /// correspond with a non-null <see cref="Published"/> value.
        /// </remarks>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Gets or sets the canonical URL path (relative, without domain) used to route to this content.
        /// </summary>
        /// <remarks>
        /// Must be unique within its logical scope. Leading slash conventions should be standardized externally.
        /// Max length enforced by <see cref="MaxLengthAttribute"/>.
        /// </remarks>
        [MaxLength(1999)]
        public string UrlPath { get; set; }

        /// <summary>
        /// Gets or sets the parent page's URL path (if this content is hierarchically nested).
        /// </summary>
        /// <remarks>
        /// May be null or empty for root-level content. Used for breadcrumb generation and hierarchical navigation.
        /// </remarks>
        [MaxLength(1999)]
        public string ParentUrlPath { get; set; }

        /// <summary>
        /// Gets or sets the version number for this article content variant.
        /// </summary>
        /// <remarks>
        /// Incremented when significant content changes are persisted. Combined with <see cref="ArticleNumber"/>
        /// this can uniquely reference a historical version sequence.
        /// </remarks>
        public int VersionNumber { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when this specific version was (or will be) published.
        /// </summary>
        /// <remarks>
        /// Null indicates the version is not published (e.g., still draft or scheduled). Consumers should
        /// compare this value to current UTC time to determine publish activation.
        /// </remarks>
        [DataType(DataType.DateTime)]
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp past which this version should be considered expired/unavailable.
        /// </summary>
        /// <remarks>
        /// Null means no planned expiration. Application logic should treat an expired page as withdrawn or hidden.
        /// </remarks>
        [DataType(DataType.DateTime)]
        public DateTimeOffset? Expires { get; set; }

        /// <summary>
        /// Gets or sets the human-readable title displayed in listings, browser titles, and page headers.
        /// </summary>
        /// <remarks>Should be concise and descriptive. Max length enforced.</remarks>
        [MaxLength(254)]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the rendered (or render-ready) HTML body/content fragment.
        /// </summary>
        /// <remarks>
        /// This may already include layout-aware markup or may be injected into a template region.
        /// Sanitization / trust level should be enforced by upstream pipelines.
        /// </remarks>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the last modified timestamp (UTC) of this record's persisted representation.
        /// </summary>
        /// <remarks>
        /// Updated whenever editorial content or metadata changes. Not necessarily identical to <see cref="Published"/>.
        /// </remarks>
        public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the banner/hero image path or URL associated with the article.
        /// </summary>
        /// <remarks>
        /// May be an empty string when no image is present. Storage format (relative vs absolute) is an application concern.
        /// </remarks>
        [Required(AllowEmptyStrings = true)]
        public string BannerImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets raw JavaScript to be injected into the HTML head section for this page only.
        /// </summary>
        /// <remarks>
        /// Use sparingly. Prefer global or modular script registration where possible.
        /// </remarks>
        public string HeaderJavaScript { get; set; }

        /// <summary>
        /// Gets or sets raw JavaScript to be appended just before the closing body tag for this page only.
        /// </summary>
        /// <remarks>
        /// Commonly used for deferred scripts or analytics specific to this content.
        /// </remarks>
        public string FooterJavaScript { get; set; }

        /// <summary>
        /// Gets or sets display-ready author information (e.g., name, byline, credits).
        /// </summary>
        /// <remarks>
        /// May include markup or structured data, depending on consumption conventions.
        /// </remarks>
        public string AuthorInfo { get; set; }

        /// <summary>
        /// Gets or sets the template identifier used to render this page.
        /// </summary>
        /// <remarks>
        /// Null indicates default or system fallback template resolution should apply.
        /// </remarks>
        public Guid? TemplateId { get; set; }

        /// <summary>
        /// Gets or sets the category/tag grouping for classification and filtering.
        /// </summary>
        /// <remarks>
        /// Keep values normalized (e.g., slug or canonical casing) to assist with indexing and filtering.
        /// </remarks>
        [MaxLength(64)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a short summary or teaser used in listings, feeds, or previews.
        /// </summary>
        /// <remarks>
        /// Should be plain text or minimally formatted. Length limited for consistent UI rendering.
        /// </remarks>
        [MaxLength(512)]
        public string Introduction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optimistic concurrency token.
        /// </summary>
        /// <remarks>
        /// For SQL databases, this value is automatically managed by the underlying ORM (e.g., EF Core).
        /// Used to detect conflicting updates.
        /// Clients should include this token when updating an existing record to avoid lost updates.
        /// </remarks>
        public long? RowVersion { get; set; }

        /// <summary>
        /// Gets or sets the numeric code representing the high-level content type or specialization.
        /// </summary>
        /// <remarks>
        /// Interpretation is application-specific (e.g., 0 = Standard Page, 1 = Blog Post, 2 = Landing Page).
        /// Prefer an enum wrapper if the domain stabilizes.
        /// </remarks>
        public int? ArticleType { get; set; } = 0;

        /// <summary>
        /// Gets or sets the blog key associated with the content, used for filtering and categorization.
        /// </summary>
        /// <remarks>
        /// This helps in classifying content under different blogs or sections within the CMS.
        /// Default is "default" indicating the primary or fallback blog.
        /// </remarks>
        [MaxLength(128)]
        public string BlogKey { get; set; } = "default";
    }
}
