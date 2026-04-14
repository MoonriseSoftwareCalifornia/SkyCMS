// <copyright file="TemplateBatchApplicationResult.cs" company="Moonrise Software, LLC">
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
    /// Batch result for applying a template to multiple articles.
    /// </summary>
    public class TemplateBatchApplicationResult
    {
        /// <summary>
        /// Gets or sets the number of articles successfully updated.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the number of articles that failed to update.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the detailed results for each article processed.
        /// </summary>
        public List<TemplateApplicationResult> Results { get; set; } = new List<TemplateApplicationResult>();

        /// <summary>
        /// Gets or sets the total time taken to process the batch operation.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets a value indicating whether all articles were successfully processed.
        /// </summary>
        public bool AllSucceeded => FailureCount == 0;

        /// <summary>
        /// Gets the total number of articles processed (success + failure).
        /// </summary>
        public int TotalProcessed => SuccessCount + FailureCount;
    }
}