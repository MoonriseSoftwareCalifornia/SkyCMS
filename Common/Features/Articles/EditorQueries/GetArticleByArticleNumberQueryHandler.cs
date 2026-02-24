// <copyright file="GetArticleByArticleNumberQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

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
/// Handler for retrieving articles by article number for editor usage.
/// </summary>
public class GetArticleByArticleNumberQueryHandler : IQueryHandler<GetArticleByArticleNumberQuery, ArticleViewModel?>
{
    private readonly ApplicationDbContext dbContext;
    private readonly ArticleLogic articleLogic;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticleByArticleNumberQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="memoryCache">Memory cache.</param>
    /// <param name="configuration">Configuration for publisher settings.</param>
    public GetArticleByArticleNumberQueryHandler(
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
        GetArticleByArticleNumberQuery query,
        CancellationToken cancellationToken = default)
    {
        var deletedEnum = (int)StatusCodeEnum.Deleted;
        var baseQuery = dbContext.Articles
            .AsNoTracking()
            .Where(a => a.ArticleNumber == query.ArticleNumber && a.StatusCode != deletedEnum);

        var entity = query.VersionNumber.HasValue
            ? await baseQuery.FirstOrDefaultAsync(a => a.VersionNumber == query.VersionNumber.Value, cancellationToken)
            : await baseQuery.OrderByDescending(a => a.VersionNumber).FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return await articleLogic.BuildArticleViewModelAsync(entity, "en-US");
    }
}
