// <copyright file="GetArticleCatalogEntryQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Constants;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Handler for retrieving article catalog entries with optional caching support.
/// </summary>
/// <remarks>
/// This handler implements strategic caching for article metadata that changes infrequently.
/// Cache keys are scoped per article number and automatically invalidated on publish/unpublish operations.
/// </remarks>
public class GetArticleCatalogEntryQueryHandler : IQueryHandler<GetArticleCatalogEntryQuery, CatalogEntry?>
{
    private readonly IApplicationDbContext dbContext;
    private readonly IMemoryCache? memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticleCatalogEntryQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="memoryCache">Optional memory cache for catalog entry caching.</param>
    public GetArticleCatalogEntryQueryHandler(IApplicationDbContext dbContext, IMemoryCache? memoryCache = null)
    {
        this.dbContext = dbContext;
        this.memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<CatalogEntry?> HandleAsync(
        GetArticleCatalogEntryQuery query,
        CancellationToken cancellationToken = default)
    {
        // If caching is enabled and duration is specified, try cache first
        if (memoryCache != null && query.CacheDuration.HasValue)
        {
            if (memoryCache.TryGetValue<CatalogEntry?>(CacheKeys.ArticleCatalog(query.ArticleNumber), out var cachedEntry))
            {
                return cachedEntry;
            }

            // Fetch from database
            var entry = await dbContext.ArticleCatalog
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == query.ArticleNumber, cancellationToken);

            // Cache the result (even if null to avoid repeated DB hits for missing entries)
            memoryCache.Set(CacheKeys.ArticleCatalog(query.ArticleNumber), entry, query.CacheDuration.Value);

            return entry;
        }

        // No caching - direct database query
        return await dbContext.ArticleCatalog
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ArticleNumber == query.ArticleNumber, cancellationToken);
    }
}
