// <copyright file="GetBlogPostQuery.cs" company="Moonrise Software, LLC">
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
    /// Query to retrieve a single blog post by its URL path.
    /// Used when displaying an individual blog post on the website.
    /// </summary>
    public class GetBlogPostQuery : IQuery<GetBlogPostQueryResult>
    {
        /// <summary>
        /// Gets or sets the blog post URL path.
        /// Example: "cat-wash/shampo" or "cat_wash/shampo"
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the language code for filtering (optional).
        /// </summary>
        public string Language { get; set; } = "en-US";

        /// <summary>
        /// Gets or sets a value indicating whether to include navigation (prev/next posts).
        /// </summary>
        public bool IncludeNavigation { get; set; } = true;

        /// <summary>
        /// Gets or sets the cache duration for the result.
        /// </summary>
        public TimeSpan? CacheDuration { get; set; }
    }
}
