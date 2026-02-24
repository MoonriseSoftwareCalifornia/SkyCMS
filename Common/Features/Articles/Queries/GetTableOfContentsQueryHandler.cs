// <copyright file="GetTableOfContentsQueryHandler.cs" company="Moonrise Software, LLC">
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
/// Handler for retrieving table of contents listings.
/// </summary>
public class GetTableOfContentsQueryHandler : IQueryHandler<GetTableOfContentsQuery, TableOfContents>
{
    private readonly ArticleLogic articleLogic;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTableOfContentsQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="memoryCache">Memory cache.</param>
    /// <param name="configuration">Configuration for publisher settings.</param>
    public GetTableOfContentsQueryHandler(
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
    public Task<TableOfContents> HandleAsync(
        GetTableOfContentsQuery query,
        CancellationToken cancellationToken = default)
    {
        return articleLogic.GetTableOfContents(
            query.Page,
            query.PageNo,
            query.PageSize,
            query.OrderByPublishedDate);
    }
}
