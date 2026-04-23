// <copyright file="EditorInventoryRowType.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models
{
    /// <summary>
    /// Inventory row type constants for the unified editor inventory.
    /// </summary>
    public static class EditorInventoryRowType
    {
        /// <summary>
        /// Standard page row.
        /// </summary>
        public const string Page = "page";

        /// <summary>
        /// Blog stream parent row.
        /// </summary>
        public const string Blog = "blog";

        /// <summary>
        /// Blog post child row.
        /// </summary>
        public const string BlogPost = "blogPost";
    }
}