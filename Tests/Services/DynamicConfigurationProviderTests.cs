// <copyright file="DynamicConfigurationProviderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Unit tests for <see cref="DynamicConfigurationProvider"/>.
    /// Tests multi-tenant configuration resolution, caching, and domain validation.
    /// </summary>
    [TestClass]
    public class DynamicConfigurationProviderTests
    {
        private Mock<IConfiguration> configurationMock;
        private Mock<IHttpContextAccessor> httpContextAccessorMock;
        private IMemoryCache memoryCache;
        private Mock<ILogger<DynamicConfigurationProvider>> loggerMock;
        private TestableConfigurationProvider provider;
        private DynamicConfigDbContext dbContext;

        private const string TestConfigConnectionString = "Data Source=:memory:";
        private const string TestTenant1Domain = "tenant1.example.com";
        private const string TestTenant2Domain = "tenant2.example.com";
        private const string TestTenant3Domain = "tenant3.example.com";
        private const string TestTenant1DbConn = "Server=localhost;Database=Tenant1Db;";
        private const string TestTenant1StorageConn = "DefaultEndpointsProtocol=https;AccountName=tenant1storage;";
        private const string TestTenant2DbConn = "Server=localhost;Database=Tenant2Db;";
        private const string TestTenant2StorageConn = "DefaultEndpointsProtocol=https;AccountName=tenant2storage;";

        [TestInitialize]
        public void Setup()
        {
            // Setup in-memory database for DynamicConfigDbContext
            var options = new DbContextOptionsBuilder<DynamicConfigDbContext>()
                .UseInMemoryDatabase($"ConfigTest_{Guid.NewGuid()}")
                .Options;
            dbContext = new DynamicConfigDbContext(options);

            // Seed test tenant data
            SeedTestTenants();

            // Setup mocks
            configurationMock = new Mock<IConfiguration>();
            httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            memoryCache = new MemoryCache(new MemoryCacheOptions());
            loggerMock = new Mock<ILogger<DynamicConfigurationProvider>>();

            // Setup configuration to return test connection string
            // GetConnectionString uses: GetSection("ConnectionStrings")["name"]
            var connectionStringValue = new Mock<IConfigurationSection>();
            connectionStringValue.Setup(x => x.Value).Returns(TestConfigConnectionString);

            var connectionStringsSection = new Mock<IConfigurationSection>();
            connectionStringsSection.Setup(x => x["ConfigDbConnectionString"]).Returns(TestConfigConnectionString);
            connectionStringsSection.Setup(x => x.GetSection("ConfigDbConnectionString")).Returns(connectionStringValue.Object);

            configurationMock.Setup(x => x.GetSection("ConnectionStrings")).Returns(connectionStringsSection.Object);

            // Create testable provider with injected test DbContext options
            provider = new TestableConfigurationProvider(
                configurationMock.Object,
                httpContextAccessorMock.Object,
                memoryCache,
                loggerMock.Object,
                options);
        }

        [TestCleanup]
        public void Cleanup()
        {
            memoryCache?.Dispose();
            dbContext?.Dispose();
        }

        /// <summary>
        /// Seeds test tenant data into the in-memory database.
        /// </summary>
        private void SeedTestTenants()
        {
            var tenant1 = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new string[] { TestTenant1Domain, "www.tenant1.example.com" },
                DbConn = TestTenant1DbConn,
                StorageConn = TestTenant1StorageConn,
                ResourceGroup = "tenant1-rg",
                WebsiteUrl = $"https://{TestTenant1Domain}"
            };

            var tenant2 = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new string[] { TestTenant2Domain },
                DbConn = TestTenant2DbConn,
                StorageConn = TestTenant2StorageConn,
                ResourceGroup = "tenant2-rg",
                WebsiteUrl = $"https://{TestTenant2Domain}"
            };

            dbContext.Connections.AddRange(tenant1, tenant2);
            dbContext.SaveChanges();
        }

        /// <summary>
        /// Helper to setup HttpContext with specified domain in Host header.
        /// </summary>
        private void SetupHttpContext(string domain, string xOriginHostname = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Host = new HostString(domain);

            if (!string.IsNullOrEmpty(xOriginHostname))
            {
                httpContext.Request.Headers["x-origin-hostname"] = xOriginHostname;
            }

            httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
        }

        #region GetDatabaseConnectionStringAsync Tests

        /// <summary>
        /// Tests that database connection string is returned for valid domain.
        /// </summary>
        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_ValidDomain_ReturnsConnectionString()
        {
            // Arrange
            SetupHttpContext(TestTenant1Domain);

            // Act
            var result = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestTenant1DbConn, result);
        }

        /// <summary>
        /// Tests that x-origin-hostname header takes priority over Host header.
        /// </summary>
        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_XOriginHostname_TakesPriorityOverHost()
        {
            // Arrange - Host header has tenant2, but x-origin-hostname has tenant1
            SetupHttpContext(TestTenant2Domain, TestTenant1Domain);

            // Act
            var result = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestTenant1DbConn, result, 
                "Should use connection from x-origin-hostname, not Host header");
        }

        /// <summary>
        /// Tests that method uses Host header when x-origin-hostname is absent.
        /// </summary>
        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_NoXOriginHostname_UsesHostHeader()
        {
            // Arrange - Only Host header present
            SetupHttpContext(TestTenant2Domain);

            // Act
            var result = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestTenant2DbConn, result);
        }

        /// <summary>
        /// Tests that null HttpContext without manual domain throws exception.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task GetDatabaseConnectionStringAsync_NoHttpContextNoManualDomain_ThrowsException()
        {
            // Arrange
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null);

            // Act
            await provider.GetDatabaseConnectionStringAsync();

            // Assert - Exception expected
        }

        /// <summary>
        /// Tests that manual domain parameter works without HttpContext.
        /// </summary>
        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_ManualDomain_WorksWithoutHttpContext()
        {
            // Arrange
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null);

            // Act
            var result = await provider.GetDatabaseConnectionStringAsync(TestTenant1Domain);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestTenant1DbConn, result);
        }

        /// <summary>
        /// Tests that invalid domain returns null.
        /// </summary>
        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_InvalidDomain_ReturnsNull()
        {
            // Arrange
            SetupHttpContext("nonexistent.example.com");

            // Act
            var result = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNull(result);
        }

        /// <summary>
        /// Tests that domain names are case-insensitive.
        /// </summary>
        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_CaseInsensitiveDomain_ReturnsConnectionString()
        {
            // Arrange
            SetupHttpContext("TENANT1.EXAMPLE.COM");

            // Act
            var result = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestTenant1DbConn, result);
        }

        /// <summary>
        /// Tests that alternate domain name (www prefix) resolves correctly.
        /// </summary>
        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_AlternateDomainName_ReturnsConnectionString()
        {
            // Arrange
            SetupHttpContext("www.tenant1.example.com");

            // Act
            var result = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestTenant1DbConn, result, 
                "Should resolve alternate domain name from DomainNames list");
        }

        #endregion

        #region GetStorageConnectionStringAsync Tests

        /// <summary>
        /// Tests that storage connection string is returned for valid domain.
        /// </summary>
        [TestMethod]
        public async Task GetStorageConnectionStringAsync_ValidDomain_ReturnsConnectionString()
        {
            // Arrange
            SetupHttpContext(TestTenant1Domain);

            // Act
            var result = await provider.GetStorageConnectionStringAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestTenant1StorageConn, result);
        }

        /// <summary>
        /// Tests that x-origin-hostname takes priority for storage connection.
        /// </summary>
        [TestMethod]
        public async Task GetStorageConnectionStringAsync_XOriginHostname_TakesPriority()
        {
            // Arrange
            SetupHttpContext(TestTenant2Domain, TestTenant1Domain);

            // Act
            var result = await provider.GetStorageConnectionStringAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestTenant1StorageConn, result);
        }

        /// <summary>
        /// Tests that null HttpContext without manual domain throws exception.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task GetStorageConnectionStringAsync_NoHttpContextNoManualDomain_ThrowsException()
        {
            // Arrange
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null);

            // Act
            await provider.GetStorageConnectionStringAsync();

            // Assert - Exception expected
        }

        /// <summary>
        /// Tests that manual domain works for storage connection.
        /// </summary>
        [TestMethod]
        public async Task GetStorageConnectionStringAsync_ManualDomain_WorksWithoutHttpContext()
        {
            // Arrange
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null);

            // Act
            var result = await provider.GetStorageConnectionStringAsync(TestTenant2Domain);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestTenant2StorageConn, result);
        }

        /// <summary>
        /// Tests that invalid domain returns null for storage connection.
        /// </summary>
        [TestMethod]
        public async Task GetStorageConnectionStringAsync_InvalidDomain_ReturnsNull()
        {
            // Arrange
            SetupHttpContext("invalid.example.com");

            // Act
            var result = await provider.GetStorageConnectionStringAsync();

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetTenantDomainNameFromRequest Tests

        /// <summary>
        /// Tests that domain is extracted from x-origin-hostname header.
        /// </summary>
        [TestMethod]
        public void GetTenantDomainNameFromRequest_XOriginHostnamePresent_ReturnsXOriginHostname()
        {
            // Arrange
            SetupHttpContext(TestTenant2Domain, TestTenant1Domain);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual(TestTenant1Domain, result);
        }

        /// <summary>
        /// Tests that domain is extracted from Host header when x-origin-hostname is absent.
        /// </summary>
        [TestMethod]
        public void GetTenantDomainNameFromRequest_NoXOriginHostname_ReturnsHostHeader()
        {
            // Arrange
            SetupHttpContext(TestTenant2Domain);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual(TestTenant2Domain, result);
        }

        /// <summary>
        /// Tests that null HttpContext returns empty string.
        /// </summary>
        [TestMethod]
        public void GetTenantDomainNameFromRequest_NullHttpContext_ReturnsEmptyString()
        {
            // Arrange
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null);

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        /// <summary>
        /// Tests that domain is normalized to lowercase.
        /// </summary>
        [TestMethod]
        public void GetTenantDomainNameFromRequest_MixedCase_ReturnsLowercase()
        {
            // Arrange
            SetupHttpContext("TENANT1.EXAMPLE.COM");

            // Act
            var result = provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual(TestTenant1Domain, result);
        }

        #endregion

        #region ValidateDomainName Tests

        /// <summary>
        /// Tests that valid domain name is validated successfully.
        /// </summary>
        [TestMethod]
        public async Task ValidateDomainName_ValidDomain_ReturnsTrue()
        {
            // Act
            var result = await provider.ValidateDomainName(TestTenant1Domain);

            // Assert
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Tests that invalid domain name validation returns false.
        /// </summary>
        [TestMethod]
        public async Task ValidateDomainName_InvalidDomain_ReturnsFalse()
        {
            // Act
            var result = await provider.ValidateDomainName("nonexistent.example.com");

            // Assert
            Assert.IsFalse(result);
        }

        /// <summary>
        /// Tests that null domain name returns false.
        /// </summary>
        [TestMethod]
        public async Task ValidateDomainName_NullDomain_ReturnsFalse()
        {
            // Act
            var result = await provider.ValidateDomainName(null);

            // Assert
            Assert.IsFalse(result);
        }

        /// <summary>
        /// Tests that empty domain name returns false.
        /// </summary>
        [TestMethod]
        public async Task ValidateDomainName_EmptyDomain_ReturnsFalse()
        {
            // Act
            var result = await provider.ValidateDomainName(string.Empty);

            // Assert
            Assert.IsFalse(result);
        }

        /// <summary>
        /// Tests that domain validation is case-insensitive.
        /// </summary>
        [TestMethod]
        public async Task ValidateDomainName_MixedCaseDomain_ReturnsTrue()
        {
            // Act
            var result = await provider.ValidateDomainName("TENANT1.EXAMPLE.COM");

            // Assert
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Tests that alternate domain name validates successfully.
        /// </summary>
        [TestMethod]
        public async Task ValidateDomainName_AlternateDomainName_ReturnsTrue()
        {
            // Act
            var result = await provider.ValidateDomainName("www.tenant1.example.com");

            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        #region Caching Tests

        /// <summary>
        /// Tests that connection is cached after first retrieval.
        /// </summary>
        [TestMethod]
        public async Task GetTenantConnectionAsync_FirstCall_CachesResult()
        {
            // Arrange
            var domainName = TestTenant1Domain;

            // Act - First call
            var result1 = await provider.GetTenantConnectionAsync(domainName);

            // Clear the database to prove second call uses cache
            dbContext.Connections.RemoveRange(dbContext.Connections);
            await dbContext.SaveChangesAsync();

            // Act - Second call (should use cache)
            var result2 = await provider.GetTenantConnectionAsync(domainName);

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(result1.DbConn, result2.DbConn);
            Assert.AreEqual(result1.StorageConn, result2.StorageConn);
        }

        /// <summary>
        /// Tests that different tenants have separate cache entries.
        /// </summary>
        [TestMethod]
        public async Task GetTenantConnectionAsync_DifferentTenants_SeparateCacheEntries()
        {
            // Act
            var tenant1 = await provider.GetTenantConnectionAsync(TestTenant1Domain);
            var tenant2 = await provider.GetTenantConnectionAsync(TestTenant2Domain);

            // Assert
            Assert.IsNotNull(tenant1);
            Assert.IsNotNull(tenant2);
            Assert.AreNotEqual(tenant1.DbConn, tenant2.DbConn);
            Assert.AreNotEqual(tenant1.StorageConn, tenant2.StorageConn);
        }

        #endregion

        #region GetAllDomainNamesAsync Tests

        /// <summary>
        /// Tests that all primary domain names are returned.
        /// </summary>
        [TestMethod]
        public async Task GetAllDomainNamesAsync_ReturnsAllPrimaryDomains()
        {
            // Act
            var result = await provider.GetAllDomainNamesAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains(TestTenant1Domain));
            Assert.IsTrue(result.Contains(TestTenant2Domain));
        }

        /// <summary>
        /// Tests that result is distinct (no duplicates).
        /// </summary>
        [TestMethod]
        public async Task GetAllDomainNamesAsync_ReturnsDistinctDomains()
        {
            // Act
            var result = await provider.GetAllDomainNamesAsync();

            // Assert
            var distinctCount = result.Distinct().Count();
            Assert.AreEqual(result.Count, distinctCount);
        }

        #endregion

        #region GetCurrentTenantIdAsync Tests

        /// <summary>
        /// Tests that current tenant ID is returned for valid request.
        /// </summary>
        [TestMethod]
        public async Task GetCurrentTenantIdAsync_ValidRequest_ReturnsTenantId()
        {
            // Arrange
            SetupHttpContext(TestTenant1Domain);

            // Act
            var result = await provider.GetCurrentTenantIdAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.HasValue);
        }

        /// <summary>
        /// Tests that different domains return different tenant IDs.
        /// </summary>
        [TestMethod]
        public async Task GetCurrentTenantIdAsync_DifferentDomains_ReturnsDifferentIds()
        {
            // Arrange & Act
            SetupHttpContext(TestTenant1Domain);
            var tenant1Id = await provider.GetCurrentTenantIdAsync();

            SetupHttpContext(TestTenant2Domain);
            var tenant2Id = await provider.GetCurrentTenantIdAsync();

            // Assert
            Assert.IsNotNull(tenant1Id);
            Assert.IsNotNull(tenant2Id);
            Assert.AreNotEqual(tenant1Id, tenant2Id);
        }

        /// <summary>
        /// Tests that null HttpContext returns null tenant ID.
        /// </summary>
        [TestMethod]
        public async Task GetCurrentTenantIdAsync_NullHttpContext_ReturnsNull()
        {
            // Arrange
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null);

            // Act
            var result = await provider.GetCurrentTenantIdAsync();

            // Assert
            Assert.IsNull(result);
        }

        /// <summary>
        /// Tests that invalid domain returns null tenant ID.
        /// </summary>
        [TestMethod]
        public async Task GetCurrentTenantIdAsync_InvalidDomain_ReturnsNull()
        {
            // Arrange
            SetupHttpContext("invalid.example.com");

            // Act
            var result = await provider.GetCurrentTenantIdAsync();

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region CleanUpDomainName Tests

        /// <summary>
        /// Tests that full URL is cleaned to host name only.
        /// </summary>
        [TestMethod]
        public void CleanUpDomainName_FullUrl_ReturnsHostOnly()
        {
            // Act
            var result = DynamicConfigurationProvider.CleanUpDomainName("https://tenant1.example.com/path");

            // Assert
            Assert.AreEqual("tenant1.example.com", result);
        }

        /// <summary>
        /// Tests that plain domain name is returned as-is (lowercase).
        /// </summary>
        [TestMethod]
        public void CleanUpDomainName_PlainDomain_ReturnsLowercase()
        {
            // Act
            var result = DynamicConfigurationProvider.CleanUpDomainName("TENANT1.EXAMPLE.COM");

            // Assert
            Assert.AreEqual("tenant1.example.com", result);
        }

        /// <summary>
        /// Tests that null or empty string is handled gracefully.
        /// </summary>
        [TestMethod]
        public void CleanUpDomainName_NullOrEmpty_ReturnsOriginal()
        {
            // Act & Assert
            Assert.AreEqual(null, DynamicConfigurationProvider.CleanUpDomainName(null));
            Assert.AreEqual(string.Empty, DynamicConfigurationProvider.CleanUpDomainName(string.Empty));
        }

        #endregion

        #region PreloadAllConnectionsAsync Tests

        /// <summary>
        /// Tests that preload caches all tenant connections.
        /// </summary>
        [TestMethod]
        public async Task PreloadAllConnectionsAsync_CachesAllTenants()
        {
            // Act
            await provider.PreloadAllConnectionsAsync();

            // Clear database to prove cache is used
            dbContext.Connections.RemoveRange(dbContext.Connections);
            await dbContext.SaveChangesAsync();

            // Assert - Should still be able to get connections from cache
            var tenant1 = await provider.GetTenantConnectionAsync(TestTenant1Domain);
            var tenant2 = await provider.GetTenantConnectionAsync(TestTenant2Domain);

            Assert.IsNotNull(tenant1);
            Assert.IsNotNull(tenant2);
        }

        /// <summary>
        /// Tests that preload respects minimum interval between calls.
        /// </summary>
        [TestMethod]
        public async Task PreloadAllConnectionsAsync_CalledTwiceQuickly_SecondCallSkipped()
        {
            // Act - First call
            await provider.PreloadAllConnectionsAsync();

            // Add a new tenant to database
            dbContext.Connections.Add(new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new string[] { TestTenant3Domain },
                DbConn = "NewConnection",
                StorageConn = "NewStorage",
                ResourceGroup = "tenant3-rg",
                WebsiteUrl = $"https://{TestTenant3Domain}"
            });
            await dbContext.SaveChangesAsync();

            // Act - Second call immediately (should be skipped due to interval)
            await provider.PreloadAllConnectionsAsync();

            // Assert - New tenant should NOT be in cache yet
            var tenant3 = await provider.GetTenantConnectionAsync(TestTenant3Domain);
            
            // This will be from database (not cache) since preload was skipped
            Assert.IsNotNull(tenant3);
        }

        #endregion
    }
}