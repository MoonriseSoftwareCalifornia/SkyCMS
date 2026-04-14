// <copyright file="LayoutFileUploadViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Layout file upload view model.
    /// </summary>
    public class LayoutFileUploadViewModel
    {
        /// <summary>
        /// Gets or sets layout ID number (once saved).
        /// </summary>
        [Display(Name = "Choose layout to replace:")]
        public Guid? Id { get; set; }

        /// <summary>
        /// Gets or sets layout name.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Layout name:")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets layout description.
        /// </summary>
        [Display(Name = "Description/Notes:")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets layer file to upload.
        /// </summary>
        [Display(Name = "Select file to upload:")]
        public IFormFile File { get; set; }
    }
}
