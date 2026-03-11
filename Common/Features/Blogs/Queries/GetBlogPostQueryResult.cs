// <copyright file="GetBlogPostQueryResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Blogs.Queries
{
    using System;

    /// <summary>
    /// Result of retrieving a blog post query.
    /// Contains full blog post content and optional navigation information.
    /// </summary>
    public class GetBlogPostQueryResult
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
        /// Gets or sets the blog post content (HTML).
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog post introduction/excerpt.
        /// </summary>
        public string Introduction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog post URL path.
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog key this post belongs to.
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog stream title (parent stream).
        /// </summary>
        public string BlogStreamTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog stream URL path.
        /// </summary>
        public string BlogStreamUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the banner/hero image URL.
        /// </summary>
        public string BannerImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the publication date of the blog post.
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the date the blog post was last updated.
        /// </summary>
        public DateTimeOffset Updated { get; set; }

        /// <summary>
        /// Gets or sets the author name/ID (if available).
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the navigation information (previous and next posts).
        /// </summary>
        public BlogPostNavigation? Navigation { get; set; }
    }

    /// <summary>
    /// Navigation information for a blog post.
    /// Contains links to the previous and next posts in the stream.
    /// </summary>
    public class BlogPostNavigation
    {
        /// <summary>
        /// Gets or sets the previous post (if available).
        /// </summary>
        public BlogPostLink? PreviousPost { get; set; }

        /// <summary>
        /// Gets or sets the next post (if available).
        /// </summary>
        public BlogPostLink? NextPost { get; set; }
    }
}
