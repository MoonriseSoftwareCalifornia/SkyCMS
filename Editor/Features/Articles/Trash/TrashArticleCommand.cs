// <copyright file="TrashArticleCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Editor.Features.Articles.Trash
{
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to permanently remove a previously deleted article and related artifacts.
    /// </summary>
    public class TrashArticleCommand : ICommand<CommandResult<Unit>>
    {
        /// <summary>
        /// Gets or sets the article number to permanently remove.
        /// </summary>
        public int ArticleNumber { get; set; }
    }
}
