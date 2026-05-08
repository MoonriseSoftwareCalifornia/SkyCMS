// <copyright file="LayoutInventoryItem.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models
{
    /// <summary>
    /// Represents a layout in the inventory for editor and VS Code APIs.
    /// </summary>
    public class LayoutInventoryItem
    {
        /// <summary>
        /// Gets or sets the layout number (family identifier).
        /// </summary>
        public int LayoutNumber { get; set; }

        /// <summary>
        /// Gets or sets the layout name.
        /// </summary>
        public string LayoutName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current version number.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the default layout.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Gets or sets the last published date in ISO format.
        /// </summary>
        public string LastPublished { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the layout has ever been published.
        /// </summary>
        public bool IsPublished { get; set; }

        /// <summary>
        /// Gets or sets the last modified date in ISO format.
        /// </summary>
        public string LastModified { get; set; } = string.Empty;
    }
}
