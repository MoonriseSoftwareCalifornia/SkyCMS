// <copyright file="CopilotProxyOptionsServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Editor.Services.Copilot;

using Cosmos.Common.Data;
using Cosmos.Common.Services.Caching;
using Cosmos.DynamicConfig;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Sky.Editor.Models;
using Sky.Editor.Services.Copilot;
using System;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="CopilotProxyOptionsService"/>.
/// </summary>
[TestClass]
public class CopilotProxyOptionsServiceTests
{
    private const string CopilotGroup = "COPILOTPROXYSETTINGS";

    private ApplicationDbContext dbContext = null!;
    private Mock<ICacheService<CopilotProxyOptions>> cacheMock = null!;
    private Mock<IDynamicConfigurationProvider> dynamicConfigurationProviderMock = null!;
    private Mock<ILogger<CopilotProxyOptionsService>> loggerMock = null!;
    private CopilotProxyOptionsService service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"CopilotProxyOptionsService_{Guid.NewGuid()}")
            .Options;

        dbContext = new ApplicationDbContext(options);
        cacheMock = new Mock<ICacheService<CopilotProxyOptions>>();
        dynamicConfigurationProviderMock = new Mock<IDynamicConfigurationProvider>();
        loggerMock = new Mock<ILogger<CopilotProxyOptionsService>>();

        dynamicConfigurationProviderMock
            .Setup(d => d.GetTenantDomainNameFromRequest())
            .Returns("tenant-a.example.com");

        service = new CopilotProxyOptionsService(
            dbContext,
            cacheMock.Object,
            dynamicConfigurationProviderMock.Object,
            loggerMock.Object);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        dbContext.Dispose();
    }

    [TestMethod]
    public async Task GetOptionsAsync_WithEmptyTenantDomain_UsesDefaultCacheKey()
    {
        CopilotProxyOptions noCache = null!;
        cacheMock
            .Setup(c => c.TryGet(It.IsAny<string>(), out noCache))
            .Returns(false);

        dynamicConfigurationProviderMock
            .Setup(d => d.GetTenantDomainNameFromRequest())
            .Returns(string.Empty);

        _ = await service.GetOptionsAsync();

        cacheMock.Verify(
            c => c.Set(
                It.Is<string>(k => k == "COPILOT_PROXY_OPTIONS:default"),
                It.IsAny<CopilotProxyOptions>(),
                It.Is<TimeSpan>(t => t == TimeSpan.FromSeconds(30))),
            Times.Once);
    }

    [TestMethod]
    public async Task GetOptionsAsync_WithMixedCaseTenantDomain_UsesLowerCaseCacheKey()
    {
        CopilotProxyOptions noCache = null!;
        cacheMock
            .Setup(c => c.TryGet(It.IsAny<string>(), out noCache))
            .Returns(false);

        dynamicConfigurationProviderMock
            .Setup(d => d.GetTenantDomainNameFromRequest())
            .Returns("Tenant-A.Example.Com");

        _ = await service.GetOptionsAsync();

        cacheMock.Verify(
            c => c.Set(
                It.Is<string>(k => k == "COPILOT_PROXY_OPTIONS:tenant-a.example.com"),
                It.IsAny<CopilotProxyOptions>(),
                It.Is<TimeSpan>(t => t == TimeSpan.FromSeconds(30))),
            Times.Once);
    }

    [TestMethod]
    public async Task GetOptionsAsync_WithCachedValue_ReturnsCachedOptions()
    {
        var cached = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://cached.example/v1/chat/completions",
        };

        cacheMock
            .Setup(c => c.TryGet(It.IsAny<string>(), out cached))
            .Returns(true);

        var result = await service.GetOptionsAsync();

        Assert.IsTrue(result.Enabled);
        Assert.AreEqual("https://cached.example/v1/chat/completions", result.Endpoint);

        cacheMock.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<CopilotProxyOptions>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [TestMethod]
    public async Task GetOptionsAsync_WithNoSetting_ReturnsDefaultOptions()
    {
        CopilotProxyOptions noCache = null!;
        cacheMock
            .Setup(c => c.TryGet(It.IsAny<string>(), out noCache))
            .Returns(false);

        var options = await service.GetOptionsAsync();

        Assert.IsFalse(options.Enabled);
        Assert.AreEqual(string.Empty, options.Endpoint);
        Assert.AreEqual("auto", options.Model);
        Assert.AreEqual(string.Empty, options.AccessToken);
        Assert.AreEqual(8000, options.TimeoutMs);
        Assert.AreEqual(0.2, options.Temperature);
        Assert.AreEqual(160, options.MaxTokens);

        cacheMock.Verify(
            c => c.Set(
                It.Is<string>(k => k == "COPILOT_PROXY_OPTIONS:tenant-a.example.com"),
                It.IsAny<CopilotProxyOptions>(),
                It.IsAny<TimeSpan>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetOptionsAsync_WithValidSetting_ReturnsSavedOptions()
    {
        CopilotProxyOptions noCache = null!;
        cacheMock
            .Setup(c => c.TryGet(It.IsAny<string>(), out noCache))
            .Returns(false);

        var expected = new CopilotProxyOptions
        {
            Enabled = true,
            Endpoint = "https://upstream.example/v1/chat/completions",
            Model = "gpt-4.1-mini",
            AccessToken = "token-value",
            TimeoutMs = 10000,
            Temperature = 0.4,
            MaxTokens = 300,
            EnableEmbeddingSemanticRerank = true,
            EmbeddingModel = "text-embedding-3-small",
            AutoRetryUnknownModel = true,
        };

        dbContext.Settings.Add(new Setting
        {
            Group = CopilotGroup,
            Name = nameof(CopilotProxyOptions),
            Value = JsonConvert.SerializeObject(expected),
            Description = "Settings used by the Copilot completion proxy",
        });
        await dbContext.SaveChangesAsync();

        var actual = await service.GetOptionsAsync();

        Assert.IsTrue(actual.Enabled);
        Assert.AreEqual(expected.Endpoint, actual.Endpoint);
        Assert.AreEqual(expected.Model, actual.Model);
        Assert.AreEqual(expected.AccessToken, actual.AccessToken);
        Assert.AreEqual(expected.TimeoutMs, actual.TimeoutMs);
        Assert.AreEqual(expected.Temperature, actual.Temperature);
        Assert.AreEqual(expected.MaxTokens, actual.MaxTokens);
        Assert.AreEqual(expected.EnableEmbeddingSemanticRerank, actual.EnableEmbeddingSemanticRerank);
        Assert.AreEqual(expected.EmbeddingModel, actual.EmbeddingModel);
        Assert.AreEqual(expected.AutoRetryUnknownModel, actual.AutoRetryUnknownModel);
    }

    [TestMethod]
    public async Task GetOptionsAsync_WithInvalidJson_ReturnsDefaultOptionsAndLogsWarning()
    {
        CopilotProxyOptions noCache = null!;
        cacheMock
            .Setup(c => c.TryGet(It.IsAny<string>(), out noCache))
            .Returns(false);

        dbContext.Settings.Add(new Setting
        {
            Group = CopilotGroup,
            Name = nameof(CopilotProxyOptions),
            Value = "{ invalid json }",
            Description = "Settings used by the Copilot completion proxy",
        });
        await dbContext.SaveChangesAsync();

        var options = await service.GetOptionsAsync();

        Assert.IsFalse(options.Enabled);
        Assert.AreEqual(string.Empty, options.Endpoint);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<JsonException>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [TestMethod]
    public async Task InvalidateCurrentTenantCacheAsync_RemovesCurrentTenantCacheKey()
    {
        // Act
        await service.InvalidateCurrentTenantCacheAsync();

        // Assert
        cacheMock.Verify(
            c => c.Remove("COPILOT_PROXY_OPTIONS:tenant-a.example.com"),
            Times.Once);
    }
}
