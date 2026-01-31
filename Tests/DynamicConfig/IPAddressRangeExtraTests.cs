// <copyright file="IPAddressRangeExtraTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

using System;
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

namespace Sky.Tests.DynamicConfig
{
    [TestClass]
    public class IPAddressRangeExtraTests
    {
        private static string TempFilePath(string name) => Path.Combine(Path.GetTempPath(), name);

        private static string SqliteConnectionString(string filePath)
        {
            return $"Data Source={filePath};";
        }

        private static string GetConfigFilePath() => TempFilePath($"skycms-config-{Guid.NewGuid()}.db");

        private static IHttpContextAccessor CreateHttpContextAccessor(string host)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString(host);
            return new HttpContextAccessor { HttpContext = httpContext };
        }

        [TestMethod]
        public void Parse_BracketedIpv6_And_ZoneId_Reject()
        {
            try
            {
                IPAddressRange.Parse("[::1]");
                Assert.Fail();
            }
            catch (FormatException)
            {
                // expected
            }

            // zone id with name likely invalid for IPAddress.TryParse
            try
            {
                IPAddressRange.Parse("fe80::1%eth0");
                Assert.Fail();
            }
            catch (FormatException)
            {
                // expected
            }
        }

        [TestMethod]
        public void Parse_Ipv4MappedIpv6_IsTreatedAsIpv6()
        {
            var s = "::ffff:192.0.2.1";
            var r = IPAddressRange.Parse(s);
            Assert.IsTrue(r.Contains(IPAddress.Parse("::ffff:192.0.2.1")));
            // Should not match the plain IPv4 when range was defined as IPv6
            Assert.IsFalse(r.Contains(IPAddress.Parse("192.0.2.1")));
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_HttpContextNull_UsesProvidedDomain()
        {
            var configDb = GetConfigFilePath();
            var tenant = TempFilePath("tenant.db");
            foreach (var f in new[] { configDb, tenant }) { if (File.Exists(f)) File.Delete(f); }

            var conn = new Connection { DomainNames = new[] { "example.test" }, DbConn = SqliteConnectionString(tenant), StorageConn = "s", WebsiteUrl = "https://example.test", ResourceGroup = "rg" };

            var options = AspNetCore.Identity.FlexDb.CosmosDbOptionsBuilder.GetDbOptions<DynamicConfigDbContext>(SqliteConnectionString(configDb));
            await using (var ctx = new DynamicConfigDbContext(options))
            {
                try { ctx.Database.EnsureDeleted(); } catch { }
                ctx.Database.EnsureCreated();
                ctx.Connections.Add(conn);
                await ctx.SaveChangesAsync();
            }

            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?> { { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) } };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            // HttpContextAccessor present but HttpContext is null
            var httpAccessor = new HttpContextAccessor { HttpContext = null };
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = false });
            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            var dbConn = await provider.GetDatabaseConnectionStringAsync("example.test");
            Assert.IsNotNull(dbConn);
            Assert.AreEqual(conn.DbConn, dbConn);

            foreach (var f in new[] { configDb, tenant }) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
        }

        [TestMethod]
        public async Task Preload_CachePersistsAfterDbChange()
        {
            var configDb = GetConfigFilePath();
            var tenant = TempFilePath("tenant.db");
            foreach (var f in new[] { configDb, tenant }) { if (File.Exists(f)) File.Delete(f); }

            var conn = new Connection { DomainNames = new[] { "cache.test" }, DbConn = SqliteConnectionString(tenant), StorageConn = "s", WebsiteUrl = "https://cache.test", ResourceGroup = "rg" };

            var options = AspNetCore.Identity.FlexDb.CosmosDbOptionsBuilder.GetDbOptions<DynamicConfigDbContext>(SqliteConnectionString(configDb));
            await using (var ctx = new DynamicConfigDbContext(options))
            {
                try { ctx.Database.EnsureDeleted(); } catch { }
                ctx.Database.EnsureCreated();
                ctx.Connections.Add(conn);
                await ctx.SaveChangesAsync();
            }

            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?> { { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) } };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpAccessor = CreateHttpContextAccessor("cache.test");
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings());
            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            // Preload cache
            await provider.PreloadAllConnectionsAsync();

            // Remove from DB
            await using (var ctx = new DynamicConfigDbContext(options))
            {
                var existing = await ctx.Connections.FindAsync(conn.Id);
                if (existing != null) ctx.Connections.Remove(existing);
                await ctx.SaveChangesAsync();
            }

            // Should still resolve from cache
            var cached = await provider.GetTenantConnectionAsync("cache.test");
            Assert.IsNotNull(cached);

            foreach (var f in new[] { configDb, tenant }) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
        }

        [TestMethod]
        public async Task Preload_Exception_DoesNotDeadlockAndAllowsRetry()
        {
            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?>();
            inMemorySettings["ConnectionStrings:ConfigDbConnectionString"] = "FakeConnectionString";
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
            var httpAccessor = CreateHttpContextAccessor("localhost");
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings());
            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();

            bool first = true;
            var provider = new TestPreloadProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings, ct =>
            {
                if (first)
                {
                    first = false;
                    throw new InvalidOperationException("boom");
                }
                return Task.CompletedTask;
            });

            try
            {
                await provider.PreloadAllConnectionsAsync();
                Assert.Fail("Expected InvalidOperationException on first preload");
            }
            catch (InvalidOperationException)
            {
                // expected
            }

            // Second call should be allowed and not blocked by previous exception
            await provider.PreloadAllConnectionsAsync();
        }

        [TestMethod]
        public void Fuzz_Parse_RandomInputs_DoNotCrash()
        {
            var rand = new Random(12345);
            var chars = "0123456789abcdefABCDEF:./- %[]abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            for (int i = 0; i < 1000; i++)
            {
                var len = rand.Next(1, 40);
                var s = new char[len];
                for (int j = 0; j < len; j++) s[j] = chars[rand.Next(chars.Length)];
                var str = new string(s);
                try
                {
                    var _ = IPAddressRange.Parse(str);
                }
                catch (FormatException) { }
                catch (ArgumentException) { }
                catch (Exception ex)
                {
                    Assert.Fail($"Unexpected exception type {ex.GetType()} for input '{str}'");
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
    }
}
