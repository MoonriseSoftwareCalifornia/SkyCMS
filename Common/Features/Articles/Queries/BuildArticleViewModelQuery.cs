// <copyright file="BuildArticleViewModelQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Query to build an ArticleViewModel from an Article entity.
/// Replaces ArticleLogic.BuildArticleViewModel(Article) method.
/// </summary>
/// <param name="article">Source article entity.</param>
/// <param name="languageCode">Language code (e.g., "en-US").</param>
/// <param name="includeLayout">Whether to include layout information.</param>
public record BuildArticleViewModelQuery(
    Article article,
    string languageCode = "en",
    bool includeLayout = true): IQuery<ArticleViewModel>;
