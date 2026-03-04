// <copyright file="DynamicConfigurationProviderMultiTenantFlagTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
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
    public class DynamicConfigurationProviderMultiTenantFlagTests
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

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_HeaderIgnored_WhenMultiTenantFalse()
        {
            var configDb = GetConfigFilePath();
            var tenantA = TempFilePath("mt-a.db");
            var tenantB = TempFilePath("mt-b.db");
            foreach (var f in new[] { configDb, tenantA, tenantB }) { if (File.Exists(f)) File.Delete(f); }

            var hostA = "tenant-a.test";
            var hostB = "tenant-b.test";

            var connA = new Connection { DomainNames = new[] { hostA }, DbConn = SqliteConnectionString(tenantA), StorageConn = "s1", WebsiteUrl = $"https://{hostA}", ResourceGroup = "rg" };
            var connB = new Connection { DomainNames = new[] { hostB }, DbConn = SqliteConnectionString(tenantB), StorageConn = "s2", WebsiteUrl = $"https://{hostB}", ResourceGroup = "rg" };
            await SeedConfigDatabaseAsync(configDb, new[] { connA, connB });

            var inMemorySettings = new Dictionary<string, string> {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) },
                { "MultiTenant", "false" }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpContext = new DefaultHttpContext();
            // Host header points to tenant A
            httpContext.Request.Host = new HostString(hostA);
            // x-origin-hostname would point to tenant B, but MultiTenant=false so header must be ignored
            httpContext.Request.Headers["x-origin-hostname"] = hostB;
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

            var httpAccessor = new HttpContextAccessor { HttpContext = httpContext };
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = true, TrustedProxyIPs = new List<string> { "127.0.0.1" } });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            var dbConn = await provider.GetDatabaseConnectionStringAsync();
            Assert.IsNotNull(dbConn);
            Assert.AreEqual(connA.DbConn, dbConn, "Expected header to be ignored when MultiTenant is false and host header used.");

            foreach (var f in new[] { configDb, tenantA, tenantB }) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
        }
    }
}
