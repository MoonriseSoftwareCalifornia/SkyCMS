// <copyright file="TemplateBatchPublishResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Templates.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of publishing draft versions created by template application.
    /// </summary>
    public class TemplateBatchPublishResult
    {
        /// <summary>
        /// Gets or sets the number of articles successfully published.
        /// </summary>
        public int PublishedCount { get; set; }

        /// <summary>
        /// Gets or sets the number of articles that failed to publish.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the number of articles skipped (no draft version to publish).
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// Gets or sets detailed results per article.
        /// </summary>
        public List<ArticlePublishResult> Results { get; set; } = new List<ArticlePublishResult>();

        /// <summary>
        /// Gets or sets the total time taken to publish all articles.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets a value indicating whether all articles were successfully published.
        /// </summary>
        public bool AllSucceeded => FailureCount == 0;

        /// <summary>
        /// Gets the total number of articles processed.
        /// </summary>
        public int TotalProcessed => PublishedCount + FailureCount + SkippedCount;
    }
}