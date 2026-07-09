// <copyright file="AiProviderModelCatalogServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

#nullable enable

namespace Sky.Tests.Editor.Services.Copilot;

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Sky.Editor.Models;
using Sky.Editor.Services.Copilot;

[TestClass]
public class AiProviderModelCatalogServiceTests
{
    [TestMethod]
    public async Task GetCatalogAsync_WithLiveOpenAiCatalog_UsesCacheUntilForceRefresh()
    {
        var callCount = 0;
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var httpClient = CreateHttpClient((_, _) =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"id\":\"gpt-4o-mini\",\"owned_by\":\"openai\"}]}", Encoding.UTF8, "application/json"),
            };
        });

        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new AiProviderModelCatalogService(httpClientFactory.Object, memoryCache, Mock.Of<ILogger<AiProviderModelCatalogService>>());
        var options = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://api.openai.com/v1/chat/completions",
            AccessToken = "token",
            Model = "auto",
        };

        var first = await service.GetCatalogAsync(options);
        var second = await service.GetCatalogAsync(options);
        var refreshed = await service.GetCatalogAsync(options, forceRefresh: true);

        Assert.AreEqual(AiProviderDiscoveryStates.LiveCatalog, first.DiscoveryState);
        Assert.AreEqual(1, first.Models.Count);
        Assert.AreEqual(1, second.Models.Count);
        Assert.AreEqual(1, refreshed.Models.Count);
        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public async Task GetCatalogAsync_WithAzureOpenAiDeploymentEndpoint_ReturnsInferredCatalog()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var service = new AiProviderModelCatalogService(httpClientFactory.Object, new MemoryCache(new MemoryCacheOptions()), Mock.Of<ILogger<AiProviderModelCatalogService>>());

        var result = await service.GetCatalogAsync(new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://example.openai.azure.com/openai/deployments/editor-deployment/chat/completions?api-version=2024-10-21",
            AccessToken = "token",
            Model = "auto",
        });

        Assert.AreEqual("azure-openai", result.ProviderKey);
        Assert.AreEqual(AiProviderDiscoveryStates.Inferred, result.DiscoveryState);
        Assert.AreEqual(1, result.Models.Count);
        Assert.AreEqual("editor-deployment", result.Models[0].Id);
    }

    [TestMethod]
    public async Task GetCatalogAsync_WithFoundryEndpoint_ReturnsNeedsAdditionalConfiguration()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var service = new AiProviderModelCatalogService(httpClientFactory.Object, new MemoryCache(new MemoryCacheOptions()), Mock.Of<ILogger<AiProviderModelCatalogService>>());

        var result = await service.GetCatalogAsync(new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://example.services.ai.azure.com/models/chat/completions?api-version=2024-05-01-preview",
            AccessToken = "token",
            Model = "auto",
        });

        Assert.AreEqual("azure-ai-foundry", result.ProviderKey);
        Assert.AreEqual(AiProviderDiscoveryStates.NeedsAdditionalConfiguration, result.DiscoveryState);
        Assert.AreEqual(0, result.Models.Count);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.DiscoveryError));
    }

    [TestMethod]
    public async Task GetCatalogAsync_WhenCancelled_PropagatesCancellation()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var httpClient = CreateHttpClient((_, cancellationToken) => throw new OperationCanceledException(cancellationToken));
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new AiProviderModelCatalogService(httpClientFactory.Object, new MemoryCache(new MemoryCacheOptions()), Mock.Of<ILogger<AiProviderModelCatalogService>>());

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
        {
            await service.GetCatalogAsync(new CopilotProxyOptions
            {
                Enabled = true,
                Endpoint = "https://api.openai.com/v1/chat/completions",
                AccessToken = "token",
                Model = "auto",
            }, cancellationToken: new CancellationToken(canceled: true));
        });
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
    {
        return new HttpClient(new DelegateHttpMessageHandler(send));
    }

    private sealed class DelegateHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send;

        public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
        {
            this.send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.send(request, cancellationToken));
        }
    }
}