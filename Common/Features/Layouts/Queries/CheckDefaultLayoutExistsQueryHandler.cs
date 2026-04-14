// <copyright file="CheckDefaultLayoutExistsQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Layouts.Queries;

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
/// Handler for checking if any default layout exists.
/// Useful for setup/initialization scenarios to determine if a default layout needs to be created.
/// </summary>
/// <param name="dbContext">Database context for layout queries.</param>
/// <param name="memoryCache">Optional memory cache for caching layout existence checks.</param>
public class CheckDefaultLayoutExistsQueryHandler(IApplicationDbContext dbContext, IMemoryCache? memoryCache = null): IQueryHandler<CheckDefaultLayoutExistsQuery, bool>
{
    private readonly IApplicationDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly IMemoryCache? memoryCache = memoryCache;

    /// <inheritdoc />
    public async Task<bool> HandleAsync(CheckDefaultLayoutExistsQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        // Check cache first if caching is enabled
        if (memoryCache != null && query.CacheDuration.HasValue)
        {
            if (memoryCache.TryGetValue<bool>(CacheKeys.DefaultLayoutExists, out var cachedResult))
            {
                return cachedResult;
            }

            var exists = await CheckOrHealDefaultAsync(cancellationToken);

            // Cache the result
            memoryCache.Set(CacheKeys.DefaultLayoutExists, exists, query.CacheDuration.Value);

            return exists;
        }

        // No caching - direct query
        return await CheckOrHealDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Fast-path check for an explicitly-defaulted published layout, with a
    /// self-healing fallback that promotes any published layout to default.
    /// </summary>
    private async Task<bool> CheckOrHealDefaultAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Fast path: layout already marked as default and published.
        var exists = await dbContext.Layouts
            .AsNoTracking()
            .Where(l => l.IsDefault && l.Published != null && l.Published <= now)
            .CosmosAnyAsync();

        if (exists)
        {
            return true;
        }

        // Self-healing fallback: find any actually-published layout and promote it to default.
        var layout = await dbContext.Layouts
            .Where(l => l.Published != null && l.Published <= now)
            .OrderBy(l => l.Version)
            .LastOrDefaultAsync(cancellationToken);

        if (layout != null)
        {
            layout.Published = now;
            layout.IsDefault = true;
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }
}
