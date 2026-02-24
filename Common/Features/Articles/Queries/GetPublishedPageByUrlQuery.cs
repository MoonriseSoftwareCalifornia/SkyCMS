// <copyright file="GetPublishedPageByUrlQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using System;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Query to retrieve a published page by URL path.
/// </summary>
public class GetPublishedPageByUrlQuery : IQuery<ArticleViewModel?>
{
    /// <summary>
    /// Gets or sets the URL path to resolve.
    /// </summary>
    public string UrlPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language code.
    /// </summary>
    public string Lang { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cache duration for the article view model.
    /// </summary>
    public TimeSpan? CacheSpan { get; set; }

    /// <summary>
    /// Gets or sets the cache duration for layout resolution.
    /// </summary>
    public TimeSpan? LayoutCache { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include layout information.
    /// </summary>
    public bool IncludeLayout { get; set; } = true;
}
