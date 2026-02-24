// <copyright file="BlogListItem.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Models.Blog
{
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Lightweight projection of a blog article used in list and summary views.
    /// </summary>
    public class BlogListItem
    {
        /// <summary>
        /// Gets or sets the unique identifier (specific version or aggregate root ID for the blog entry).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the logical article number shared across versions of the blog post.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the display title of the blog post.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display author info of the blog post.
        /// </summary>
        public string AuthorInfo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL path (slug) used to access the blog post.
        /// </summary>
        [MaxLength(1999)]
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC timestamp when the blog post was published (null if draft/unpublished).
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the blog post was last updated.
        /// </summary>
        public DateTimeOffset? Updated { get; set; }

        /// <summary>
        /// Gets or sets the banner image URL or relative path.
        /// </summary>
        public string BannerImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a short introductory excerpt or summary.
        /// </summary>
        public string Introduction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category or taxonomy label for grouping.
        /// </summary>
        public string Category { get; set; } = string.Empty;
    }
}
