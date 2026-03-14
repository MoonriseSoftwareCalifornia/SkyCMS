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
/// <param name="Article">Source article entity.</param>
/// <param name="LanguageCode">Language code (e.g., "en-US").</param>
/// <param name="IncludeLayout">Whether to include layout information.</param>
public record BuildArticleViewModelQuery(
    Article Article,
    string LanguageCode = "en",
    bool IncludeLayout = true) : IQuery<ArticleViewModel>;
