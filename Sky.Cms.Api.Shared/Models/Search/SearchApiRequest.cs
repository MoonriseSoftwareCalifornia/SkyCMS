// <copyright file="SearchApiRequest.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Models.Search;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request model for search API operations.
/// </summary>
public class SearchApiRequest
{
    /// <summary>
    /// Gets or sets the search query text.
    /// </summary>
    [Required]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Query must be between 1 and 500 characters")]
    public string? Query { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of results per page.
    /// </summary>
    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
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
    /// Valid values: relevance, date, title, popularity.
    /// </summary>
    [RegularExpression("^(relevance|date|title|popularity)$", 
        ErrorMessage = "SortBy must be one of: relevance, date, title, popularity")]
    public string? SortBy { get; set; } = "relevance";

    /// <summary>
    /// Gets or sets a value indicating whether to include content snippets in results.
    /// </summary>
    public bool IncludeContent { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include search term highlights.
    /// </summary>
    public bool IncludeHighlights { get; set; } = true;
}