// <copyright file="DesignerDataViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Cosmos.Cms.Data;

    /// <summary>
    /// GrapesJs designer post data view model.
    /// </summary>
    public class DesignerDataViewModel
    {
        /// <summary>
        /// Gets or sets the ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets title. 
        /// </summary>
        [MaxLength(80)]
        [StringLength(80)]
        [ArticleTitleValidation]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the HTML content.
        /// </summary>
        public string HtmlContent { get; set; }

        /// <summary>
        /// Gets or sets the CSS content.
        /// </summary>
        public string CssContent { get; set; }
    }
}
