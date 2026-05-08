// <copyright file="GetLayoutInventoryQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Inventory
{
    using System.Collections.Generic;
    using Cosmos.Common.Features.Shared;
    using Sky.Editor.Models;

    /// <summary>
    /// Query for retrieving layout inventory with status and version information.
    /// </summary>
    public class GetLayoutInventoryQuery : IQuery<List<LayoutInventoryItem>>
    {
        /// <summary>
        /// Gets or sets optional filter term for searching layout names.
        /// </summary>
        public string Term { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to return only published layouts.
        /// </summary>
        public bool PublishedOnly { get; set; } = false;
    }
}
