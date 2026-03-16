// <copyright file="PublishingService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Publishing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Constants;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services.BlogPublishing;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Sky.Cms.Services;
    using Sky.Editor.Domain.Events;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Authors;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.StaticFiles;
    using Sky.Editor.Services.TableOfContents;

    /// <summary>
    /// Orchestrates publishing of articles and blog content.
    /// </summary>
    /// <remarks>
    /// This service persists published page records, generates optional static HTML files,
    /// updates the site table of contents, and coordinates CDN cache purges so new content
    /// becomes visible immediately. Blog-specific publishing is delegated to <see cref="IBlogPublishingService"/>.
    /// </remarks>
    public class PublishingService : IPublishingService
    {
        private readonly IPublishingContext context;
        private readonly ILogger<PublishingService> logger;
        private readonly IAuthorInfoService authors;
        private readonly IClock systemClock;
        private readonly IStaticFileServiceFactory staticFileServiceFactory;
        private readonly IPublishingProgressReporter progressReporter;
        private readonly IDomainEventDispatcher? eventDispatcher;
        private readonly IPublishingAuxiliaryServices auxiliaryServices;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishingService"/> class.
        /// </summary>
        /// <param name="context">Publishing context providing database, storage, settings, HTTP context, and catalog query service.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="authors">The author information service.</param>
        /// <param name="systemClock">The system clock.</param>
        /// <param name="staticFileServiceFactory">Factory for creating scoped static file service instances during parallel processing.</param>
        /// <param name="progressReporter">The publishing progress reporter.</param>
        /// <param name="eventDispatcher">Optional domain event dispatcher for publishing cache invalidation events.</param>
        /// <param name="auxiliaryServices">Composite service providing CDN, TOC, static file, and blog publishing services.</param>
        public PublishingService(
            IPublishingContext context,
            ILogger<PublishingService> logger,
            Authors.IAuthorInfoService authors,
            IClock systemClock,
            IStaticFileServiceFactory staticFileServiceFactory,
            IPublishingProgressReporter progressReporter,
            IDomainEventDispatcher? eventDispatcher,
            IPublishingAuxiliaryServices auxiliaryServices)
        {
            this.context = context;
            this.logger = logger;
            this.authors = authors;
            this.systemClock = systemClock;
            this.staticFileServiceFactory = staticFileServiceFactory;
            this.progressReporter = progressReporter;
            this.eventDispatcher = eventDispatcher;
            this.auxiliaryServices = auxiliaryServices;
        }

        private Guid userId => Guid.Parse(context.HttpContextAccessor.HttpContext.User.Claims
            .FirstOrDefault(f => f.Type == "sub")?.Value ?? Guid.Empty.ToString());

        /// <summary>
        /// Publishes (or updates) a blog stream page for the specified blog key and user.
        /// </summary>
        /// <param name="blog">The blog stream metadata and content input. The <see cref="Article.BlogKey"/> identifies the stream; the HTML is generated with <see cref="IBlogRenderingService.GenerateBlogStreamHtml(Article)"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of CDN purge results indicating cache invalidation status per provider after publishing.</returns>
        /// <remarks>
        /// If a blog stream article already exists for the given <see cref="Article.BlogKey"/>,
        /// its metadata is updated and the <see cref="Article.VersionNumber"/> is incremented;
        /// otherwise a new article record is created. In both cases, content is produced by
        /// <see cref="IBlogRenderingService.GenerateBlogStreamHtml(Article)"/> and the operation
        /// delegates to <see cref="PublishAsync(Article, CancellationToken)"/> to create the published page, write
        /// optional static files, update the TOC, and purge the CDN.
        /// </remarks>
        public Task<List<CdnResult>> PublishBlogStreamAsync(Article blog, CancellationToken cancellationToken = default)
        {
            return auxiliaryServices.BlogPublishingService.PublishBlogStreamAsync(blog, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<CdnResult>> PublishAsync(Article article, CancellationToken token = default)
        {
            if (article.Published == null)
            {
                article.Published = DateTimeOffset.UtcNow.AddSeconds(-1);
            }

            // Validate UserId before parsing
            if (string.IsNullOrWhiteSpace(article.UserId))
            {
                throw new ArgumentException("User ID cannot be null or empty when publishing an article.", nameof(article));
            }

            if (!Guid.TryParse(article.UserId, out var userId))
            {
                throw new ArgumentException($"User ID '{article.UserId}' is not a valid GUID format.", nameof(article));
            }

            // Unpublish earlier versions of this article number.
            await UnpublishEalierVersions(article);

            // Remove prior published (non-redirect) pages for this article number
            var prior = await context.Database.Pages
                .Where(p => p.ArticleNumber == article.ArticleNumber && p.StatusCode != (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            if (prior.Any())
            {
                context.Database.Pages.RemoveRange(prior);
                await context.Database.SaveChangesAsync();

                auxiliaryServices.StaticFileService.DeleteStaticFiles(prior);
            }

            // ✅ BUGFIX: Save the Article entity with its Published property
            // This ensures the Published timestamp persists to the database
            await context.Database.SaveChangesAsync();

            var authorInfo = await authors.GetOrCreateAsync(userId);

            PublishedPage page;

            if (article.ArticleType == (int)ArticleType.BlogPost)
            {
                // Delegate blog post rendering to the blog publishing service
                page = await auxiliaryServices.BlogPublishingService.RenderBlogPostPageAsync(
                    article,
                    authorInfo == null ? string.Empty : JsonConvert.SerializeObject(authorInfo).Replace("\"", "'"));
            }
            else
            {
                page = new PublishedPage
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = article.ArticleNumber,
                    StatusCode = article.StatusCode,
                    UrlPath = article.UrlPath,
                    VersionNumber = article.VersionNumber,
                    Published = article.Published,
                    Expires = article.Expires,
                    Title = article.Title,
                    Content = article.Content,
                    Updated = article.Updated,
                    BannerImage = article.BannerImage,
                    HeaderJavaScript = article.HeaderJavaScript,
                    FooterJavaScript = article.FooterJavaScript,
                    ParentUrlPath = article.UrlPath.Contains('/')
                        ? article.UrlPath[..article.UrlPath.LastIndexOf('/')]
                        : string.Empty,
                    AuthorInfo = authorInfo == null ? string.Empty :
                        JsonConvert.SerializeObject(authorInfo).Replace("\"", "'"),
                    ArticleType = article.ArticleType,
                    Category = article.Category,
                    Introduction = article.Introduction
                };
            }

            context.Database.Pages.Add(page);
            await context.Database.SaveChangesAsync();

            await auxiliaryServices.StaticFileService.CreateStaticFileAsync(page);
            await WriteTocAsync("/");

            // If this is a blog post, also update the TOC for the blog stream
            if (article.ArticleType == (int)ArticleType.BlogPost || article.ArticleType == (int)ArticleType.BlogStream)
            {
                await WriteTocAsync($"/{article.BlogKey}");
            }

            // Publish domain event for cache invalidation after successful publish
            if (eventDispatcher != null)
            {
                await eventDispatcher.DispatchAsync(new ArticlePublishedEvent(article.ArticleNumber, article.Id));
            }

            return await PurgeCdnAsync(page);
        }

        /// <summary>
        /// Creates static HTML files for the specified published pages and purges the CDN cache.
        /// </summary>
        /// <param name="ids">Collection of page identifiers to generate static files for.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// This method is used for batch static page generation, typically during republishing operations
        /// or site-wide regeneration events. It performs the following actions:
        /// </para>
        /// <list type="number">
        ///   <item><description>Retrieves all published pages matching the provided IDs from the database</description></item>
        ///   <item><description>Generates and uploads static HTML files for each page to blob storage in parallel with retry logic</description></item>
        ///   <item><description>Regenerates the table of contents (TOC) JSON file</description></item>
        ///   <item><description>Triggers a full CDN cache purge if a CDN service is configured</description></item>
        /// </list>
        /// <para>
        /// Unlike ArticleEditLogic.PublishAsync(), this method performs a full CDN purge rather than selective path purging.
        /// Only processes pages if <see cref="IEditorSettings.StaticWebPages"/> is enabled.
        /// Static file generation is parallelized with a configurable degree of parallelism (default: 4).
        /// Failed uploads are retried up to 3 times with exponential backoff (initial delay: 500ms, multiplier: 2).
        /// </para>
        /// </remarks>
        public async Task CreateStaticPages(IEnumerable<Guid> ids)
        {
            const int batchSize = 50;

            // If no IDs provided, publish all pages
            var pageIds = (ids == null || !ids.Any())
                ? await context.Database.Pages.Select(p => p.Id).ToListAsync()
                : ids.ToList();

            await progressReporter.ReportProgressAsync(0, pageIds.Count, "Preparing to generate static pages...");

            // Determine optimal parallelism based on storage backend
            var parallelism = StorageParallelismHelper.GetOptimalParallelism(
                context.Storage,
                logger,
                context.Settings.StaticPageParallelism);

            logger.LogInformation(
                "Starting static page generation for {PageCount} page(s) with parallelism: {Parallelism}",
                pageIds.Count,
                parallelism);

            await progressReporter.ReportProgressAsync(
                0,
                pageIds.Count,
                $"Starting generation of {pageIds.Count} page(s) with parallelism: {parallelism}");

            var processedCount = 0;
            var progressLock = new object();

            // Process in batches to control memory
            for (int i = 0; i < pageIds.Count; i += batchSize)
            {
                var batchIds = pageIds.Skip(i).Take(batchSize);
                var pages = await context.Database.Pages.Where(w => batchIds.Contains(w.Id)).ToListAsync();

                // Process this batch with adaptive parallelism
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallelism
                };

                var batchProcessedCount = 0;

                await Parallel.ForEachAsync(pages, options, async (page, cancellationToken) =>
                {
                    var scopedStaticFileService = staticFileServiceFactory.CreateScoped();

                    try
                    {
                        await scopedStaticFileService.CreateStaticFileAsync(page);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to create static file for page {PageId} ({UrlPath}). Skipping this page.", page.Id, page.UrlPath);
                    }

                    // Thread-safe increment and progress reporting
                    int currentCount;
                    lock (progressLock)
                    {
                        batchProcessedCount++;
                        currentCount = processedCount + batchProcessedCount;
                    }

                    // Report progress every 5 pages to avoid flooding SignalR
                    if (currentCount % 5 == 0 || currentCount == pageIds.Count)
                    {
                        await progressReporter.ReportProgressAsync(
                            currentCount,
                            pageIds.Count,
                            $"Generated {currentCount} of {pageIds.Count} page(s)");
                    }
                });

                processedCount += batchProcessedCount;

                logger.LogInformation(
                    "Completed batch {BatchNumber}/{TotalBatches} ({PagesProcessed}/{TotalPages} pages)",
                    (i / batchSize) + 1,
                    (pageIds.Count + batchSize - 1) / batchSize,
                    Math.Min(i + batchSize, pageIds.Count),
                    pageIds.Count);

                await progressReporter.ReportProgressAsync(
                    processedCount,
                    pageIds.Count,
                    $"Completed batch {(i / batchSize) + 1}/{(pageIds.Count + batchSize - 1) / batchSize}");
            }

            await progressReporter.ReportProgressAsync(
                pageIds.Count,
                pageIds.Count,
                "Updating table of contents...");

            // Write TOC and purge CDN after all batches
            await WriteTocAsync("/");

            await progressReporter.ReportProgressAsync(
                pageIds.Count,
                pageIds.Count,
                "Purging CDN cache...");

            var cdnService = await CdnService.GetCdnServiceAsync(context.Database, logger, context.HttpContextAccessor.HttpContext);
            if (cdnService != null)
            {
                await cdnService.PurgeCdn();
            }

            await progressReporter.ReportProgressAsync(
                pageIds.Count,
                pageIds.Count,
                "Static page generation completed successfully!");
        }

        /// <inheritdoc/>
        public async Task UnpublishAsync(Article article)
        {
            var articleNumber = article.ArticleNumber;

            var versions = await context.Database.Articles.Where(a => a.ArticleNumber == articleNumber && a.Published != null).ToListAsync();
            if (!versions.Any())
            {
                return;
            }

            foreach (var v in versions)
            {
                v.Published = null;
            }

            var pages = await context.Database.Pages
                .Where(p => p.ArticleNumber == articleNumber && p.StatusCode != (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            context.Database.Pages.RemoveRange(pages);
            await context.Database.SaveChangesAsync();
            auxiliaryServices.StaticFileService.DeleteStaticFiles(pages);

            foreach (var page in pages)
            {
                await PurgeCdnAsync(page);
            }

            // Update catalog entry to reflect unpublished state
            var catalogEntry = await context.Database.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == articleNumber);
            if (catalogEntry != null)
            {
                catalogEntry.Published = null;
                await context.Database.SaveChangesAsync();
            }

            // Publish domain event for cache invalidation after successful unpublish
            if (eventDispatcher != null)
            {
                await eventDispatcher.DispatchAsync(new ArticleUnpublishedEvent(articleNumber));
            }

            await WriteTocAsync("/");
        }

        /// <inheritdoc/>
        public Task WriteTocAsync(string prefix = "/")
        {
            return auxiliaryServices.TocService.WriteTocAsync(prefix);
        }

        /// <summary>
        /// Unpublishes earlier versions of an article to ensure only the latest published version is active.
        /// </summary>
        /// <param name="article">The article being published. Must have a valid <see cref="Article.ArticleNumber"/> and <see cref="Article.VersionNumber"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// This method ensures content integrity by removing earlier published versions of the same article
        /// when a newer version is published. It performs the following actions:
        /// </para>
        /// <list type="number">
        ///   <item><description>Validates the article's publish status (must be published now or earlier)</description></item>
        ///   <item><description>Locates all earlier published versions of the same article number</description></item>
        ///   <item><description>Marks those versions as unpublished (sets <c>Published</c> to null)</description></item>
        ///   <item><description>Removes their corresponding published page records from the database</description></item>
        ///   <item><description>Deletes associated static files from storage</description></item>
        /// </list>
        /// <para>
        /// If the article is scheduled for future publication or is not published, this method exits early without changes.
        /// This prevents premature cleanup of existing published content.
        /// </para>
        /// </remarks>
        private async Task UnpublishEalierVersions(Article article)
        {
            var dateTime = systemClock.UtcNow;

            if (article.Published == null || article.Published > dateTime)
            {
                // Nothing to do.
                // We only publish versions that are published now or earlier.
                return;
            }

            var versionNumber = article.VersionNumber;

            // Find previous versions of this article number that are published before this one.
            var others = await context.Database.Articles.Where(a =>
                a.ArticleNumber == article.ArticleNumber &&
                a.Published != null &&
                a.VersionNumber < versionNumber).ToListAsync();

            if (!others.Any())
            {
                // There are no previous versions published.
                return;
            }

            var ids = others.Select(s => s.Id).ToList();

            // Unpublish them.
            foreach (var o in others)
            {
                o.Published = null;
            }

            await context.Database.SaveChangesAsync();

            // Remove their published pages.
            var doomedPages = await context.Database.Pages
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();

            context.Database.Pages.RemoveRange(doomedPages);
            await context.Database.SaveChangesAsync();

            // Remove their static files.
            auxiliaryServices.StaticFileService.DeleteStaticFiles(doomedPages);
        }

        /// <summary>
        /// Purges the CDN cache for the specified published page's URL path.
        /// </summary>
        /// <param name="page">The published page whose CDN cache should be invalidated. Must have a valid <see cref="PublishedPage.UrlPath"/>.</param>
        /// <returns>
        /// A task producing a list of <see cref="CdnResult"/> objects representing the outcome of CDN purge operations.
        /// Returns an empty list if no CDN service is configured or if the operation fails.
        /// </returns>
        /// <remarks>
        /// Delegates to <see cref="ICdnPurgeService"/> for CDN cache invalidation.
        /// </remarks>
        private Task<List<CdnResult>> PurgeCdnAsync(PublishedPage page)
        {
            return auxiliaryServices.CdnPurgeService.PurgePageCacheAsync(page);
        }
    }
}
