// <copyright file="GetArticleByUrlQueryHandler.cs" company="Moonrise Software, LLC">
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
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Handler for retrieving latest articles by URL for editor usage.
/// </summary>
public class GetArticleByUrlQueryHandler : IQueryHandler<GetArticleByUrlQuery, ArticleViewModel?>
{
    private readonly ApplicationDbContext dbContext;
    private readonly ArticleLogic articleLogic;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticleByUrlQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="memoryCache">Memory cache.</param>
    /// <param name="configuration">Configuration for publisher settings.</param>
    public GetArticleByUrlQueryHandler(
        ApplicationDbContext dbContext,
        IMemoryCache memoryCache,
        IConfiguration configuration)
    {
        this.dbContext = dbContext;

        var publisherUrl = configuration.GetValue<string>("CosmosPublisherUrl") ?? string.Empty;
        var blobPublicUrl = configuration.GetValue<string>("BlobPublicUrl")
            ?? configuration.GetValue<string>("AzureBlobStorageEndPoint")
            ?? string.Empty;

        articleLogic = new ArticleLogic(dbContext, memoryCache, publisherUrl, blobPublicUrl, isEditor: true);
    }

    /// <inheritdoc />
    public async Task<ArticleViewModel?> HandleAsync(
        GetArticleByUrlQuery query,
        CancellationToken cancellationToken = default)
    {
        var urlPath = query.UrlPath;
        if (string.IsNullOrWhiteSpace(urlPath) || urlPath.Equals("/"))
        {
            urlPath = "root";
        }

        urlPath = urlPath.TrimStart('/');
        var deletedEnum = (int)StatusCodeEnum.Deleted;
        var entity = await dbContext.Articles
            .Where(a => a.UrlPath == urlPath && a.StatusCode != deletedEnum)
            .OrderByDescending(a => a.VersionNumber)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return await articleLogic.BuildArticleViewModelAsync(entity, "en-US");
    }
}
