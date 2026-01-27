using System;
using System.Collections.Generic;

// <copyright file="SearchResults.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Search.Models;

/// <summary>
/// Search results container.
/// </summary>
public class SearchResults
{
    /// <summary>
    /// Gets or sets the search result items.
    /// </summary>
    public IEnumerable<SearchResultItem> Items { get; set; } = Array.Empty<SearchResultItem>();

    /// <summary>
    /// Gets or sets the total number of results.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the current page number (0-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets the total number of pages (calculated property).
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Gets or sets the search execution time in milliseconds.
    /// </summary>
    public long SearchTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the available facets.
    /// </summary>
    public Dictionary<string, List<FacetItem>> Facets { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether there is a next page available.
    /// </summary>
    public bool HasNextPage => Page < TotalPages - 1;

    /// <summary>
    /// Gets a value indicating whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => Page > 0;
}

/// <summary>
/// Search facet for filtering
/// </summary>
public class SearchFacet
{
    /// <summary>
    /// Facet value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Number of documents with this facet value.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Whether this facet is currently selected.
    /// </summary>
    public bool IsSelected { get; set; }
}