// <copyright file="GetPublishedPageByUrlQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using Cosmos.Common.Features.Articles.Shared;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Handler for retrieving published pages by URL.
/// </summary>
public class GetPublishedPageByUrlQueryHandler : IQueryHandler<GetPublishedPageByUrlQuery, ArticleViewModel?>
{
    private readonly IPublishedPageQueryService publishedPageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetPublishedPageByUrlQueryHandler"/> class.
    /// </summary>
    /// <param name="publishedPageService">Service for querying published pages.</param>
    public GetPublishedPageByUrlQueryHandler(IPublishedPageQueryService publishedPageService)
    {
        this.publishedPageService = publishedPageService;
    }

    /// <inheritdoc />
    public Task<ArticleViewModel?> HandleAsync(
        GetPublishedPageByUrlQuery query,
        CancellationToken cancellationToken = default)
    {
        return publishedPageService.GetPublishedPageByUrlAsync(
            query.UrlPath,
            query.Lang,
            query.CacheSpan,
            query.LayoutCache,
            query.IncludeLayout);
    }
}
