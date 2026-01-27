// <copyright file="LuceneSearchPresets.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System;

namespace Cosmos.Common.Services.Search.Configuration;

/// <summary>
/// Provides preset configurations for Lucene search service.
/// </summary>
public static class LuceneSearchPresets
{
    /// <summary>
    /// Development preset: Fast in-memory index, ideal for local development.
    /// </summary>
    public static LuceneSearchOptions Development => new()
    {
        UseRamDirectory = true,
        IndexPath = "./App_Data/SearchIndex_Dev",
        MaxResults = 500,
        AutoCommit = true,
        HighlightFragmentCount = 3,
        HighlightFragmentSize = 150,
        Boosts = new FieldBoosts
        {
            Title = 5.0f,
            Content = 1.0f,
            Summary = 3.0f,
            Tags = 2.0f,
            Author = 1.5f,
            Keywords = 2.5f
        }
    };

    /// <summary>
    /// Testing preset: Isolated in-memory index for unit/integration tests.
    /// </summary>
    public static LuceneSearchOptions Testing => new()
    {
        UseRamDirectory = true,
        IndexPath = $"./TestIndex_{Guid.NewGuid():N}",
        MaxResults = 1000,
        AutoCommit = true,
        HighlightFragmentCount = 2,
        HighlightFragmentSize = 100,
        Boosts = new FieldBoosts
        {
            Title = 5.0f,
            Content = 1.0f,
            Summary = 3.0f,
            Tags = 2.0f,
            Author = 1.5f,
            Keywords = 2.5f
        }
    };

    /// <summary>
    /// Production preset: File-based index with performance optimizations.
    /// </summary>
    public static LuceneSearchOptions Production => new()
    {
        UseRamDirectory = false,
        IndexPath = "/var/app/search-index",
        MaxResults = 5000,
        AutoCommit = false,
        CommitIntervalMs = 10000,
        HighlightFragmentCount = 3,
        HighlightFragmentSize = 200,
        Boosts = new FieldBoosts
        {
            Title = 5.0f,
            Content = 1.0f,
            Summary = 3.0f,
            Tags = 2.0f,
            Author = 1.5f,
            Keywords = 2.5f
        }
    };

    /// <summary>
    /// Windows Production preset: File-based index optimized for Windows Server.
    /// </summary>
    public static LuceneSearchOptions WindowsProduction => new()
    {
        UseRamDirectory = false,
        IndexPath = @"C:\AppData\SkyCMS\SearchIndex",
        MaxResults = 5000,
        AutoCommit = false,
        CommitIntervalMs = 10000,
        HighlightFragmentCount = 3,
        HighlightFragmentSize = 200,
        Boosts = new FieldBoosts
        {
            Title = 5.0f,
            Content = 1.0f,
            Summary = 3.0f,
            Tags = 2.0f,
            Author = 1.5f,
            Keywords = 2.5f
        }
    };

    /// <summary>
    /// Docker/Container preset: Optimized for containerized deployments.
    /// </summary>
    public static LuceneSearchOptions Docker => new()
    {
        UseRamDirectory = false,
        IndexPath = "/app/data/search-index",
        MaxResults = 3000,
        AutoCommit = false,
        CommitIntervalMs = 15000,
        HighlightFragmentCount = 3,
        HighlightFragmentSize = 150,
        Boosts = new FieldBoosts
        {
            Title = 5.0f,
            Content = 1.0f,
            Summary = 3.0f,
            Tags = 2.0f,
            Author = 1.5f,
            Keywords = 2.5f
        }
    };

    /// <summary>
    /// Default preset: Balanced configuration for general use.
    /// </summary>
    public static LuceneSearchOptions Default => new()
    {
        UseRamDirectory = false,
        IndexPath = "./App_Data/SearchIndex",
        MaxResults = 1000,
        AutoCommit = true,
        CommitIntervalMs = 5000,
        HighlightFragmentCount = 3,
        HighlightFragmentSize = 150,
        Boosts = new FieldBoosts
        {
            Title = 5.0f,
            Content = 1.0f,
            Summary = 3.0f,
            Tags = 2.0f,
            Author = 1.5f,
            Keywords = 2.5f
        }
    };

    /// <summary>
    /// Gets a preset by environment name.
    /// </summary>
    /// <param name="environmentName">Environment name (Development, Production, Testing, etc.)</param>
    /// <returns>Matching preset or Default if not found.</returns>
    public static LuceneSearchOptions GetPresetForEnvironment(string? environmentName)
    {
        return environmentName?.ToLowerInvariant() switch
        {
            "development" or "dev" => Development,
            "production" or "prod" => Production,
            "testing" or "test" => Testing,
            "docker" or "container" => Docker,
            _ => Default
        };
    }
}