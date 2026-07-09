// <copyright file="AiFaqIndexService.cs" company="Moonrise Software, LLC">
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
/// Extracts FAQ-style question/answer pairs from the SkyCMS documentation index.
/// Reuses the same search index as <see cref="AiDocumentationIndexService"/>; no separate HTTP call.
/// </summary>
public sealed class AiFaqIndexService : IAiFaqIndexService
{
    private const int MaxFaqResults = 2;
    private const int EmbeddingCandidateLimit = 6;
    private const int MaxAnswerLength = 600;
    private const string SearchIndexUrl = "https://docs.sky-cms.com/search/search_index.json";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "it", "in", "of", "to", "and", "or", "for",
        "on", "at", "by", "with", "do", "be", "as", "up", "my", "we", "i",
        "can", "how", "what", "when", "where", "why", "who", "this", "that",
    };

    // Titles that look like questions or have interrogative openers.
    private static readonly string[] QuestionPrefixes =
    [
        "how", "what", "why", "when", "where", "who", "can i", "can you",
        "is it", "are there", "does", "do i", "should i",
    ];

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMemoryCache memoryCache;
    private readonly IAiEmbeddingSemanticRanker embeddingSemanticRanker;
    private readonly ILogger<AiFaqIndexService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiFaqIndexService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="memoryCache">In-memory cache.</param>
    /// <param name="logger">Logger.</param>
    public AiFaqIndexService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        IAiEmbeddingSemanticRanker embeddingSemanticRanker,
        ILogger<AiFaqIndexService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.memoryCache = memoryCache;
        this.embeddingSemanticRanker = embeddingSemanticRanker;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiFaqMatch>> SearchFaqAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var entries = await this.GetIndexEntriesAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return [];
        }

        var keywords = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => Regex.Replace(w, "[^a-zA-Z0-9]", string.Empty))
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywords.Count == 0)
        {
            return [];
        }

        var scoredFaqMatches = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Text) && IsFaqEntry(e.Title))
            .Select(e =>
            {
                var searchable = $"{e.Title} {e.Text}";
                var keywordScore = keywords.Sum(kw => CountOccurrences(searchable, kw));
                var semanticScore = ComputeSemanticSimilarity(query, searchable);
                var score = keywordScore + (int)Math.Round(semanticScore * 5, MidpointRounding.AwayFromZero);
                return (Entry: e, Searchable: searchable, Score: score, SemanticScore: semanticScore, EmbeddingScore: 0d);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.SemanticScore)
            .Take(EmbeddingCandidateLimit)
            .ToList();

        if (scoredFaqMatches.Count > 0)
        {
            var embeddingScores = await this.embeddingSemanticRanker
                .ScoreAsync(query, scoredFaqMatches.Select(x => x.Searchable).ToList(), cancellationToken)
                .ConfigureAwait(false);

            if (embeddingScores.Count > 0)
            {
                var scoreByIndex = embeddingScores.ToDictionary(x => x.candidateIndex, x => x.score);
                scoredFaqMatches = scoredFaqMatches
                    .Select((candidate, index) =>
                    {
                        scoreByIndex.TryGetValue(index, out var embeddingScore);
                        var adjustedScore = candidate.Score + (int)Math.Round(embeddingScore * 12, MidpointRounding.AwayFromZero);
                        return (candidate.Entry, candidate.Searchable, Score: adjustedScore, candidate.SemanticScore, EmbeddingScore: embeddingScore);
                    })
                    .ToList();
            }
        }

        var faqMatches = scoredFaqMatches
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.EmbeddingScore)
            .ThenByDescending(x => x.SemanticScore)
            .Take(MaxFaqResults)
            .Select(x => new AiFaqMatch
            {
                Question = x.Entry.Title,
                Answer = x.Entry.Text.Length > MaxAnswerLength
                    ? x.Entry.Text[..MaxAnswerLength]
                    : x.Entry.Text,
                SourceUrl = $"https://docs.sky-cms.com/{x.Entry.Location}",
                RelevanceScore = x.Score,
            })
            .ToList();

        return faqMatches;
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

    private static bool IsFaqEntry(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var lower = title.Trim().ToLowerInvariant();
        return title.Contains('?')
            || QuestionPrefixes.Any(prefix => lower.StartsWith(prefix, StringComparison.Ordinal));
    }

    private async Task<List<SearchIndexEntry>> GetIndexEntriesAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "ai-faq:search-index";

        if (this.memoryCache.TryGetValue(cacheKey, out List<SearchIndexEntry>? entries) && entries != null)
        {
            return entries;
        }

        try
        {
            var client = this.httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(6);

            var json = await client.GetStringAsync(SearchIndexUrl, cancellationToken);
            var index = JsonSerializer.Deserialize<SearchIndex>(json, JsonOptions);
            entries = index?.Docs ?? [];
            this.memoryCache.Set(cacheKey, entries, CacheDuration);
            return entries;
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to load FAQ search index from {Url}.", SearchIndexUrl);
            return [];
        }
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
