// <copyright file="ConfigureIndexViewModel.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models
{
    using System.ComponentModel.DataAnnotations;
    using Cosmos.Cms.Common.Services.Configurations;

    /// <summary>
    /// Configuration view model for setup wizard.
    /// </summary>
    public class ConfigureIndexViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureIndexViewModel"/> class.
        /// </summary>
        public ConfigureIndexViewModel()
        {
            SiteSettings = new SiteSettings();
        }

        /// <summary>
        /// Gets or sets site-wide settings.
        /// </summary>
        public SiteSettings SiteSettings { get; set; }

        /// <summary>
        /// Gets or sets microsoft application ID.
        /// </summary>
        public string MicrosoftAppId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets default Microsoft Client ID.
        /// </summary>
        [Display(Name = "Default Microsoft Client ID")]
        public string DefaultMicrosoftClientId { get; set; }

        /// <summary>
        /// Gets or sets default Microsoft Secret.
        /// </summary>
        [Display(Name = "Default Microsoft Secret")]
        public string DefaultMicrosoftSecret { get; set; }

        /// <summary>
        /// Gets or sets import JSON.
        /// </summary>
        [Display(Name = "Import JSON")]
        public string ImportJson { get; set; }

        /// <summary>
        /// Gets or sets AWS S3 connections JSON.
        /// </summary>
        public string AwsS3ConnectionsJson { get; set; }

        /// <summary>
        /// Gets or sets Azure Blob connections JSON.
        /// </summary>
        public string AzureBlobConnectionsJson { get; set; }

        /// <summary>
        /// Gets or sets blob connections JSON.
        /// </summary>
        public string BlobConnectionsJson { get; set; }

        /// <summary>
        /// Gets or sets Google Blob connections JSON.
        /// </summary>
        public string GoogleBlobConnectionsJson { get; set; }

        /// <summary>
        /// Gets or sets Redis connections JSON.
        /// </summary>
        public string RedisConnectionsJson { get; set; }

        /// <summary>
        /// Gets or sets SQL connections JSON.
        /// </summary>
        public string SqlConnectionsJson { get; set; }

        /// <summary>
        /// Gets or sets editor URLs JSON.
        /// </summary>
        public string EditorUrlsJson { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether test was successful.
        /// </summary>
        public bool TestSuccess { get; set; }

        /// <summary>
        /// Gets a value indicating whether secrets can be saved.
        /// </summary>
        public bool CanSaveSecrets { get; private set; }
    }
}
