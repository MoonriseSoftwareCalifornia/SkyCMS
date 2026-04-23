// <copyright file="EditorInventoryItem.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents a row in the unified editor inventory.
    /// </summary>
    public class EditorInventoryItem
    {
        /// <summary>
        /// Gets or sets the article number.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the article type.
        /// </summary>
        public int? ArticleType { get; set; }

        /// <summary>
        /// Gets or sets the row type for the inventory UI.
        /// </summary>
        public string RowType { get; set; } = "page";

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stored article URL path.
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the preview URL path used by the inventory UI.
        /// </summary>
        public string PreviewUrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog key when the row belongs to a blog.
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this is the home page.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Gets or sets the last published date in ISO format.
        /// </summary>
        public string LastPublished { get; set; }

        /// <summary>
        /// Gets or sets the updated date in ISO format.
        /// </summary>
        public string Updated { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the item supports the visual editor.
        /// </summary>
        public bool HtmlEditorEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the item supports the visual editor.
        /// Compatibility alias used by existing client code.
        /// </summary>
        public bool UsesHtmlEditor { get; set; }

        /// <summary>
        /// Gets or sets the number of child blog posts.
        /// </summary>
        public int ChildCount { get; set; }

        /// <summary>
        /// Gets or sets child rows for tree rendering.
        /// </summary>
        [JsonPropertyName("_children")]
        public List<EditorInventoryItem> Children { get; set; } = new();
    }
}