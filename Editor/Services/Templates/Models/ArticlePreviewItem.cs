// <copyright file="ArticlePreviewItem.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Templates.Models
{
    using System;

    /// <summary>
    /// Preview details for a single article showing merge compatibility and warnings.
    /// </summary>
    public class ArticlePreviewItem
    {
        /// <summary>
        /// Gets or sets the article number (unique identifier across versions).
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the article title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the article URL path.
        /// </summary>
        public string UrlPath { get; set; }

        /// <summary>
        /// Gets or sets the current version number of the article.
        /// </summary>
        public int CurrentVersionNumber { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the article has a published version.
        /// </summary>
        /// <remarks>
        /// If true, the published version will be preserved when template is applied.
        /// If false, this is a draft-only article.
        /// </remarks>
        public bool HasPublishedVersion { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the article was last published.
        /// </summary>
        public DateTimeOffset? LastPublished { get; set; }

        /// <summary>
        /// Gets or sets the number of editable regions (data-ccms-ceid markers) found in the current article version.
        /// </summary>
        public int EditableRegionsCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the template can be successfully merged with this article.
        /// </summary>
        /// <remarks>
        /// Set to false if:
        /// - Template has fewer editable regions than the article (content would be lost)
        /// - Critical editable region IDs don't match
        /// - Article content is corrupted or unparseable.
        /// </remarks>
        public bool CanMerge { get; set; }

        /// <summary>
        /// Gets or sets a warning message if there are potential merge issues.
        /// </summary>
        /// <remarks>
        /// Example warnings:
        /// - "Template is missing 2 editable regions present in the article"
        /// - "Article has 3 regions that won't be preserved in the new template"
        /// - "Region IDs don't match - manual review recommended".
        /// </remarks>
        public string MergeWarning { get; set; }
    }
}