// <copyright file="GetBlogPostNavigationQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Blogs.EditorQueries
{
    using System;
    using System.Collections.Generic;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Query to retrieve navigation information for blog posts within a stream.
    /// Returns previous and next posts relative to a given post,
    /// useful for building "next/previous" navigation UI.
    /// </summary>
    public class GetBlogPostNavigationQuery : IQuery<GetBlogPostNavigationQueryResult>
    {
        /// <summary>
        /// Gets or sets the blog key (stream identifier).
        /// Example: "cat-wash" or "cat_wash".
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current blog post URL path.
        /// Example: "cat-wash/shampo" or "cat_wash/shampo".
        /// </summary>
        public string CurrentPostUrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the language code for filtering.
        /// </summary>
        public string Language { get; set; } = "en-US";

        /// <summary>
        /// Gets or sets a value indicating whether to include all posts in the stream.
        /// Useful for breadcrumb navigation or post listing.
        /// </summary>
        public bool IncludeAllPosts { get; set; } = false;

        /// <summary>
        /// Gets or sets the cache duration for the result.
        /// </summary>
        public TimeSpan? CacheDuration { get; set; }
    }

    /// <summary>
    /// Result of the blog post navigation query.
    /// Contains navigation links and optionally all posts in the stream.
    /// </summary>
    public class GetBlogPostNavigationQueryResult
    {
        /// <summary>
        /// Gets or sets the blog stream key.
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the previous post in the stream (chronologically).
        /// Null if this is the first/latest post.
        /// </summary>
        public BlogPostNavigationItem? PreviousPost { get; set; }

        /// <summary>
        /// Gets or sets the next post in the stream (chronologically).
        /// Null if this is the last/oldest post.
        /// </summary>
        public BlogPostNavigationItem? NextPost { get; set; }

        /// <summary>
        /// Gets or sets all posts in the stream (if requested).
        /// Useful for breadcrumbs or complete navigation menus.
        /// </summary>
        public List<BlogPostNavigationItem> AllPosts { get; set; } = new();

        /// <summary>
        /// Gets or sets the current post position in the stream (1-based index).
        /// Example: 1 = latest post, 2 = second latest, etc.
        /// </summary>
        public int CurrentPostPosition { get; set; }

        /// <summary>
        /// Gets or sets the total count of published posts in the stream.
        /// </summary>
        public int TotalPostCount { get; set; }
    }

    /// <summary>
    /// A navigation item representing a blog post.
    /// Used in navigation lists and breadcrumbs.
    /// </summary>
    public class BlogPostNavigationItem
    {
        /// <summary>
        /// Gets or sets the blog post URL path.
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog post title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the publication date of the post.
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the position of this post in the stream (1-based, where 1 is the latest).
        /// </summary>
        public int Position { get; set; }
    }
}
