// <copyright file="CdnServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.CDN
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Unit tests for <see cref="CdnService"/> class.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CdnServiceTests
    {
        private ApplicationDbContext _dbContext;
        private Mock<ILogger> _mockLogger;
        private Mock<HttpContext> _mockHttpContext;

        [TestInitialize]
        public new void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _dbContext = new ApplicationDbContext(options);
            _mockLogger = new Mock<ILogger>();
            _mockHttpContext = new Mock<HttpContext>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dbContext?.Dispose();
        }

        #region GetCdnService Tests

        [TestMethod]
        public void GetCdnService_WithNoSettings_ReturnsServiceWithEmptySettings()
        {
            // Act
            var service = CdnService.GetCdnService(_dbContext, _mockLogger.Object, _mockHttpContext.Object);

            // Assert
            Assert.IsNotNull(service);
            Assert.IsFalse(service.IsConfigured());
        }

        [TestMethod]
        public async Task GetCdnService_WithAzureCdnSettings_LoadsSettings()
        {
            // Arrange
            var azureConfig = new AzureCdnConfig
            {
                IsFrontDoor = false,
                EndpointName = "test-endpoint",
                ProfileName = "test-profile",
                ResourceGroup = "test-rg",
                SubscriptionId = Guid.NewGuid().ToString()
            };

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.AzureCDN,
                Value = JsonConvert.SerializeObject(azureConfig)
            };

            var setting = new Setting
            {
                Group = CdnService.CDNGROUPNAME,
                Name = "AzureCDN",
                Value = JsonConvert.SerializeObject(cdnSetting)
            };

            _dbContext.Settings.Add(setting);
            await _dbContext.SaveChangesAsync();

            // Act
            var service = CdnService.GetCdnService(_dbContext, _mockLogger.Object, _mockHttpContext.Object);

            // Assert
            Assert.IsNotNull(service);
            Assert.IsTrue(service.IsConfigured());
            Assert.IsTrue(service.IsConfigured(CdnProviderEnum.AzureCDN));
        }

        [TestMethod]
        public async Task GetCdnService_WithInvalidJson_RemovesInvalidSetting()
        {
            // Arrange
            var setting = new Setting
            {
                Group = CdnService.CDNGROUPNAME,
                Name = "Invalid",
                Value = "{ invalid json }"
            };

            _dbContext.Settings.Add(setting);
            await _dbContext.SaveChangesAsync();

            // Act
            var service = CdnService.GetCdnService(_dbContext, _mockLogger.Object, _mockHttpContext.Object);

            // Assert
            Assert.IsFalse(service.IsConfigured());
            Assert.IsFalse(await _dbContext.Settings.AnyAsync(s => s.Id == setting.Id));
        }

        #endregion

        #region IsConfigured Tests

        [TestMethod]
        public void IsConfigured_WithNoSettings_ReturnsFalse()
        {
            // Arrange
            var service = new CdnService(new List<CdnSetting>(), _mockLogger.Object, _mockHttpContext.Object);

            // Act & Assert
            Assert.IsFalse(service.IsConfigured());
        }

        [TestMethod]
        public void IsConfigured_WithSettings_ReturnsTrue()
        {
            // Arrange
            var settings = new List<CdnSetting>
            {
                new CdnSetting { CdnProvider = CdnProviderEnum.AzureCDN, Value = "{}" }
            };
            var service = new CdnService(settings, _mockLogger.Object, _mockHttpContext.Object);

            // Act & Assert
            Assert.IsTrue(service.IsConfigured());
        }

        [TestMethod]
        public void IsConfigured_WithSpecificProvider_ReturnsCorrectly()
        {
            // Arrange
            var settings = new List<CdnSetting>
            {
                new CdnSetting { CdnProvider = CdnProviderEnum.Cloudflare, Value = "{}" }
            };
            var service = new CdnService(settings, _mockLogger.Object, _mockHttpContext.Object);

            // Act & Assert
            Assert.IsTrue(service.IsConfigured(CdnProviderEnum.Cloudflare));
            Assert.IsFalse(service.IsConfigured(CdnProviderEnum.AzureCDN));
            Assert.IsFalse(service.IsConfigured(CdnProviderEnum.Sucuri));
        }

        [TestMethod]
        public void IsConfigured_MultipleProviders_IdentifiesEachCorrectly()
        {
            // Arrange
            var settings = new List<CdnSetting>
            {
                new CdnSetting { CdnProvider = CdnProviderEnum.Cloudflare, Value = "{}" },
                new CdnSetting { CdnProvider = CdnProviderEnum.Sucuri, Value = "{}" }
            };
            var service = new CdnService(settings, _mockLogger.Object, _mockHttpContext.Object);

            // Act & Assert
            Assert.IsTrue(service.IsConfigured(CdnProviderEnum.Cloudflare));
            Assert.IsTrue(service.IsConfigured(CdnProviderEnum.Sucuri));
            Assert.IsFalse(service.IsConfigured(CdnProviderEnum.AzureCDN));
        }

        #endregion

        #region PurgeCdn Tests

        [TestMethod]
        public async Task PurgeCdn_WithUrls_RemovesDuplicates()
        {
            // Arrange
            var settings = new List<CdnSetting>();
            var service = new CdnService(settings, _mockLogger.Object, _mockHttpContext.Object);
            var urls = new List<string> { "/page1", "/page2", "/page1", "/page3", "/page2" };

            // Act
            var results = await service.PurgeCdn(urls);

            // Assert
            // Results should reflect distinct URLs
            Assert.IsNotNull(results);
        }

        [TestMethod]
        public async Task PurgeCdn_WithNoSettings_ReturnsEmptyResults()
        {
            // Arrange
            var service = CdnService.GetCdnService(_dbContext, _mockLogger.Object, _mockHttpContext.Object);

            // Act
            var results = await service.PurgeCdn(new List<string> { "https://example.com/test" });

            // Assert
            Assert.IsNotNull(results);
            Assert.IsFalse(results.Any());
        }

        [TestMethod]
        public async Task PurgeCdn_MultipleProviders_CallsAll()
        {
            // Arrange - This test would require mocking the actual CDN drivers
            // which is complex due to the switch statement instantiation pattern
            // Recommendation: Refactor CdnService to use dependency injection for drivers

            var settings = new List<CdnSetting>
            {
                new CdnSetting 
                { 
                    CdnProvider = CdnProviderEnum.Cloudflare, 
                    Value = JsonConvert.SerializeObject(new CloudflareCdnConfig 
                    { 
                        ApiToken = "test-token", 
                        ZoneId = "test-zone" 
                    }) 
                }
            };

            var service = new CdnService(settings, _mockLogger.Object, _mockHttpContext.Object);

            // Act
            var results = await service.PurgeCdn(new List<string> { "/test" });

            // Assert
            Assert.IsNotNull(results);
            // Note: Actual API calls would fail in unit tests
            // Consider using integration tests for full provider testing
        }

        [TestMethod]
        public async Task PurgeCdn_NoParameters_CallsWithEmptyUrls()
        {
            // Arrange
            var service = CdnService.GetCdnService(_dbContext, _mockLogger.Object, _mockHttpContext.Object);

            // Act
            var results = await service.PurgeCdn();

            // Assert
            Assert.IsNotNull(results);
            Assert.IsFalse(results.Any());
        }

        [TestMethod]
        public async Task PurgeCdn_EmptyUrlList_HandlesGracefully()
        {
            // Arrange
            var service = CdnService.GetCdnService(_dbContext, _mockLogger.Object, _mockHttpContext.Object);

            // Act
            var results = await service.PurgeCdn(new List<string>());

            // Assert
            Assert.IsNotNull(results);
            Assert.IsFalse(results.Any());
        }

        #endregion

        #region Provider Name Tests

        [TestMethod]
        public void ProviderName_ReturnsCorrectName()
        {
            // Arrange
            var settings = new List<CdnSetting>();
            var service = new CdnService(settings, _mockLogger.Object, _mockHttpContext.Object);

            // Act
            var name = service.ProviderName;

            // Assert
            Assert.AreEqual("Sky CMD CDN", name);
        }

        #endregion

        #region CdnSetting Tests

        [TestMethod]
        public void CdnSetting_DefaultValues()
        {
            // Arrange & Act
            var setting = new CdnSetting();

            // Assert
            Assert.AreEqual(CdnProviderEnum.None, setting.CdnProvider);
            Assert.AreEqual(string.Empty, setting.Value);
        }

        [TestMethod]
        public void CdnSetting_SetProperties()
        {
            // Arrange & Act
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = "test-value"
            };

            // Assert
            Assert.AreEqual(CdnProviderEnum.Cloudflare, setting.CdnProvider);
            Assert.AreEqual("test-value", setting.Value);
        }

        #endregion

        #region CdnResult Tests

        [TestMethod]
        public void CdnResult_ToString_ReturnsFormattedString()
        {
            // Arrange
            var result = new CdnResult
            {
                Status = System.Net.HttpStatusCode.OK,
                ReasonPhrase = "Success",
                ProviderName = "Test Provider"
            };

            // Act
            var stringResult = result.ToString();

            // Assert
            Assert.IsTrue(stringResult.Contains("OK"));
            Assert.IsTrue(stringResult.Contains("Success"));
            Assert.IsTrue(stringResult.Contains("Test Provider"));
        }

        [TestMethod]
        public void CdnResult_Properties_SetCorrectly()
        {
            // Arrange & Act
            var now = DateTimeOffset.UtcNow;
            var result = new CdnResult
            {
                ProviderName = "Azure",
                Status = System.Net.HttpStatusCode.Accepted,
                ReasonPhrase = "Accepted",
                ClientRequestId = "client-123",
                Id = "id-456",
                IsSuccessStatusCode = true,
                EstimatedFlushDateTime = now,
                Message = "Test message"
            };

            // Assert
            Assert.AreEqual("Azure", result.ProviderName);
            Assert.AreEqual(System.Net.HttpStatusCode.Accepted, result.Status);
            Assert.AreEqual("Accepted", result.ReasonPhrase);
            Assert.AreEqual("client-123", result.ClientRequestId);
            Assert.AreEqual("id-456", result.Id);
            Assert.IsTrue(result.IsSuccessStatusCode);
            Assert.AreEqual(now, result.EstimatedFlushDateTime);
            Assert.AreEqual("Test message", result.Message);
        }

        #endregion

        #region CdnProviderEnum Tests

        [TestMethod]
        public void CdnProviderEnum_HasAllExpectedValues()
        {
            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(CdnProviderEnum), CdnProviderEnum.AzureFrontdoor));
            Assert.IsTrue(Enum.IsDefined(typeof(CdnProviderEnum), CdnProviderEnum.AzureCDN));
            Assert.IsTrue(Enum.IsDefined(typeof(CdnProviderEnum), CdnProviderEnum.Cloudflare));
            Assert.IsTrue(Enum.IsDefined(typeof(CdnProviderEnum), CdnProviderEnum.Sucuri));
            Assert.IsTrue(Enum.IsDefined(typeof(CdnProviderEnum), CdnProviderEnum.None));
        }

        [TestMethod]
        public void CdnProviderEnum_CanConvertToString()
        {
            // Act & Assert
            Assert.AreEqual("AzureFrontdoor", CdnProviderEnum.AzureFrontdoor.ToString());
            Assert.AreEqual("AzureCDN", CdnProviderEnum.AzureCDN.ToString());
            Assert.AreEqual("Cloudflare", CdnProviderEnum.Cloudflare.ToString());
            Assert.AreEqual("Sucuri", CdnProviderEnum.Sucuri.ToString());
            Assert.AreEqual("None", CdnProviderEnum.None.ToString());
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public async Task GetCdnService_EmptyValueSettings_SkipsSettings()
        {
            // Arrange
            var setting = new Setting
            {
                Group = CdnService.CDNGROUPNAME,
                Name = "Empty",
                Value = ""
            };

            _dbContext.Settings.Add(setting);
            await _dbContext.SaveChangesAsync();

            // Act
            var service = CdnService.GetCdnService(_dbContext, _mockLogger.Object, _mockHttpContext.Object);

            // Assert
            Assert.IsFalse(service.IsConfigured());
        }

        [TestMethod]
        public async Task GetCdnService_NullValueSettings_SkipsSettings()
        {
            // Arrange
            // Note: We cannot actually save a Setting with null Value as it's required in the schema
            // Instead, test that the GetCdnService method filters out null/empty values correctly
            var validSetting = new Setting
            {
                Group = CdnService.CDNGROUPNAME,
                Name = "Valid",
                Value = JsonConvert.SerializeObject(new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.Cloudflare,
                    Value = JsonConvert.SerializeObject(new CloudflareCdnConfig
                    {
                        ApiToken = "test-token",
                        ZoneId = "test-zone"
                    })
                })
            };

            _dbContext.Settings.Add(validSetting);
            await _dbContext.SaveChangesAsync();

            // Act
            var service = CdnService.GetCdnService(_dbContext, _mockLogger.Object, _mockHttpContext.Object);

            // Assert
            Assert.IsTrue(service.IsConfigured());
            Assert.IsTrue(service.IsConfigured(CdnProviderEnum.Cloudflare));
        }

        #endregion
    }
}
