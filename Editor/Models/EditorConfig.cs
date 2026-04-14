// <copyright file="EditorConfig.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models
{
    using System.ComponentModel.DataAnnotations;
    using Sky.Editor.Services.EditorSettings;

    /// <summary>
    /// Editor instance configuration saved in the database..
    /// </summary>
    public class EditorConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EditorConfig"/> class.
        /// </summary>
        public EditorConfig()
        {
            AllowedFileTypes = ".jpg,.jpeg,.png,.gif,.bmp,.svg,.webp,.mp4,.mp3,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.zip";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorConfig"/> class.
        /// </summary>
        /// <param name="serializedJson">Serialized json string.</param>
        public EditorConfig(string serializedJson)
        {
            if (string.IsNullOrEmpty(serializedJson))
            {
                AllowedFileTypes = ".jpg,.jpeg,.png,.gif,.bmp,.svg,.webp,.mp4,.mp3,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.zip";
                return;
            }

            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<EditorConfig>(serializedJson);
            if (config != null)
            {
                this.AllowSetup = config.AllowSetup;
                this.BlobPublicUrl = config.BlobPublicUrl;
                this.CosmosRequiresAuthentication = config.CosmosRequiresAuthentication;
                this.IsMultiTenantEditor = config.IsMultiTenantEditor;
                this.MicrosoftAppId = config.MicrosoftAppId;
                this.PublisherUrl = config.PublisherUrl;
                this.StaticWebPages = config.StaticWebPages;
                this.AllowedFileTypes = config.AllowedFileTypes;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorConfig"/> class.
        /// </summary>
        /// <param name="settings">Editor settings.</param>
        public EditorConfig(IEditorSettings settings)
        {
            this.AllowSetup = settings.AllowSetup;
            this.BlobPublicUrl = settings.BlobPublicUrl;
            this.CosmosRequiresAuthentication = settings.CosmosRequiresAuthentication;
            this.IsMultiTenantEditor = settings.IsMultiTenantEditor;
            this.MicrosoftAppId = settings.MicrosoftAppId;
            this.PublisherUrl = settings.PublisherUrl;
            this.StaticWebPages = settings.StaticWebPages;
            this.AllowedFileTypes = settings.AllowedFileTypes;
        }

        /// <summary>
        /// Gets or sets allowed file types for the file uploader.
        /// </summary>
        public string AllowedFileTypes
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether it is possible to run setup routines for this editor instance.
        /// </summary>
        [Display(Name = "Allow setup routines")]
        public bool AllowSetup { get; set; } = false;

        /// <summary>
        /// Gets or sets URL of the blob public website (can be same as publisher URL).
        /// </summary>
        /// <remarks>
        /// Publisher URL can be the same as blob public url, but this requires
        /// request rules to be setup that route requests to blob storage. See documentation
        /// for more information.
        /// </remarks>
        [Display(Name = "Static assets URL")]
        [Required(AllowEmptyStrings = false)]
        public string BlobPublicUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether publisher requires authentication.
        /// </summary>
        [Display(Name = "Website requires authentication")]
        public bool CosmosRequiresAuthentication { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the editor is a multi-tenant editor.
        /// </summary>
        public bool IsMultiTenantEditor { get; set; }

        /// <summary>
        /// Gets or sets a value for the Azure Registered App ID.
        /// </summary>
        public string MicrosoftAppId { get; set; }

        /// <summary>
        ///     Gets or sets uRI of the publisher website.
        /// </summary>
        [Display(Name = "Website URL")]
        [Required(AllowEmptyStrings = false)]
        [Url]
        public string PublisherUrl { get; set; } = string.Empty;

        /// <summary>
        ///    Gets or sets a value indicating whether publish to static website.
        /// </summary>
        [Display(Name = "Static mode website")]
        public bool StaticWebPages { get; set; } = false;
    }
}
