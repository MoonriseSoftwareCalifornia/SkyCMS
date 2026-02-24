// <copyright file="GetPublishedPageHeaderByUrlQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Queries;

using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;

/// <summary>
/// Query to retrieve a published page header by URL path.
/// </summary>
public class GetPublishedPageHeaderByUrlQuery : IQuery<ArticleViewModel?>
{
    /// <summary>
    /// Gets or sets the URL path to resolve.
    /// </summary>
    public string UrlPath { get; set; } = string.Empty;
}
