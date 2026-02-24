// <copyright file="GetTableOfContentsQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Features.Articles.Shared;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Handler for retrieving table of contents listings.
/// </summary>
public class GetTableOfContentsQueryHandler : IQueryHandler<GetTableOfContentsQuery, TableOfContents>
{
    private readonly IArticleCatalogQueryService catalogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTableOfContentsQueryHandler"/> class.
    /// </summary>
    /// <param name="catalogService">Service for querying article catalog.</param>
    public GetTableOfContentsQueryHandler(IArticleCatalogQueryService catalogService)
    {
        this.catalogService = catalogService;
    }

    /// <inheritdoc />
    public Task<TableOfContents> HandleAsync(
        GetTableOfContentsQuery query,
        CancellationToken cancellationToken = default)
    {
        return catalogService.GetTableOfContentsAsync(
            query.Page,
            query.PageNo,
            query.PageSize,
            query.OrderByPublishedDate);
    }
}
