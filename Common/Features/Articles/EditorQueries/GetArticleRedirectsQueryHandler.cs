// <copyright file="GetArticleRedirectsQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using Cosmos.Common.Constants;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Handler for retrieving article redirects.
/// </summary>
public class GetArticleRedirectsQueryHandler : IQueryHandler<GetArticleRedirectsQuery, IEnumerable<RedirectItemViewModel>>
{
    private readonly ApplicationDbContext dbContext;
    private readonly IMemoryCache? memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticleRedirectsQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="memoryCache">Optional memory cache for article redirects caching.</param>
    public GetArticleRedirectsQueryHandler(ApplicationDbContext dbContext, IMemoryCache? memoryCache = null)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RedirectItemViewModel>> HandleAsync(
        GetArticleRedirectsQuery query,
        CancellationToken cancellationToken = default)
    {
        var redirectStatusCode = (int)StatusCodeEnum.Redirect;

        if (memoryCache != null && query.CacheDuration != null)
        {
            var cacheKey = CacheKeys.ArticleRedirects;
            if (memoryCache.TryGetValue(cacheKey, out IEnumerable<RedirectItemViewModel>? cachedRedirects))
            {
                return cachedRedirects!;
            }

            var redirects = await dbContext.Articles
                .Where(p => p.StatusCode == redirectStatusCode)
                .Select(p => new RedirectItemViewModel
                {
                    Id = p.Id,
                    FromUrl = p.UrlPath,
                    ToUrl = p.BannerImage,
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            memoryCache.Set(cacheKey, redirects, query.CacheDuration.Value);
            return redirects;
        }

        return await dbContext.Articles
            .Where(p => p.StatusCode == redirectStatusCode)
            .Select(p => new RedirectItemViewModel
            {
                Id = p.Id,
                FromUrl = p.UrlPath,
                ToUrl = p.BannerImage,
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
