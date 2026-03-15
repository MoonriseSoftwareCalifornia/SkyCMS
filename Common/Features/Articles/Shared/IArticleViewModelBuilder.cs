// <copyright file="IArticleViewModelBuilder.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Shared;

using Cosmos.Common.Data;
using Cosmos.Common.Models;
using System;
using System.Threading.Tasks;

/// <summary>
/// Service for building ArticleViewModel instances from Article and PublishedPage entities.
/// Extracted from ArticleLogic to decouple CQRS query handlers from legacy logic classes.
/// </summary>
public interface IArticleViewModelBuilder
{
    /// <summary>
    /// Builds an ArticleViewModel from an Article entity.
    /// </summary>
    /// <param name="article">Source article entity.</param>
    /// <param name="lang">Language code (e.g., "en-US").</param>
    /// <param name="includeLayout">Whether to include layout information.</param>
    /// <returns>Populated ArticleViewModel.</returns>
    Task<ArticleViewModel> BuildFromArticleAsync(Article article, string lang, bool includeLayout = true);

    /// <summary>
    /// Builds an ArticleViewModel from a PublishedPage entity.
    /// </summary>
    /// <param name="publishedPage">Source published page entity.</param>
    /// <param name="lang">Language code (e.g., "en-US").</param>
    /// <param name="layoutCacheDuration">Optional layout cache duration.</param>
    /// <param name="includeLayout">Whether to include layout information.</param>
    /// <returns>Populated ArticleViewModel.</returns>
    Task<ArticleViewModel> BuildFromPublishedPageAsync(
        PublishedPage publishedPage,
        string lang,
        TimeSpan? layoutCacheDuration = null,
        bool includeLayout = true);
}
