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
        ILogger<AiFaqIndexService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.memoryCache = memoryCache;
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

        var faqMatches = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Text) && IsFaqEntry(e.Title))
            .Select(e =>
            {
                var searchable = $"{e.Title} {e.Text}";
                var score = keywords.Sum(kw => CountOccurrences(searchable, kw));
                return (Entry: e, Score: score);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
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
