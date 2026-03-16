// <copyright file="GetArticleCatalogEntryQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Handler for retrieving article catalog entries.
/// </summary>
public class GetArticleCatalogEntryQueryHandler : IQueryHandler<GetArticleCatalogEntryQuery, CatalogEntry?>
{
    private readonly ApplicationDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticleCatalogEntryQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    public GetArticleCatalogEntryQueryHandler(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<CatalogEntry?> HandleAsync(
        GetArticleCatalogEntryQuery query,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ArticleCatalog
            .FirstOrDefaultAsync(a => a.ArticleNumber == query.ArticleNumber, cancellationToken);
    }
}
