// <copyright file="GetEditableArticleForEditCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.GetEditable
{
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to resolve the editable article version for editing operations.
    /// </summary>
    public class GetEditableArticleForEditCommand : ICommand<CommandResult<GetEditableArticleForEditResult>>
    {
        /// <summary>
        /// Gets or sets the stable article number.
        /// </summary>
        public int ArticleNumber { get; set; }
    }
}
