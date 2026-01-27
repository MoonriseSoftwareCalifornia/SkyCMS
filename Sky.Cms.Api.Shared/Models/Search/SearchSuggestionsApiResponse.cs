// <copyright file="SearchSuggestionsApiResponse.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Models.Search;

/// <summary>
/// Response model for search suggestions API.
/// </summary>
public class SearchSuggestionsApiResponse
{
    /// <summary>
    /// Gets or sets the list of search suggestions.
    /// </summary>
    public string[] Suggestions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the query that was used to generate suggestions.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time taken to generate suggestions (in milliseconds).
    /// </summary>
    public long GenerationTimeMs { get; set; }
}

/// <summary>
/// Response model for search health check API.
/// </summary>
public class SearchHealthApiResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the search service is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the health status message.
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the service version information.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last health check timestamp.
    /// </summary>
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets additional health metrics.
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();
}