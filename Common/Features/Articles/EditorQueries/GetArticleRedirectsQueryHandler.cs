// <copyright file="GetArticleRedirectsQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Constants;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Handler for retrieving article redirects with optional caching.
/// </summary>
public class GetArticleRedirectsQueryHandler : IQueryHandler<GetArticleRedirectsQuery, IEnumerable<RedirectItemViewModel>>
{
    private readonly IApplicationDbContext dbContext;
    private readonly IMemoryCache? memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticleRedirectsQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="memoryCache">Optional memory cache for caching redirect list.</param>
    public GetArticleRedirectsQueryHandler(IApplicationDbContext dbContext, IMemoryCache? memoryCache = null)
    {
        this.dbContext = dbContext;
        this.memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RedirectItemViewModel>> HandleAsync(
        GetArticleRedirectsQuery query,
        CancellationToken cancellationToken = default)
    {
        // Try cache first if caching is enabled
        if (memoryCache != null && query.CacheDuration != null)
        {
            if (memoryCache.TryGetValue(CacheKeys.ArticleRedirects, out IEnumerable<RedirectItemViewModel>? cachedRedirects) && cachedRedirects != null)
            {
                return cachedRedirects;
            }
        }

        // Fetch from database
        var redirects = await dbContext.Articles
            .Where(p => p.StatusCode == (int)StatusCodeEnum.Redirect)
            .Select(p => new RedirectItemViewModel
            {
                Id = p.Id,
                FromUrl = p.UrlPath,
                ToUrl = p.BannerImage,
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Cache if caching is enabled
        if (memoryCache != null && query.CacheDuration != null)
        {
            memoryCache.Set(CacheKeys.ArticleRedirects, redirects, query.CacheDuration.Value);
        }

        return redirects;
    }
}
