// <copyright file="DynamicConfigurationProviderTenantResolutionTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Sky.Tests.TestHelpers;

namespace Sky.Tests.DynamicConfig
{
    /// <summary>
    /// SECURITY-FOCUSED TESTS for tenant resolution and header validation.
    /// Tests trusted proxy IP validation (IPv4/IPv6/CIDR), x-origin-hostname security,
    /// malformed hostname injection attacks, and domain normalization.
    /// </summary>
    /// <remarks>
    /// These tests focus on the security aspects of tenant resolution:
    /// - Trusted proxy IP/CIDR range validation
    /// - Header injection attack prevention
    /// - Malformed hostname handling
    /// For general provider tests, see Tests\DynamicConfig\DynamicConfigurationProviderTests.cs
    /// </remarks>
    [TestClass]
    [TestCategory("MultiTenantConfiguration")]
    [TestCategory("SecurityTest")]
    [TestCategory("IntegrationTest")]
    public class DynamicConfigurationProviderTenantResolutionTests
    {
        private Mock<ILogger<DynamicConfigurationProvider>> _loggerMock = null!;
        private IMemoryCache _memoryCache = null!;
        private IConfiguration _configuration = null!;
        private DbContextOptions<DynamicConfigDbContext> _dbOptions = null!;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<DynamicConfigurationProvider>>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());

            // Use a temporary SQLite file like the working tests
            var configDbFile = Path.Combine(Path.GetTempPath(), $"skycms-config-{Guid.NewGuid()}.db");

            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ConnectionStrings:ConfigDbConnectionString", $"Data Source={configDbFile};" },  // ✅ Correct key name
                    { "MultiTenant", "true" }
                });
            _configuration = configBuilder.Build();

            // Use SQLite provider like the working tests (not InMemory)
            _dbOptions = AspNetCore.Identity.FlexDb.CosmosDbOptionsBuilder
                .GetDbOptions<DynamicConfigDbContext>($"Data Source={configDbFile};");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _memoryCache?.Dispose();
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithXOriginHostnameHeader_ReturnsTenantFromHeader()
        {
            // Arrange
            var httpContext = CreateHttpContext("default-host.com");
            httpContext.Request.Headers["x-origin-hostname"] = "tenant-from-header.com";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("tenant-from-header.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithoutXOriginHostname_FallsBackToHostHeader()
        {
            // Arrange
            var httpContext = CreateHttpContext("fallback-host.com");
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("fallback-host.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithUntrustedProxy_IgnoresXOriginHostname()
        {
            // Arrange
            var httpContext = CreateHttpContext("host-from-header.com");
            httpContext.Request.Headers["x-origin-hostname"] = "malicious-tenant.com";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100"); // Not in trusted range

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("host-from-header.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithTrustXOriginHostnameDisabled_IgnoresHeader()
        {
            // Arrange
            var httpContext = CreateHttpContext("host-trust-disabled.com");
            httpContext.Request.Headers["x-origin-hostname"] = "should-be-ignored.com";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: false,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("host-trust-disabled.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_NormalizesToLowercase()
        {
            // Arrange
            var httpContext = CreateHttpContext("UPPERCASE-HOST.COM");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: false,
                trustedProxyIPs: new List<string>()
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: false);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("uppercase-host.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithMalformedXOriginHostname_FallsBackToHost()
        {
            // Arrange
            var httpContext = CreateHttpContext("safe-host.com");
            httpContext.Request.Headers["x-origin-hostname"] = "malformed<script>alert('xss')</script>.com";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("safe-host.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithUriInXOriginHostname_ExtractsHost()
        {
            // Arrange
            var httpContext = CreateHttpContext("default-host.com");
            httpContext.Request.Headers["x-origin-hostname"] = "http://tenant-with-uri.com/path";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("tenant-with-uri.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithNullHttpContext_ReturnsEmpty()
        {
            // Arrange
            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            httpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: false,
                trustedProxyIPs: new List<string>()
            );

            var provider = new DynamicConfigurationProvider(
                _configuration,
                httpContextAccessor.Object,
                _memoryCache,
                _loggerMock.Object,
                proxySettings
            );

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithNullRequest_ThrowsException()
        {
            // Arrange
            var httpContext = new Mock<HttpContext>();
            httpContext.Setup(x => x.Request).Returns((HttpRequest)null);

            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext.Object);

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: false,
                trustedProxyIPs: new List<string>()
            );

            var provider = new DynamicConfigurationProvider(
                _configuration,
                httpContextAccessor.Object,
                _memoryCache,
                _loggerMock.Object,
                proxySettings
            );

            // Act & Assert
            try
            {
                provider.GetTenantDomainNameFromRequest();
                Assert.Fail("Expected InvalidOperationException was not thrown");
            }
            catch (InvalidOperationException)
            {
                // Expected exception
            }
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_LogsWarningForMalformedHeader()
        {
            // Arrange
            var httpContext = CreateHttpContext("safe-host-logging.com");
            httpContext.Request.Headers["x-origin-hostname"] = "malformed!!!hostname";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rejected malformed x-origin-hostname header")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithValidHostnamePattern_AcceptsXOriginHostname()
        {
            // Arrange
            var httpContext = CreateHttpContext("default-host.com");
            httpContext.Request.Headers["x-origin-hostname"] = "valid-tenant-123.example.com";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("valid-tenant-123.example.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithEmptyXOriginHostname_FallsBackToHost()
        {
            // Arrange
            var httpContext = CreateHttpContext("fallback-empty-header.com");
            httpContext.Request.Headers["x-origin-hostname"] = string.Empty;
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("fallback-empty-header.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithWhitespaceXOriginHostname_FallsBackToHost()
        {
            // Arrange
            var httpContext = CreateHttpContext("fallback-whitespace.com");
            httpContext.Request.Headers["x-origin-hostname"] = "   ";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("fallback-whitespace.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_InSingleTenantMode_IgnoresXOriginHostname()
        {
            // Arrange
            var httpContext = CreateHttpContext("single-tenant-host.com");
            httpContext.Request.Headers["x-origin-hostname"] = "should-ignore.com";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "10.0.0.0/24" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: false);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("single-tenant-host.com", result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_WithIPv6Address_HandlesTrustedProxy()
        {
            // Arrange
            var httpContext = CreateHttpContext("ipv6-host.com");
            httpContext.Request.Headers["x-origin-hostname"] = "ipv6-tenant.com";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("2001:db8::1");

            var proxySettings = CreateProxySettings(
                trustXOriginHostname: true,
                trustedProxyIPs: new List<string> { "2001:db8::/32" }
            );

            var provider = CreateProvider(httpContext, proxySettings, multiTenant: true);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual("ipv6-tenant.com", result);
        }

        private HttpContext CreateHttpContext(string host)
        {
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString(host);
            return context;
        }

        private IOptions<ProxySettings> CreateProxySettings(bool trustXOriginHostname, List<string> trustedProxyIPs)
        {
            var settings = new ProxySettings
            {
                TrustXOriginHostname = trustXOriginHostname,
                TrustedProxyIPs = trustedProxyIPs
            };
            return Options.Create(settings);
        }

        private DynamicConfigurationProvider CreateProvider(
            HttpContext httpContext,
            IOptions<ProxySettings> proxySettings,
            bool multiTenant)
        {
            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            // Create a new configuration with the correct MultiTenant setting
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "ConnectionStrings:ConfigDbConnectionString", _configuration.GetConnectionString("ConfigDbConnectionString") },
                { "MultiTenant", multiTenant.ToString() }
            });
            var configuration = configBuilder.Build();

            if (multiTenant)
            {
                // Seed using the same approach as working tests
                using var ctx = new DynamicConfigDbContext(_dbOptions);
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();

                ctx.Connections.Add(new Connection
                {
                    DomainNames = new[] { "test.com" },
                    DbConn = "Data Source=test.db",
                    StorageConn = "test-storage",
                    ResourceGroup = "test-resource-group",
                    WebsiteUrl = "https://test.com"
                });
                ctx.SaveChanges();
            }

            // Use DynamicConfigurationProvider directly, not TestableConfigurationProvider
            return new DynamicConfigurationProvider(
                configuration,
                httpContextAccessor.Object,
                _memoryCache,
                _loggerMock.Object,
                proxySettings
            );
        }
    }
}
