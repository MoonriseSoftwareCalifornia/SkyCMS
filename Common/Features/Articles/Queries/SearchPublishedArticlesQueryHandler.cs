// <copyright file="SearchPublishedArticlesQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using Cosmos.Common.Features.Articles.Shared;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Handler for searching published articles.
/// </summary>
public class SearchPublishedArticlesQueryHandler : IQueryHandler<SearchPublishedArticlesQuery, List<TableOfContentsItem>>
{
    private readonly IArticleCatalogQueryService catalogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchPublishedArticlesQueryHandler"/> class.
    /// </summary>
    /// <param name="catalogService">Service for querying article catalog.</param>
    public SearchPublishedArticlesQueryHandler(IArticleCatalogQueryService catalogService)
    {
        this.catalogService = catalogService;
    }

    /// <inheritdoc />
    public Task<List<TableOfContentsItem>> HandleAsync(
        SearchPublishedArticlesQuery query,
        CancellationToken cancellationToken = default)
    {
        return catalogService.SearchAsync(query.Text);
    }
}
