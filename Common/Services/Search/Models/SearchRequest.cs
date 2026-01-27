// <copyright file="SearchRequest.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Search.Models;

using System;

/// <summary>
/// Request model for search operations.
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// Gets or sets the search query text.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the page number (0-based).
    /// </summary>
    public int Page { get; set; } = 0;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets content types to filter by.
    /// </summary>
    public string[]? ContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the start date filter.
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// Gets or sets the end date filter.
    /// </summary>
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// Gets or sets the sort field.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Gets or sets the tenant domain for multi-tenancy filtering.
    /// </summary>
    public string TenantDomain { get; set; } = string.Empty;
}