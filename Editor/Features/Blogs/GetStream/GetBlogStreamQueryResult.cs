// <copyright file="GetBlogStreamQueryResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.GetStream
{
    using System;
    using Cosmos.Common.Data;

    /// <summary>
    /// Result data transfer object for blog stream retrieval queries.
    /// </summary>
    public class GetBlogStreamQueryResult
    {
        /// <summary>
        /// Gets or sets the blog stream article.
        /// </summary>
        public Article Article { get; set; }

        /// <summary>
        /// Gets or sets the blog stream title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog key (URL-safe identifier).
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hero/banner image URL.
        /// </summary>
        public string HeroImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the published date.
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the URL path.
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;
    }
}
