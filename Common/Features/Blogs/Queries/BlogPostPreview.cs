// <copyright file="GetBlogStreamQueryResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Blogs.Queries
{
    using System;

    /// <summary>
    /// A preview/summary of a blog post, typically used in lists or stream views.
    /// </summary>
    public class BlogPostPreview
    {
        /// <summary>
        /// Gets or sets the blog post ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the blog post title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog post URL path (e.g., "cat-wash/shampo").
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the publication date of the blog post.
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the date the blog post was last updated.
        /// </summary>
        public DateTimeOffset Updated { get; set; }

        /// <summary>
        /// Gets or sets a brief excerpt/introduction from the blog post.
        /// </summary>
        public string Excerpt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the author name (if available).
        /// </summary>
        public string Author { get; set; } = string.Empty;
    }
}
