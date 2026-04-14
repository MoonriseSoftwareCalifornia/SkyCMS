// <copyright file="GetArticlesForUserQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Handler for retrieving all articles accessible to a user based on their roles and permissions.
/// </summary>
/// <param name="dbContext">Database context for querying articles and permissions.</param>
public class GetArticlesForUserQueryHandler(IApplicationDbContext dbContext): IQueryHandler<GetArticlesForUserQuery, List<TableOfContentsItem>>
{
    private readonly IApplicationDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    public async Task<List<TableOfContentsItem>> HandleAsync(GetArticlesForUserQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var userId = query.user.FindFirstValue(ClaimTypes.NameIdentifier);

        var objectIds = await dbContext.UserRoles
            .Where(w => w.UserId == userId)
            .Select(s => s.RoleId)
            .ToListAsync(cancellationToken);

        objectIds.Add(userId);

        var articleNumbers = await dbContext.ArticleCatalog
            .Where(
                w => w.ArticlePermissions.Any() == false ||
                w.ArticlePermissions.Any(a => objectIds.Contains(a.IdentityObjectId)))
            .Select(s => s.ArticleNumber)
            .ToArrayAsync(cancellationToken);

        var data = await dbContext.Pages
            .Where(w => articleNumbers.Contains(w.ArticleNumber))
            .Select(s => new TableOfContentsItem()
            {
                AuthorInfo = s.AuthorInfo,
                BannerImage = s.BannerImage,
                Published = s.Published.Value,
                Title = s.Title,
                Updated = s.Updated,
                UrlPath = s.UrlPath
            })
            .ToListAsync(cancellationToken);

        return data;
    }
}
