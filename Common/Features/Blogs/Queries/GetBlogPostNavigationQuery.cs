// <copyright file="GetBlogPostNavigationQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Blogs.Queries
{
    using Cosmos.Common.Features.Shared;
    using System;

    /// <summary>
    /// Query to retrieve navigation information for blog posts within a stream.
    /// Returns previous and next posts relative to a given post,
    /// useful for building "next/previous" navigation UI.
    /// </summary>
    public class GetBlogPostNavigationQuery : IQuery<GetBlogPostNavigationQueryResult>
    {
        /// <summary>
        /// Gets or sets the blog key (stream identifier).
        /// Example: "cat-wash" or "cat_wash"
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current blog post URL path.
        /// Example: "cat-wash/shampo" or "cat_wash/shampo"
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
}
