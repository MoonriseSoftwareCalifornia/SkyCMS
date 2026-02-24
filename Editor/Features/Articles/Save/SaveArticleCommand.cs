// <copyright file="SaveArticleCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Save
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to save (update) an existing article.
    /// </summary>
    public sealed class SaveArticleCommand : ICommand<CommandResult<ArticleUpdateResult>>
    {
        /// <summary>
        /// Gets or sets the article number (logical identifier across versions).
        /// </summary>
        public int ArticleNumber { get; init; }

        /// <summary>
        /// Gets or sets the article title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTML content.
        /// </summary>
        [Required(AllowEmptyStrings = true, ErrorMessage = "Content is required.")]
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL path/slug.
        /// </summary>
        public string UrlPath { get; init; }

        /// <summary>
        /// Gets or sets the header JavaScript.
        /// </summary>
        public string HeadJavaScript { get; init; }

        /// <summary>
        /// Gets or sets the footer JavaScript.
        /// </summary>
        public string FooterJavaScript { get; init; }

        /// <summary>
        /// Gets or sets the banner image URL.
        /// </summary>
        public string BannerImage { get; init; }

        /// <summary>
        /// Gets or sets the article type.
        /// </summary>
        public ArticleType ArticleType { get; init; }

        /// <summary>
        /// Gets or sets the category (for blog posts).
        /// </summary>
        public string Category { get; init; }

        /// <summary>
        /// Gets or sets the introduction/summary text.
        /// </summary>
        public string Introduction { get; init; }

        /// <summary>
        /// Gets or sets the published timestamp (null for unpublished).
        /// </summary>
        public DateTimeOffset? Published { get; init; }

        /// <summary>
        /// Gets or sets the user performing the save.
        /// </summary>
        public Guid UserId { get; init; }
    }
}
