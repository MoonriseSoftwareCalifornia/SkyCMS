// <copyright file="GetBlogPostNavigationQueryResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Blogs.Queries
{
    using System.Collections.Generic;

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
}
