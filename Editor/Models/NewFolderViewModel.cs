// <copyright file="NewFolderViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Sky.Cms.Models
{
    /// <summary>
    /// New folder view model.
    /// </summary>
    public class NewFolderViewModel
    {
        /// <summary>
        /// Gets or sets the parent folder where new folder is created as a child.
        /// </summary>
        public string ParentFolder { get; set; }

        /// <summary>
        /// Gets or sets new folder name.
        /// </summary>
        [Required(AllowEmptyStrings = false, ErrorMessage = "Folder name is required.")]
        public string FolderName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether directory only mode for file browser.
        /// </summary>
        public bool DirectoryOnly { get; set; }
    }
}
