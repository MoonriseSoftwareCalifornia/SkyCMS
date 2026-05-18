// <copyright file="LoginAssetBootstrapService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Editor.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Ensures login-time static assets exist in blob storage.
    /// </summary>
    public sealed class LoginAssetBootstrapService : ILoginAssetBootstrapService
    {
        private static readonly string ContainerName = "$web";

        private static readonly IReadOnlyList<LoginAsset> RequiredAssets = new[]
        {
            new LoginAsset("lib/picocss/pico.conditional.min.css", "pub/lib/picocss/pico.conditional.min.css", "text/css"),
            new LoginAsset("lib/ckeditor/ckeditor5-content.css", "pub/lib/ckeditor/ckeditor5-content.css", "text/css"),
        };

        private readonly IWebHostEnvironment webHostEnvironment;
        private readonly IDynamicConfigurationProvider dynamicConfigurationProvider;
        private readonly ILoginAssetBlobClient blobClient;
        private readonly ILogger<LoginAssetBootstrapService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginAssetBootstrapService"/> class.
        /// </summary>
        /// <param name="webHostEnvironment">Web host environment.</param>
        /// <param name="dynamicConfigurationProvider">Dynamic configuration provider.</param>
        /// <param name="blobClient">Blob client abstraction.</param>
        /// <param name="logger">Logger.</param>
        public LoginAssetBootstrapService(
            IWebHostEnvironment webHostEnvironment,
            IDynamicConfigurationProvider dynamicConfigurationProvider,
            ILoginAssetBlobClient blobClient,
            ILogger<LoginAssetBootstrapService> logger)
        {
            this.webHostEnvironment = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
            this.dynamicConfigurationProvider = dynamicConfigurationProvider ?? throw new ArgumentNullException(nameof(dynamicConfigurationProvider));
            this.blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task EnsureRequiredAssetsAsync(string website = "")
        {
            try
            {
                var storageConnection = await dynamicConfigurationProvider.GetStorageConnectionStringAsync(website);
                if (string.IsNullOrWhiteSpace(storageConnection))
                {
                    logger.LogWarning("Unable to resolve storage connection for login asset bootstrap.");
                    return;
                }

                var webRootPath = webHostEnvironment.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRootPath))
                {
                    logger.LogWarning("Web root path is not configured; skipping login asset bootstrap.");
                    return;
                }

                await blobClient.EnsureContainerExistsAsync(storageConnection, ContainerName);

                foreach (var asset in RequiredAssets)
                {
                    var sourcePath = Path.Combine(webRootPath, asset.SourcePath.Replace('/', Path.DirectorySeparatorChar));
                    if (!global::System.IO.File.Exists(sourcePath))
                    {
                        logger.LogWarning("Required login asset not found on disk: {AssetPath}", sourcePath);
                        continue;
                    }

                    if (await blobClient.BlobExistsAsync(storageConnection, ContainerName, asset.BlobName))
                    {
                        continue;
                    }

                    await using var fileStream = global::System.IO.File.OpenRead(sourcePath);
                    await blobClient.UploadAsync(storageConnection, ContainerName, asset.BlobName, fileStream, asset.ContentType);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to ensure required login assets exist in blob storage.");
            }
        }

        private sealed record LoginAsset(string SourcePath, string BlobName, string ContentType);
    }
}
