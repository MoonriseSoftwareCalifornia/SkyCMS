// <copyright file="GetSitemapQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Sitemap.Queries;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Constants;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using X.Web.Sitemap;

/// <summary>
/// Handler for generating website sitemap.
/// </summary>
/// <param name="dbContext">Database context.</param>
/// <param name="memoryCache">Optional memory cache for caching sitemap results.</param>
public class GetSitemapQueryHandler(IApplicationDbContext dbContext, IMemoryCache? memoryCache = null) : IQueryHandler<GetSitemapQuery, X.Web.Sitemap.Sitemap>
{
    private readonly IApplicationDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly IMemoryCache? memoryCache = memoryCache;

    /// <inheritdoc/>
    public async Task<X.Web.Sitemap.Sitemap> HandleAsync(GetSitemapQuery query, CancellationToken cancellationToken = default)
    {
        // Check cache first if caching is enabled
        if (memoryCache != null && query.CacheDuration.HasValue)
        {
            if (memoryCache.TryGetValue<X.Web.Sitemap.Sitemap>(CacheKeys.Sitemap, out var cachedSitemap))
            {
                return cachedSitemap;
            }
        }

        var publicUrl = "/";
        var dt = DateTimeOffset.UtcNow.AddMinutes(10); // slight future window to allow near-future scheduled pages

        var items = await dbContext.ArticleCatalog
            .AsNoTracking()
            .Where(t => t.Published <= dt)
            .Select(t => new
            {
                t.UrlPath,
                t.Title,
                t.Published,
                t.Updated,
                t.BannerImage
            })
            .ToListAsync(cancellationToken);

        var home = items.FirstOrDefault(f => f.UrlPath == "root");
        var others = items.Where(w => w.UrlPath != "root").ToList();

        var sitemap = new X.Web.Sitemap.Sitemap();

        // Add home page
        if (home != null)
        {
            var timestamp = home.Updated != default(DateTimeOffset) ? home.Updated
                : home.Published.HasValue && home.Published.Value != default(DateTimeOffset) ? home.Published.Value
                : DateTimeOffset.UtcNow;

            var homeUrl = new Url
            {
                Location = publicUrl,
                TimeStamp = timestamp.DateTime,
                ChangeFrequency = ChangeFrequency.Weekly,
                Priority = 1.0
            };

            if (!string.IsNullOrEmpty(home.BannerImage))
            {
                homeUrl.Images = new System.Collections.Generic.List<Image>
                {
                    new Image
                    {
                        Location = home.BannerImage.StartsWith("http")
                            ? home.BannerImage
                            : publicUrl.TrimEnd('/') + "/" + home.BannerImage.TrimStart('/'),
                        Title = home.Title
                    }
                };
            }

            sitemap.Add(homeUrl);
        }

        // Add other pages
        foreach (var other in others)
        {
            var timestamp = other.Updated != default(DateTimeOffset) ? other.Updated
                : other.Published.HasValue && other.Published.Value != default(DateTimeOffset) ? other.Published.Value
                : DateTimeOffset.UtcNow;

            var url = new Url
            {
                Location = publicUrl.TrimEnd('/') + "/" + other.UrlPath.TrimStart('/'),
                TimeStamp = timestamp.DateTime,
                ChangeFrequency = ChangeFrequency.Weekly,
                Priority = 0.5
            };

            if (!string.IsNullOrEmpty(other.BannerImage))
            {
                url.Images = new System.Collections.Generic.List<Image>
                {
                    new Image
                    {
                        Location = other.BannerImage.StartsWith("http")
                            ? other.BannerImage
                            : publicUrl.TrimEnd('/') + "/" + other.BannerImage.TrimStart('/'),
                        Title = other.Title
                    }
                };
            }

            sitemap.Add(url);
        }

        // Cache the result if caching is enabled
        if (memoryCache != null && query.CacheDuration.HasValue)
        {
            memoryCache.Set(CacheKeys.Sitemap, sitemap, query.CacheDuration.Value);
        }

        return sitemap;
    }
}
