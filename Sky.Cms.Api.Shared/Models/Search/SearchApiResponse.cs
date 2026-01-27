// <copyright file="SearchApiResponse.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Models.Search;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Response model for search API operations.
/// </summary>
public class SearchApiResponse
{
    /// <summary>
    /// Gets or sets the search query that was executed.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of matching results.
    /// </summary>
    public int TotalResults { get; set; }

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the number of results per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages available.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets or sets the search results.
    /// </summary>
    public List<SearchResultItem> Results { get; set; } = new();

    /// <summary>
    /// Gets or sets the time taken to execute the search (in milliseconds).
    /// </summary>
    public long SearchTimeMs { get; set; }

    /// <summary>
    /// Gets or sets any search suggestions for query refinement.
    /// </summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// Gets or sets available facets for filtering.
    /// </summary>
    public Dictionary<string, List<FacetItem>> Facets { get; set; } = new();
}

/// <summary>
/// Represents a single search result item.
/// </summary>
public class SearchResultItem
{
    /// <summary>
    /// Gets or sets the unique identifier of the content.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the content.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content snippet.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content with search term highlights.
    /// </summary>
    public string HighlightedContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL to the content.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the publish date.
    /// </summary>
    public DateTime? PublishDate { get; set; }

    /// <summary>
    /// Gets or sets the last modified date.
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Gets or sets the author of the content.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relevance score.
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a search facet item.
/// </summary>
public class FacetItem
{
    /// <summary>
    /// Gets or sets the facet value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of results matching this facet.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this facet is currently selected.
    /// </summary>
    public bool IsSelected { get; set; }
}