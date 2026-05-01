// <copyright file="IEditorSettings.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.EditorSettings
{
    using System;
    using System.Threading.Tasks;
    using Sky.Editor.Models;

    /// <summary>
    /// Interface for editor settings.
    /// </summary>
    public interface IEditorSettings
    {
        /// <summary>
        ///  Gets a value indicating the allowed file types.
        /// </summary>
        string AllowedFileTypes { get; }

        /// <summary>
        /// Gets a value indicating whether the setup is allowed.
        /// </summary>
        bool AllowSetup { get; }

        /// <summary>
        /// Gets the blob storage (static web) URL.
        /// </summary>
        string BlobPublicUrl { get; }

        /// <summary>
        /// Gets the backup storage connection string.
        /// </summary>
        string BackupStorageConnectionString { get; }

        /// <summary>
        /// Gets a value indicating whether the publisher requires authentication.
        /// </summary>
        bool CosmosRequiresAuthentication { get; }

        /// <summary>
        /// Gets the Microsoft App ID value.
        /// </summary>
        string MicrosoftAppId { get; }

        /// <summary>
        /// Gets the Publisher or website URL.
        /// </summary>
        string PublisherUrl { get; }

        /// <summary>
        /// Gets a value indicating whether this is a multi-tenant editor.
        /// </summary>
        bool IsMultiTenantEditor { get; }

        /// <summary>
        /// Gets a value indicating whether the website uses static web page mode.
        /// </summary>
        bool StaticWebPages { get; }

        /// <summary>
        /// Gets a value indicating whether the modern elFinder-based file explorer is enabled.
        /// When false the legacy file explorer is shown instead.
        /// </summary>
        bool UseModernFileExplorer { get; }

        /// <summary>
        /// Gets the maximum degree of parallelism for static page generation.
        /// If null, the system will auto-detect based on storage provider.
        /// </summary>
        int? StaticPageParallelism { get; }

        /// <summary>
        /// Gets the blob storage (static web) URL.
        /// </summary>
        /// <returns>Uri.</returns>
        Uri GetBlobAbsoluteUrl();

        /// <summary>
        /// Gets the editor configuration settings asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains the editor configuration.</returns>
        Task<EditorConfig> GetEditorConfigAsync();
    }
}
