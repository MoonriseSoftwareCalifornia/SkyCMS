// <copyright file="AiDocumentationIndexServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services.Copilot;

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Sky.Editor.Services.Copilot;

/// <summary>
/// Tests for <see cref="AiDocumentationIndexService"/>.
/// </summary>
[TestClass]
public class AiDocumentationIndexServiceTests
{
    private IHttpClientFactory httpClientFactory = null!;
    private IMemoryCache memoryCache = null!;
    private AiDocumentationIndexService service = null!;

    [TestInitialize]
    public void Setup()
    {
        memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    [TestCleanup]
    public void Cleanup()
    {
        memoryCache.Dispose();
    }

    [TestMethod]
    public async Task SearchDocsAsync_ReturnsRankedMatchesFromSearchIndex()
    {
        var json = """
        {
          "docs": [
            { "location": "for-editors/creating-articles.md", "title": "Creating Articles", "text": "Create general articles, blog posts, and blogs in SkyCMS." },
            { "location": "for-editors/visual-editor-technical-reference.md", "title": "Visual Editor Technical Reference", "text": "CKEditor inline editing and toolbar profiles." }
          ]
        }
        """;

        service = CreateService(json);

        var result = await service.SearchDocsAsync("create article blog");

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ContextText));
        Assert.IsTrue(result.ContextText.Contains("Creating Articles", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.ContextText.Contains("docs.sky-cms.com/for-editors/creating-articles.md", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SearchDocsAsync_PrioritizesMoreRelevantDocument()
    {
        var json = """
        {
            "docs": [
                { "location": "configuration/cookie-domain.md", "title": "Cookie Domain Isolation", "text": "Configure tenant cookie domain isolation and host strategy." },
                { "location": "for-editors/quick-start.md", "title": "Editor Quick Start", "text": "Basic editor workflow and publishing actions." }
            ]
        }
        """;

        service = CreateService(json);

        var result = await service.SearchDocsAsync("tenant cookie domain isolation strategy");

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ContextText));
        var highRelevanceIndex = result.ContextText.IndexOf("Cookie Domain Isolation", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(highRelevanceIndex >= 0);
    }

    [TestMethod]
    public async Task SearchDocsAsync_HandlesPunctuationAndStillFindsMatches()
    {
        var json = """
        {
            "docs": [
                { "location": "configuration/rate-limits.md", "title": "Rate Limiting", "text": "Configure contact-form rate limiting policy for production and development." }
            ]
        }
        """;

        service = CreateService(json);

        var result = await service.SearchDocsAsync("rate-limiting?? contact-form!!");

        Assert.IsTrue(result.ContextText.Contains("Rate Limiting", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SearchDocsAsync_EmbeddingRerankerCanPromoteLowerKeywordCandidate()
    {
        var json = """
        {
          "docs": [
            { "location": "reference/base.md", "title": "Base Result", "text": "tenant routing and cache" },
            { "location": "reference/promoted.md", "title": "Promoted Result", "text": "routing architecture" }
          ]
        }
        """;

        service = CreateService(
            json,
            new FixedScoreEmbeddingSemanticRanker(
            [
                new AiEmbeddingSemanticScore(0, 0.0),
                new AiEmbeddingSemanticScore(1, 1.0),
            ]));

        var result = await service.SearchDocsAsync("tenant routing cache");

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ContextText));
        var promotedIndex = result.ContextText.IndexOf("Promoted Result", StringComparison.OrdinalIgnoreCase);
        var baseIndex = result.ContextText.IndexOf("Base Result", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(promotedIndex >= 0);
        Assert.IsTrue(baseIndex >= 0);
        Assert.IsTrue(promotedIndex < baseIndex);
    }

    [TestMethod]
    public async Task SearchDocsAsync_ReturnsEmptyResult_ForBlankQuery()
    {
        service = CreateService("{\"docs\":[]}");

        var result = await service.SearchDocsAsync(string.Empty);

        Assert.AreEqual(string.Empty, result.ContextText);
    }

    private AiDocumentationIndexService CreateService(string responseText, IAiEmbeddingSemanticRanker? semanticRanker = null)
    {
        var handler = new StaticResponseHandler(responseText);
        var httpClient = new HttpClient(handler);
        httpClientFactory = new FakeHttpClientFactory(httpClient);

        return new AiDocumentationIndexService(
            httpClientFactory,
            memoryCache,
            semanticRanker ?? new StubEmbeddingSemanticRanker(),
            Mock.Of<ILogger<AiDocumentationIndexService>>());
    }

    private sealed class StubEmbeddingSemanticRanker : IAiEmbeddingSemanticRanker
    {
        public Task<IReadOnlyList<AiEmbeddingSemanticScore>> ScoreAsync(string query, IReadOnlyList<string> candidates, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AiEmbeddingSemanticScore>>([]);
        }
    }

    private sealed class FixedScoreEmbeddingSemanticRanker : IAiEmbeddingSemanticRanker
    {
        private readonly IReadOnlyList<AiEmbeddingSemanticScore> scores;

        public FixedScoreEmbeddingSemanticRanker(IReadOnlyList<AiEmbeddingSemanticScore> scores)
        {
            this.scores = scores;
        }

        public Task<IReadOnlyList<AiEmbeddingSemanticScore>> ScoreAsync(string query, IReadOnlyList<string> candidates, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(scores);
        }
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly string responseText;

        public StaticResponseHandler(string responseText)
        {
            this.responseText = responseText;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseText, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient httpClient;

        public FakeHttpClientFactory(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public HttpClient CreateClient(string name)
        {
            return this.httpClient;
        }
    }
}