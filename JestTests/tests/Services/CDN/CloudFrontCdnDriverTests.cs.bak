// <copyright file="CloudFrontCdnDriverTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.CDN
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Unit tests for <see cref="CloudFrontCdnDriver"/> class.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CloudFrontCdnDriverTests
    {
        private Mock<ILogger> mockLogger;

        [TestInitialize]
        public void Setup()
        {
            mockLogger = new Mock<ILogger>();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithValidCloudFrontSettings_InitializesDriver()
        {
            // Arrange
            var config = new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Region = "us-east-1"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(driver);
            Assert.AreEqual("CloudFront", driver.ProviderName);
        }

        [TestMethod]
        public void Constructor_WithNullSetting_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                var driver = new CloudFrontCdnDriver(null, mockLogger.Object);
            });
        }

        [TestMethod]
        public void Constructor_WithEmptyValue_ThrowsArgumentException()
        {
            // Arrange
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = string.Empty
            };

            // Act & Assert
            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);
            });

            Assert.IsTrue(exception.Message.Contains("cannot be null or empty"));
        }

        [TestMethod]
        public void Constructor_WithWhitespaceValue_ThrowsArgumentException()
        {
            // Arrange
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = "   "
            };

            // Act & Assert
            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);
            });

            Assert.IsTrue(exception.Message.Contains("cannot be null or empty"));
        }

        [TestMethod]
        public void Constructor_WithInvalidJson_ThrowsArgumentException()
        {
            // Arrange
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = "{ invalid json }"
            };

            // Act & Assert
            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);
            });

            Assert.IsTrue(exception.Message.Contains("Invalid JSON"));
        }

        [TestMethod]
        public void Constructor_WithNullJsonObject_ThrowsArgumentException()
        {
            // Arrange
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = "null"
            };

            // Act & Assert
            var exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);
            });

            Assert.IsTrue(exception.Message.Contains("Failed to deserialize"));
        }

        [TestMethod]
        public void Constructor_WithMinimalValidConfig_Succeeds()
        {
            // Arrange
            var config = new CloudFrontCdnConfig
            {
                DistributionId = "E",
                AccessKeyId = "A",
                SecretAccessKey = "S",
                Region = "us-east-1"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(driver);
        }

        #endregion

        #region ProviderName Tests

        [TestMethod]
        public void ProviderName_ReturnsCloudFront()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var name = driver.ProviderName;

            // Assert
            Assert.AreEqual("CloudFront", name);
        }

        [TestMethod]
        public void ProviderName_CalledMultipleTimes_ReturnsConsistentValue()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var name1 = driver.ProviderName;
            var name2 = driver.ProviderName;
            var name3 = driver.ProviderName;

            // Assert
            Assert.AreEqual(name1, name2);
            Assert.AreEqual(name2, name3);
            Assert.AreEqual("CloudFront", name1);
        }

        #endregion

        #region CloudFrontCdnConfig Tests

        [TestMethod]
        public void CloudFrontCdnConfig_DefaultValues()
        {
            // Arrange & Act
            var config = new CloudFrontCdnConfig();

            // Assert
            Assert.AreEqual(string.Empty, config.DistributionId);
            Assert.AreEqual(string.Empty, config.AccessKeyId);
            Assert.AreEqual(string.Empty, config.SecretAccessKey);
            Assert.AreEqual("us-east-1", config.Region);
        }

        [TestMethod]
        public void CloudFrontCdnConfig_SetProperties()
        {
            // Arrange & Act
            var config = new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Region = "eu-west-1"
            };

            // Assert
            Assert.AreEqual("E1234567890ABC", config.DistributionId);
            Assert.AreEqual("AKIAIOSFODNN7EXAMPLE", config.AccessKeyId);
            Assert.AreEqual("wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", config.SecretAccessKey);
            Assert.AreEqual("eu-west-1", config.Region);
        }

        [TestMethod]
        public void CloudFrontCdnConfig_SerializeDeserialize_MaintainsValues()
        {
            // Arrange
            var original = new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Region = "ap-northeast-1"
            };

            // Act
            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<CloudFrontCdnConfig>(json);

            // Assert
            Assert.AreEqual(original.DistributionId, deserialized.DistributionId);
            Assert.AreEqual(original.AccessKeyId, deserialized.AccessKeyId);
            Assert.AreEqual(original.SecretAccessKey, deserialized.SecretAccessKey);
            Assert.AreEqual(original.Region, deserialized.Region);
        }

        [TestMethod]
        public void CloudFrontCdnConfig_AllRegions_CanBeSet()
        {
            // Arrange
            var regions = new[]
            {
                "us-east-1", "us-east-2", "us-west-1", "us-west-2",
                "eu-west-1", "eu-central-1",
                "ap-southeast-1", "ap-northeast-1"
            };

            // Act & Assert
            foreach (var region in regions)
            {
                var config = new CloudFrontCdnConfig { Region = region };
                Assert.AreEqual(region, config.Region);
            }
        }

        #endregion

        #region PurgeCdn Method Tests

        [TestMethod]
        public async Task PurgeCdn_WithNullUrls_PurgesAllContent()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(null);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("CloudFront", result[0].ProviderName);
            Assert.IsFalse(result[0].IsSuccessStatusCode); // Will fail without real AWS credentials
        }

        [TestMethod]
        public async Task PurgeCdn_WithEmptyList_PurgesAllContent()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string>());

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("CloudFront", result[0].ProviderName);
        }

        [TestMethod]
        public async Task PurgeCdn_WithRootPath_PurgesAllContent()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string> { "/" });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("CloudFront", result[0].ProviderName);
        }

        [TestMethod]
        public async Task PurgeCdn_WithRootKeyword_PurgesAllContent()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string> { "root" });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("CloudFront", result[0].ProviderName);
        }

        [TestMethod]
        public async Task PurgeCdn_WithSpecificUrls_ReturnsCdnResult()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);
            var urls = new List<string> { "/page1.html", "/page2.html", "/css/style.css" };

            // Act
            var result = await driver.PurgeCdn(urls);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("CloudFront", result[0].ProviderName);
            Assert.IsNotNull(result[0].ClientRequestId);
            Assert.IsNotNull(result[0].Message);
        }

        [TestMethod]
        public async Task PurgeCdn_NoParameters_CallsOverloadWithWildcard()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("CloudFront", result[0].ProviderName);
        }

        [TestMethod]
        public async Task PurgeCdn_ReturnsEstimatedFlushDateTime()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);
            var beforeCall = DateTimeOffset.UtcNow;

            // Act
            var result = await driver.PurgeCdn(new List<string> { "/test.html" });
            var afterCall = DateTimeOffset.UtcNow.AddMinutes(5);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].EstimatedFlushDateTime > beforeCall);
            Assert.IsTrue(result[0].EstimatedFlushDateTime <= afterCall.AddSeconds(5)); // Allow small buffer
        }

        [TestMethod]
        public async Task PurgeCdn_WithInvalidCredentials_ReturnsErrorResult()
        {
            // Arrange
            var config = new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "INVALID_KEY",
                SecretAccessKey = "INVALID_SECRET",
                Region = "us-east-1"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string> { "/test.html" });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.IsFalse(result[0].IsSuccessStatusCode);
            Assert.AreEqual("CloudFront", result[0].ProviderName);
        }

        #endregion

        #region CdnResult Validation Tests

        [TestMethod]
        public async Task PurgeCdn_Result_HasClientRequestId()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string> { "/test.html" });

            // Assert
            Assert.IsNotNull(result[0].ClientRequestId);
            Assert.AreNotEqual(string.Empty, result[0].ClientRequestId);
        }

        [TestMethod]
        public async Task PurgeCdn_Result_HasProviderName()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string> { "/test.html" });

            // Assert
            Assert.AreEqual("CloudFront", result[0].ProviderName);
        }

        [TestMethod]
        public async Task PurgeCdn_Result_HasStatusCode()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string> { "/test.html" });

            // Assert
            Assert.IsNotNull(result[0].Status);
        }

        [TestMethod]
        public async Task PurgeCdn_Result_HasReasonPhrase()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Act
            var result = await driver.PurgeCdn(new List<string> { "/test.html" });

            // Assert
            Assert.IsNotNull(result[0].ReasonPhrase);
            Assert.AreNotEqual(string.Empty, result[0].ReasonPhrase);
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public async Task PurgeCdn_WithDuplicateUrls_HandlesCorrectly()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);
            var urls = new List<string> { "/test.html", "/test.html", "/test.html" };

            // Act
            var result = await driver.PurgeCdn(urls);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task PurgeCdn_WithVeryLongUrl_HandlesCorrectly()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);
            var longUrl = "/very/long/path/" + new string('a', 500) + ".html";
            var urls = new List<string> { longUrl };

            // Act
            var result = await driver.PurgeCdn(urls);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task PurgeCdn_WithSpecialCharactersInUrl_HandlesCorrectly()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);
            var urls = new List<string> { "/test page.html", "/test&file.html", "/test<file>.html" };

            // Act
            var result = await driver.PurgeCdn(urls);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task PurgeCdn_WithManyUrls_HandlesCorrectly()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);
            var urls = new List<string>();
            for (int i = 0; i < 100; i++)
            {
                urls.Add($"/page{i}.html");
            }

            // Act
            var result = await driver.PurgeCdn(urls);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        #endregion

        #region Integration Test Documentation

        /*
         * INTEGRATION TESTS NEEDED (Separate test project recommended):
         * 
         * These tests require actual AWS CloudFront resources and should NOT be in unit tests:
         * 
         * 1. PurgeCdn_WithValidCredentials_ReturnsSuccess
         *    - Requires: AWS CloudFront distribution, IAM user with proper permissions
         * 
         * 2. PurgeCdn_WithInvalidDistributionId_ReturnsError
         *    - Requires: AWS account with intentionally invalid distribution ID
         * 
         * 3. PurgeCdn_WithExpiredCredentials_ReturnsAuthError
         *    - Requires: AWS account with expired/rotated credentials
         * 
         * 4. PurgeCdn_WithInsufficientPermissions_ReturnsPermissionError
         *    - Requires: AWS IAM user without cloudfront:CreateInvalidation permission
         * 
         * 5. PurgeCdn_With3000Paths_CreatesInvalidationSuccessfully
         *    - Requires: AWS CloudFront distribution (max 3000 paths per invalidation)
         * 
         * 6. PurgeCdn_ConcurrentInvalidations_HandlesCorrectly
         *    - Requires: AWS CloudFront distribution
         * 
         * 7. PurgeCdn_NetworkFailure_HandlesGracefully
         *    - Requires: Network manipulation/fault injection
         * 
         * 8. PurgeCdn_DifferentRegions_AllWork
         *    - Requires: AWS credentials configured for multiple regions
         * 
         * 9. PurgeCdn_VerifyAwsSignatureV4_AuthenticatesCorrectly
         *    - Requires: AWS CloudFront distribution
         * 
         * 10. PurgeCdn_CheckInvalidationStatus_ReturnsValidId
         *    - Requires: AWS CloudFront distribution
         * 
         * 11. PurgeCdn_RateLimitExceeded_HandlesGracefully
         *    - Requires: AWS CloudFront (3000 invalidations per month free tier)
         */

        #endregion

        #region Helper Methods

        private CloudFrontCdnConfig CreateValidConfig()
        {
            return new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Region = "us-east-1"
            };
        }

        #endregion
    }
}