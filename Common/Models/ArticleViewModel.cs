// <copyright file="ArticleViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Models
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// View model used to render an article (page, blog post, or specialized content) within Razor Pages.
    /// This model is the aggregation point of content (HTML), metadata (Open Graph, SEO),
    /// publishing life cycle state (status, version, temporal boundaries), layout selection,
    /// and runtime behavioral flags (authoring vs. public rendering).
    /// </summary>
    /// <remarks>
    /// Typical usage:
    /// <list type="bullet">
    ///   <item>Loaded by a page handler or service layer when resolving a URL to content.</item>
    ///   <item>Used to determine cache lifetime (via <see cref="Expires"/> / <see cref="CacheDuration"/>).</item>
    ///   <item>Supports conditional editing surfaces when <see cref="ReadWriteMode"/> or <see cref="EditModeOn"/> are true.</item>
    ///   <item>Allows client-side enrichment via injected HEAD / footer JavaScript blocks.</item>
    /// </list>
    /// Thread-safety: Instances are not thread-safe; treat as per-request scope DTO.
    /// </remarks>
    [Serializable]
    public class ArticleViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleViewModel"/> class.
        /// </summary>
        public ArticleViewModel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleViewModel"/> class.
        /// </summary>
        /// <param name="article">The article.</param>
        /// <param name="layout">The layout.</param>
        /// <param name="isEditor">Is for an editor.</param>
        /// <param name="authorInfo">The author information.</param>
        /// <param name="lang">The language.</param>
        public ArticleViewModel(Article article, LayoutViewModel layout, bool isEditor = false, string authorInfo = "", string lang = "")
        {
            if (article == null)
            {
                throw new ArgumentNullException(nameof(article));
            }

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            ArticleNumber = article.ArticleNumber;
            LanguageCode = lang;
            LanguageName = string.Empty;
            CacheDuration = 10;
            Content = article.Content;
            StatusCode = (StatusCodeEnum)article.StatusCode;
            Id = article.Id;
            Published = article.Published ?? null;
            Title = article.Title;
            UrlPath = article.UrlPath;
            Updated = article.Updated;
            VersionNumber = article.VersionNumber;
            HeadJavaScript = article.HeaderJavaScript;
            FooterJavaScript = article.FooterJavaScript;
            Layout = layout;
            ReadWriteMode = isEditor;
            Expires = article.Expires ?? null;
            BannerImage = article.BannerImage;
            AuthorInfo = authorInfo;
            ArticleType = (ArticleType)(article.ArticleType ?? 0);
            Category = article.Category;
            Introduction = article.Introduction;
        }

        /// <summary>
        /// Gets or sets the stable unique identifier of the article (primary key).
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the workflow / publication status of the article.
        /// Controls visibility and routing behavior (e.g., <see cref="StatusCodeEnum.Redirect"/>).
        /// </summary>
        public StatusCodeEnum StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the logical article number (business-facing sequence or grouping id).
        /// Used in version lineage and uniqueness validations (see <see cref="Title"/> remote validation).
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the ISO two-letter language code (e.g., en, fr, es) describing content locale.
        /// Defaults to "en". Used for localization, routing, or language negotiation.
        /// </summary>
        public string LanguageCode { get; set; } = "en";

        /// <summary>
        /// Gets or sets the human friendly language display name (e.g., English, Français).
        /// </summary>
        public string LanguageName { get; set; } = "English";

        /// <summary>
        /// Gets or sets the relative URL path (slug) for this article (minus protocol/host).
        /// Must be unique per language/versioning rules.
        /// </summary>
        [MaxLength(1999)]
        public string UrlPath { get; set; }

        /// <summary>
        /// Gets or sets the sequential version number of the article (1 = first revision).
        /// Incremented on content updates preserving immutable history when applicable.
        /// </summary>
        [Display(Name = "Article version")]
        public int VersionNumber { get; set; }

        /// <summary>
        /// Gets or sets the article title (display heading / primary semantic H1).
        /// Remote validation ensures uniqueness within the <see cref="ArticleNumber"/> scope.
        /// </summary>
        [MaxLength(80)]
        [StringLength(80)]
        [Display(Name = "Article title")]
        [Remote("CheckTitle", "Edit", AdditionalFields = "ArticleNumber")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the fully prepared (already sanitized/encoded as per pipeline) HTML body content.
        /// May contain embedded components or server-side tokens depending on system capabilities.
        /// </summary>
        [DataType(DataType.Html)]
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets optional custom JavaScript (raw) injected into the page HEAD.
        /// Use sparingly—prefer bundling / static assets where possible.
        /// </summary>
        [DataType(DataType.Html)]
        public string HeadJavaScript { get; set; }

        /// <summary>
        /// Gets or sets optional custom JavaScript (raw) injected just before closing BODY tag.
        /// Suitable for deferred execution scripts or page-level enhancements.
        /// </summary>
        [DataType(DataType.Html)]
        public string FooterJavaScript { get; set; }

        /// <summary>
        /// Gets or sets the layout applied to this article (template container defining chrome).
        /// When null, system default resolution heuristics may apply.
        /// </summary>
        public LayoutViewModel Layout { get; set; }

        /// <summary>
        /// Gets or sets the last persisted modification timestamp (UTC or offset aware).
        /// Value is updated upon save operations (content or metadata).
        /// </summary>
        [Display(Name = "Article last saved")]
        public virtual DateTimeOffset Updated { get; set; }

        /// <summary>
        /// Gets or sets descriptive information about the article's author/editor (e.g., byline).
        /// Can include plain text or light markup depending on rendering policies.
        /// </summary>
        [Display(Name = "Author information")]
        public virtual string AuthorInfo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the scheduled publish timestamp.
        /// Article should not be publicly visible (unless previewed) before this instant.
        /// </summary>
        [Display(Name = "Publish on date/time (PST):")]
        [DataType(DataType.DateTime)]
        [DateTimeUtcKind]
        public virtual DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the time when this version should expire (stop serving from normal routes).
        /// If null, caching / expiry may defer to global defaults or no-expiration semantics.
        /// </summary>
        [Display(Name = "Expires on (UTC):")]
        [DataType(DataType.DateTime)]
        public virtual DateTimeOffset? Expires { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the site is currently in authoring (read/write) mode.
        /// Typically reflects a global flag injected via <see cref="IOptions{TOptions}"/>.
        /// </summary>
        public bool ReadWriteMode { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the article is being rendered in preview (pre-publication) mode.
        /// Enables display of unpublished / future-dated content to authorized users.
        /// </summary>
        public bool PreviewMode { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether editing affordances (inline editors, toolbars) should be displayed.
        /// True typically requires both <see cref="ReadWriteMode"/> and suitable user permissions.
        /// </summary>
        public bool EditModeOn { get; set; } = false;

        /// <summary>
        /// Gets or sets the effective cache duration in seconds derived from expiration or system defaults.
        /// A value of 0 may indicate no explicit caching policy is applied.
        /// </summary>
        public int CacheDuration { get; set; } = 0;

        /// <summary>
        /// Gets or sets the path or URL to the banner image associated with the article (hero / header image).
        /// Empty string when not set.
        /// </summary>
        public string BannerImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Open Graph image (og:image) override improving social media link previews.
        /// Fallback may be site default if left empty.
        /// </summary>
        public string OGImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Open Graph description (og:description) to enhance sharing snippets.
        /// Should be concise (typically 140–200 characters).
        /// </summary>
        public string OGDescription { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the canonical Open Graph URL (og:url) representing this resource.
        /// When empty, runtime generation may supply current route.
        /// </summary>
        public string OGUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the previous article's title in a navigable sequence (e.g., blog archive).
        /// Runtime only (not persisted).
        /// </summary>
        public string PreviousTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the previous article's URL path in a navigable sequence.
        /// Runtime only (not persisted).
        /// </summary>
        public string PreviousUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the next article's title in a navigable sequence (e.g., blog archive).
        /// Runtime only (not persisted).
        /// </summary>
        public string NextTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the next article's URL path in a navigable sequence.
        /// Runtime only (not persisted).
        /// </summary>
        public string NextUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog category or taxonomy label. Empty when not categorized or not a blog.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a short summary or introduction (excerpt) used in listings, teasers, or meta descriptions.
        /// Recommended: keep concise and avoid raw HTML beyond minimal inline markup.
        /// </summary>
        public string Introduction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the semantic classification of this article (e.g., General, BlogPost).
        /// Guides template selection, routing, filtering and analytics grouping.
        /// </summary>
        public ArticleType ArticleType { get; set; } = ArticleType.General;
    }
}
