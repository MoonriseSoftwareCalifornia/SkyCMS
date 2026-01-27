// <copyright file="SearchQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Features.Search.Query;

using Cosmos.Common.Features.Shared;
using Sky.Cms.Api.Shared.Models.Search;

/// <summary>
/// Query for searching content.
/// </summary>
public class SearchQuery : IQuery<SearchApiResponse>
{
    /// <summary>
    /// Gets or sets the search query text.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of results per page.
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets the content types to filter by.
    /// </summary>
    public string[]? ContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the start date for filtering results.
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// Gets or sets the end date for filtering results.
    /// </summary>
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// Gets or sets the sort order.
    /// </summary>
    public string SortBy { get; set; } = "relevance";

    /// <summary>
    /// Gets or sets a value indicating whether to include content snippets in results.
    /// </summary>
    public bool IncludeContent { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include search term highlights.
    /// </summary>
    public bool IncludeHighlights { get; set; } = true;
}