// <copyright file="LuceneSearchOptions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Search.Configuration;

/// <summary>
/// Configuration options for Lucene.Net search service.
/// </summary>
public class LuceneSearchOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SearchService:Lucene";

    /// <summary>
    /// Gets or sets the base directory path for Lucene indexes.
    /// Default: "./App_Data/SearchIndex"
    /// </summary>
    public string IndexPath { get; set; } = "./App_Data/SearchIndex";

    /// <summary>
    /// Gets or sets whether to use RAM directory (in-memory) instead of file system.
    /// Useful for testing or small datasets. Default: false
    /// </summary>
    public bool UseRamDirectory { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of search results to return.
    /// Default: 1000
    /// </summary>
    public int MaxResults { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the number of fragments for highlighting.
    /// Default: 3
    /// </summary>
    public int HighlightFragmentCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the fragment size for highlighting.
    /// Default: 150
    /// </summary>
    public int HighlightFragmentSize { get; set; } = 150;

    /// <summary>
    /// Gets or sets field boost values for scoring.
    /// </summary>
    public FieldBoosts Boosts { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to auto-commit after each write.
    /// Default: true
    /// </summary>
    public bool AutoCommit { get; set; } = true;

    /// <summary>
    /// Gets or sets the commit interval in milliseconds when AutoCommit is false.
    /// Default: 5000 (5 seconds)
    /// </summary>
    public int CommitIntervalMs { get; set; } = 5000;
}

/// <summary>
/// Field boost configuration for search relevance tuning.
/// </summary>
public class FieldBoosts
{
    /// <summary>
    /// Title field boost. Default: 5.0
    /// </summary>
    public float Title { get; set; } = 5.0f;

    /// <summary>
    /// Content field boost. Default: 1.0
    /// </summary>
    public float Content { get; set; } = 1.0f;

    /// <summary>
    /// Summary field boost. Default: 3.0
    /// </summary>
    public float Summary { get; set; } = 3.0f;

    /// <summary>
    /// Tags field boost. Default: 2.0
    /// </summary>
    public float Tags { get; set; } = 2.0f;

    /// <summary>
    /// Author field boost. Default: 1.5
    /// </summary>
    public float Author { get; set; } = 1.5f;

    /// <summary>
    /// Keywords field boost. Default: 2.5
    /// </summary>
    public float Keywords { get; set; } = 2.5f;
}