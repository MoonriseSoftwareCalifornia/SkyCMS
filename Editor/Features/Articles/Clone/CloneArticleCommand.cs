// <copyright file="CloneArticleCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Clone
{
    using System;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;

    /// <summary>
    /// Command to clone an existing article with a new title.
    /// </summary>
    /// <remarks>
    /// Cloning differs from creating a new article because it copies ALL properties
    /// from the source article, including content, scripts, and configuration.
    /// This is used when duplicating pages to maintain consistent structure.
    /// </remarks>
    public sealed class CloneArticleCommand : ICommand<CommandResult<ArticleViewModel>>
    {
        /// <summary>
        /// Gets the ID of the source article to clone from.
        /// </summary>
        public Guid SourceArticleId { get; init; }

        /// <summary>
        /// Gets the title for the cloned article.
        /// </summary>
        public string NewTitle { get; init; } = string.Empty;

        /// <summary>
        /// Gets the user ID performing the clone operation.
        /// </summary>
        public Guid UserId { get; init; }

        /// <summary>
        /// Gets the optional publish date/time for the cloned article.
        /// </summary>
        /// <remarks>
        /// If null, the cloned article will be unpublished.
        /// </remarks>
        public DateTimeOffset? Published { get; init; }
    }
}