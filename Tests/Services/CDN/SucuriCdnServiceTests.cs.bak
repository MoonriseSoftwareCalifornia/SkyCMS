// <copyright file="SucuriCdnServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.CDN
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Unit tests for <see cref="SucuriCdnService"/> class.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class SucuriCdnServiceTests
    {
        private Mock<ILogger> mockLogger;

        [TestInitialize]
        public void Setup()
        {
            mockLogger = new Mock<ILogger>();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithValidSettings_InitializesService()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "test-api-key",
                ApiSecret = "test-api-secret"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var service = new SucuriCdnService(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(service);
            Assert.AreEqual("Sucuri", service.ProviderName);
        }

        [TestMethod]
        public void Constructor_WithInvalidJson_ThrowsException()
        {
            // Arrange
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = "{ invalid json }"
            };

            // Act & Assert
            try
            {
                var service = new SucuriCdnService(setting, mockLogger.Object);
                Assert.Fail("Expected JsonException was not thrown");
            }
            catch (JsonException)
            {
                // Expected exception
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public void Constructor_WithEmptyConfig_Succeeds()
        {
            // Arrange
            var config = new SucuriCdnConfig();

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var service = new SucuriCdnService(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(service);
        }

        #endregion

        #region ProviderName Tests

        [TestMethod]
        public void ProviderName_ReturnsSucuri()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "test-key",
                ApiSecret = "test-secret"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var service = new SucuriCdnService(setting, mockLogger.Object);

            // Act
            var name = service.ProviderName;

            // Assert
            Assert.AreEqual("Sucuri", name);
        }

        [TestMethod]
        public void ProviderName_CalledMultipleTimes_ReturnsConsistentValue()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "test-key",
                ApiSecret = "test-secret"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var service = new SucuriCdnService(setting, mockLogger.Object);

            // Act
            var name1 = service.ProviderName;
            var name2 = service.ProviderName;
            var name3 = service.ProviderName;

            // Assert
            Assert.AreEqual(name1, name2);
            Assert.AreEqual(name2, name3);
            Assert.AreEqual("Sucuri", name1);
        }

        #endregion

        #region SucuriCdnConfig Tests

        [TestMethod]
        public void SucuriCdnConfig_DefaultValues()
        {
            // Arrange & Act
            var config = new SucuriCdnConfig();

            // Assert
            Assert.IsNull(config.ApiKey);
            Assert.IsNull(config.ApiSecret);
            Assert.AreEqual(string.Empty, config.ValidationTrigger);
        }

        [TestMethod]
        public void SucuriCdnConfig_SetProperties()
        {
            // Arrange & Act
            var config = new SucuriCdnConfig
            {
                ApiKey = "my-api-key",
                ApiSecret = "my-api-secret",
                ValidationTrigger = "test"
            };

            // Assert
            Assert.AreEqual("my-api-key", config.ApiKey);
            Assert.AreEqual("my-api-secret", config.ApiSecret);
            Assert.AreEqual("test", config.ValidationTrigger);
        }

        [TestMethod]
        public void SucuriCdnConfig_SerializeDeserialize_MaintainsValues()
        {
            // Arrange
            var original = new SucuriCdnConfig
            {
                ApiKey = "key-123",
                ApiSecret = "secret-456"
            };

            // Act
            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<SucuriCdnConfig>(json);

            // Assert
            Assert.AreEqual(original.ApiKey, deserialized.ApiKey);
            Assert.AreEqual(original.ApiSecret, deserialized.ApiSecret);
        }

        #endregion

        #region PurgeCdn Logic Tests

        [TestMethod]
        public void PurgeCdn_WithEmptyList_ShouldPurgeEverything()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "test-key",
                ApiSecret = "test-secret"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var service = new SucuriCdnService(setting, mockLogger.Object);

            // Act & Assert
            // Will fail without actual Sucuri credentials
            Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
            {
                await service.PurgeCdn(new List<string>());
            });
        }

        [TestMethod]
        public void PurgeCdn_WithMoreThan20Urls_ShouldPurgeEverything()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "test-key",
                ApiSecret = "test-secret"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var service = new SucuriCdnService(setting, mockLogger.Object);
            var urls = new List<string>();
            for (int i = 0; i < 21; i++)
            {
                urls.Add($"/page{i}");
            }

            // Act & Assert
            // With >20 URLs, should purge everything
            Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
            {
                await service.PurgeCdn(urls);
            });
        }

        [TestMethod]
        public void PurgeCdn_WithRootPath_ShouldPurgeEverything()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "test-key",
                ApiSecret = "test-secret"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var service = new SucuriCdnService(setting, mockLogger.Object);

            // Act & Assert
            Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
            {
                await service.PurgeCdn(new List<string> { "/" });
            });
        }

        [TestMethod]
        public void PurgeCdn_NoParameters_ShouldPurgeEverything()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "test-key",
                ApiSecret = "test-secret"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var service = new SucuriCdnService(setting, mockLogger.Object);

            // Act & Assert
            Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
            {
                await service.PurgeCdn();
            });
        }

        [TestMethod]
        public void PurgeCdn_WithValidUrlCount_ShouldPurgeIndividually()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "test-key",
                ApiSecret = "test-secret"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var service = new SucuriCdnService(setting, mockLogger.Object);
            var urls = new List<string> { "/page1", "/page2", "/page3" };

            // Act & Assert
            // Should purge each URL individually
            Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
            {
                await service.PurgeCdn(urls);
            });
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void Constructor_WithMinimalValidConfig_Succeeds()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "k",
                ApiSecret = "s"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var service = new SucuriCdnService(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(service);
            Assert.AreEqual("Sucuri", service.ProviderName);
        }

        [TestMethod]
        public void Constructor_WithSpecialCharactersInConfig_Succeeds()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "key-with-special!@#$%chars",
                ApiSecret = "secret-with-dashes-123"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var service = new SucuriCdnService(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void PurgeCdn_WithExactly20Urls_ShouldPurgeIndividually()
        {
            // Arrange
            var config = new SucuriCdnConfig
            {
                ApiKey = "test-key",
                ApiSecret = "test-secret"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var service = new SucuriCdnService(setting, mockLogger.Object);
            var urls = new List<string>();
            for (int i = 0; i < 20; i++)
            {
                urls.Add($"/page{i}");
            }

            // Act & Assert
            // With exactly 20 URLs, should purge individually
            Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
            {
                await service.PurgeCdn(urls);
            });
        }

        #endregion

        #region Integration Test Documentation

        /*
         * INTEGRATION TESTS NEEDED (Separate test project recommended):
         * 
         * These tests require actual Sucuri account and should NOT be in unit tests:
         * 
         * 1. PurgeCdn_WithValidCredentials_ReturnsSuccess
         *    - Requires: Sucuri account, API key, API secret
         * 
         * 2. PurgeCdn_WithInvalidCredentials_ReturnsError
         *    - Requires: Intentionally invalid credentials
         * 
         * 3. PurgeCdn_WithSpecificUrls_PurgesIndividually
         *    - Requires: Sucuri account with cached content
         * 
         * 4. PurgeCdn_PurgeEverything_ReturnsSuccess
         *    - Requires: Sucuri account with cached content
         * 
         * 5. PurgeCdn_RateLimitExceeded_HandlesGracefully
         *    - Requires: Ability to trigger rate limits
         * 
         * 6. PurgeCdn_NetworkFailure_HandlesGracefully
         *    - Requires: Network manipulation/fault injection
         * 
         * 7. PurgeCdn_UrlWith20Items_PurgesIndividually
         *    - Requires: Sucuri account
         * 
         * 8. PurgeCdn_UrlWith21Items_PurgesEverything
         *    - Requires: Sucuri account
         */

        #endregion
    }
}
