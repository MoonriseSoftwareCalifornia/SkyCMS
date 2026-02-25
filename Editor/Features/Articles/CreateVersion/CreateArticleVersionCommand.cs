// <copyright file="CreateArticleVersionCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Editor.Features.Articles.CreateVersion
{
    using System;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Sky.Cms.Models;

    /// <summary>
    /// Command to create a new version of an existing article.
    /// </summary>
    public class CreateArticleVersionCommand : ICommand<CommandResult<CreateArticleVersionCommandResult>>
    {
        /// <summary>
        /// Gets or sets the article number to create a version for.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the optional source version ID. If provided, new version is based on this specific version.
        /// If null, new version is based on the latest version.
        /// </summary>
        public Guid? SourceVersionId { get; set; }
    }

    /// <summary>
    /// Result containing the newly created article version.
    /// </summary>
    public class CreateArticleVersionCommandResult
    {
        /// <summary>
        /// Gets or sets the newly created article view model.
        /// </summary>
        public ArticleViewModel Article { get; set; }
    }
}
