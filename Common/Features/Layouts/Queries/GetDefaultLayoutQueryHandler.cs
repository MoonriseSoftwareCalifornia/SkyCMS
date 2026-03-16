// <copyright file="GetDefaultLayoutQueryHandler.cs" company="Moonrise Software, LLC">
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
using Cosmos.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Handler for retrieving the default layout with optional caching.
/// </summary>
/// <param name="dbContext">Database context.</param>
/// <param name="memoryCache">Optional memory cache for layout caching.</param>
public class GetDefaultLayoutQueryHandler(
    IApplicationDbContext dbContext,
    IMemoryCache? memoryCache = null) : IQueryHandler<GetDefaultLayoutQuery, LayoutViewModel>
{
    private readonly IApplicationDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly IMemoryCache? memoryCache = memoryCache;

    /// <inheritdoc/>
    public async Task<LayoutViewModel> HandleAsync(
        GetDefaultLayoutQuery query,
        CancellationToken cancellationToken = default)
    {
        // Try cache first if caching is enabled
        if (memoryCache != null && query.CacheDuration != null)
        {
            if (memoryCache.TryGetValue(CacheKeys.DefaultLayout, out LayoutViewModel? cachedLayout) && cachedLayout != null)
            {
                return cachedLayout;
            }
        }

        // Fetch from database
        var now = DateTimeOffset.UtcNow;
        var entity = await dbContext.Layouts
            .Where(l => l.IsDefault && l.Published <= now)
            .OrderBy(l => l.Version)
            .AsNoTracking()
            .LastOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            // Return null instead of throwing - let calling code decide how to handle
            return null!;
        }

        var viewModel = new LayoutViewModel(entity);

        // Cache if caching is enabled
        if (memoryCache != null && query.CacheDuration != null)
        {
            memoryCache.Set(CacheKeys.DefaultLayout, viewModel, query.CacheDuration.Value);
        }

        return viewModel;
    }
}
