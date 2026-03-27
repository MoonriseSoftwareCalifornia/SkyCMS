// <copyright file="CacheServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Cosmos.Common.Tests.Services;

using Cosmos.Common.Services.Caching;
using Cosmos.DynamicConfig;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// Unit tests for <see cref="CacheService{T}"/>.
/// </summary>
[TestClass]
public class CacheServiceTests
{
    [TestMethod]
    public void SetAndGet_WithoutTenantProvider_UsesUnscopedKey()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<CacheService<string>>>();
        var cacheService = new CacheService<string>(memoryCache, loggerMock.Object);

        cacheService.Set("shared-key", "value", TimeSpan.FromMinutes(1));
        var result = cacheService.Get("shared-key");

        Assert.AreEqual("value", result);
    }

    [TestMethod]
    public void SetAndGet_WithDifferentTenants_IsolatesValuesByScopedKey()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var loggerTenantAMock = new Mock<ILogger<CacheService<string>>>();
        var loggerTenantBMock = new Mock<ILogger<CacheService<string>>>();

        var tenantAProvider = new Mock<IDynamicConfigurationProvider>();
        tenantAProvider.Setup(p => p.GetTenantDomainNameFromRequest()).Returns("tenant-a.example.com");

        var tenantBProvider = new Mock<IDynamicConfigurationProvider>();
        tenantBProvider.Setup(p => p.GetTenantDomainNameFromRequest()).Returns("tenant-b.example.com");

        var cacheServiceTenantA = new CacheService<string>(memoryCache, loggerTenantAMock.Object, tenantAProvider.Object);
        var cacheServiceTenantB = new CacheService<string>(memoryCache, loggerTenantBMock.Object, tenantBProvider.Object);

        cacheServiceTenantA.Set("sensitive-key", "tenant-a-secret", TimeSpan.FromMinutes(1));
        cacheServiceTenantB.Set("sensitive-key", "tenant-b-secret", TimeSpan.FromMinutes(1));

        var tenantAValue = cacheServiceTenantA.Get("sensitive-key");
        var tenantBValue = cacheServiceTenantB.Get("sensitive-key");

        Assert.AreEqual("tenant-a-secret", tenantAValue);
        Assert.AreEqual("tenant-b-secret", tenantBValue);
    }

    [TestMethod]
    public void Remove_WithDifferentTenants_RemovesOnlyCurrentTenantValue()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var tenantAProvider = new Mock<IDynamicConfigurationProvider>();
        tenantAProvider.Setup(p => p.GetTenantDomainNameFromRequest()).Returns("tenant-a.example.com");

        var tenantBProvider = new Mock<IDynamicConfigurationProvider>();
        tenantBProvider.Setup(p => p.GetTenantDomainNameFromRequest()).Returns("tenant-b.example.com");

        var loggerTenantAMock = new Mock<ILogger<CacheService<string>>>();
        var loggerTenantBMock = new Mock<ILogger<CacheService<string>>>();

        var cacheServiceTenantA = new CacheService<string>(memoryCache, loggerTenantAMock.Object, tenantAProvider.Object);
        var cacheServiceTenantB = new CacheService<string>(memoryCache, loggerTenantBMock.Object, tenantBProvider.Object);

        cacheServiceTenantA.Set("sensitive-key", "tenant-a-secret", TimeSpan.FromMinutes(1));
        cacheServiceTenantB.Set("sensitive-key", "tenant-b-secret", TimeSpan.FromMinutes(1));

        cacheServiceTenantA.Remove("sensitive-key");

        Assert.IsFalse(cacheServiceTenantA.TryGet("sensitive-key", out _));
        Assert.IsTrue(cacheServiceTenantB.TryGet("sensitive-key", out var tenantBValue));
        Assert.AreEqual("tenant-b-secret", tenantBValue);
    }
}
