// <copyright file="GetLastPublishedDateQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Constants;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Handler for retrieving the last published date of an article with optional caching.
/// </summary>
public class GetLastPublishedDateQueryHandler : IQueryHandler<GetLastPublishedDateQuery, DateTimeOffset?>
{
    private readonly IApplicationDbContext dbContext;
    private readonly IMemoryCache? memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLastPublishedDateQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="memoryCache">Optional memory cache for caching last published dates.</param>
    public GetLastPublishedDateQueryHandler(IApplicationDbContext dbContext, IMemoryCache? memoryCache = null)
    {
        this.dbContext = dbContext;
        this.memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> HandleAsync(
        GetLastPublishedDateQuery query,
        CancellationToken cancellationToken = default)
    {
        // Try cache first if caching is enabled
        if (memoryCache != null && query.CacheDuration != null)
        {
            if (memoryCache.TryGetValue(CacheKeys.LastPublished(query.ArticleNumber), out DateTimeOffset? cachedDate))
            {
                return cachedDate;
            }
        }

        // Fetch from database
        var publishedDate = await dbContext.Articles
            .Where(a => a.ArticleNumber == query.ArticleNumber && a.Published.HasValue)
            .OrderByDescending(a => a.Published)
            .Select(a => a.Published)
            .FirstOrDefaultAsync(cancellationToken);

        // Cache if caching is enabled
        if (memoryCache != null && query.CacheDuration != null)
        {
            memoryCache.Set(CacheKeys.LastPublished(query.ArticleNumber), publishedDate, query.CacheDuration.Value);
        }

        return publishedDate;
    }
}
