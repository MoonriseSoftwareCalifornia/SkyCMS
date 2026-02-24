// <copyright file="GetPublishedPageHeaderByUrlQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Handler for retrieving published page headers by URL.
/// </summary>
public class GetPublishedPageHeaderByUrlQueryHandler : IQueryHandler<GetPublishedPageHeaderByUrlQuery, ArticleViewModel?>
{
    private readonly ArticleLogic articleLogic;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetPublishedPageHeaderByUrlQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="memoryCache">Memory cache.</param>
    /// <param name="configuration">Configuration for publisher settings.</param>
    public GetPublishedPageHeaderByUrlQueryHandler(
        ApplicationDbContext dbContext,
        IMemoryCache memoryCache,
        IConfiguration configuration)
    {
        var publisherUrl = configuration.GetValue<string>("CosmosPublisherUrl") ?? string.Empty;
        var blobPublicUrl = configuration.GetValue<string>("BlobPublicUrl")
            ?? configuration.GetValue<string>("AzureBlobStorageEndPoint")
            ?? string.Empty;

        articleLogic = new ArticleLogic(dbContext, memoryCache, publisherUrl, blobPublicUrl);
    }

    /// <inheritdoc />
    public Task<ArticleViewModel?> HandleAsync(
        GetPublishedPageHeaderByUrlQuery query,
        CancellationToken cancellationToken = default)
    {
        return articleLogic.GetPublishedPageHeaderByUrl(query.UrlPath);
    }
}
