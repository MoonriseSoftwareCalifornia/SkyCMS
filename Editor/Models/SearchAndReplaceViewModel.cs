// <copyright file="SearchAndReplaceViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Models
{
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Search and replace mode.
    /// </summary>
    public class SearchAndReplaceViewModel
    {
        /// <summary>
        /// Gets or sets a value indicating whether include content in search and replace?.
        /// </summary>
        [Display(Name = "Include content in search and replace?")]
        public bool IncludeContent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include the title in search and replace.
        /// </summary>
        [Display(Name = "Include title in search and replace?")]
        public bool IncludeTitle { get; set; }

        /// <summary>
        /// Gets or sets limit to article.
        /// </summary>
        [Display(Name = "Limit to article:")]
        public int? ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether limit to only published articles?.
        /// </summary>
        [Display(Name = "Limit to published articles?")]
        public bool LimitToPublished { get; set; } = true;

        /// <summary>
        /// Gets or sets find:.
        /// </summary>
        [Display(Name = "Find:")]
        public string FindValue { get; set; }

        /// <summary>
        /// Gets or sets replace with:.
        /// </summary>
        [Display(Name = "Replace:")]
        public string ReplaceValue { get; set; }
    }
}
