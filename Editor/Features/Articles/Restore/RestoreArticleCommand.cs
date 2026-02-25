// <copyright file="RestoreArticleCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Editor.Features.Articles.Restore
{
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to restore a previously deleted article from trash.
    /// </summary>
    public class RestoreArticleCommand : ICommand<CommandResult<Unit>>
    {
        /// <summary>
        /// Gets or sets the article number to restore.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the user ID performing the restore.
        /// </summary>
        public string UserId { get; set; }
    }
}
