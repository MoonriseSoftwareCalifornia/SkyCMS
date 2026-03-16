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
    /// becomes visible immediately. Blog streams and blog posts receive special rendering
    /// via the injected <see cref="IBlogStreamRenderingService"/>.
    /// </remarks>
    public class PublishingService : IPublishingService
    {
        private readonly ApplicationDbContext db;
        private readonly IStorageContext storage;
        private readonly IEditorSettings settings;
        private readonly ILogger<PublishingService> logger;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IAuthorInfoService authors;
        private readonly IClock systemClock;
        private readonly Cosmos.Common.Features.Shared.IMediator mediator;
        private readonly IBlogStreamRenderingService blogStreamRenderingService;
        private readonly IViewRenderService viewRenderService;
        private readonly IServiceProvider serviceProvider;
        private readonly IPublishingProgressReporter progressReporter;
        private readonly IArticleCatalogQueryService articleCatalogQueryService;
        private readonly IDomainEventDispatcher? eventDispatcher;
        private readonly ICdnPurgeService cdnPurgeService;
        private readonly ITocService tocService;
        private readonly IStaticFileService staticFileService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishingService"/> class.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="storage">The storage context.</param>
        /// <param name="settings">The editor settings.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="accessor">The HTTP context accessor.</param>
        /// <param name="authors">The author information service.</param>
        /// <param name="systemClock">The system clock.</param>
        /// <param name="mediator">Mediator for CQRS queries.</param>
        /// <param name="blogStreamRenderingService">The blog stream rendering service.</param>
        /// <param name="viewRenderService">View rendering service.</param>
        /// <param name="serviceProvider">Service provider for creating scoped dependencies.</param>
        /// <param name="progressReporter">The publishing progress reporter.</param>
        /// <param name="articleCatalogQueryService">Article catalog service.</param>
        /// <param name="eventDispatcher">Optional domain event dispatcher for publishing cache invalidation events.</param>
        /// <param name="cdnPurgeService">CDN purge service for cache invalidation.</param>
        /// <param name="tocService">Table of Contents service for generating TOC JSON files.</param>
        /// <param name="staticFileService">Static file service for generating and managing static HTML files.</param>
        public PublishingService(
            ApplicationDbContext db,
            IStorageContext storage,
            IEditorSettings settings,
            ILogger<PublishingService> logger,
            IHttpContextAccessor accessor,
            Authors.IAuthorInfoService authors,
            IClock systemClock,
            Cosmos.Common.Features.Shared.IMediator mediator,
            IBlogStreamRenderingService blogStreamRenderingService,
            IViewRenderService viewRenderService,
            IServiceProvider serviceProvider,
            IPublishingProgressReporter progressReporter,
            IArticleCatalogQueryService articleCatalogQueryService,
            IDomainEventDispatcher? eventDispatcher,
            ICdnPurgeService cdnPurgeService,
            ITocService tocService,
            IStaticFileService staticFileService)
        {
            this.db = db;
            this.storage = storage;
            this.settings = settings;
            this.logger = logger;
            httpContextAccessor = accessor;
            this.authors = authors;
            this.systemClock = systemClock;
            this.mediator = mediator;
            this.blogStreamRenderingService = blogStreamRenderingService;
            this.viewRenderService = viewRenderService;
            this.serviceProvider = serviceProvider;
            this.progressReporter = progressReporter;
            this.articleCatalogQueryService = articleCatalogQueryService;
            this.eventDispatcher = eventDispatcher;
            this.cdnPurgeService = cdnPurgeService;
            this.tocService = tocService;
            this.staticFileService = staticFileService;
        }

        private Guid userId => Guid.Parse(httpContextAccessor.HttpContext.User.Claims
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
        public async Task<List<CdnResult>> PublishBlogStreamAsync(Article blog, CancellationToken cancellationToken = default)
        {
            var article = await db.Articles
                .Where(a => a.BlogKey == blog.BlogKey && a.ArticleType == (int)ArticleType.BlogStream)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();

            if (article == null)
            {
                var articleNumber = (await db.Articles.AnyAsync()) ?
                    (await db.Articles.Select(s => s.ArticleNumber).MaxAsync()) + 1 : 1;

                article = new Article
                {
                    ArticleNumber = articleNumber,
                    UrlPath = blog.BlogKey,
                    VersionNumber = 1,
                    Published = DateTimeOffset.UtcNow,
                    Expires = null,
                    Title = blog.Title,
                    Content = string.Empty,
                    Updated = blog.Updated,
                    BannerImage = blog.BannerImage,
                    HeaderJavaScript = string.Empty,
                    FooterJavaScript = string.Empty,
                    UserId = userId.ToString(),
                    StatusCode = (int)StatusCodeEnum.Active,
                    ArticleType = (int)ArticleType.BlogStream,
                    Category = "blog-stream",
                    Introduction = blog.Introduction,
                    BlogKey = blog.BlogKey
                };

                db.Articles.Add(article);
            }
            else
            {
                article.UrlPath = blog.BlogKey;
                article.Published = DateTimeOffset.UtcNow;
                article.Title = blog.Title;
                article.Updated = blog.Updated;
                article.BannerImage = blog.BannerImage;
                article.Introduction = blog.Introduction;
                article.UserId = userId.ToString();
                article.StatusCode = (int)StatusCodeEnum.Active;
                article.VersionNumber += 1;
            }

            // Generate wrapper HTML with embedded JSON metadata
            article.Content = await blogStreamRenderingService.GenerateBlogStreamWrapperAsync(article, blog.BlogKey);

            // Publish the blog stream article
            var cdnResults = await PublishAsync(article);

            // Additionally publish the versioned wrapper as a static file for direct access
            var wrapperPath = GetWrapperVersionedPath(blog.BlogKey);
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(article.Content));
            await storage.AppendBlob(ms, new FileUploadMetaData
            {
                ChunkIndex = 0,
                ContentType = "text/html",
                FileName = Path.GetFileName(wrapperPath),
                RelativePath = wrapperPath,
                TotalChunks = 1,
                TotalFileSize = ms.Length,
                UploadUid = Guid.NewGuid().ToString()
            });

            return cdnResults;
        }

        /// <summary>
        /// Gets a versioned wrapper filename using UTC ticks for cache busting.
        /// </summary>
        /// <param name="blogKey">The blog stream key.</param>
        /// <returns>Relative file path for the versioned wrapper (e.g., /blog/painting/blog-stream-wrapper-638708432156789123.html).</returns>
        private string GetWrapperVersionedPath(string blogKey)
        {
            var ticks = DateTimeOffset.UtcNow.Ticks;
            return $"/{blogKey.TrimStart('/')}/blog-stream-wrapper-{ticks}.html";
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
            var prior = await db.Pages
                .Where(p => p.ArticleNumber == article.ArticleNumber && p.StatusCode != (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            if (prior.Any())
            {
                db.Pages.RemoveRange(prior);
                await db.SaveChangesAsync();

                staticFileService.DeleteStaticFiles(prior);
            }

            // ✅ BUGFIX: Save the Article entity with its Published property
            // This ensures the Published timestamp persists to the database
            await db.SaveChangesAsync();

            var authorInfo = await authors.GetOrCreateAsync(userId);

            PublishedPage page;

            if (article.ArticleType == (int)ArticleType.BlogPost)
            {
                // For blog posts, generate full page content (for direct access to individual posts)
                // The snippet version can be generated on-demand via blogStreamRenderingService.GenerateBlogPostSnippetAsync()
                var layout = await mediator.QueryAsync(new Cosmos.Common.Features.Layouts.Queries.GetDefaultLayoutQuery());

                var model = new ArticleViewModel()
                {
                    ArticleNumber = article.ArticleNumber,
                    Title = article.Title,
                    Content = article.Content,
                    HeadJavaScript = article.HeaderJavaScript,
                    FooterJavaScript = article.FooterJavaScript,
                    Updated = article.Updated,
                    Published = article.Published,
                    Expires = article.Expires,
                    BannerImage = article.BannerImage,
                    UrlPath = article.UrlPath,
                    ArticleType = (ArticleType)(article.ArticleType ?? 0),
                    Category = article.Category,
                    Introduction = article.Introduction,
                    Id = article.Id,
                    EditModeOn = false,
                    PreviewMode = false,
                    ReadWriteMode = false,
                    VersionNumber = article.VersionNumber,
                    CacheDuration = 0,
                    Layout = layout
                };

                var blogContent = await viewRenderService.RenderToStringAsync("~/Views/Home/Index.cshtml", model);

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
                    Content = blogContent,
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
                    Introduction = article.Introduction,
                    BlogKey = article.BlogKey
                };
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

            db.Pages.Add(page);
            await db.SaveChangesAsync();

            await staticFileService.CreateStaticFileAsync(page);
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
                ? await db.Pages.Select(p => p.Id).ToListAsync()
                : ids.ToList();

            await progressReporter.ReportProgressAsync(0, pageIds.Count, "Preparing to generate static pages...");

            // Determine optimal parallelism based on storage backend
            var parallelism = StorageParallelismHelper.GetOptimalParallelism(
                storage,
                logger,
                settings.StaticPageParallelism);

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
                var pages = await db.Pages.Where(w => batchIds.Contains(w.Id)).ToListAsync();

                // Process this batch with adaptive parallelism
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallelism
                };

                var batchProcessedCount = 0;

                await Parallel.ForEachAsync(pages, options, async (page, cancellationToken) =>
                {
                    await using var scope = serviceProvider.CreateAsyncScope();
                    var scopedStaticFileService = scope.ServiceProvider.GetRequiredService<IStaticFileService>();

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

            var cdnService = await CdnService.GetCdnServiceAsync(db, logger, httpContextAccessor.HttpContext);
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

            var versions = await db.Articles.Where(a => a.ArticleNumber == articleNumber && a.Published != null).ToListAsync();
            if (!versions.Any())
            {
                return;
            }

            foreach (var v in versions)
            {
                v.Published = null;
            }

            var pages = await db.Pages
                .Where(p => p.ArticleNumber == articleNumber && p.StatusCode != (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            db.Pages.RemoveRange(pages);
            await db.SaveChangesAsync();
            staticFileService.DeleteStaticFiles(pages);

            foreach (var page in pages)
            {
                await PurgeCdnAsync(page);
            }

            // Update catalog entry to reflect unpublished state
            var catalogEntry = await db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == articleNumber);
            if (catalogEntry != null)
            {
                catalogEntry.Published = null;
                await db.SaveChangesAsync();
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
            return tocService.WriteTocAsync(prefix);
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
            var others = await db.Articles.Where(a =>
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

            await db.SaveChangesAsync();

            // Remove their published pages.
            var doomedPages = await db.Pages
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();

            db.Pages.RemoveRange(doomedPages);
            await db.SaveChangesAsync();

            // Remove their static files.
            staticFileService.DeleteStaticFiles(doomedPages);
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
            return cdnPurgeService.PurgePageCacheAsync(page);
        }
    }
}
