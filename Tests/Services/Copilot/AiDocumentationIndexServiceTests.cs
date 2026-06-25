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
    public async Task SearchDocsAsync_ReturnsEmptyResult_ForBlankQuery()
    {
        service = CreateService("{\"docs\":[]}");

        var result = await service.SearchDocsAsync(string.Empty);

        Assert.AreEqual(string.Empty, result.ContextText);
    }

    private AiDocumentationIndexService CreateService(string responseText)
    {
        var handler = new StaticResponseHandler(responseText);
        var httpClient = new HttpClient(handler);
        httpClientFactory = new FakeHttpClientFactory(httpClient);

        return new AiDocumentationIndexService(
            httpClientFactory,
            memoryCache,
            Mock.Of<ILogger<AiDocumentationIndexService>>());
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