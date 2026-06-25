// <copyright file="AiDocumentationIndexService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// Index-backed documentation search for SkyCMS help queries.
/// </summary>
public sealed class AiDocumentationIndexService : IAiDocumentationIndexService
{
    private const int MaxSearchResults = 3;
    private const int MaxSearchSectionLength = 500;
    private const int MaxContextLength = 2000;
    private const string SearchIndexUrl = "https://docs.sky-cms.com/search/search_index.json";

    private static readonly TimeSpan SearchIndexCacheDuration = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions SearchIndexJsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly object HealthGate = new();

    private static DateTimeOffset? lastSuccessfulRefreshUtc;
    private static DateTimeOffset? lastAttemptUtc;
    private static DateTimeOffset? lastFetchErrorUtc;
    private static DateTimeOffset? lastParseErrorUtc;
    private static int lastIndexedEntryCount;
    private static string? lastFetchError;
    private static string? lastParseError;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "it", "in", "of", "to", "and", "or", "for",
        "on", "at", "by", "with", "do", "be", "as", "up", "my", "we", "i",
        "can", "how", "what", "when", "where", "why", "who", "this", "that",
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<AiDocumentationIndexService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiDocumentationIndexService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="memoryCache">In-memory cache.</param>
    /// <param name="logger">Logger.</param>
    public AiDocumentationIndexService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<AiDocumentationIndexService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.memoryCache = memoryCache;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<AiDocumentationContextResult> SearchDocsAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new AiDocumentationContextResult();
        }

        var indexEntries = await this.GetIndexEntriesAsync(cancellationToken);
        if (indexEntries.Count == 0)
        {
            return new AiDocumentationContextResult();
        }

        var keywords = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => Regex.Replace(w, "[^a-zA-Z0-9]", string.Empty))
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywords.Count == 0)
        {
            return new AiDocumentationContextResult();
        }

        var scored = indexEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.Text))
            .Select(e =>
            {
                var searchable = $"{e.Title} {e.Text}";
                var score = keywords.Sum(kw => CountOccurrences(searchable, kw));
                return (Entry: e, Score: score);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(MaxSearchResults)
            .ToList();

        if (scored.Count == 0)
        {
            return new AiDocumentationContextResult();
        }

        var resultText = BuildContextText(scored);
        return new AiDocumentationContextResult { ContextText = resultText };
    }

    /// <summary>
    /// Returns documentation-index freshness and health metadata.
    /// </summary>
    /// <returns>Index health snapshot.</returns>
    public static AiIndexHealthSnapshot GetHealthSnapshot()
    {
        lock (HealthGate)
        {
            return new AiIndexHealthSnapshot
            {
                IndexName = "docs",
                LastSuccessfulRefreshUtc = lastSuccessfulRefreshUtc,
                LastAttemptUtc = lastAttemptUtc,
                LastIndexedEntryCount = lastIndexedEntryCount,
                LastFetchError = lastFetchError,
                LastFetchErrorUtc = lastFetchErrorUtc,
                LastParseError = lastParseError,
                LastParseErrorUtc = lastParseErrorUtc,
            };
        }
    }

    private async Task<List<SearchIndexEntry>> GetIndexEntriesAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "ai-docs:search-index";

        RecordAttempt();

        if (this.memoryCache.TryGetValue(cacheKey, out List<SearchIndexEntry>? entries) && entries != null)
        {
            return entries;
        }

        string json;
        try
        {
            var client = this.httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(6);

            json = await client.GetStringAsync(SearchIndexUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            RecordFetchError(ex.Message);
            this.logger.LogWarning(ex, "Docs index fetch error from {Url}.", SearchIndexUrl);
            return [];
        }

        try
        {
            var index = JsonSerializer.Deserialize<SearchIndex>(json, SearchIndexJsonOptions);
            entries = index?.Docs ?? [];
            this.memoryCache.Set(cacheKey, entries, SearchIndexCacheDuration);
            RecordSuccess(entries.Count);
            return entries;
        }
        catch (JsonException ex)
        {
            RecordParseError(ex.Message);
            this.logger.LogWarning(ex, "Docs index parse error for {Url}.", SearchIndexUrl);
            return [];
        }
        catch (Exception ex)
        {
            RecordParseError(ex.Message);
            this.logger.LogWarning(ex, "Docs index parse error for {Url}.", SearchIndexUrl);
            return [];
        }
    }

    private static void RecordAttempt()
    {
        lock (HealthGate)
        {
            lastAttemptUtc = DateTimeOffset.UtcNow;
        }
    }

    private static void RecordSuccess(int indexedEntryCount)
    {
        lock (HealthGate)
        {
            lastSuccessfulRefreshUtc = DateTimeOffset.UtcNow;
            lastIndexedEntryCount = indexedEntryCount;
            lastFetchError = null;
            lastFetchErrorUtc = null;
            lastParseError = null;
            lastParseErrorUtc = null;
        }
    }

    private static void RecordFetchError(string message)
    {
        lock (HealthGate)
        {
            lastFetchError = message;
            lastFetchErrorUtc = DateTimeOffset.UtcNow;
        }
    }

    private static void RecordParseError(string message)
    {
        lock (HealthGate)
        {
            lastParseError = message;
            lastParseErrorUtc = DateTimeOffset.UtcNow;
        }
    }

    private static string BuildContextText(IEnumerable<(SearchIndexEntry Entry, int Score)> scored)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Documentation context from docs.sky-cms.com:");

        foreach (var (entry, _) in scored)
        {
            var snippet = entry.Text.Length > MaxSearchSectionLength
                ? entry.Text[..MaxSearchSectionLength]
                : entry.Text;
            sb.AppendLine($"- [{entry.Title}] {snippet}");
        }

        sb.AppendLine("Sources:");
        foreach (var (entry, _) in scored)
        {
            sb.AppendLine($"- https://docs.sky-cms.com/{entry.Location}");
        }

        var text = sb.ToString();
        return text.Length > MaxContextLength ? text[..MaxContextLength] : text;
    }

    private static int CountOccurrences(string text, string keyword)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += keyword.Length;
        }

        return count;
    }

    private sealed class SearchIndex
    {
        public List<SearchIndexEntry>? Docs { get; set; }
    }

    private sealed class SearchIndexEntry
    {
        public string Location { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }
}