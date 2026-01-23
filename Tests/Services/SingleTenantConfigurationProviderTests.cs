// <copyright file="SingleTenantConfigurationProviderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cosmos.DynamicConfig;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="SingleTenantConfigurationProvider"/>.
    /// </summary>
    [TestClass]
    public class SingleTenantConfigurationProviderTests
    {
        private IConfiguration _configuration;
        private SingleTenantConfigurationProvider _provider;

        [TestInitialize]
        public void Setup()
        {
            var configValues = new Dictionary<string, string>
            {
                ["ConnectionStrings:ApplicationDbContextConnection"] = "Server=localhost;Database=TestDb;",
                ["ConnectionStrings:StorageConnectionString"] = "UseDevelopmentStorage=true"
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            _provider = new SingleTenantConfigurationProvider(_configuration);
        }

        [TestMethod]
        public void IsMultiTenantConfigured_ShouldReturnFalse()
        {
            // Act
            var result = _provider.IsMultiTenantConfigured;

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task GetCurrentTenantIdAsync_ShouldReturnGuidEmpty()
        {
            // Act
            var tenantId = await _provider.GetCurrentTenantIdAsync();

            // Assert
            Assert.AreEqual(Guid.Empty, tenantId);
        }

        [TestMethod]
        public async Task GetDatabaseConnectionStringAsync_ShouldReturnConnectionString()
        {
            // Act
            var connectionString = await _provider.GetDatabaseConnectionStringAsync();

            // Assert
            Assert.AreEqual("Server=localhost;Database=TestDb;", connectionString);
        }

        [TestMethod]
        public async Task GetStorageConnectionStringAsync_ShouldReturnStorageConnectionString()
        {
            // Act
            var connectionString = await _provider.GetStorageConnectionStringAsync();

            // Assert
            Assert.AreEqual("UseDevelopmentStorage=true", connectionString);
        }

        [TestMethod]
        public async Task GetAllDomainNamesAsync_ShouldReturnEmptyList()
        {
            // Act
            var domainNames = await _provider.GetAllDomainNamesAsync();

            // Assert
            Assert.IsNotNull(domainNames);
            Assert.AreEqual(0, domainNames.Count);
        }

        [TestMethod]
        public void GetTenantDomainNameFromRequest_ShouldReturnEmpty()
        {
            // Act
            var domainName = _provider.GetTenantDomainNameFromRequest();

            // Assert
            Assert.AreEqual(string.Empty, domainName);
        }
    }
}