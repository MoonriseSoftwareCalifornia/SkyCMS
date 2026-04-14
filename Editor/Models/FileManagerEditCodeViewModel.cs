// <copyright file="FileManagerEditCodeViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Models
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using Sky.Cms.Models.Interfaces;

    /// <summary>
    /// File manager edit code view model.
    /// </summary>
    public class FileManagerEditCodeViewModel : ICodeEditorViewModel
    {
        /// <summary>
        /// Gets or sets paths.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets content.
        /// </summary>
        [DataType(DataType.Html)]
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets editor mode.
        /// </summary>
        public EditorMode EditorMode { get; set; }

        /// <summary>
        /// Gets or sets file ID.
        /// </summary>
        [Key]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets editing field.
        /// </summary>
        public string EditingField { get; set; }

        /// <summary>
        /// Gets or sets editor title.
        /// </summary>
        public string EditorTitle { get; set; }

        /// <summary>
        /// Gets or sets editing fields.
        /// </summary>
        public IEnumerable<EditorField> EditorFields { get; set; }

        /// <summary>
        /// Gets or sets custom buttons.
        /// </summary>
        public IEnumerable<string> CustomButtons { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether code is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets editor type.
        /// </summary>
        public string EditorType { get; set; } = nameof(FileManagerEditCodeViewModel);
    }
}
