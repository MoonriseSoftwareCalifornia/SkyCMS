// <copyright file="PublishedPageQueryService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Shared;

using System;
using System.Linq;
using System.Threading.Tasks;
using Cosmos.Cms.Common;
using Cosmos.Common.Data;
using Cosmos.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Implementation of IPublishedPageQueryService for querying published page snapshots.
/// Handles retrieval of PublishedPage entities with view model conversion and caching.
/// </summary>
/// <remarks>
/// If the requested URL corresponds to the root of a blog stream, this service will
/// automatically fetch the latest blog stream entry instead of the root page. This
/// allows for seamless handling of blog stream URLs while still supporting regular pages.
/// </remarks>
public class PublishedPageQueryService : IPublishedPageQueryService
{
    private readonly ApplicationDbContext dbContext;
    private readonly IMemoryCache memoryCache;
    private readonly IArticleViewModelBuilder viewModelBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishedPageQueryService"/> class.
    /// </summary>
    /// <param name="dbContext">Database context for querying published pages.</param>
    /// <param name="memoryCache">Optional memory cache for caching view models (can be null to disable caching).</param>
    /// <param name="viewModelBuilder">Service for building ArticleViewModel from PublishedPage entities.</param>
    public PublishedPageQueryService(
        ApplicationDbContext dbContext,
        IMemoryCache memoryCache,
        IArticleViewModelBuilder viewModelBuilder)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.memoryCache = memoryCache;
        this.viewModelBuilder = viewModelBuilder ?? throw new ArgumentNullException(nameof(viewModelBuilder));
    }

    /// <inheritdoc />
    public async Task<ArticleViewModel?> GetPublishedPageByUrlAsync(
        string urlPath,
        string lang = "",
        TimeSpan? cacheSpan = null,
        TimeSpan? layoutCacheDuration = null,
        bool includeLayout = true)
    {
        urlPath = urlPath?.ToLower().Trim(new char[] { ' ', '/' });
        if (string.IsNullOrEmpty(urlPath) || urlPath.Trim() == "/")
        {
            urlPath = "root";
        }

        // Try cache first if caching is enabled
        if (memoryCache != null && cacheSpan != null)
        {
            var cacheKey = $"{urlPath}-{lang}-{includeLayout}";
            if (memoryCache.TryGetValue(cacheKey, out ArticleViewModel? cachedModel))
            {
                return cachedModel;
            }

            // Not in cache, fetch from database
            var dt = DateTimeOffset.UtcNow;
            var entity = await dbContext.Pages
                .Where(p => p.UrlPath == urlPath && p.Published.HasValue && p.Published <= dt)
                .OrderByDescending(p => p.VersionNumber)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (entity == null)
            {
                return null;
            }

            // Check if we hit the root of a blog stream.
            if (entity.ArticleType == (int)ArticleType.BlogStream)
            {
                var blogKey = entity.BlogKey;

                // If so, we need to fetch the latest blog stream entry instead.
                var blogStreamEntry = await dbContext.Pages
                    .Where(p => p.BlogKey == blogKey)
                    .OrderByDescending(p => p.Published)
                    .ThenByDescending(p => p.VersionNumber)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (blogStreamEntry != null)
                {
                    entity = blogStreamEntry;
                }
            }

            var model = await viewModelBuilder.BuildFromPublishedPageAsync(
                entity,
                lang,
                layoutCacheDuration,
                includeLayout);

            memoryCache.Set(cacheKey, model, cacheSpan.Value);
            return model;
        }

        // Cache disabled, fetch directly from database
        {
            var dt = DateTimeOffset.UtcNow;
            var publishedPage = await dbContext.Pages
                .Where(p => p.UrlPath == urlPath && p.Published.HasValue && p.Published <= dt)
                .OrderByDescending(p => p.VersionNumber)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (publishedPage == null)
            {
                return null;
            }

            return await viewModelBuilder.BuildFromPublishedPageAsync(
                publishedPage,
                lang,
                layoutCacheDuration,
                includeLayout);
        }
    }

    /// <inheritdoc />
    public async Task<ArticleViewModel?> GetPublishedPageHeaderByUrlAsync(string urlPath)
    {
        urlPath = urlPath?.ToLower().Trim(new char[] { ' ', '/' });
        if (string.IsNullOrEmpty(urlPath) || urlPath.Trim() == "/")
        {
            urlPath = "root";
        }

        var dt = DateTimeOffset.UtcNow;
        return await dbContext.Pages
            .Where(p => p.UrlPath == urlPath && p.Published.HasValue && p.Published <= dt)
            .Select(p => new ArticleViewModel
            {
                ArticleNumber = p.ArticleNumber,
                Id = p.Id,
                Expires = p.Expires,
                Updated = p.Updated,
                VersionNumber = p.VersionNumber
            })
            .OrderByDescending(p => p.VersionNumber)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }
}
