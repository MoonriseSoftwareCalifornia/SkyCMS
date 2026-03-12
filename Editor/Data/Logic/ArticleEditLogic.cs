// <copyright file="ArticleEditLogic.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Data.Logic
{
    // PATCHED: orchestrates via services; legacy method names preserved
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Models;
    using Cosmos.DynamicConfig;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Newtonsoft.Json;
    using SendGrid.Helpers.Errors.Model;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using Sky.Cms.Services;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Articles.Save;
    using Sky.Editor.Features.Shared;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Redirects;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Services.Titles;
    using ArticleUpdateResult = Cms.Models.ArticleUpdateResult;

    /// <summary>
    /// Article editing and management logic (editor-facing). Inherits read/view logic from <see cref="ArticleLogic"/>.
    /// Coordinates persistence, publishing, catalog updates, static artifact generation and title/slug change handling.
    /// </summary>
    public partial class ArticleEditLogic : ArticleLogic
    {
        private readonly IStorageContext storageContext; // Used only for deleting static artifacts.
        private readonly ILogger<ArticleEditLogic> logger;
        private readonly IMemoryCache localCache;
        private readonly IEditorSettings settings;
        private readonly IDynamicConfigurationProvider configurationProvider;

        // Service dependencies
        private readonly IClock clock;
        private readonly ISlugService slugService;
        private readonly IArticleHtmlService htmlService;
        private readonly ICatalogService catalogService;
        private readonly IPublishingService publishingService;
        private readonly ITitleChangeService titleChangeService;
        private readonly ITemplateService templateService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleEditLogic"/> class.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="memoryCache">Process memory cache for transient items.</param>
        /// <param name="storageContext">Blob/file storage context for static artifacts.</param>
        /// <param name="logger">Logger for diagnostic events.</param>
        /// <param name="settings">Editor (instance) settings.</param>
        /// <param name="clock">Clock abstraction for testable UTC timestamps.</param>
        /// <param name="slugService">Slug normalization service.</param>
        /// <param name="htmlService">HTML transformation / injection service.</param>
        /// <param name="catalogService">Catalog (index) maintenance service.</param>
        /// <param name="publishingService">Publishing state manager.</param>
        /// <param name="titleChangeService">Title change coordinator (redirects, child slugs, events).</param>
        /// <param name="redirectService">Redirect service (kept for DI compatibility; not directly used here).</param>
        /// <param name="templateService">Template service for managing article templates.</param>
        /// <param name="configurationProvider">Dynamic configuration provider for tenant resolution.</param>
        public ArticleEditLogic(
            ApplicationDbContext dbContext,
            IMemoryCache memoryCache,
            IStorageContext storageContext, // Used only for deleting static artifacts.
            ILogger<ArticleEditLogic> logger,
            IEditorSettings settings,
            IClock clock,
            ISlugService slugService,
            IArticleHtmlService htmlService,
            ICatalogService catalogService,
            IPublishingService publishingService,
            ITitleChangeService titleChangeService,
            IRedirectService redirectService,
            ITemplateService templateService,
            IDynamicConfigurationProvider configurationProvider = null)
            : base(
                dbContext,
                memoryCache,
                settings.PublisherUrl,
                settings.BlobPublicUrl,
                true)
        {
            this.storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext)); // Used only for deleting static artifacts.
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.localCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.slugService = slugService ?? throw new ArgumentNullException(nameof(slugService));
            this.htmlService = htmlService ?? throw new ArgumentNullException(nameof(htmlService));
            this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            this.publishingService = publishingService ?? throw new ArgumentNullException(nameof(publishingService));
            this.titleChangeService = titleChangeService ?? throw new ArgumentNullException(nameof(titleChangeService));
            this.templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
            this.configurationProvider = configurationProvider; // Optional: null for single-tenant scenarios
        }

        /// <summary>
        /// Gets the strongly-typed application database context (shadowing base protected context for convenience).
        /// </summary>
        public new ApplicationDbContext DbContext => base.DbContext;

        /// <summary>
        /// Gets the current tenant domain from the configuration provider (or null if single-tenant/not configured).
        /// </summary>
        /// <returns>Tenant domain string or null.</returns>
        private Task<string> GetCurrentTenantDomainAsync()
        {
            try
            {
                // Use the configuration provider to get tenant domain from request context
                // This ensures proper tenant isolation in multi-tenant scenarios
                if (configurationProvider != null)
                {
                    var tenantDomain = configurationProvider.GetTenantDomainNameFromRequest();
                    return Task.FromResult(tenantDomain);
                }
                
                // If no configuration provider, assume single-tenant scenario
                return Task.FromResult<string>(null);
            }
            catch
            {
                // If any error occurs, assume single-tenant scenario
                return Task.FromResult<string>(null);
            }
        }

        /// <summary>
        /// Returns the most recent published timestamp (UTC) for the specified logical article number, or null if never published.
        /// </summary>
        /// <param name="articleNumber">Logical article number.</param>
        /// <returns>Latest published <see cref="DateTimeOffset"/> or <c>null</c>.</returns>
        //public async Task<DateTimeOffset?> GetLastPublishedDate(int articleNumber) =>
        //    await DbContext.Articles
        //        .Where(a => a.ArticleNumber == articleNumber && a.Published != null)
        //        .OrderByDescending(a => a.Published)
        //        .Select(a => a.Published)
        //        .FirstOrDefaultAsync();

        /// <summary>
        /// Retrieves a specific version (or latest) of an article by logical article number for editing contexts.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="versionNumber">Target version; if null the latest version is returned.</param>
        /// <returns>Article view model or null if not found.</returns>
        /// <remarks>
        /// <strong>⚠️ DEPRECATED:</strong> Use <see cref="GetArticleByArticleNumberQuery"/> via the mediator pattern instead. This method will be removed in version 3.0.
        /// </remarks>
        //[Obsolete("Use GetArticleByArticleNumberQuery via IMediator instead. This method will be removed in version 3.0.", error: false)]
        //public async Task<ArticleViewModel> GetArticleByArticleNumber(int articleNumber, int? versionNumber)
        //{
        //    // Get current tenant domain for filtering
        //    var tenantDomain = await GetCurrentTenantDomainAsync();

        //    // Explicitly project required fields to ensure EF loads them
        //    IQueryable<Article> q = DbContext.Articles
        //        .AsNoTracking() // Prevent tracking issues in concurrent contexts
        //        .Where(a => a.ArticleNumber == articleNumber && a.StatusCode != (int)StatusCodeEnum.Deleted);

        //    var entity = versionNumber.HasValue
        //        ? await q.FirstOrDefaultAsync(a => a.VersionNumber == versionNumber.Value)
        //        : await q.OrderByDescending(a => a.VersionNumber).FirstOrDefaultAsync();

        //    if (entity == null)
        //    {
        //        logger.LogWarning(
        //            "Article {ArticleNumber} (version: {VersionNumber}) not found",
        //            articleNumber,
        //            versionNumber?.ToString() ?? "latest");
        //        return null;
        //    }

        //    // **CHECK 1**: Verify Content was loaded from database
        //    if (entity.Content == null)
        //    {
        //        logger.LogError(
        //            "Article {ArticleNumber} version {VersionNumber} has NULL content in database (Id: {Id})",
        //            entity.ArticleNumber,
        //            entity.VersionNumber,
        //            entity.Id);
                
        //        // Try to reload explicitly
        //        entity = await DbContext.Articles
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(a => a.Id == entity.Id);
                
        //        if (entity?.Content == null)
        //        {
        //            logger.LogError(
        //                "Article {ArticleNumber} version {VersionNumber} still has NULL content after reload",
        //                articleNumber,
        //                versionNumber ?? entity?.VersionNumber ?? 0);
        //        }
        //    }

        //    // **CHECK 2**: Warn if Content is empty (might be intentional, but worth logging)
        //    if (string.IsNullOrWhiteSpace(entity.Content))
        //    {
        //        logger.LogWarning(
        //            "Article {ArticleNumber} version {VersionNumber} has empty/whitespace content (Id: {Id})",
        //            entity.ArticleNumber,
        //            entity.VersionNumber,
        //            entity.Id);
        //    }

        //    // **CHECK 3**: Log content retrieval for debugging
        //    logger.LogDebug(
        //        "Retrieved article {ArticleNumber} version {VersionNumber} with content length: {ContentLength}",
        //        entity.ArticleNumber,
        //        entity.VersionNumber,
        //        entity.Content?.Length ?? 0);

        //    var viewModel = await BuildArticleViewModel(entity, "en-US");

        //    // **CHECK 4**: Verify Content survived the mapping to ViewModel
        //    if (viewModel.Content == null && entity.Content != null)
        //    {
        //        logger.LogError(
        //            "Content was lost during BuildArticleViewModel for article {ArticleNumber} version {VersionNumber}",
        //            entity.ArticleNumber,
        //            entity.VersionNumber);
        //    }

        //    // **CHECK 5**: Validate consistency between entity and view model
        //    if (entity.Content != viewModel.Content)
        //    {
        //        logger.LogWarning(
        //            "Content mismatch between entity and view model for article {ArticleNumber} version {VersionNumber}. " +
        //            "Entity length: {EntityLength}, ViewModel length: {ViewModelLength}",
        //            entity.ArticleNumber,
        //            entity.VersionNumber,
        //            entity.Content?.Length ?? 0,
        //            viewModel.Content?.Length ?? 0);
        //    }

        //    return viewModel;
        //}

        /// <summary>
        /// Retrieves an article by row (GUID) identifier, excluding deleted versions.
        /// </summary>
        /// <param name="id">Article row ID.</param>
        /// <param name="userId">User context (unused).</param>
        /// <returns>Article view model or null.</returns>
        /// <remarks>
        /// <strong>⚠️ DEPRECATED:</strong> Use <see cref="GetArticleByIdQuery"/> via the mediator pattern instead. This method will be removed in version 3.0.
        /// </remarks>
        //[Obsolete("Use GetArticleByIdQuery via IMediator instead. This method will be removed in version 3.0.", error: false)]
        //public async Task<ArticleViewModel> GetArticleById(Guid id, Guid userId)
        //{
        //    // Get current tenant domain for filtering
        //    var tenantDomain = await GetCurrentTenantDomainAsync();
            
        //    IQueryable<Article> query = DbContext.Articles
        //        .Where(a => a.Id == id && a.StatusCode != (int)StatusCodeEnum.Deleted);
            
        //    var entity = await query.FirstOrDefaultAsync();
        //    return entity == null ? null : await BuildArticleViewModel(entity, "en-US");
        //}

        /// <summary>
        /// Retrieves the latest non-deleted article by URL path (slug). Empty path is treated as root.
        /// </summary>
        /// <param name="urlPath">Slug path (or empty/root).</param>
        /// <param name="controllerName">Legacy controller hint (unused).</param>
        /// <param name="userId">User context (unused).</param>
        /// <returns>Article view model or null.</returns>
        /// <remarks>
        /// <strong>⚠️ DEPRECATED:</strong> Use <see cref="GetArticleByUrlQuery"/> via the mediator pattern instead. This method will be removed in version 3.0.
        /// </remarks>
        //[Obsolete("Use GetArticleByUrlQuery via IMediator instead. This method will be removed in version 3.0.", error: false)]
        //public async Task<ArticleViewModel> GetArticleByUrl(string urlPath, EnumControllerName controllerName, Guid userId)
        //{
        //    if (string.IsNullOrWhiteSpace(urlPath) || urlPath.Equals("/"))
        //    {
        //        urlPath = "root";
        //    }

        //    urlPath = urlPath.TrimStart('/');

        //    // Get current tenant domain for filtering
        //    var tenantDomain = await GetCurrentTenantDomainAsync();
            
        //    var deletedEnum = (int)StatusCodeEnum.Deleted;
        //    IQueryable<Article> query = DbContext.Articles
        //        .Where(a => a.UrlPath == urlPath && a.StatusCode != deletedEnum);
            
        //    var entity = await query
        //        .OrderByDescending(a => a.VersionNumber)
        //        .FirstOrDefaultAsync();

        //    return entity == null ? null : await BuildArticleViewModel(entity, "en-US");
        //}

        /// <summary>
        /// Convenience overload returning latest article version by slug.
        /// </summary>
        /// <param name="urlPath">Slug path.</param>
        /// <returns>Article view model or null.</returns>
        /// <remarks>
        /// <strong>⚠️ DEPRECATED:</strong> Use <see cref="GetArticleByUrlQuery"/> via the mediator pattern instead. This method will be removed in version 3.0.
        /// </remarks>
        //[Obsolete("Use GetArticleByUrlQuery via IMediator instead. This method will be removed in version 3.0.", error: false)]
        //public Task<ArticleViewModel> GetArticleByUrl(string urlPath) =>
        //    GetArticleByUrl(urlPath, EnumControllerName.Edit, Guid.Empty);

        /// <summary>
        /// Convenience overload with a (currently ignored) published-only flag for API symmetry.
        /// </summary>
        /// <param name="urlPath">Slug path.</param>
        /// <param name="publishedOnly">If true would filter to published; ignored in editor mode.</param>
        /// <returns>Article view model or null.</returns>
        /// <remarks>
        /// <strong>⚠️ DEPRECATED:</strong> Use <see cref="GetArticleByUrlQuery"/> via the mediator pattern instead. This method will be removed in version 3.0.
        /// </remarks>
        //[Obsolete("Use GetArticleByUrlQuery via IMediator instead. This method will be removed in version 3.0.", error: false)]
        //public Task<ArticleViewModel> GetArticleByUrl(string urlPath, bool publishedOnly) =>
        //    GetArticleByUrl(urlPath, EnumControllerName.Edit, Guid.Empty);

        /// <summary>
        /// Returns redirect items (articles whose status represents redirect entries).
        /// </summary>
        /// <returns>Queryable redirect view models.</returns>
        /// <remarks>
        /// <strong>⚠️ DEPRECATED:</strong> Use <see cref="GetArticleRedirectsQuery"/> via the mediator pattern instead. This method will be removed in version 3.0.
        /// </remarks>
        //[Obsolete("Use GetArticleRedirectsQuery via IMediator instead. This method will be removed in version 3.0.", error: false)]
        //public IQueryable<RedirectItemViewModel> GetArticleRedirects() =>
        //    DbContext.Articles
        //        .Where(p => p.StatusCode == (int)StatusCodeEnum.Redirect)
        //        .Select(p => new RedirectItemViewModel
        //        {
        //            Id = p.Id,
        //            FromUrl = p.UrlPath,
        //            ToUrl = p.BannerImage,
        //        });

        /// <summary>
        /// Produces a standalone HTML document for the provided article view model (no sanitization beyond what is stored).
        /// </summary>
        /// <param name="article">Article model.</param>
        /// <param name="renderer">Optional renderer (unused; placeholder for future layout wrapping).</param>
        /// <returns>HTML string (empty if model null).</returns>
        public async Task<string> ExportArticle(ArticleViewModel article, IViewRenderService renderer)
        {
            if (article == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder()
                .AppendLine("<!DOCTYPE html>")
                .AppendLine("<html lang=\"en\">\n<head>")
                .AppendLine("<meta charset=\"utf-8\" />")
                .AppendLine("<title>" + System.Net.WebUtility.HtmlEncode(article.Title) + "</title>");

            if (!string.IsNullOrWhiteSpace(article.HeadJavaScript))
            {
                sb.AppendLine(article.HeadJavaScript);
            }

            sb.AppendLine("</head><body>")
              .AppendLine(article.Content);

            if (!string.IsNullOrWhiteSpace(article.FooterJavaScript))
            {
                sb.AppendLine(article.FooterJavaScript);
            }

            sb.AppendLine("</body></html>");
            return await Task.FromResult(sb.ToString());
        }

        /// <summary>
        /// Gets (or creates) a catalog entry for an article view model identifier.
        /// </summary>
        /// <param name="model">Article view model referencing an article ID.</param>
        /// <returns>Catalog entry.</returns>
        /// <remarks>
        /// <strong>⚠️ DEPRECATED:</strong> Use <see cref="GetArticleCatalogEntryQuery"/> via the mediator pattern instead. This method will be removed in version 3.0.
        /// </remarks>
        //[Obsolete("Use GetArticleCatalogEntryQuery via IMediator instead. This method will be removed in version 3.0.", error: false)]
        //public async Task<CatalogEntry> GetCatalogEntry(ArticleViewModel model)
        //{
        //    var article = await DbContext.Articles.FirstOrDefaultAsync(f => f.Id == model.Id);
        //    return await GetCatalogEntry(article);
        //}

        /// <summary>
        /// Gets (or creates) a catalog entry for an article entity.
        /// </summary>
        /// <param name="article">Article entity.</param>
        /// <returns>Catalog entry.</returns>
        /// <remarks>
        /// <strong>⚠️ DEPRECATED:</strong> Use <see cref="GetArticleCatalogEntryQuery"/> via the mediator pattern instead. This method will be removed in version 3.0.
        /// </remarks>
        //[Obsolete("Use GetArticleCatalogEntryQuery via IMediator instead. This method will be removed in version 3.0.", error: false)]
        //public async Task<CatalogEntry> GetCatalogEntry(Article article)
        //{
        //    var entry = await DbContext.ArticleCatalog
        //        .FirstOrDefaultAsync(f => f.ArticleNumber == article.ArticleNumber);

        //    return entry ?? await UpsertCatalogEntry(article);
        //}

        /// <summary>
        /// Reassigns the root (home) page to the specified article number and republish both old and new root pages.
        /// </summary>
        /// <param name="model">New home page request model.</param>
        /// <returns>Awaitable task.</returns>
        public async Task CreateHomePage(NewHomeViewModel model)
        {
            var oldHomeArticle = await DbContext.Articles
                .Where(w => w.UrlPath.ToLower() == "root").ToListAsync();
            if (oldHomeArticle.Count == 0)
            {
                throw new ArgumentException("No existing home page found.");
            }

            var newHomeArticle = await DbContext.Articles
                .Where(w => w.ArticleNumber == model.ArticleNumber).ToListAsync();
            if (newHomeArticle.Count == 0)
            {
                throw new ArgumentException("New home page not found.");
            }

            var newUrl = slugService.Normalize(oldHomeArticle.First().Title);
            foreach (var article in oldHomeArticle)
            {
                article.UrlPath = newUrl;
            }

            await DbContext.SaveChangesAsync();

            foreach (var article in newHomeArticle)
            {
                article.UrlPath = "root";
            }

            await DbContext.SaveChangesAsync();

            var oldHome = oldHomeArticle
                .OrderBy(o => o.VersionNumber)
                .LastOrDefault(f => f.Published.HasValue);
            var newHome = newHomeArticle
                .OrderBy(o => o.VersionNumber)
                .LastOrDefault(f => f.Published.HasValue);

            await PublishArticle(oldHome.Id, DateTimeOffset.UtcNow);
            await UpsertCatalogEntry(oldHome);

            await PublishArticle(newHome.Id, DateTimeOffset.UtcNow);
            await UpsertCatalogEntry(newHome);
        }

        /// <summary>
        /// Soft-deletes (trashes) all versions of an article and removes related published artifacts and catalog entry.
        /// </summary>
        /// <param name="articleNumber">Target article number.</param>
        /// <returns>Awaitable task.</returns>
        [Obsolete("Use TrashArticle instead. This method will be removed in version 3.0.", error: false)]
        public async Task DeleteArticle(int articleNumber)
        {
            var doomed = await DbContext.Articles
                .Where(w => w.ArticleNumber == articleNumber).ToListAsync();
            var url = doomed.FirstOrDefault()?.UrlPath;

            if (doomed == null || doomed.Count == 0)
            {
                throw new KeyNotFoundException($"Article number {articleNumber} not found.");
            }

            if (doomed.Exists(a => a.UrlPath.Equals("root", StringComparison.OrdinalIgnoreCase)))
            {
                throw new NotSupportedException("Cannot trash the home page. Replace it then delete.");
            }

            foreach (var article in doomed)
            {
                article.StatusCode = (int)StatusCodeEnum.Deleted;
            }

            var doomedPages = await DbContext.Pages
                .Where(w => w.ArticleNumber == articleNumber).ToListAsync();
            DbContext.Pages.RemoveRange(doomedPages);

            await DbContext.SaveChangesAsync();
            await DeleteCatalogEntry(articleNumber);
            DeleteStaticWebpage(url);
            await publishingService.WriteTocAsync();
        }

        /// <summary>
        /// Restores a previously deleted article (all versions) to active status, assigning new title if conflict exists.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="userId">User restoring the article (unused currently).</param>
        /// <returns>Awaitable task.</returns>
        public async Task RestoreArticle(int articleNumber, string userId)
        {
            var redeemed = await DbContext.Articles
                .Where(w => w.ArticleNumber == articleNumber).ToListAsync();
            if (redeemed == null || redeemed.Count == 0)
            {
                throw new KeyNotFoundException($"Article number {articleNumber} not found.");
            }

            var title = redeemed.First().Title.ToLower();
            if (await DbContext.Articles.Where(a =>
                    a.Title.ToLower() == title &&
                    a.ArticleNumber != articleNumber &&
                    a.StatusCode == (int)StatusCodeEnum.Deleted).CosmosAnyAsync())
            {
                var newTitle = title + " (" + await DbContext.Articles.CountAsync() + ")";
                var url = slugService.Normalize(newTitle);
                foreach (var article in redeemed)
                {
                    article.Title = newTitle;
                    article.UrlPath = url;
                    article.StatusCode = (int)StatusCodeEnum.Active;
                    article.Published = null;
                }
            }
            else
            {
                foreach (var article in redeemed)
                {
                    article.StatusCode = (int)StatusCodeEnum.Active;
                    article.Published = null;
                }
            }

            var sample = redeemed.First();
            DbContext.ArticleCatalog.Add(new CatalogEntry
            {
                ArticleNumber = sample.ArticleNumber,
                Published = null,
                Status = "Active",
                Title = sample.Title,
                Updated = DateTimeOffset.UtcNow,
                UrlPath = sample.UrlPath
            });
            await DbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Publishes the specified article version (unpublishing others), updates catalog, and refreshes published artifacts.
        /// </summary>
        /// <param name="articleId">Article row ID.</param>
        /// <param name="dateTime">Optional explicit publish time (UTC); if null current time is used.</param>
        /// <returns>List of CDN purge results (empty if none).</returns>
        public async Task<List<CdnResult>> PublishArticle(Guid articleId, DateTimeOffset? dateTime)
        {
            var article = await DbContext.Articles.FirstOrDefaultAsync(a => a.Id == articleId);
            if (article == null)
            {
                return new List<CdnResult>();
            }

            article.Published = dateTime ?? clock.UtcNow;

            var cdnResults = await publishingService.PublishAsync(article);
            await UpsertCatalogEntry(article);

            return cdnResults;
        }

        /// <summary>
        /// Retrieves (or creates) cached author info for a given user id.
        /// </summary>
        /// <param name="userId">User identifier.</param>
        /// <returns>Author info or null if user not found.</returns>
        private async Task<AuthorInfo> GetAuthorInfoForUserId(Guid userId)
        {
            var key = userId.ToString();
            var cacheKey = "authorinfo:" + key;
            if (localCache.TryGetValue(cacheKey, out AuthorInfo cached))
            {
                return cached;
            }

            var existing = await DbContext.AuthorInfos.FirstOrDefaultAsync(a => a.Id == key);
            if (existing == null)
            {
                var identity = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == key);
                if (identity == null)
                {
                    return null;
                }

                existing = new AuthorInfo
                {
                    Id = key,
                    AuthorName = identity.UserName ?? identity.Email ?? key,
                    AuthorDescription = string.Empty
                };
                DbContext.AuthorInfos.Add(existing);
                await DbContext.SaveChangesAsync();
            }

            localCache.Set(cacheKey, existing, TimeSpan.FromMinutes(10));
            return existing;
        }

        /// <summary>
        /// Deletes a static HTML page artifact if static mode is enabled (except under /pub which is protected).
        /// </summary>
        /// <param name="filePath">File path or slug (root -> index.html).</param>
        private void DeleteStaticWebpage(string filePath)
        {
            if (!settings.StaticWebPages)
            {
                return;
            }

            if (filePath.StartsWith("/pub", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Cannot remove web page from path /pub.");
            }

            filePath = filePath.Equals("root", StringComparison.OrdinalIgnoreCase) ? "/index.html" : filePath;
            storageContext.DeleteFile(filePath);
        }

        /// <summary>
        /// Creates or replaces a catalog entry for the supplied article based on current top version state.
        /// Generates an introduction if missing.
        /// </summary>
        /// <param name="article">Article entity reference.</param>
        /// <returns>Up-to-date catalog entry.</returns>
        private async Task<CatalogEntry> UpsertCatalogEntry(Article article)
        {
            var lastVersion = await DbContext.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(o => o.VersionNumber)
                .FirstOrDefaultAsync();

            var userId = lastVersion?.UserId ?? article.UserId;
            var authorInfo = await GetAuthorInfoForUserId(Guid.Parse(userId));

            if (string.IsNullOrWhiteSpace(article.Introduction))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(lastVersion?.Content))
                    {
                        var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                        htmlDoc.LoadHtml(lastVersion.Content);
                        var paragraphs = htmlDoc.DocumentNode.SelectNodes("//p");
                        var first = paragraphs?
                            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.InnerText));
                        if (first != null)
                        {
                            var intro = first.InnerText.Trim();
                            article.Introduction = intro.Length > 512 ? intro[..512] : intro;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error parsing article content during catalog entry.");
                }
            }

            var oldEntry = await DbContext.ArticleCatalog
                .FirstOrDefaultAsync(f => f.ArticleNumber == article.ArticleNumber);
            if (oldEntry != null)
            {
                DbContext.ArticleCatalog.Remove(oldEntry);
                // Don't save yet - will save after adding new entry
            }

            var entry = new CatalogEntry
            {
                ArticleNumber = article.ArticleNumber,
                BannerImage = article.BannerImage,
                Published = article.Published,
                Status = article.StatusCode == (int)StatusCodeEnum.Active ? "Active" : "Inactive",
                Title = article.Title,
                Updated = article.Updated,
                UrlPath = article.UrlPath,
                TemplateId = article.TemplateId,
                AuthorInfo = authorInfo == null ? string.Empty : JsonConvert.SerializeObject(authorInfo).Replace("\"", "'"),
                Introduction = article.Introduction,
                BlogKey = article.BlogKey,
            };

            DbContext.ArticleCatalog.Add(entry);
            await DbContext.SaveChangesAsync(); // Single SaveChangesAsync for both remove and add
            return entry;
        }

        /// <summary>
        /// Deletes a catalog entry (if it exists) for a logical article number.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Awaitable task.</returns>
        private async Task DeleteCatalogEntry(int articleNumber)
        {
            var catalogEntry = await DbContext.ArticleCatalog
                .FirstOrDefaultAsync(f => f.ArticleNumber == articleNumber);
            if (catalogEntry != null)
            {
                DbContext.ArticleCatalog.Remove(catalogEntry);
                await DbContext.SaveChangesAsync();
            }
        }
    }
}