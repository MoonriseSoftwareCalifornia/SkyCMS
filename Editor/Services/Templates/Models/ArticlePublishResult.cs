// <copyright file="ArticlePublishResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Templates.Models
{
    /// <summary>
    /// Result of publishing a single article.
    /// </summary>
    public class ArticlePublishResult
    {
        /// <summary>
        /// Gets or sets the article number.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the article title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the article was successfully published.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the article was skipped (no draft to publish).
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>
        /// Gets or sets the version number that was published.
        /// </summary>
        public int? PublishedVersionNumber { get; set; }

        /// <summary>
        /// Gets or sets the error message if publishing failed.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}