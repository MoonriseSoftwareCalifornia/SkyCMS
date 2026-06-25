// <copyright file="AiFaqIndexServiceTests.cs" company="Moonrise Software, LLC">
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
/// Tests for <see cref="AiFaqIndexService"/>.
/// </summary>
[TestClass]
public class AiFaqIndexServiceTests
{
    private IMemoryCache memoryCache = null!;

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
    public async Task SearchFaqAsync_ReturnsFaqMatchesForInterrogativeTitles()
    {
        const string json = """
        {
          "docs": [
            { "location": "for-editors/publishing/", "title": "How do I publish a page?", "text": "Navigate to Pages, select the page, click Publish." },
            { "location": "for-editors/creating-articles.md", "title": "Creating Articles", "text": "Create articles and blog posts in SkyCMS." },
            { "location": "for-editors/faq/", "title": "What is a layout?", "text": "A layout defines the outer shell of every page." }
          ]
        }
        """;

        var service = CreateService(json);

        var results = await service.SearchFaqAsync("publish page layout");

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results[0].Question.StartsWith("How", StringComparison.OrdinalIgnoreCase)
            || results[0].Question.StartsWith("What", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(string.IsNullOrWhiteSpace(results[0].Answer));
        Assert.IsTrue(results[0].SourceUrl.Contains("docs.sky-cms.com", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SearchFaqAsync_ExcludesNonFaqEntries()
    {
        const string json = """
        {
          "docs": [
            { "location": "reference/api.md", "title": "API Reference", "text": "Full API reference documentation." }
          ]
        }
        """;

        var service = CreateService(json);

        var results = await service.SearchFaqAsync("api reference");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchFaqAsync_ReturnsEmpty_ForBlankQuery()
    {
        var service = CreateService("{\"docs\":[]}");

        var results = await service.SearchFaqAsync(string.Empty);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchFaqAsync_TruncatesLongAnswers()
    {
        var longAnswer = new string('x', 2000);
        var json = $$"""
        {
          "docs": [
            { "location": "faq/long/", "title": "How does truncation work?", "text": "{{longAnswer}}" }
          ]
        }
        """;

        var service = CreateService(json);

        var results = await service.SearchFaqAsync("truncation");

        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].Answer.Length <= 600);
    }

    private AiFaqIndexService CreateService(string responseText)
    {
        var handler = new StaticResponseHandler(responseText);
        var httpClient = new HttpClient(handler);
        IHttpClientFactory factory = new FakeHttpClientFactory(httpClient);

        return new AiFaqIndexService(factory, memoryCache, Mock.Of<ILogger<AiFaqIndexService>>());
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly string responseText;

        public StaticResponseHandler(string responseText) => this.responseText = responseText;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(this.responseText, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient httpClient;

        public FakeHttpClientFactory(HttpClient httpClient) => this.httpClient = httpClient;

        public HttpClient CreateClient(string name) => this.httpClient;
    }
}
