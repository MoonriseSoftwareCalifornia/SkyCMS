// <copyright file="IArticleCatalogQueryService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Shared;

using Cosmos.Common.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Service for querying the article catalog - indexed metadata of published articles.
/// Provides hierarchical table of contents, pagination, and full-text search capabilities.
/// </summary>
/// <remarks>
/// The ArticleCatalog table is an optimized index of published articles designed for efficient
/// querying of article listings, hierarchical navigation, and full-text search operations.
/// This service encapsulates all catalog-based queries and returns view models suitable
/// for public-facing navigation and search functionality.
/// </remarks>
public interface IArticleCatalogQueryService
{
    /// <summary>
    /// Returns a paged list of articles in a hierarchical level (one level deep from prefix).
    /// </summary>
    /// <param name="prefix">Parent path fragment (e.g., "blog"). Slash/whitespace normalized. Empty for root-level pages.</param>
    /// <param name="pageNo">Zero-based page index.</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <param name="orderByPublishedDate">When true, sorts newest first; when false, sorts by title.</param>
    /// <returns>
    /// TableOfContents with paged items, pagination metadata, and publisher URL context.
    /// Uses regex matching to approximate one-level deep children (future: replace with persisted depth metadata).
    /// </returns>
    /// <remarks>
    /// ⚠️ WARNING: Uses Regex.IsMatch in LINQ - compatibility with Cosmos DB needs testing.
    /// </remarks>
    Task<TableOfContents> GetTableOfContentsAsync(
        string prefix,
        int pageNo = 0,
        int pageSize = 10,
        bool orderByPublishedDate = false);

    /// <summary>
    /// Full-text search across published article titles and content.
    /// </summary>
    /// <param name="searchText">Search query. Multiple terms are AND-combined.</param>
    /// <returns>
    /// List of TableOfContentsItem matching the search criteria, ordered by title.
    /// Uses LIKE-based substring matching (expensive for large datasets).
    /// </returns>
    /// <remarks>
    /// This is an unindexed, full-text search suitable for small to medium datasets.
    /// For production scale or complex search, consider external indexing (Azure Search, Elasticsearch, etc.).
    /// </remarks>
    Task<List<TableOfContentsItem>> SearchAsync(string searchText);
}
