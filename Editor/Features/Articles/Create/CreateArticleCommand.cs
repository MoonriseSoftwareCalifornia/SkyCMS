// <copyright file="CreateArticleCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Create
{
    using System;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Models;
    using Sky.Editor.Features.Shared;

    /// <summary>
    /// Command to create a new article.
    /// </summary>
    public sealed class CreateArticleCommand : ICommand<CommandResult<ArticleViewModel>>
    {
        /// <summary>
        /// Gets or sets the article title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the user ID of the article creator.
        /// </summary>
        public Guid UserId { get; init; }

        /// <summary>
        /// Gets the template ID to use for the new article.
        /// </summary>
        public Guid? TemplateId { get; init; }

        /// <summary>
        /// Gets the blog key where the article will be created.
        /// </summary>
        public string BlogKey { get; init; } = "default";

        /// <summary>
        /// Gets the type of the article.
        /// </summary>
        public ArticleType ArticleType { get; init; } = ArticleType.General;

        /// <summary>
        /// Gets the optional category for the article.
        /// </summary>
        public string? Category { get; init; }

        /// <summary>
        /// Gets the optional introduction/description for the article.
        /// </summary>
        public string? Introduction { get; init; }

        /// <summary>
        /// Gets the optional banner/hero image URL.
        /// </summary>
        public string? BannerImage { get; init; }

        /// <summary>
        /// Gets the optional content override (takes precedence over template content).
        /// </summary>
        public string? ContentOverride { get; init; }

        /// <summary>
        /// Gets the optional publish date/time (overrides auto-publish for first article).
        /// </summary>
        public DateTimeOffset? Published { get; init; }

        /// <summary>
        /// Gets the optional status code (overrides default Active status).
        /// </summary>
        public StatusCodeEnum? StatusCode { get; init; }

        /// <summary>
        /// Gets the optional URL path override (for special cases like "root" for home page).
        /// </summary>
        public string? UrlPathOverride { get; init; }

        /// <summary>
        /// Gets the optional head JavaScript/CSS content.
        /// </summary>
        public string? HeadJavaScript { get; init; }

        /// <summary>
        /// Gets the optional footer JavaScript content.
        /// </summary>
        public string? FooterJavaScript { get; init; }
    }
}
