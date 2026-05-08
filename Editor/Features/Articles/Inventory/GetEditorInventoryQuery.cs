// <copyright file="GetEditorInventoryQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Inventory
{
    using System.Collections.Generic;
    using Cosmos.Common.Features.Shared;
    using Sky.Editor.Models;

    /// <summary>
    /// Query to retrieve editor article inventory rows with optional filtering.
    /// </summary>
    public class GetEditorInventoryQuery : IQuery<List<EditorInventoryItem>>
    {
        /// <summary>
        /// Gets or sets optional search term for title/path/blog fields.
        /// </summary>
        public string Term { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether only published articles should be returned.
        /// </summary>
        public bool PublishedOnly { get; set; } = true;

        /// <summary>
        /// Gets or sets optional article type filter. Zero means all article types.
        /// </summary>
        public int ArticleType { get; set; }
    }
}
