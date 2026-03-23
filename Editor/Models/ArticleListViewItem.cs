// <copyright file="ArticleListViewItem.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models
{
    using System;

    /// <summary>
    /// Represents a single article in the list view.
    /// </summary>
    public class ArticleListViewItem
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
        /// Gets or sets the article title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the article URL path.
        /// </summary>
        public string UrlPath { get; set; }

        /// <summary>
        /// Gets or sets the article published date.
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the article updated date.
        /// </summary>
        public DateTimeOffset Updated { get; set; }

        /// <summary>
        /// Gets or sets the count of versions in the group that contain page-editor markers.
        /// </summary>
        public int? EditableRegionCount { get; set; }

        /// <summary>
        /// Gets or sets if an article has editable regions.
        /// </summary>
        public bool? HasEditableRegions { get; set; }
    }
}
