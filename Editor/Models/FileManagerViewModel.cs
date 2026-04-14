// <copyright file="FileManagerViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Models
{
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Mvc.Rendering;

    /// <summary>
    /// File manager view model.
    /// </summary>
    public class FileManagerViewModel
    {
        /// <summary>
        /// Gets or sets team ID.
        /// </summary>
        public int? TeamId { get; set; }

        /// <summary>
        /// Gets or sets team folders.
        /// </summary>
        public IEnumerable<SelectListItem> TeamFolders { get; set; }
    }
}
