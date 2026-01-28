// <copyright file="DynamicConfigurationProviderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Cosmos.DynamicConfig;
using Cosmos.DynamicConfig.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sky.Tests.DynamicConfig
{
    [TestClass]
    public class DynamicConfigurationProviderTests
    {
        private static string TempFilePath(string name) => Path.Combine(Path.GetTempPath(), name);

        private static string SqliteConnectionString(string filePath)
        {
            return $"Data Source={filePath};";
        }

        private async Task SeedConfigDatabaseAsync(string configDbFile, Connection[] connections)
        {
            var configConn = SqliteConnectionString(configDbFile);
            var options = AspNetCore.Identity.FlexDb.CosmosDbOptionsBuilder.GetDbOptions<DynamicConfigDbContext>(configConn);

            await using var ctx = new DynamicConfigDbContext(options);
            // ensure a clean DB
            try { ctx.Database.EnsureDeleted(); } catch { }
            ctx.Database.EnsureCreated();

            ctx.Connections.AddRange(connections);
            await ctx.SaveChangesAsync();
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_UsesHostHeader_ReturnsTenantDbConn()
        {
            // Arrange
            var configDb = TempFilePath($"skycms-config-{Guid.NewGuid()}.db");
            var tenantA = TempFilePath($"tenantA-{Guid.NewGuid()}.db");
            var tenantB = TempFilePath($"tenantB-{Guid.NewGuid()}.db");

            // remove files if present
            foreach (var f in new[] { configDb, tenantA, tenantB })
            {
                if (File.Exists(f)) File.Delete(f);
            }

            var connA = new Connection
            {
                DomainNames = new[] { "tenanta.example.com" }, // lowercased for normalization
                DbConn = SqliteConnectionString(tenantA),
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://tenantA.example.com",
                ResourceGroup = "rg"
            };

            var connB = new Connection
            {
                DomainNames = new[] { "tenantb.example.com" }, // lowercased for normalization
                DbConn = SqliteConnectionString(tenantB),
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://tenantB.example.com",
                ResourceGroup = "rg"
            };

            await SeedConfigDatabaseAsync(configDb, new[] { connA, connB });

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString("tenantA.example.com");

            var httpAccessor = new HttpContextAccessor { HttpContext = httpContext };
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = false });

            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, NullLogger<DynamicConfigurationProvider>.Instance, proxySettings);

            // Act
            var dbConn = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNotNull(dbConn);
            Assert.AreEqual(connA.DbConn, dbConn);

            // cleanup
            foreach (var f in new[] { configDb, tenantA, tenantB })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_UsesXOriginHostname_WhenTrustedProxy_ReturnsTenantDbConn()
        {
            // Arrange
            var configDb = TempFilePath($"skycms-config-{Guid.NewGuid()}.db");
            var tenantA = TempFilePath($"tenantA-{Guid.NewGuid()}.db");
            var tenantB = TempFilePath($"tenantB-{Guid.NewGuid()}.db");

            foreach (var f in new[] { configDb, tenantA, tenantB })
            {
                if (File.Exists(f)) File.Delete(f);
            }

            var connA = new Connection
            {
                DomainNames = new[] { "tenanta.example.com" }, // lowercased for normalization
                DbConn = SqliteConnectionString(tenantA),
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://tenantA.example.com",
                ResourceGroup = "rg"
            };

            var connB = new Connection
            {
                DomainNames = new[] { "tenantb.example.com" }, // lowercased for normalization
                DbConn = SqliteConnectionString(tenantB),
                StorageConn = "Storage=test;",
                WebsiteUrl = "https://tenantB.example.com",
                ResourceGroup = "rg"
            };

            await SeedConfigDatabaseAsync(configDb, new[] { connA, connB });

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpContext = new DefaultHttpContext();
            // Host is not tenantB, but x-origin-hostname should override when trusted
            httpContext.Request.Host = new HostString("someproxy.local");
            httpContext.Request.Headers["x-origin-hostname"] = "tenantB.example.com";
            httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

            var httpAccessor = new HttpContextAccessor { HttpContext = httpContext };
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = true, TrustedProxyIPs = new List<string> { "127.0.0.1" } });

            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, NullLogger<DynamicConfigurationProvider>.Instance, proxySettings);

            // Act
            var dbConn = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNotNull(dbConn);
            Assert.AreEqual(connB.DbConn, dbConn);

            // cleanup
            foreach (var f in new[] { configDb, tenantA, tenantB })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }
    }
}
