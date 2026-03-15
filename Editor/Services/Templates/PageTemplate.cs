// <copyright file="PageTemplate.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System.Collections.Generic;

namespace Sky.Editor.Services.Templates
{

    /// <summary>
    /// Represents metadata for a page template.
    /// </summary>
    public class PageTemplate
    {
        /// <summary>
        /// Gets or sets unique identifier/key for the template.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets display name for the template.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets description of what this template is for.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets category (e.g., "Marketing", "Blog", "E-commerce").
        /// </summary>
        public string Category { get; set; } = "General";

        /// <summary>
        /// Gets or sets thumbnail image path for preview.
        /// </summary>
        public string ThumbnailPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets path to the template file (relative to Templates folder).
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets tags for searching/filtering.
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether whether this template requires specific configuration.
        /// </summary>
        public bool RequiresConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the actual HTML content (loaded on demand).
        /// </summary>
        public string Content { get; set; }
    }
}
