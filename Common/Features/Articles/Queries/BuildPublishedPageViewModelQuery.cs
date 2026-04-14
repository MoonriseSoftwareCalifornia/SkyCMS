// <copyright file="BuildPublishedPageViewModelQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Query to build an ArticleViewModel from a PublishedPage entity.
/// Replaces ArticleLogic.BuildArticleViewModel(PublishedPage) method.
/// </summary>
/// <param name="publishedPage">Source published page entity.</param>
/// <param name="languageCode">Language code (e.g., "en-US").</param>
/// <param name="layoutCacheDuration">Optional layout cache duration.</param>
/// <param name="includeLayout">Whether to include layout information.</param>
public record BuildPublishedPageViewModelQuery(
    PublishedPage publishedPage,
    string languageCode = "en",
    TimeSpan? layoutCacheDuration = null,
    bool includeLayout = true): IQuery<ArticleViewModel>;
