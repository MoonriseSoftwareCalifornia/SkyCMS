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
    /// Result of retrieving a blog stream query.
    /// Contains stream metadata and a preview of the latest blog post.
    /// </summary>
    public class GetBlogStreamQueryResult
    {
        /// <summary>
        /// Gets or sets the blog stream ID.
        /// </summary>
        public Guid StreamId { get; set; }

        /// <summary>
        /// Gets or sets the blog stream title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog stream description/introduction.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog stream hero/banner image URL.
        /// </summary>
        public string HeroImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog stream URL path (e.g., "cat-wash").
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog key used to identify this stream.
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the publication date of the stream (when it was first published).
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the last update date of the stream.
        /// </summary>
        public DateTimeOffset Updated { get; set; }

        /// <summary>
        /// Gets or sets the latest blog post in the stream.
        /// This is a preview/summary of the most recent post.
        /// </summary>
        public BlogPostPreview? LatestPost { get; set; }

        /// <summary>
        /// Gets or sets the total count of published posts in this stream.
        /// </summary>
        public int PublishedPostCount { get; set; }
    }
}
