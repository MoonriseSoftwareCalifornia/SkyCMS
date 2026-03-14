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
/// <param name="PublishedPage">Source published page entity.</param>
/// <param name="LanguageCode">Language code (e.g., "en-US").</param>
/// <param name="LayoutCacheDuration">Optional layout cache duration.</param>
/// <param name="IncludeLayout">Whether to include layout information.</param>
public record BuildPublishedPageViewModelQuery(
    PublishedPage PublishedPage,
    string LanguageCode = "en",
    TimeSpan? LayoutCacheDuration = null,
    bool IncludeLayout = true) : IQuery<ArticleViewModel>;
