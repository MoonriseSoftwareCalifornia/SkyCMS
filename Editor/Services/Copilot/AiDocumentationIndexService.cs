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
    private const int EmbeddingCandidateLimit = 8;
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
    private readonly IAiEmbeddingSemanticRanker embeddingSemanticRanker;
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
        IAiEmbeddingSemanticRanker embeddingSemanticRanker,
        ILogger<AiDocumentationIndexService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.memoryCache = memoryCache;
        this.embeddingSemanticRanker = embeddingSemanticRanker;
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
                var keywordScore = keywords.Sum(kw => CountOccurrences(searchable, kw));
                var semanticScore = ComputeSemanticSimilarity(query, searchable);
                var combinedScore = keywordScore + (int)Math.Round(semanticScore * 5, MidpointRounding.AwayFromZero);
                return (Entry: e, Searchable: searchable, Score: combinedScore, SemanticScore: semanticScore, EmbeddingScore: 0d);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.SemanticScore)
            .Take(EmbeddingCandidateLimit)
            .ToList();

        if (scored.Count > 0)
        {
            var embeddingScores = await this.embeddingSemanticRanker
                .ScoreAsync(query, scored.Select(x => x.Searchable).ToList(), cancellationToken)
                .ConfigureAwait(false);

            if (embeddingScores.Count > 0)
            {
                var scoreByIndex = embeddingScores.ToDictionary(x => x.CandidateIndex, x => x.Score);
                scored = scored
                    .Select((candidate, index) =>
                    {
                        scoreByIndex.TryGetValue(index, out var embeddingScore);
                        var adjustedScore = candidate.Score + (int)Math.Round(embeddingScore * 12, MidpointRounding.AwayFromZero);
                        return (candidate.Entry, candidate.Searchable, Score: adjustedScore, candidate.SemanticScore, EmbeddingScore: embeddingScore);
                    })
                    .ToList();
            }
        }

        scored = scored
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.EmbeddingScore)
            .ThenByDescending(x => x.SemanticScore)
            .Take(MaxSearchResults)
            .ToList();

        if (scored.Count == 0)
        {
            return new AiDocumentationContextResult();
        }

        var resultText = BuildContextText(scored.Select(x => (x.Entry, x.Score)));
        return new AiDocumentationContextResult { ContextText = resultText };
    }

    private static double ComputeSemanticSimilarity(string query, string content)
    {
        try
        {
            var queryVector = BuildTermVector(query);
            var contentVector = BuildTermVector(content);
            if (queryVector.Count == 0 || contentVector.Count == 0)
            {
                return 0;
            }

            var dot = 0d;
            foreach (var term in queryVector)
            {
                if (contentVector.TryGetValue(term.Key, out var contentWeight))
                {
                    dot += term.Value * contentWeight;
                }
            }

            if (dot <= 0)
            {
                return 0;
            }

            var queryNorm = Math.Sqrt(queryVector.Values.Sum(v => v * v));
            var contentNorm = Math.Sqrt(contentVector.Values.Sum(v => v * v));
            if (queryNorm <= 0 || contentNorm <= 0)
            {
                return 0;
            }

            return dot / (queryNorm * contentNorm);
        }
        catch
        {
            // Fallback to keyword-only ranking if semantic scoring fails.
            return 0;
        }
    }

    private static Dictionary<string, double> BuildTermVector(string text)
    {
        var tokens = Regex.Matches(text ?? string.Empty, "[A-Za-z0-9]+")
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Length > 2 && !StopWords.Contains(token))
            .ToList();

        var total = tokens.Count;
        if (total == 0)
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in tokens.GroupBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            vector[group.Key] = (double)group.Count() / total;
        }

        return vector;
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