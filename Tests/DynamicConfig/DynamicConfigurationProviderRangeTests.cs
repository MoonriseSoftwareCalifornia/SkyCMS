// <copyright file="DynamicConfigurationProviderRangeTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

using Cosmos.DynamicConfig;
using Cosmos.DynamicConfig.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace Sky.Tests.DynamicConfig
{
    [TestClass]
    public class DynamicConfigurationProviderRangeTests
    {
        private static string TempFilePath(string name) => Path.Combine(Path.GetTempPath(), name);

        private static string dns1 = "acme-range.test";
        private static string dns2 = "range-target.test";

        private static string SqliteConnectionString(string filePath)
        {
            return $"Data Source={filePath};";
        }

        private static string GetConfigFilePath() => TempFilePath($"skycms-config-{Guid.NewGuid():N}.db");

        private static string GetTenantDbFilePath(string prefix) => TempFilePath($"{prefix}-{Guid.NewGuid():N}.db");

        private static IHttpContextAccessor CreateHttpContextAccessor(string host)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString(host);
            return new HttpContextAccessor { HttpContext = httpContext };
        }

        private async Task SeedConfigDatabaseAsync(string configDbFile, Connection[] connections)
        {
            var configConn = SqliteConnectionString(configDbFile);
            var options = AspNetCore.Identity.FlexDb.CosmosDbOptionsBuilder.GetDbOptions<DynamicConfigDbContext>(configConn);

            await using var ctx = new DynamicConfigDbContext(options);
            try { ctx.Database.EnsureDeleted(); } catch { }
            ctx.Database.EnsureCreated();

            ctx.Connections.AddRange(connections);
            await ctx.SaveChangesAsync();
        }

        // Helper to match provider's normalization logic for domain names
        private static string NormalizeDomainName(string domain)
        {
            return domain?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_TrustedProxy_CidrEntry_AllowsXOrigin()
        {
            var configDb = GetConfigFilePath();
            var tenantA = GetTenantDbFilePath("acme-range");
            var tenantB = GetTenantDbFilePath("range-target");
            foreach (var f in new[] { configDb, tenantA, tenantB }) { if (File.Exists(f)) File.Delete(f); }

            var connA = new Connection { DomainNames = new[] { dns1 }, DbConn = SqliteConnectionString(tenantA), StorageConn = "s1", WebsiteUrl = $"https://{dns1}", ResourceGroup = "rg" };
            var connB = new Connection { DomainNames = new[] { NormalizeDomainName(dns2) }, DbConn = SqliteConnectionString(tenantB), StorageConn = "s2", WebsiteUrl = $"https://{dns2}", ResourceGroup = "rg" };
            await SeedConfigDatabaseAsync(configDb, new[] { connA, connB });

            var inMemorySettings = new Dictionary<string, string?> {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) },
                { "MultiTenant", "true" }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpAccessor = CreateHttpContextAccessor("someproxy.local");
            httpAccessor.HttpContext.Request.Headers["x-origin-hostname"] = dns2;
            // Use an IP inside 10.0.0.0/8
            httpAccessor.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.5.6.7");
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            // Use CIDR entry that should include 10.5.6.7
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = true, TrustedProxyIPs = new List<string> { "10.0.0.0/8" } });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            var dbConn = await provider.GetDatabaseConnectionStringAsync();
            Assert.IsNotNull(dbConn);
            Assert.AreEqual(connB.DbConn, dbConn);

            foreach (var f in new[] { configDb, tenantA, tenantB }) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_TrustedProxy_RangeEntry_AllowsXOrigin()
        {
            var configDb = GetConfigFilePath();
            var tenantA = GetTenantDbFilePath("acme-range");
            var tenantB = GetTenantDbFilePath("range-target");
            foreach (var f in new[] { configDb, tenantA, tenantB }) { if (File.Exists(f)) File.Delete(f); }

            var connA = new Connection { DomainNames = new[] { dns1 }, DbConn = SqliteConnectionString(tenantA), StorageConn = "s1", WebsiteUrl = $"https://{dns1}", ResourceGroup = "rg" };
            var connB = new Connection { DomainNames = new[] { NormalizeDomainName(dns2) }, DbConn = SqliteConnectionString(tenantB), StorageConn = "s2", WebsiteUrl = $"https://{dns2}", ResourceGroup = "rg" };
            await SeedConfigDatabaseAsync(configDb, new[] { connA, connB });

            var inMemorySettings = new Dictionary<string, string?> { { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) }, { "MultiTenant", "true" } };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpAccessor = CreateHttpContextAccessor("someproxy.local");
            httpAccessor.HttpContext.Request.Headers["x-origin-hostname"] = dns2;
            // Use an IP inside the specified range
            httpAccessor.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.5.10");
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            // Range that includes 192.168.5.10
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = true, TrustedProxyIPs = new List<string> { "192.168.5.1-192.168.5.100" } });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            var dbConn = await provider.GetDatabaseConnectionStringAsync();
            Assert.IsNotNull(dbConn);
            Assert.AreEqual(connB.DbConn, dbConn);

            foreach (var f in new[] { configDb, tenantA, tenantB }) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
        }
    }
}
