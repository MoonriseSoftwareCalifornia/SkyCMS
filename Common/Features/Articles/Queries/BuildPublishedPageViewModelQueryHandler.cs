// <copyright file="BuildPublishedPageViewModelQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Features.Articles.Shared;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Handler for building an ArticleViewModel from a PublishedPage entity.
/// Delegates to IArticleViewModelBuilder service for layout integration and OG metadata generation.
/// </summary>
/// <param name="viewModelBuilder">Service for building article view models.</param>
public class BuildPublishedPageViewModelQueryHandler(IArticleViewModelBuilder viewModelBuilder): IQueryHandler<BuildPublishedPageViewModelQuery, ArticleViewModel>
{
    private readonly IArticleViewModelBuilder viewModelBuilder = viewModelBuilder ?? throw new ArgumentNullException(nameof(viewModelBuilder));

    /// <inheritdoc />
    public async Task<ArticleViewModel> HandleAsync(BuildPublishedPageViewModelQuery query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        return await viewModelBuilder.BuildFromPublishedPageAsync(
            query.publishedPage,
            query.languageCode,
            query.layoutCacheDuration,
            query.includeLayout);
    }
}
