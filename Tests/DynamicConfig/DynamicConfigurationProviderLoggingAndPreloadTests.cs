// <copyright file="DynamicConfigurationProviderLoggingAndPreloadTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Cosmos.DynamicConfig;
using Cosmos.DynamicConfig.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Linq;

namespace Sky.Tests.DynamicConfig
{
    [TestClass]
    public class DynamicConfigurationProviderLoggingAndPreloadTests
    {
        private static string TempFilePath(string name) => Path.Combine(Path.GetTempPath(), name);

        private static string SqliteConnectionString(string filePath)
        {
            return $"Data Source={filePath};";
        }

        private static string GetConfigFilePath()
        {
            return TempFilePath($"skycms-config-{Guid.NewGuid()}.db");
        }

        private static IHttpContextAccessor CreateHttpContextAccessor(string host)
        {
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            httpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString(host);
            return new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = httpContext };
        }

        private static bool LogMessageContainsInvalidTrustedProxyIPs(object v)
        {
            var props = v as IEnumerable<KeyValuePair<string, object>>;
            if (props != null)
            {
                var message = props.FirstOrDefault(kvp => kvp.Key == "{OriginalFormat}").Value as string;
                return message != null && message.Contains("Invalid entry in TrustedProxyIPs");
            }
            return v.ToString().Contains("Invalid entry in TrustedProxyIPs");
        }

        [TestMethod]
        public void Constructor_InvalidTrustedProxyEntries_LogsWarnings()
        {
            var inMemorySettings = new Dictionary<string, string>
            {
                {"ConnectionStrings:ConfigDbConnectionString", "DummyConnectionStringValue"}
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpAccessor = CreateHttpContextAccessor("localhost");
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var invalids = new List<string> { "not-an-ip", "192.168.1.0 255.255.255.0", "[::1]" };
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = false, TrustedProxyIPs = invalids });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();

            // Act: construct provider which will parse and log warnings for invalid entries
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            // Assert: verify at least one log entry contains the expected warning
            var found = mockLogger.Invocations.Any(invocation =>
                invocation.Method.Name == "Log" &&
                invocation.Arguments.Count >= 3 &&
                LogMessageContainsInvalidTrustedProxyIPs(invocation.Arguments[2]));
            Assert.IsTrue(found, "Expected a warning log for invalid TrustedProxyIPs, but none was found.");
        }

        [TestMethod]
        public async Task PreloadAllConnectionsAsync_ConcurrentCalls_InvokeCoreOnce()
        {
            var inMemorySettings = new Dictionary<string, string>
            {
                {"ConnectionStrings:ConfigDbConnectionString", "DummyConnectionStringValue"}
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpAccessor = CreateHttpContextAccessor("localhost");
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings());
            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();

            // Create a test provider that overrides the core preload and counts invocations
            int invokeCount = 0;
            var provider = new TestPreloadProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings,
                async (ct) =>
                {
                    // Simulate work
                    invokeCount++;
                    await Task.Delay(200);
                });

            // Start two concurrent preloads
            var t1 = provider.PreloadAllConnectionsAsync();
            var t2 = provider.PreloadAllConnectionsAsync();

            await Task.WhenAll(t1, t2);

            // Core should have been invoked exactly once
            Assert.AreEqual(1, invokeCount);
        }

        private sealed class TestPreloadProvider : DynamicConfigurationProvider
        {
            private readonly Func<System.Threading.CancellationToken, Task> _coreOverride;

            public TestPreloadProvider(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, ILogger<DynamicConfigurationProvider> logger, IOptions<ProxySettings> proxyOptions, Func<System.Threading.CancellationToken, Task> coreOverride)
                : base(configuration, httpContextAccessor, memoryCache, logger, proxyOptions)
            {
                _coreOverride = coreOverride ?? throw new ArgumentNullException(nameof(coreOverride));
            }

            protected override Task PreloadAllConnectionsCoreAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                return _coreOverride(cancellationToken);
            }
        }
    }
}
