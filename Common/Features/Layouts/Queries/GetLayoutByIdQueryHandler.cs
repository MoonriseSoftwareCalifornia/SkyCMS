// <copyright file="GetLayoutByIdQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Layouts.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Constants;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Handler for retrieving a layout by its unique identifier.
/// </summary>
/// <param name="dbContext">Database context for layout queries.</param>
/// <param name="memoryCache">Optional memory cache for caching layout results.</param>
public class GetLayoutByIdQueryHandler(IApplicationDbContext dbContext, IMemoryCache? memoryCache = null): IQueryHandler<GetLayoutByIdQuery, Layout?>
{
    private readonly IApplicationDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly IMemoryCache? memoryCache = memoryCache;

    /// <inheritdoc />
    public async Task<Layout?> HandleAsync(GetLayoutByIdQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (query.layoutId == Guid.Empty)
        {
            return null;
        }

        // Check cache first if caching is enabled
        if (memoryCache != null && query.CacheDuration.HasValue)
        {
            if (memoryCache.TryGetValue<Layout?>(CacheKeys.Layout(query.layoutId), out var cachedLayout))
            {
                return cachedLayout;
            }

            var layout = await dbContext.Layouts
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == query.layoutId, cancellationToken);

            // Cache the result (including null to avoid repeated queries for non-existent layouts)
            memoryCache.Set(CacheKeys.Layout(query.layoutId), layout, query.CacheDuration.Value);

            return layout;
        }

        // No caching - direct query
        return await dbContext.Layouts
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == query.layoutId, cancellationToken);
    }
}
