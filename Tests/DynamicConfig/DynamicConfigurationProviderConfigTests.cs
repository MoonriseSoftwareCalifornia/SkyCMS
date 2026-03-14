// <copyright file="DynamicConfigurationProviderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

#nullable enable

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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Sky.Tests.DynamicConfig
{
    /// <summary>
    /// INTEGRATION TESTS using real DynamicConfigurationProvider with SQLite database.
    /// Tests actual behavior with seeded data, caching, and multi-tenant scenarios.
    /// </summary>
    /// <remarks>
    /// These tests create real provider instances with SQLite databases to verify end-to-end behavior.
    /// For fast unit tests with mocks, see Tests\Configuration\DynamicConfigurationProviderTests.cs
    /// </remarks>
    [TestClass]
    [TestCategory("MultiTenantConfiguration")]
    [TestCategory("IntegrationTest")]
    [DoNotParallelize]
    public class DynamicConfigurationProviderConfigTests
    {
        private static string TempFilePath(string name) => Path.Combine(Path.GetTempPath(), name);

        private static string dns1 = "acme.com";
        private static string dns2 = "perk.net";
        private static string dns3 = "cats.org";

        private static string db1 = "acme.db";
        private static string db2 = "perk.db";
        private static string db3 = "cats.db";

        private static string storage1 = "DefaultEndpointsProtocol=http;\r\nAccountName=account1;\r\nAccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;\r\nBlobEndpoint=http://127.0.0.1:10000/account1;";
        private static string storage2 = "DefaultEndpointsProtocol=http;\r\nAccountName=account2;\r\nAccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;\r\nBlobEndpoint=http://127.0.0.1:10000/account2;";
        private static string storage3 = "DefaultEndpointsProtocol=http;\r\nAccountName=account2;\r\nAccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;\r\nBlobEndpoint=http://127.0.0.1:10000/account3;";

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

        private static IHttpContextAccessor CreateHttpContextAccessor(string host)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString(host);
            return new HttpContextAccessor { HttpContext = httpContext };
        }

        private static string GetConfigFilePath()
        {
            return TempFilePath($"skycms-config-{Guid.NewGuid()}.db");
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_UsesHostHeader_ReturnsTenantDbConn()
        {
            // Arrange
            var configDb = GetConfigFilePath();
            var tenantA = TempFilePath(db1);
            var tenantB = TempFilePath(db2);
            var tenantC = TempFilePath(db3);

            // remove files if present
            foreach (var f in new[] { configDb, tenantA, tenantB, tenantC })
            {
                if (File.Exists(f)) File.Delete(f);
            }

            var connA = new Connection
            {
                DomainNames = new[] { dns1 }, // lowercased for normalization
                DbConn = SqliteConnectionString(tenantA),
                StorageConn = storage1,
                WebsiteUrl = $"https://{dns1}",
                ResourceGroup = "rg"
            };

            var connB = new Connection
            {
                DomainNames = new[] { dns2 }, // lowercased for normalization
                DbConn = SqliteConnectionString(tenantB),
                StorageConn = storage2,
                WebsiteUrl = $"https://{dns2}",
                ResourceGroup = "rg"
            };

            var connC = new Connection
            {
                DomainNames = new[] { dns3 }, // lowercased for normalization
                DbConn = SqliteConnectionString(tenantC),
                StorageConn = storage3,
                WebsiteUrl = $"https://{dns3}",
                ResourceGroup = "rg"
            };

            await SeedConfigDatabaseAsync(configDb, new[] { connA, connB, connC });

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) },
                { "MultiTenant", "true" }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpAccessor = CreateHttpContextAccessor(dns1);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = false });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            // Act
            var dbConn = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNotNull(dbConn);
            Assert.AreEqual(connA.DbConn, dbConn);
            Assert.AreEqual(connA.StorageConn, storage1);

            // cleanup
            foreach (var f in new[] { configDb, tenantA, tenantB })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_UnknownHost_ReturnsNull()
        {
            // Arrange
            var configDb = GetConfigFilePath();
            var tenantA = TempFilePath(db1);

            foreach (var f in new[] { configDb, tenantA })
            {
                if (File.Exists(f)) File.Delete(f);
            }

            var connA = new Connection
            {
                DomainNames = new[] { dns1 },
                DbConn = SqliteConnectionString(tenantA),
                StorageConn = storage1,
                WebsiteUrl = $"https://{dns1}",
                ResourceGroup = "rg"
            };

            await SeedConfigDatabaseAsync(configDb, new[] { connA });

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpAccessor = CreateHttpContextAccessor("unknownhost.com");
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = false });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            // Act
            var dbConn = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNull(dbConn);

            // cleanup
            foreach (var f in new[] { configDb, tenantA })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_MultipleDomainsPerTenant_ResolvesCorrectly()
        {
            // Arrange
            var configDb = GetConfigFilePath();
            var tenantA = TempFilePath(db1);

            foreach (var f in new[] { configDb, tenantA })
            {
                if (File.Exists(f)) File.Delete(f);
            }

            var connA = new Connection
            {
                DomainNames = new[] { dns1, "alias.com" },
                DbConn = SqliteConnectionString(tenantA),
                StorageConn = storage1,
                WebsiteUrl = $"https://{dns1}",
                ResourceGroup = "rg"
            };

            await SeedConfigDatabaseAsync(configDb, new[] { connA });

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpAccessor = CreateHttpContextAccessor("alias.com");
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = false });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            // Act
            var dbConn = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNotNull(dbConn);
            Assert.AreEqual(connA.DbConn, dbConn);

            // cleanup
            foreach (var f in new[] { configDb, tenantA })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_HostHeader_CaseInsensitive()
        {
            // Arrange
            var configDb = GetConfigFilePath();
            var tenantA = TempFilePath(db1);

            foreach (var f in new[] { configDb, tenantA })
            {
                if (File.Exists(f)) File.Delete(f);
            }

            var connA = new Connection
            {
                DomainNames = new[] { dns1 },
                DbConn = SqliteConnectionString(tenantA),
                StorageConn = storage1,
                WebsiteUrl = $"https://{dns1}",
                ResourceGroup = "rg"
            };

            await SeedConfigDatabaseAsync(configDb, new[] { connA });

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpAccessor = CreateHttpContextAccessor("ACME.COM"); // upper case
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = false });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            // Act
            var dbConn = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNotNull(dbConn);
            Assert.AreEqual(connA.DbConn, dbConn);

            // cleanup
            foreach (var f in new[] { configDb, tenantA })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_SpoofedXOriginHostname_UntrustedProxy_IgnoresHeader()
        {
            // Arrange
            var configDb = GetConfigFilePath();
            var tenantA = TempFilePath(db1);
            var tenantB = TempFilePath(db2);

            foreach (var f in new[] { configDb, tenantA, tenantB })
            {
                if (File.Exists(f)) File.Delete(f);
            }

            var connA = new Connection
            {
                DomainNames = new[] { dns1 },
                DbConn = SqliteConnectionString(tenantA),
                StorageConn = storage1,
                WebsiteUrl = $"https://{dns1}",
                ResourceGroup = "rg"
            };
            var connB = new Connection
            {
                DomainNames = new[] { dns2 },
                DbConn = SqliteConnectionString(tenantB),
                StorageConn = storage2,
                WebsiteUrl = $"https://{dns2}",
                ResourceGroup = "rg"
            };

            await SeedConfigDatabaseAsync(configDb, new[] { connA, connB });

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) },
                { "MultiTenant", "true" }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString(dns1);
            httpContext.Request.Headers["x-origin-hostname"] = dns2; // spoofed
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("8.8.8.8"); // not trusted

            var httpAccessor = new HttpContextAccessor { HttpContext = httpContext };
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = true, TrustedProxyIPs = new List<string> { "127.0.0.1" } });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            // Act
            var dbConn = await provider.GetDatabaseConnectionStringAsync();

            // Assert: Should resolve to dns1, not spoofed dns2
            Assert.IsNotNull(dbConn);
            Assert.AreEqual(connA.DbConn, dbConn);

            // cleanup
            foreach (var f in new[] { configDb, tenantA, tenantB })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_HostHeaderInjection_MalformedHost_HandledGracefully()
        {
            // Arrange
            var configDb = GetConfigFilePath();
            var tenantA = TempFilePath(db1);

            foreach (var f in new[] { configDb, tenantA })
            {
                if (File.Exists(f)) File.Delete(f);
            }

            var connA = new Connection
            {
                DomainNames = new[] { dns1 },
                DbConn = SqliteConnectionString(tenantA),
                StorageConn = storage1,
                WebsiteUrl = $"https://{dns1}",
                ResourceGroup = "rg"
            };

            await SeedConfigDatabaseAsync(configDb, new[] { connA });

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();


            var malformedHosts = new[]
            {
                "acme.com\r\nX-Forwarded-Host: attacker.com",
                "acme.com:8080",
                "acme.com/evil",
                "acme.com\0.attacker.com",
                " acme.com",
                "acme.com,attacker.com",
                "127.0.0.1",
                "acme.com#fragment",
                new string('a', 10000) + ".com",
                // Additional attack strings
                "acme.com\nSet-Cookie: session=evil",
                "acme.com%0d%0aX-Real-IP:127.0.0.1",
                "acme.com..attacker.com",
                ".acme.com",
                "acme.com.",
                "acme.com@attacker.com",
                "acme.com%00.attacker.com",
                "acme.com%20.attacker.com",
                "acme.com\tattacker.com",
                "acme.com%2Fattacker.com",
                "acme.com%2Eattacker.com",
                "acme.com%252Eattacker.com",
                "acme.com%2Cattacker.com",
                "acme.com%3Battacker.com",
                "acme.com%3A8080",
                "acme.com%23fragment",
                "acme.com%2B.attacker.com",
                "acme.com%2Dattacker.com",
                "acme.com%5Cattacker.com",
                "acme.com%2E%2Eattacker.com",
                "acme.com%2E%2E%2Fattacker.com",
                "[::1]",
                "[2001:db8::1]",
                "acme.com[::1]",
                "acme.com%",
                "acme.com%G0attacker.com",
                new string('a', 64) + ".com", // overly long label
                "acme...com",
                "-acme.com",
                "acme-.com",
                "acme.com(/*comment*/).attacker.com",
                "user:pass@acme.com",
                "acme.com/../attacker.com",
                "acme.com?foo=bar"
            };

            foreach (var malformedHost in malformedHosts)
            {
                // Start things off by creating a legit host name for the dns request.
                var httpAccessor = CreateHttpContextAccessor("proxy.acme.com");

                // Set the malformed host in the x-origin-hostname header.
                httpAccessor.HttpContext.Request.Headers["x-origin-hostname"] = malformedHost;
                var memoryCache = new MemoryCache(new MemoryCacheOptions());

                var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = true });

                var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
                var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

                // Act
                var dbConn = provider.GetValidHostName(malformedHost);

                // Assert: Should not match any tenant, handled gracefully
                Assert.AreEqual(dbConn, string.Empty);
            }

            // cleanup
            foreach (var f in new[] { configDb, tenantA })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_MissingHostAndXOriginHostname_ReturnsNull()
        {
            // Arrange
            var configDb = GetConfigFilePath();
            var tenantA = TempFilePath(db1);

            foreach (var f in new[] { configDb, tenantA })
            {
                if (File.Exists(f)) File.Delete(f);
            }

            var connA = new Connection
            {
                DomainNames = new[] { dns1 },
                DbConn = SqliteConnectionString(tenantA),
                StorageConn = storage1,
                WebsiteUrl = $"https://{dns1}",
                ResourceGroup = "rg"
            };

            await SeedConfigDatabaseAsync(configDb, new[] { connA });

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var httpContext = new DefaultHttpContext();
            // No Host, no x-origin-hostname
            var httpAccessor = new HttpContextAccessor { HttpContext = httpContext };
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = false });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            // Act
            var dbConn = await provider.GetDatabaseConnectionStringAsync();

            // Assert: Should not match
            Assert.IsNull(dbConn);

            // cleanup
            foreach (var f in new[] { configDb, tenantA })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_OverlyLongHostHeader_HandledGracefully()
        {
            // Arrange
            var configDb = GetConfigFilePath();
            var tenantA = TempFilePath(db1);

            foreach (var f in new[] { configDb, tenantA })
            {
                if (File.Exists(f)) File.Delete(f);
            }

            var connA = new Connection
            {
                DomainNames = new[] { dns1 },
                DbConn = SqliteConnectionString(tenantA),
                StorageConn = storage1,
                WebsiteUrl = $"https://{dns1}",
                ResourceGroup = "rg"
            };

            await SeedConfigDatabaseAsync(configDb, new[] { connA });

            var inMemorySettings = new Dictionary<string, string>
            {
                { "ConnectionStrings:ConfigDbConnectionString", SqliteConnectionString(configDb) }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            // Overly long host header
            var longHost = new string('a', 10000) + ".com";
            var httpAccessor = CreateHttpContextAccessor(longHost);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var proxySettings = Options.Create(new ProxySettings { TrustXOriginHostname = false });

            var mockLogger = new Mock<ILogger<DynamicConfigurationProvider>>();
            var provider = new DynamicConfigurationProvider(configuration, httpAccessor, memoryCache, mockLogger.Object, proxySettings);

            // Act
            var dbConn = await provider.GetDatabaseConnectionStringAsync();

            // Assert: Should not match, but should not throw
            Assert.IsNull(dbConn);

            // cleanup
            foreach (var f in new[] { configDb, tenantA })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }
    }
}
