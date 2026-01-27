// <copyright file="SearchResultItem.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System;
using System.Collections.Generic;

namespace Cosmos.Common.Services.Search.Models;

/// <summary>
/// Individual search result item.
/// </summary>
public class SearchResultItem
{
    /// <summary>
    /// Gets or sets the document ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content snippet.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the highlighted content with search terms marked.
    /// </summary>
    public string HighlightedContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the published date.
    /// </summary>
    public DateTimeOffset? PublishedDate { get; set; }

    /// <summary>
    /// Gets or sets the last modified date.
    /// </summary>
    public DateTimeOffset? ModifiedDate { get; set; }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relevance score (0.0 to 1.0).
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}