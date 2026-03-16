// <copyright file="SingleTenantConfigurationProviderExtendedTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using Cosmos.DynamicConfig;
using Microsoft.Extensions.Configuration;

namespace Sky.Tests.DynamicConfig
{
    /// <summary>
    /// Extended tests for SingleTenantConfigurationProvider - Priority 1 multi-tenant core infrastructure.
    /// Tests GetTenantConnectionAsync, ValidateDomainName, and connection string retrieval.
    /// </summary>
    [TestClass]
    public class SingleTenantConfigurationProviderExtendedTests
    {
        private IConfiguration _configuration = null!;
        private SingleTenantConfigurationProvider _provider = null!;

        [TestInitialize]
        public void Setup()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:ApplicationDbContextConnection", "Data Source=app.db;Cache=Shared" },
                { "ConnectionStrings:StorageConnectionString", "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key123" },
                { "ConnectionStrings:CustomConnection", "Server=localhost;Database=custom" },
                { "AppSettings:SomeKey", "SomeValue" }
            });
            _configuration = configBuilder.Build();
            _provider = new SingleTenantConfigurationProvider(_configuration);
        }

        [TestMethod]
        public void IsMultiTenantConfigured_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(_provider.IsMultiTenantConfigured);
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_ReturnsConfiguredConnectionString()
        {
            // Act
            var result = await _provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.AreEqual("Data Source=app.db;Cache=Shared", result);
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_WithDomainName_IgnoresDomainAndReturnsConnectionString()
        {
            // Act
            var result = await _provider.GetDatabaseConnectionStringAsync("ignored-domain.com");

            // Assert
            Assert.AreEqual("Data Source=app.db;Cache=Shared", result);
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_WithCancellationToken_ReturnsConnectionString()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            // Act
            var result = await _provider.GetDatabaseConnectionStringAsync("domain.com", cts.Token);

            // Assert
            Assert.AreEqual("Data Source=app.db;Cache=Shared", result);
        }

        [TestMethod]
        public async Task GetStorageConnectionStringAsync_ReturnsConfiguredStorageConnectionString()
        {
            // Act
            var result = await _provider.GetStorageConnectionStringAsync();

            // Assert
            Assert.AreEqual("DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key123", result);
        }

        [TestMethod]
        public async Task GetStorageConnectionStringAsync_WithDomainName_IgnoresDomainAndReturnsStorageConnectionString()
        {
            // Act
            var result = await _provider.GetStorageConnectionStringAsync("ignored-domain.com");

            // Assert
            Assert.AreEqual("DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key123", result);
        }

        [TestMethod]
        public void GetConfigurationValue_ReturnsCorrectValue()
        {
            // Act
            var result = _provider.GetConfigurationValue("AppSettings:SomeKey");

            // Assert
            Assert.AreEqual("SomeValue", result);
        }

        [TestMethod]
        public void GetConfigurationValue_WithNonExistentKey_ReturnsNull()
        {
            // Act
            var result = _provider.GetConfigurationValue("NonExistent:Key");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetConnectionStringByName_ReturnsNamedConnectionString()
        {
            // Act
            var result = _provider.GetConnectionStringByName("CustomConnection");

            // Assert
            Assert.AreEqual("Server=localhost;Database=custom", result);
        }

        [TestMethod]
        public void GetConnectionStringByName_WithNonExistentName_ReturnsNull()
        {
            // Act
            var result = _provider.GetConnectionStringByName("NonExistentConnection");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_ReturnsEmpty()
        {
            // Act
            var result = _provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public async Task GetAllDomainNamesAsync_ReturnsEmptyList()
        {
            // Act
            var result = await _provider.GetAllDomainNamesAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetTenantConnectionAsync_ReturnsSingleTenantConnection()
        {
            // Arrange
            const string domainName = "test-domain.com";

            // Act
            var result = await _provider.GetTenantConnectionAsync(domainName);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(Guid.Empty, result.Id);
            Assert.AreEqual(1, result.DomainNames.Length);
            Assert.AreEqual(domainName, result.DomainNames[0]);
            Assert.AreEqual("Data Source=app.db;Cache=Shared", result.DbConn);
            Assert.AreEqual("DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key123", result.StorageConn);
        }

        [TestMethod]
        public async Task GetTenantConnectionAsync_WithEmptyDomain_ReturnsConnectionWithEmptyDomain()
        {
            // Act
            var result = await _provider.GetTenantConnectionAsync(string.Empty);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.DomainNames.Length);
            Assert.AreEqual(string.Empty, result.DomainNames[0]);
        }

        [TestMethod]
        public async Task GetTenantConnectionAsync_WithCancellationToken_ReturnsConnection()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            // Act
            var result = await _provider.GetTenantConnectionAsync("domain.com", cts.Token);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("domain.com", result.DomainNames[0]);
        }

        [TestMethod]
        public async Task PreloadAllConnectionsAsync_CompletesSuccessfully()
        {
            // Act & Assert - Should complete without throwing
            await _provider.PreloadAllConnectionsAsync();
        }

        [TestMethod]
        public async Task PreloadAllConnectionsAsync_WithCancellationToken_CompletesSuccessfully()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            // Act & Assert
            await _provider.PreloadAllConnectionsAsync(cts.Token);
        }

        [TestMethod]
        public async Task ValidateDomainName_AlwaysReturnsTrue()
        {
            // Act
            var result1 = await _provider.ValidateDomainName("any-domain.com");
            var result2 = await _provider.ValidateDomainName("another-domain.org");
            var result3 = await _provider.ValidateDomainName(string.Empty);

            // Assert
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            Assert.IsTrue(result3);
        }

        [TestMethod]
        public async Task GetCurrentTenantIdAsync_ReturnsEmptyGuid()
        {
            // Act
            var result = await _provider.GetCurrentTenantIdAsync();

            // Assert
            Assert.AreEqual(Guid.Empty, result);
        }

        [TestMethod]
        public void GetConfigurationValue_HandlesNestedKeys()
        {
            // Arrange
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Section:SubSection:Key", "NestedValue" }
            });
            var config = configBuilder.Build();
            var provider = new SingleTenantConfigurationProvider(config);

            // Act
            var result = provider.GetConfigurationValue("Section:SubSection:Key");

            // Assert
            Assert.AreEqual("NestedValue", result);
        }

        [TestMethod]
        public async Task GetTenantConnectionAsync_WithNullConnectionStrings_ReturnsConnectionWithNullValues()
        {
            // Arrange
            var emptyConfig = new ConfigurationBuilder().Build();
            var provider = new SingleTenantConfigurationProvider(emptyConfig);

            // Act
            var result = await provider.GetTenantConnectionAsync("test.com");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.DbConn);
            Assert.IsNull(result.StorageConn);
        }

        [TestMethod]
        public async Task MultipleConcurrentCalls_ReturnConsistentResults()
        {
            // Arrange
            var tasks = new List<Task<Connection?>>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_provider.GetTenantConnectionAsync($"domain-{i}.com"));
            }
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(10, results.Length);
            foreach (var result in results)
            {
                Assert.IsNotNull(result);
                Assert.AreEqual("Data Source=app.db;Cache=Shared", result.DbConn);
            }
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_WithMissingConnectionString_ReturnsNull()
        {
            // Arrange
            var emptyConfig = new ConfigurationBuilder().Build();
            var provider = new SingleTenantConfigurationProvider(emptyConfig);

            // Act
            var result = await provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetStorageConnectionStringAsync_WithMissingConnectionString_ReturnsNull()
        {
            // Arrange
            var emptyConfig = new ConfigurationBuilder().Build();
            var provider = new SingleTenantConfigurationProvider(emptyConfig);

            // Act
            var result = await provider.GetStorageConnectionStringAsync();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetConnectionStringByName_WithApplicationDbContextConnection_ReturnsCorrectValue()
        {
            // Act
            var result = _provider.GetConnectionStringByName("ApplicationDbContextConnection");

            // Assert
            Assert.AreEqual("Data Source=app.db;Cache=Shared", result);
        }

        [TestMethod]
        public void GetConnectionStringByName_WithStorageConnectionString_ReturnsCorrectValue()
        {
            // Act
            var result = _provider.GetConnectionStringByName("StorageConnectionString");

            // Assert
            Assert.AreEqual("DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key123", result);
        }

        [TestMethod]
        public async Task GetTenantConnectionAsync_PreservesDomainNameCase()
        {
            // Arrange
            const string domainName = "MixedCase-Domain.COM";

            // Act
            var result = await _provider.GetTenantConnectionAsync(domainName);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(domainName, result.DomainNames[0]);
        }
    }
}
