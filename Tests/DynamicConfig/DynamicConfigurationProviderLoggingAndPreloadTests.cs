// <copyright file="DynamicConfigurationProviderLoggingAndPreloadTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

using Cosmos.DynamicConfig;
using Cosmos.DynamicConfig.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

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

        [TestMethod]
        public async Task PreloadAllConnectionsAsync_SequentialCallsWithinThrottleWindow_InvokeCoreOnce()
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

            int invokeCount = 0;
            var provider = new TestPreloadProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings,
                async ct =>
                {
                    invokeCount++;
                    await Task.CompletedTask;
                });

            await provider.PreloadAllConnectionsAsync();
            await provider.PreloadAllConnectionsAsync();

            Assert.AreEqual(1, invokeCount, "Second call should be skipped inside preload throttle interval.");
        }

        [TestMethod]
        public async Task PreloadAllConnectionsAsync_CoreThrows_PropagatesException()
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

            var provider = new TestPreloadProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings,
                ct => throw new InvalidOperationException("preload failed"));

            try
            {
                await provider.PreloadAllConnectionsAsync();
                Assert.Fail("Expected InvalidOperationException was not thrown.");
            }
            catch (InvalidOperationException)
            {
                // Expected.
            }
        }

        [TestMethod]
        public async Task GetTenantConnectionAsync_MissingDomain_UsesNegativeCacheUntilExpiry()
        {
            var configDb = GetConfigFilePath();
            var tenantDb = TempFilePath($"skycms-tenant-{Guid.NewGuid()}.db");

            try
            {
                var dbOptions = AspNetCore.Identity.FlexDb.CosmosDbOptionsBuilder.GetDbOptions<DynamicConfigDbContext>(SqliteConnectionString(configDb));
                await using (var context = new DynamicConfigDbContext(dbOptions))
                {
                    context.Database.EnsureDeleted();
                    context.Database.EnsureCreated();
                }

                var inMemorySettings = new Dictionary<string, string>
                {
                    { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) },
                    { "MultiTenant", "true" }
                };

                var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
                var provider = new DynamicConfigurationProvider(
                    configuration,
                    CreateHttpContextAccessor("missing.tenant.test"),
                    new MemoryCache(new MemoryCacheOptions()),
                    new Mock<ILogger<DynamicConfigurationProvider>>().Object,
                    Options.Create(new ProxySettings()));

                var firstLookup = await provider.GetTenantConnectionAsync("missing.tenant.test");
                Assert.IsNull(firstLookup);

                await using (var context = new DynamicConfigDbContext(dbOptions))
                {
                    context.Connections.Add(new Connection
                    {
                        DomainNames = new[] { "missing.tenant.test" },
                        DbConn = SqliteConnectionString(tenantDb),
                        StorageConn = "storage-conn",
                        WebsiteUrl = "https://missing.tenant.test",
                        ResourceGroup = "rg"
                    });
                    await context.SaveChangesAsync();
                }

                var secondLookup = await provider.GetTenantConnectionAsync("missing.tenant.test");
                Assert.IsNull(secondLookup, "Negative cache should prevent immediate DB re-query after a miss.");
            }
            finally
            {
                foreach (var file in new[] { configDb, tenantDb })
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        [TestMethod]
        public async Task GetTenantConnectionAsync_ConcurrentSameDomain_DeduplicatesDatabaseFetch()
        {
            var configDb = GetConfigFilePath();
            var tenantDb = TempFilePath($"skycms-tenant-{Guid.NewGuid()}.db");

            try
            {
                var dbOptions = AspNetCore.Identity.FlexDb.CosmosDbOptionsBuilder.GetDbOptions<DynamicConfigDbContext>(SqliteConnectionString(configDb));
                await using (var context = new DynamicConfigDbContext(dbOptions))
                {
                    context.Database.EnsureDeleted();
                    context.Database.EnsureCreated();
                    context.Connections.Add(new Connection
                    {
                        DomainNames = new[] { "singleflight.tenant.test" },
                        DbConn = SqliteConnectionString(tenantDb),
                        StorageConn = "storage-conn",
                        WebsiteUrl = "https://singleflight.tenant.test",
                        ResourceGroup = "rg"
                    });
                    await context.SaveChangesAsync();
                }

                var inMemorySettings = new Dictionary<string, string>
                {
                    { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) },
                    { "MultiTenant", "true" }
                };

                var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
                var provider = new CountingDbContextProvider(
                    configuration,
                    CreateHttpContextAccessor("singleflight.tenant.test"),
                    new MemoryCache(new MemoryCacheOptions()),
                    new Mock<ILogger<DynamicConfigurationProvider>>().Object,
                    Options.Create(new ProxySettings()),
                    dbOptions);

                var lookups = Enumerable.Range(0, 8)
                    .Select(_ => provider.GetTenantConnectionAsync("singleflight.tenant.test"));
                var results = await Task.WhenAll(lookups);

                Assert.IsTrue(results.All(r => r != null));
                Assert.AreEqual(1, provider.GetDbContextCallCount, "Concurrent cache misses for same domain should use a single DB fetch.");
            }
            finally
            {
                foreach (var file in new[] { configDb, tenantDb })
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                    }
                }
            }
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

        private sealed class CountingDbContextProvider : DynamicConfigurationProvider
        {
            private readonly DbContextOptions<DynamicConfigDbContext> _dbOptions;
            private int _getDbContextCallCount;

            public CountingDbContextProvider(
                IConfiguration configuration,
                IHttpContextAccessor httpContextAccessor,
                IMemoryCache memoryCache,
                ILogger<DynamicConfigurationProvider> logger,
                IOptions<ProxySettings> proxyOptions,
                DbContextOptions<DynamicConfigDbContext> dbOptions)
                : base(configuration, httpContextAccessor, memoryCache, logger, proxyOptions)
            {
                _dbOptions = dbOptions;
            }

            public int GetDbContextCallCount => _getDbContextCallCount;

            protected override DynamicConfigDbContext GetDbContext()
            {
                Interlocked.Increment(ref _getDbContextCallCount);
                return new DynamicConfigDbContext(_dbOptions);
            }
        }
    }
}
