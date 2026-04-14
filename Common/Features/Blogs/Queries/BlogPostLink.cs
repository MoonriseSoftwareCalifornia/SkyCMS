// <copyright file="BlogPostLink.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Blogs.Queries
{
    using System;

    /// <summary>
    /// A link to a blog post, used in navigation.
    /// </summary>
    public class BlogPostLink
    {
        /// <summary>
        /// Gets or sets the blog post title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog post URL path.
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the publication date.
        /// </summary>
        public DateTimeOffset? Published { get; set; }
    }
}
