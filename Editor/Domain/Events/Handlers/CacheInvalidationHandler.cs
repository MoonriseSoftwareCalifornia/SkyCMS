// <copyright file="CacheInvalidationHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Domain.Events.Handlers
{
    using System.Threading.Tasks;
    using Cosmos.Common.Constants;
    using Cosmos.Common.Services.Caching;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Handles cache invalidation in response to domain events.
    /// Decouples cache management from business services using event-driven architecture.
    /// </summary>
    public sealed class CacheInvalidationHandler :
        IDomainEventHandler<ArticlePublishedEvent>,
        IDomainEventHandler<ArticleUnpublishedEvent>,
        IDomainEventHandler<LayoutPublishedEvent>,
        IDomainEventHandler<CatalogEntryUpdatedEvent>,
        IDomainEventHandler<CatalogEntryDeletedEvent>
    {
        private readonly ICacheService<object> cacheService;
        private readonly ILogger<CacheInvalidationHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheInvalidationHandler"/> class.
        /// </summary>
        /// <param name="cacheService">Tenant-aware cache service for invalidation operations.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        public CacheInvalidationHandler(
            ICacheService<object> cacheService,
            ILogger<CacheInvalidationHandler> logger)
        {
            this.cacheService = cacheService;
            this.logger = logger;
        }

        /// <summary>
        /// Handles article published event by invalidating article-related caches.
        /// </summary>
        /// <param name="event">The article published event.</param>
        /// <returns>A completed task.</returns>
        public Task HandleAsync(ArticlePublishedEvent @event)
        {
            logger.LogDebug(
                "Invalidating article caches for article {ArticleNumber} (Published)",
                @event.ArticleNumber);

            InvalidateArticleCaches(@event.ArticleNumber);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles article unpublished event by invalidating article-related caches.
        /// </summary>
        /// <param name="event">The article unpublished event.</param>
        /// <returns>A completed task.</returns>
        public Task HandleAsync(ArticleUnpublishedEvent @event)
        {
            logger.LogDebug(
                "Invalidating article caches for article {ArticleNumber} (Unpublished)",
                @event.ArticleNumber);

            InvalidateArticleCaches(@event.ArticleNumber);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles layout published event by invalidating layout-related caches.
        /// </summary>
        /// <param name="event">The layout published event.</param>
        /// <returns>A completed task.</returns>
        public Task HandleAsync(LayoutPublishedEvent @event)
        {
            logger.LogDebug(
                "Invalidating layout caches for layout {LayoutId} (Published as Default)",
                @event.LayoutId);

            cacheService.Remove(CacheKeys.Layout(@event.LayoutId));
            cacheService.Remove(CacheKeys.DefaultLayoutExists);
            cacheService.Remove(CacheKeys.DefaultLayout);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles catalog entry updated event by invalidating catalog cache.
        /// </summary>
        /// <param name="event">The catalog entry updated event.</param>
        /// <returns>A completed task.</returns>
        public Task HandleAsync(CatalogEntryUpdatedEvent @event)
        {
            logger.LogDebug(
                "Invalidating catalog cache for article {ArticleNumber} (Catalog Updated)",
                @event.ArticleNumber);

            cacheService.Remove(CacheKeys.ArticleCatalog(@event.ArticleNumber));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles catalog entry deleted event by invalidating catalog cache.
        /// </summary>
        /// <param name="event">The catalog entry deleted event.</param>
        /// <returns>A completed task.</returns>
        public Task HandleAsync(CatalogEntryDeletedEvent @event)
        {
            logger.LogDebug(
                "Invalidating catalog cache for article {ArticleNumber} (Catalog Deleted)",
                @event.ArticleNumber);

            cacheService.Remove(CacheKeys.ArticleCatalog(@event.ArticleNumber));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Invalidates all article-related caches for the specified article number.
        /// </summary>
        /// <param name="articleNumber">The article number.</param>
        private void InvalidateArticleCaches(int articleNumber)
        {
            cacheService.Remove(CacheKeys.ArticleCatalog(articleNumber));
            cacheService.Remove(CacheKeys.LastPublished(articleNumber));
            cacheService.Remove(CacheKeys.Sitemap);
            cacheService.Remove(CacheKeys.ArticleRedirects);
        }
    }
}
