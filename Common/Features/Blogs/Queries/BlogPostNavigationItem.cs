// <copyright file="BlogPostNavigationItem.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Blogs.Queries
{
    using System;

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
