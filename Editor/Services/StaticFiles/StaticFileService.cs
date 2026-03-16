// <copyright file="StaticFileService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.StaticFiles
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Layouts.Queries;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Microsoft.Extensions.Logging;
    using Sky.Cms.Services;
    using Sky.Editor.Services.EditorSettings;

    /// <summary>
    /// Service for generating and managing static HTML files in blob storage.
    /// </summary>
    public class StaticFileService : IStaticFileService
    {
        private readonly IStorageContext storage;
        private readonly IEditorSettings settings;
        private readonly IViewRenderService viewRenderService;
        private readonly IMediator mediator;
        private readonly ILogger<StaticFileService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticFileService"/> class.
        /// </summary>
        /// <param name="storage">The storage context.</param>
        /// <param name="settings">The editor settings.</param>
        /// <param name="viewRenderService">View rendering service.</param>
        /// <param name="mediator">Mediator for CQRS queries.</param>
        /// <param name="logger">The logger.</param>
        public StaticFileService(
            IStorageContext storage,
            IEditorSettings settings,
            IViewRenderService viewRenderService,
            IMediator mediator,
            ILogger<StaticFileService> logger)
        {
            this.storage = storage;
            this.settings = settings;
            this.viewRenderService = viewRenderService;
            this.mediator = mediator;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task CreateStaticFileAsync(PublishedPage page)
        {
            if (!settings.StaticWebPages)
            {
                logger.LogDebug("Static web pages disabled. Skipping static file creation for {UrlPath}", page.UrlPath);
                return;
            }

            var rel = page.UrlPath.Equals("root", StringComparison.OrdinalIgnoreCase)
                ? "/index.html"
                : "/" + page.UrlPath.TrimStart('/');

            logger.LogDebug("Creating static file for {UrlPath} → {RelativePath}", page.UrlPath, rel);

            var layout = await mediator.QueryAsync(new GetDefaultLayoutQuery());

            var model = new ArticleViewModel()
            {
                ArticleNumber = page.ArticleNumber,
                Title = page.Title,
                Content = page.Content,
                HeadJavaScript = page.HeaderJavaScript,
                FooterJavaScript = page.FooterJavaScript,
                Updated = page.Updated,
                AuthorInfo = page.AuthorInfo,
                Published = page.Published,
                Expires = page.Expires,
                BannerImage = page.BannerImage,
                UrlPath = page.UrlPath,
                ArticleType = (ArticleType)(page.ArticleType ?? 0),
                Category = page.Category,
                Introduction = page.Introduction,
                Id = page.Id,
                EditModeOn = false,
                PreviewMode = false,
                ReadWriteMode = false,
                VersionNumber = page.VersionNumber,
                CacheDuration = 0,
                Layout = layout
            };

            var html = await viewRenderService.RenderToStringAsync("~/Views/Home/Index.cshtml", model);

            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(html));
            await storage.AppendBlob(ms, new FileUploadMetaData
            {
                ChunkIndex = 0,
                ContentType = "text/html",
                FileName = Path.GetFileName(rel),
                RelativePath = rel,
                TotalChunks = 1,
                TotalFileSize = ms.Length,
                UploadUid = Guid.NewGuid().ToString()
            });

            logger.LogInformation("Static file created successfully for {UrlPath} at {RelativePath}", page.UrlPath, rel);
        }

        /// <inheritdoc/>
        public void DeleteStaticFiles(IEnumerable<PublishedPage> pages)
        {
            if (!settings.StaticWebPages)
            {
                logger.LogDebug("Static web pages disabled. Skipping static file deletion");
                return;
            }

            foreach (var page in pages)
            {
                var rel = page.UrlPath.Equals("root", StringComparison.OrdinalIgnoreCase)
                ? "/index.html"
                : "/" + page.UrlPath.TrimStart('/');

                try
                {
                    storage.DeleteFile(rel);
                    logger.LogDebug("Deleted static file at {RelativePath}", rel);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete static file at {RelativePath}. Continuing...", rel);
                }
            }
        }
    }
}
