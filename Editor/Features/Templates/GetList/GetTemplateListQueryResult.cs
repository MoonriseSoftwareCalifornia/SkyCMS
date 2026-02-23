// <copyright file="GetTemplateListQueryResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.GetList
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result for template list query.
    /// </summary>
    public class GetTemplateListQueryResult
    {
        /// <summary>
        /// Gets or sets the list of template view models.
        /// </summary>
        public List<TemplateListItemViewModel> Templates { get; set; } = new List<TemplateListItemViewModel>();

        /// <summary>
        /// Gets or sets the total count of templates (before pagination).
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// View model for a single template in the list.
    /// </summary>
    public class TemplateListItemViewModel
    {
        /// <summary>
        /// Gets or sets the template ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the template title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the template description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the layout name.
        /// </summary>
        public string LayoutName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the template uses the HTML editor.
        /// </summary>
        public bool UsesHtmlEditor { get; set; }
    }
}
