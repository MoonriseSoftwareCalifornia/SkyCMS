// <copyright file="GetEditableArticleForEditResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.GetEditable
{
    using Cosmos.Common.Data;

    /// <summary>
    /// Result payload for editable article resolution.
    /// </summary>
    public class GetEditableArticleForEditResult
    {
        /// <summary>
        /// Gets or sets the editable article entity.
        /// </summary>
        public Article Article { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a new draft version was created.
        /// </summary>
        public bool CreatedNewDraft { get; set; }
    }
}
