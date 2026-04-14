// <copyright file="SaveArticleCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Save
{
    using System;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to save (update) an existing article.
    /// </summary>
    public sealed class SaveArticleCommand : ICommand<CommandResult<ArticleUpdateResult>>
    {
        /// <summary>
        /// Gets the article number (logical identifier across versions).
        /// </summary>
        public int ArticleNumber { get; init; }

        /// <summary>
        /// Gets the article title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the HTML content.
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Gets the URL path/slug.
        /// </summary>
        public string UrlPath { get; init; }

        /// <summary>
        /// Gets the header JavaScript.
        /// </summary>
        public string HeadJavaScript { get; init; }

        /// <summary>
        /// Gets the footer JavaScript.
        /// </summary>
        public string FooterJavaScript { get; init; }

        /// <summary>
        /// Gets the banner image URL.
        /// </summary>
        public string BannerImage { get; init; }

        /// <summary>
        /// Gets the article type.
        /// </summary>
        public ArticleType ArticleType { get; init; }

        /// <summary>
        /// Gets the category (for blog posts).
        /// </summary>
        public string Category { get; init; }

        /// <summary>
        /// Gets the introduction/summary text.
        /// </summary>
        public string Introduction { get; init; }

        /// <summary>
        /// Gets the published timestamp (null for unpublished).
        /// </summary>
        public DateTimeOffset? Published { get; init; }

        /// <summary>
        /// Gets the user performing the save.
        /// </summary>
        public Guid UserId { get; init; }
    }
}
