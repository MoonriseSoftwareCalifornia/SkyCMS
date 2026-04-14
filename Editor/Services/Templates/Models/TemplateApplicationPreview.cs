// <copyright file="TemplateApplicationPreview.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Templates.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Preview of template application impact showing all affected articles and potential merge issues.
    /// </summary>
    /// <remarks>
    /// Use this model to show users what will happen before actually applying template changes.
    /// Returned by <see cref="ITemplateService.PreviewTemplateApplicationAsync"/>.
    /// </remarks>
    public class TemplateApplicationPreview
    {
        /// <summary>
        /// Gets or sets the template ID being previewed.
        /// </summary>
        public Guid TemplateId { get; set; }

        /// <summary>
        /// Gets or sets the template name.
        /// </summary>
        public string TemplateName { get; set; }

        /// <summary>
        /// Gets or sets the total number of articles that would be affected by this template application.
        /// </summary>
        public int TotalAffectedArticles { get; set; }

        /// <summary>
        /// Gets or sets the list of individual articles with preview details.
        /// </summary>
        public List<ArticlePreviewItem> Articles { get; set; } = new List<ArticlePreviewItem>();

        /// <summary>
        /// Gets a value indicating whether all articles can be safely merged without warnings.
        /// </summary>
        public bool AllArticlesSafe => Articles.TrueForAll(a => a.CanMerge && string.IsNullOrEmpty(a.MergeWarning));

        /// <summary>
        /// Gets the count of articles that have merge warnings.
        /// </summary>
        public int WarningCount => Articles.Count(a => !string.IsNullOrEmpty(a.MergeWarning));
    }
}