// <copyright file="GetBlogStreamQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Blogs.Queries
{
    using System;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Query to retrieve a blog stream by its key or URL path.
    /// Returns the stream metadata along with the latest blog post preview.
    /// </summary>
    public class GetBlogStreamQuery : IQuery<GetBlogStreamQueryResult>
    {
        /// <summary>
        /// Gets or sets the blog key (slugified title) or URL path.
        /// Example: "cat_wash" or "cat-wash"
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the language code for filtering (optional).
        /// </summary>
        public string Language { get; set; } = "en-US";

        /// <summary>
        /// Gets or sets the cache duration for the result.
        /// </summary>
        public TimeSpan? CacheDuration { get; set; }
    }
}
