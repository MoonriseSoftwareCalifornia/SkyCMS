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

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(20);

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
    private readonly IAiDocumentationIndexService documentationIndexService;
    private readonly ILogger<AiDocumentationContextService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiDocumentationContextService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="memoryCache">In-memory cache.</param>
    /// <param name="memoryCache">In-memory cache.</param>
    /// <param name="logger">Logger.</param>
    public AiDocumentationContextService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        IAiDocumentationIndexService documentationIndexService,
        ILogger<AiDocumentationContextService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.memoryCache = memoryCache;
        this.documentationIndexService = documentationIndexService;
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
        return await this.documentationIndexService.SearchDocsAsync(message, cancellationToken);
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

}
