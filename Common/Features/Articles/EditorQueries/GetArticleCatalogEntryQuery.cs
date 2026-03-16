// <copyright file="GetArticleCatalogEntryQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.EditorQueries;

using System;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;

/// <summary>
/// Query to retrieve a catalog entry for an article.
/// </summary>
public class GetArticleCatalogEntryQuery : IQuery<CatalogEntry?>
{
    /// <summary>
    /// Gets or sets the article number.
    /// </summary>
    public int ArticleNumber { get; set; }

    /// <summary>
    /// Gets or sets the optional cache duration. If null, no caching is applied.
    /// </summary>
    /// <remarks>
    /// Recommended duration: 5-10 minutes for article metadata that changes infrequently.
    /// Cache is automatically invalidated when articles are published/unpublished.
    /// </remarks>
    public TimeSpan? CacheDuration { get; set; }
}
