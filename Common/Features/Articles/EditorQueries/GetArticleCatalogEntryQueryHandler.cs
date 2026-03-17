// <copyright file="GetArticleCatalogEntryQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using System;
using Cosmos.Common.Constants;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Handler for retrieving article catalog entries.
/// </summary>
public class GetArticleCatalogEntryQueryHandler : IQueryHandler<GetArticleCatalogEntryQuery, CatalogEntry?>
{
    private readonly ApplicationDbContext dbContext;
    private readonly IMemoryCache? memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticleCatalogEntryQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="memoryCache">Optional memory cache for article catalog caching.</param>
    public GetArticleCatalogEntryQueryHandler(ApplicationDbContext dbContext, IMemoryCache? memoryCache = null)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<CatalogEntry?> HandleAsync(
        GetArticleCatalogEntryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (memoryCache != null && query.CacheDuration != null)
        {
            var cacheKey = CacheKeys.ArticleCatalog(query.ArticleNumber);
            if (memoryCache.TryGetValue(cacheKey, out CatalogEntry? cachedEntry))
            {
                return cachedEntry;
            }

            var entry = await dbContext.ArticleCatalog
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == query.ArticleNumber, cancellationToken);

            memoryCache.Set(cacheKey, entry, query.CacheDuration.Value);
            return entry;
        }

        return await dbContext.ArticleCatalog
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ArticleNumber == query.ArticleNumber, cancellationToken);
    }
}
