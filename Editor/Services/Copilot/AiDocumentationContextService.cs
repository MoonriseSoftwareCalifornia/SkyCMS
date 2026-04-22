// <copyright file="AiDocumentationContextService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// Retrieves compact documentation context snippets from docs.sky-cms.com.
/// </summary>
public sealed class AiDocumentationContextService : IAiDocumentationContextService
{
    private const int MaxSectionLength = 650;
    private const int MaxContextLength = 2000;
    private const string SearchIndexUrl = "https://docs.sky-cms.com/search/search_index.json";
    private const int MaxSearchResults = 3;
    private const int MaxSearchSectionLength = 500;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan SearchIndexCacheDuration = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions SearchIndexJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "it", "in", "of", "to", "and", "or", "for",
        "on", "at", "by", "with", "do", "be", "as", "up", "my", "we", "i",
        "can", "how", "what", "when", "where", "why", "who", "this", "that",
    };

    private static readonly IReadOnlyDictionary<string, string[]> ContextToUrls = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["layout-head"] =
        [
            "https://docs.sky-cms.com/for-site-builders/layouts/",
            "https://docs.sky-cms.com/for-site-builders/layout-examples/overview/",
        ],
        ["layout-body-start"] =
        [
            "https://docs.sky-cms.com/for-site-builders/layouts/",
            "https://docs.sky-cms.com/for-site-builders/layout-examples/bootstrap-5/",
        ],
        ["layout-body-end"] =
        [
            "https://docs.sky-cms.com/for-site-builders/layouts/",
            "https://docs.sky-cms.com/for-site-builders/layout-examples/overview/",
        ],
        ["template-content"] =
        [
            "https://docs.sky-cms.com/for-site-builders/templates/",
            "https://docs.sky-cms.com/for-site-builders/template-examples/overview/",
        ],
        ["article-content"] =
        [
            "https://docs.sky-cms.com/for-site-builders/pages/",
            "https://docs.sky-cms.com/for-site-builders/article-examples/overview/",
        ],
        ["blog-content"] =
        [
            "https://docs.sky-cms.com/for-editors/blogging/",
            "https://docs.sky-cms.com/for-site-builders/template-examples/blog-post/",
        ],
        ["article"] =
        [
            "https://docs.sky-cms.com/for-site-builders/pages/",
            "https://docs.sky-cms.com/for-editors/page-editor/",
        ],
        ["blog"] =
        [
            "https://docs.sky-cms.com/for-editors/blogging/",
            "https://docs.sky-cms.com/for-site-builders/template-examples/blog-post/",
        ],
        ["template"] =
        [
            "https://docs.sky-cms.com/for-site-builders/templates/",
            "https://docs.sky-cms.com/for-site-builders/template-examples/overview/",
        ],
        ["layout"] =
        [
            "https://docs.sky-cms.com/for-site-builders/layouts/",
            "https://docs.sky-cms.com/for-site-builders/layout-examples/overview/",
        ],
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<AiDocumentationContextService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiDocumentationContextService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="memoryCache">In-memory cache.</param>
    /// <param name="logger">Logger.</param>
    public AiDocumentationContextService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<AiDocumentationContextService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.memoryCache = memoryCache;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AiDocumentationContextResult> GetDocumentationContextAsync(AiContextEnrichmentRequest request, CancellationToken cancellationToken = default)
    {
        var urls = ResolveUrls(request);
        if (urls.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(request.Message))
            {
                return await this.SearchDocsByMessageAsync(request.Message, cancellationToken);
            }

            return new AiDocumentationContextResult();
        }

        var sections = new List<string>();
        var sources = new List<string>();

        foreach (var url in urls)
        {
            var text = await GetPageTextAsync(url, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            sections.Add(text.Length > MaxSectionLength ? text[..MaxSectionLength] : text);
            sources.Add(url);
        }

        if (sections.Count == 0)
        {
            return new AiDocumentationContextResult();
        }

        var sb = new StringBuilder();
        sb.AppendLine("Documentation context from docs.sky-cms.com:");
        for (var index = 0; index < sections.Count; index++)
        {
            sb.AppendLine($"- {sections[index]}");
        }

        sb.AppendLine("Sources:");
        foreach (var source in sources)
        {
            sb.AppendLine($"- {source}");
        }

        var finalText = sb.ToString();
        if (finalText.Length > MaxContextLength)
        {
            finalText = finalText[..MaxContextLength];
        }

        return new AiDocumentationContextResult
        {
            ContextText = finalText,
        };
    }

    private static List<string> ResolveUrls(AiContextEnrichmentRequest request)
    {
        var urls = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.SectionKind) && ContextToUrls.TryGetValue(request.SectionKind, out var sectionUrls))
        {
            urls.AddRange(sectionUrls);
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentKind) && ContextToUrls.TryGetValue(request.DocumentKind, out var docUrls))
        {
            urls.AddRange(docUrls);
        }

        return urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
    }

    private async Task<string?> GetPageTextAsync(string url, CancellationToken cancellationToken)
    {
        var cacheKey = $"ai-docs:{url}";
        if (this.memoryCache.TryGetValue(cacheKey, out string? cachedText) && !string.IsNullOrWhiteSpace(cachedText))
        {
            return cachedText;
        }

        try
        {
            var client = this.httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(4);

            var html = await client.GetStringAsync(url, cancellationToken);
            var text = ExtractText(html);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            this.memoryCache.Set(cacheKey, text, CacheDuration);
            return text;
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to load docs context from {Url}.", url);
            return null;
        }
    }

    private async Task<AiDocumentationContextResult> SearchDocsByMessageAsync(string message, CancellationToken cancellationToken)
    {
        const string cacheKey = "ai-docs:search-index";

        if (!this.memoryCache.TryGetValue(cacheKey, out List<SearchIndexEntry>? entries) || entries == null)
        {
            try
            {
                var client = this.httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(6);

                var json = await client.GetStringAsync(SearchIndexUrl, cancellationToken);
                var index = JsonSerializer.Deserialize<SearchIndex>(json, SearchIndexJsonOptions);
                entries = index?.Docs ?? [];
                this.memoryCache.Set(cacheKey, entries, SearchIndexCacheDuration);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to load docs search index from {Url}.", SearchIndexUrl);
                return new AiDocumentationContextResult();
            }
        }

        var keywords = message
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => Regex.Replace(w, "[^a-zA-Z0-9]", string.Empty))
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywords.Count == 0)
        {
            return new AiDocumentationContextResult();
        }

        var scored = entries
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

        var sb = new StringBuilder();
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

        var finalText = sb.ToString();
        if (finalText.Length > MaxContextLength)
        {
            finalText = finalText[..MaxContextLength];
        }

        return new AiDocumentationContextResult { ContextText = finalText };
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

    private static string ExtractText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutScripts = Regex.Replace(html, "<script[^>]*>[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
        var withoutStyles = Regex.Replace(withoutScripts, "<style[^>]*>[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withoutStyles, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, "\\s+", " ").Trim();
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
