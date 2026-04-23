// <copyright file="BlogEntryListItem.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models.Blogs
{
    using System;

    /// <summary>
    /// Lightweight projection of a blog post for list/table display.
    /// </summary>
    /// <remarks>
    /// Contains common display fields such as title, publish dates, and a short introduction.
    /// Used to render lists of posts in the editor UI.
    /// </remarks>
    public class BlogEntryListItem
    {
        /// <summary>
        /// Gets or sets the blog key this post belongs to.
        /// </summary>
        public string BlogKey { get; set; }

        /// <summary>
        /// Gets or sets the article number (per-blog sequence or identifier).
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the post title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the optional published date/time for the post.
        /// </summary>
        /// <remarks>Null indicates the post is not yet published.</remarks>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the last updated date/time for the post.
        /// </summary>
        public DateTimeOffset Updated { get; set; }

        /// <summary>
        /// Gets or sets the URL path (relative or absolute) for the post.
        /// </summary>
        public string UrlPath { get; set; }

        /// <summary>
        /// Gets or sets the short introduction or teaser text for the post.
        /// </summary>
        public string Introduction { get; set; }

        /// <summary>
        /// Gets or sets the banner image URL or path for the post.
        /// </summary>
        public string BannerImage { get; set; }
    }

    /// <summary>
    /// Preferred compatibility alias for <see cref="BlogEntryListItem"/>.
    /// </summary>
    public class BlogPostListItem : BlogEntryListItem
    {
    }
}
