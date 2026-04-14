// <copyright file="TemplateApplicationResult.cs" company="Moonrise Software, LLC">
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
    /// Result of applying a template to a single article.
    /// </summary>
    public class TemplateApplicationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the template was successfully applied.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the article number that was updated.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the new version number created by the template application.
        /// </summary>
        /// <remarks>
        /// If the article was previously at version 3, this will be 4.
        /// This new version is created as a DRAFT (not published).
        /// </remarks>
        public int NewVersionNumber { get; set; }

        /// <summary>
        /// Gets or sets the unique ID of the new article version created.
        /// </summary>
        public Guid NewVersionId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the new version is a draft (not published).
        /// </summary>
        /// <remarks>
        /// Will always be true for template application - user must explicitly publish after review.
        /// </remarks>
        public bool IsDraft { get; set; }

        /// <summary>
        /// Gets or sets the error message if the operation failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets a list of non-fatal warnings generated during the merge.
        /// </summary>
        /// <remarks>
        /// Examples:
        /// - "Editable region 'sidebar' from original content could not be preserved"
        /// - "Template has new regions that were not in the original".
        /// </remarks>
        public List<string> Warnings { get; set; } = new List<string>();
    }
}