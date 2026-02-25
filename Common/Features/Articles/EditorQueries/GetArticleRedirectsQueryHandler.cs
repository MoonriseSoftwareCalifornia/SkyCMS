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
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Handler for retrieving article redirects.
/// </summary>
public class GetArticleRedirectsQueryHandler : IQueryHandler<GetArticleRedirectsQuery, IEnumerable<RedirectItemViewModel>>
{
    private readonly ApplicationDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticleRedirectsQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    public GetArticleRedirectsQueryHandler(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RedirectItemViewModel>> HandleAsync(
        GetArticleRedirectsQuery query,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Articles
            .Where(p => p.StatusCode == (int)StatusCodeEnum.Redirect)
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
