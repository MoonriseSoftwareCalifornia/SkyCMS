// <copyright file="Article.cs" company="Moonrise Software, LLC">
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

    /// <summary>
    /// Represents a persisted unit of authored content (a page, post, or redirect) within the CMS.
    /// </summary>
    /// <remarks>
    /// An <see cref="Article"/> is the canonical content object the site renders.
    /// Multiple versions (distinguished by <see cref="VersionNumber"/>) may exist per logical article number
    /// (<see cref="ArticleNumber"/>). A published version is determined by <see cref="Published"/> (and optionally
    /// <see cref="Expires"/>). A record may also function as a redirect if <see cref="RedirectTarget"/> is populated.
    /// </remarks>
    public class Article
    {
        /// <summary>
        /// Gets or sets the immutable primary key (database identity) for this persisted record.
        /// </summary>
        /// <remarks>Distinct from <see cref="ArticleNumber"/> which represents the logical article grouping.</remarks>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the logical article number (shared across versions of the same article).
        /// </summary>
        /// <remarks>
        /// Typically used to group multiple <see cref="VersionNumber"/> revisions. Each published revision
        /// with the same article number supersedes earlier versions.
        /// </remarks>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the workflow / lifecycle status code for this version of the content.
        /// </summary>
        /// <remarks>
        /// Values correspond to <see cref="StatusCodeEnum"/> (e.g., Draft, Review, Published, Archived).
        /// A separate publish timestamp (<see cref="Published"/>) determines when the article becomes publicly visible.
        /// </remarks>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Gets or sets the relative URL path segment that uniquely locates this article when rendered.
        /// </summary>
        /// <remarks>
        /// Should be unique among published articles of the same type. Do not include protocol, host, or query string.
        /// Examples: "/", "about/company-history", "blog/my-first-post".
        /// </remarks>
        [MaxLength(1999)]
        public string UrlPath { get; set; }

        /// <summary>
        /// Gets or sets the monotonically increasing version number for the article.
        /// </summary>
        /// <remarks>Each content update should increment this value within the same <see cref="ArticleNumber"/> scope.</remarks>
        [Display(Name = "Article version")]
        public int VersionNumber { get; set; }

        /// <summary>
        /// Gets or sets the UTC date/time when this version becomes (or became) publicly visible.
        /// </summary>
        /// <remarks>
        /// Null indicates this version is not yet published. Scheduling in the future enables delayed activation.
        /// </remarks>
        [Display(Name = "Publish on (UTC):")]
        [DataType(DataType.DateTime)]
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the UTC date/time when this version should cease to be considered active.
        /// </summary>
        /// <remarks>
        /// Null indicates no explicit expiration. Rendering logic should exclude the article once the timestamp passes.
        /// </remarks>
        [Display(Name = "Expires on (UTC):")]
        [DataType(DataType.DateTime)]
        public DateTimeOffset? Expires { get; set; }

        /// <summary>
        /// Gets or sets the display title (page heading) shown in navigation, listings, and the browser title (if applied).
        /// </summary>
        [MaxLength(254)]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the HTML (or HTML-fragment) body content.
        /// </summary>
        /// <remarks>Should be sanitized or trusted prior to rendering to prevent injection issues.</remarks>
        [DataType(DataType.Html)]
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp of the most recent update to this stored version.
        /// </summary>
        /// <remarks>Automatically set on create; should be refreshed on each persisted modification.</remarks>
        [Display(Name = "Article last saved")]
        public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets optional JavaScript to inject into the page header when this article is rendered.
        /// </summary>
        /// <remarks>Intended for page-level enhancements; prefer shared bundling where possible.</remarks>
        [DataType(DataType.Html)]
        public string HeaderJavaScript { get; set; }

        /// <summary>
        /// Gets or sets optional JavaScript to inject into the page footer when this article is rendered.
        /// </summary>
        /// <remarks>Executed after main content; useful for deferred scripts.</remarks>
        [DataType(DataType.Html)]
        public string FooterJavaScript { get; set; }

        /// <summary>
        /// Gets or sets the URL (relative or absolute) to a banner or hero image associated with this article.
        /// </summary>
        /// <remarks>May be empty. Consumers should fall back to a default if not supplied.</remarks>
        [Required(AllowEmptyStrings = true)]
        public string BannerImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the user (author or editor) who owns or last modified this version.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets an optional template identifier used to vary page layout or rendering logic.
        /// </summary>
        /// <remarks>Null indicates use of a default or inferred template.</remarks>
        public Guid? TemplateId { get; set; }

        /// <summary>
        /// Gets or sets an integer classification describing the article subtype (e.g., standard page, blog post, redirect, etc.).
        /// </summary>
        /// <remarks>Application logic should map values to a semantic enumeration or constants.</remarks>
        public int? ArticleType { get; set; } = 0;

        /// <summary>
        /// Gets or sets the blog category or taxonomy label (only used when <see cref="ArticleType"/> designates a blog post).
        /// </summary>
        [MaxLength(64)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a short summary or teaser used in listing pages or meta previews.
        /// </summary>
        [MaxLength(512)]
        public string Introduction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the redirect destination URL (relative or absolute) when this record represents a redirect.
        /// </summary>
        /// <remarks>
        /// When populated, rendering logic may short‑circuit normal content output and emit an HTTP redirect/meta refresh.
        /// </remarks>
        [MaxLength(256)]
        public string RedirectTarget { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a numeric concurrency token used for optimistic concurrency control.
        /// </summary>
        /// <remarks>
        /// For SQL databases, this is incremented (or otherwise changed) on each update. EF Core's <see cref="ConcurrencyCheckAttribute"/>
        /// ensures stale writes are detected. Null indicates token may not yet have been assigned.
        /// </remarks>
        public long? RowVersion { get; set; }

        /// <summary>
        /// Gets or sets the blog key for the article.
        /// </summary>
        /// <remarks>
        /// Used to associate the article with a specific blog or grouping.
        /// Defaults to "default" if not specified.
        /// </remarks>
        [MaxLength(128)]
        public string BlogKey { get; set; } = "default";
    }
}
