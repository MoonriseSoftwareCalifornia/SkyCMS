// <copyright file="GetLastPublishedDateQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Handler for retrieving the last published date of an article.
/// </summary>
public class GetLastPublishedDateQueryHandler : IQueryHandler<GetLastPublishedDateQuery, DateTimeOffset?>
{
    private readonly ApplicationDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLastPublishedDateQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    public GetLastPublishedDateQueryHandler(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> HandleAsync(
        GetLastPublishedDateQuery query,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Articles
            .Where(a => a.ArticleNumber == query.ArticleNumber && a.Published.HasValue)
            .OrderByDescending(a => a.Published)
            .Select(a => a.Published)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
