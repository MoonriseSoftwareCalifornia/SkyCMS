// <copyright file="CloudflareCdnDriverTests.cs" company="Moonrise Software, LLC">
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
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Unit tests for <see cref="CloudflareCdnDriver"/> class.
    /// </summary>
    [TestClass]
    public class CloudflareCdnDriverTests
    {
        private Mock<ILogger> mockLogger;

        [TestInitialize]
        public void Setup()
        {
            mockLogger = new Mock<ILogger>();
        }

        #region Constructor Tests

        /// <summary>
        /// Tests that Constructor_WithValidSettings_InitializesDriver.
        /// </summary>
        [TestMethod]
        public void Constructor_WithValidSettings_InitializesDriver()
        {
            // Arrange
            var config = new CloudflareCdnConfig
            {
                ApiToken = "test-token",
                ZoneId = "test-zone-id"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(driver);
            Assert.AreEqual("Cloudflare", driver.ProviderName);
        }

        /// <summary>
        /// Tests that Constructor_WithInvalidJson_ThrowsArgumentException.
        /// </summary>
        [TestMethod]
        public void Constructor_WithInvalidJson_ThrowsArgumentException()
        {
            // Arrange
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = "{ invalid json }"
            };

            // Act & Assert
            var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            {
                var driver = new CloudflareCdnDriver(setting, mockLogger.Object);
            });
            
            // Verify the exception message contains useful information
            Assert.IsTrue(exception.Message.Contains("Invalid JSON in CDN setting"));
            Assert.IsNotNull(exception.InnerException);
            Assert.IsInstanceOfType(exception.InnerException, typeof(Newtonsoft.Json.JsonReaderException));
        }

        /// <summary>
        /// Tests that Constructor_WithEmptyConfig_Succeeds.
        /// </summary>
        [TestMethod]
        public void Constructor_WithEmptyConfig_Succeeds()
        {
            // Arrange
            var config = new CloudflareCdnConfig();

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(driver);
        }

        #endregion

        #region ProviderName Tests

        /// <summary>
        /// Tests that ProviderName_ReturnsCloudflare.
        /// </summary>
        [TestMethod]
        public void ProviderName_ReturnsCloudflare()
        {
            // Arrange
            var config = new CloudflareCdnConfig
            {
                ApiToken = "test-token",
                ZoneId = "test-zone-id"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Act
            var name = driver.ProviderName;

            // Assert
            Assert.AreEqual("Cloudflare", name);
        }

        /// <summary>
        /// Tests that ProviderName_CalledMultipleTimes_ReturnsConsistentValue.
        /// </summary>
        [TestMethod]
        public void ProviderName_CalledMultipleTimes_ReturnsConsistentValue()
        {
            // Arrange
            var config = new CloudflareCdnConfig
            {
                ApiToken = "test-token",
                ZoneId = "test-zone-id"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Act
            var name1 = driver.ProviderName;
            var name2 = driver.ProviderName;
            var name3 = driver.ProviderName;

            // Assert
            Assert.AreEqual(name1, name2);
            Assert.AreEqual(name2, name3);
            Assert.AreEqual("Cloudflare", name1);
        }

        #endregion

        #region CloudflareCdnConfig Tests

        /// <summary>
        /// Tests that CloudflareCdnConfig_DefaultValues.
        /// </summary>
        [TestMethod]
        public void CloudflareCdnConfig_DefaultValues()
        {
            // Arrange & Act
            var config = new CloudflareCdnConfig();

            // Assert
            Assert.AreEqual(string.Empty, config.ApiToken);
            Assert.AreEqual(string.Empty, config.ZoneId);
            Assert.AreEqual(string.Empty, config.ValidationTrigger);
        }

        /// <summary>
        /// Tests that CloudflareCdnConfig_SetProperties.
        /// </summary>
        [TestMethod]
        public void CloudflareCdnConfig_SetProperties()
        {
            // Arrange & Act
            var config = new CloudflareCdnConfig
            {
                ApiToken = "my-api-token",
                ZoneId = "my-zone-id",
                ValidationTrigger = "test"
            };

            // Assert
            Assert.AreEqual("my-api-token", config.ApiToken);
            Assert.AreEqual("my-zone-id", config.ZoneId);
            Assert.AreEqual("test", config.ValidationTrigger);
        }

        /// <summary>
        /// Tests that CloudflareCdnConfig_SerializeDeserialize_MaintainsValues.
        /// </summary>
        [TestMethod]
        public void CloudflareCdnConfig_SerializeDeserialize_MaintainsValues()
        {
            // Arrange
            var original = new CloudflareCdnConfig
            {
                ApiToken = "test-token-123",
                ZoneId = "zone-456"
            };

            // Act
            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<CloudflareCdnConfig>(json);

            // Assert
            Assert.AreEqual(original.ApiToken, deserialized.ApiToken);
            Assert.AreEqual(original.ZoneId, deserialized.ZoneId);
        }

        #endregion

        #region PurgeCdn Logic Tests

        /// <summary>
        /// Tests that PurgeCdn_WithNullUrlList_ShouldCallPurgeEverything.
        /// </summary>
        [TestMethod]
        public async Task PurgeCdn_WithNullUrlList_ShouldCallPurgeEverything()
        {
            // Arrange
            var config = new CloudflareCdnConfig
            {
                ApiToken = "test-token",
                ZoneId = "test-zone"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(null);

            // Assert
            // Method should complete and return a result (even if API call fails)
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(List<CdnResult>));
        }

        /// <summary>
        /// Tests that PurgeCdn_WithEmptyUrlList_ShouldCallPurgeEverything.
        /// </summary>
        [TestMethod]
        public async Task PurgeCdn_WithEmptyUrlList_ShouldCallPurgeEverything()
        {
            // Arrange
            var config = new CloudflareCdnConfig
            {
                ApiToken = "test-token",
                ZoneId = "test-zone"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string>());

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            // With invalid credentials, IsSuccessStatusCode will be false
            Assert.IsFalse(result[0].IsSuccessStatusCode);
        }

        /// <summary>
        /// Tests that PurgeCdn_WithRootPath_ShouldCallPurgeEverything.
        /// </summary>
        [TestMethod]
        public async Task PurgeCdn_WithRootPath_ShouldCallPurgeEverything()
        {
            // Arrange
            var config = new CloudflareCdnConfig
            {
                ApiToken = "test-token",
                ZoneId = "test-zone"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string> { "/" });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        /// <summary>
        /// Tests that PurgeCdn_WithRootKeyword_ShouldCallPurgeEverything.
        /// </summary>
        [TestMethod]
        public async Task PurgeCdn_WithRootKeyword_ShouldCallPurgeEverything()
        {
            // Arrange
            var config = new CloudflareCdnConfig
            {
                ApiToken = "test-token",
                ZoneId = "test-zone"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string> { "root" });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        /// <summary>
        /// Tests that PurgeCdn_NoParameters_ShouldCallPurgeEverything.
        /// </summary>
        [TestMethod]
        public async Task PurgeCdn_NoParameters_ShouldCallPurgeEverything()
        {
            // Arrange
            var config = new CloudflareCdnConfig
            {
                ApiToken = "test-token",
                ZoneId = "test-zone"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        #endregion

        #region Edge Cases

        /// <summary>
        /// Tests that Constructor_WithMinimalValidConfig_Succeeds.
        /// </summary>
        [TestMethod]
        public void Constructor_WithMinimalValidConfig_Succeeds()
        {
            // Arrange
            var config = new CloudflareCdnConfig
            {
                ApiToken = "t",
                ZoneId = "z"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(driver);
            Assert.AreEqual("Cloudflare", driver.ProviderName);
        }

        /// <summary>
        /// Tests that Constructor_WithSpecialCharactersInConfig_Succeeds.
        /// </summary>
        [TestMethod]
        public void Constructor_WithSpecialCharactersInConfig_Succeeds()
        {
            // Arrange
            var config = new CloudflareCdnConfig
            {
                ApiToken = "token-with-special!@#$%^&*()chars",
                ZoneId = "zone-with-dashes-123"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(driver);
        }

        #endregion

        #region Integration Test Documentation

        /*
         * INTEGRATION TESTS NEEDED (Separate test project recommended):
         * 
         * These tests require actual Cloudflare account and should NOT be in unit tests:
         * 
         * 1. PurgeCdn_WithValidCredentials_ReturnsSuccess
         *    - Requires: Cloudflare account, API token, Zone ID
         * 
         * 2. PurgeCdn_WithInvalidToken_ReturnsError
         *    - Requires: Cloudflare account, intentionally invalid token
         * 
         * 3. PurgeCdn_WithInvalidZoneId_ReturnsError
         *    - Requires: Valid token, intentionally invalid zone ID
         * 
         * 4. PurgeCdn_WithSpecificUrls_ReturnsSuccess
         *    - Requires: Cloudflare account with cached content
         * 
         * 5. PurgeCdn_PurgeEverything_ReturnsSuccess
         *    - Requires: Cloudflare account with cached content
         * 
         * 6. PurgeCdn_RateLimitExceeded_HandlesGracefully
         *    - Requires: Ability to trigger rate limits
         * 
         * 7. PurgeCdn_NetworkFailure_HandlesGracefully
         *    - Requires: Network manipulation/fault injection
         * 
         * 8. PurgeCdn_LargeUrlList_HandlesCorrectly
         *    - Requires: Cloudflare account, test with many URLs
         */

        #endregion

        #region Additional Tests

        /// <summary>
        /// Tests that PurgeAsync_EmptyApiToken_ReturnsUnsuccessfulResult.
        /// </summary>
        [TestMethod]
        public async Task PurgeAsync_EmptyApiToken_ReturnsUnsuccessfulResult()
        {
            // Arrange
            var invalidConfig = new CloudflareCdnConfig
            {
                ApiToken = string.Empty, // Invalid - empty token
                ZoneId = "test-zone-id"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(invalidConfig)
            };

            var driver = new CloudflareCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string> { "https://example.com/test" });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            // API call should fail with invalid credentials
            Assert.IsFalse(result[0].IsSuccessStatusCode);
        }

        #endregion
    }
}

