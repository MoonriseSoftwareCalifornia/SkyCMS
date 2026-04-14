// <copyright file="EditImagePostViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Models
{
    /// <summary>
    /// Filerobot image post model.
    /// </summary>
    public class FileRobotImagePost
    {
        /// <summary>
        /// Gets or sets file name without extension.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets file name with extension.
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// Gets or sets file extension.
        /// </summary>
        public string Extension { get; set; }

        /// <summary>
        /// Gets or sets mime type.
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// Gets or sets base 64 image data.
        /// </summary>
        public string ImageBase64 { get; set; }

        /// <summary>
        /// Gets or sets quantity.
        /// </summary>
        public double? Quantity { get; set; } = null;

        /// <summary>
        /// Gets or sets image width.
        /// </summary>
        public string Width { get; set; }

        /// <summary>
        /// Gets or sets image height.
        /// </summary>
        public string Height { get; set; }

        /// <summary>
        /// Gets or sets folder where image should reside.
        /// </summary>
        public string Folder { get; set; }
    }
}
