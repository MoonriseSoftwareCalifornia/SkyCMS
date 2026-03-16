// <copyright file="TocService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.TableOfContents
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Common.Features.Articles.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Sky.Editor.Services.EditorSettings;

    /// <summary>
    /// Service for generating and managing Table of Contents (TOC) JSON files.
    /// </summary>
    /// <remarks>
    /// This service creates denormalized TOC JSON files in blob storage for fast client-side access.
    /// Thread-safe via semaphore to prevent DbContext concurrency issues in multi-tenant environments.
    /// </remarks>
    public sealed class TocService : ITocService
    {
        private readonly IStorageContext storage;
        private readonly IEditorSettings settings;
        private readonly IArticleCatalogQueryService articleCatalogQueryService;
        private readonly ILogger<TocService> logger;
        private readonly SemaphoreSlim writeTocSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="TocService"/> class.
        /// </summary>
        /// <param name="storage">Blob storage context for writing TOC files.</param>
        /// <param name="settings">Editor settings to check if static web pages are enabled.</param>
        /// <param name="articleCatalogQueryService">Service for retrieving article catalog data.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        public TocService(
            IStorageContext storage,
            IEditorSettings settings,
            IArticleCatalogQueryService articleCatalogQueryService,
            ILogger<TocService> logger)
        {
            this.storage = storage;
            this.settings = settings;
            this.articleCatalogQueryService = articleCatalogQueryService;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task WriteTocAsync(string prefix = "/")
        {
            if (!settings.StaticWebPages)
            {
                logger.LogDebug("Static web pages disabled - skipping TOC generation for prefix: {Prefix}", prefix);
                return;
            }

            // ✅ Thread-safe: Ensure only one operation writes TOC at a time
            // This prevents "DbContext concurrent operation" exceptions when multiple tenants publish simultaneously
            await writeTocSemaphore.WaitAsync();
            try
            {
                var toc = await articleCatalogQueryService.GetTableOfContentsAsync(prefix, 0, 500, false);

                if (toc == null)
                {
                    logger.LogWarning("TOC generation returned null for prefix: {Prefix}", prefix);
                    return;
                }

                var json = JsonConvert.SerializeObject(toc);
                var target = string.IsNullOrEmpty(prefix) || prefix == "/"
                    ? "/toc.json"
                    : $"/pub/---toc/{prefix}/toc.json";

                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
                await storage.AppendBlob(ms, new FileUploadMetaData
                {
                    ChunkIndex = 0,
                    ContentType = "application/json",
                    FileName = Path.GetFileName(target),
                    RelativePath = target,
                    TotalChunks = 1,
                    TotalFileSize = ms.Length,
                    UploadUid = Guid.NewGuid().ToString()
                });

                logger.LogDebug("Successfully wrote TOC to {Target} for prefix {Prefix}", target, prefix);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write TOC for prefix: {Prefix}", prefix);
                throw;
            }
            finally
            {
                writeTocSemaphore.Release();
            }
        }
    }
}
