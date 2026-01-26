// <copyright file="CdnServiceCloudFrontTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.CDN
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Unit tests for <see cref="CdnService"/> CloudFront integration.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CdnServiceCloudFrontTests
    {
        private Mock<ILogger> mockLogger;
        private Mock<HttpContext> mockHttpContext;

        [TestInitialize]
        public void Setup()
        {
            mockLogger = new Mock<ILogger>();
            mockHttpContext = new Mock<HttpContext>();
        }

        [TestMethod]
        public void CdnService_WithCloudFrontSettings_InitializesCorrectly()
        {
            // Arrange
            var cloudFrontConfig = new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Region = "us-east-1"
            };

            var settings = new List<CdnSetting>
            {
                new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.CloudFront,
                    Value = JsonConvert.SerializeObject(cloudFrontConfig)
                }
            };

            // Act
            var service = new CdnService(settings, mockLogger.Object, mockHttpContext.Object);

            // Assert
            Assert.IsNotNull(service);
            Assert.AreEqual("Sky CMD CDN", service.ProviderName);
        }

        [TestMethod]
        public void CdnService_IsConfigured_WithCloudFront_ReturnsTrue()
        {
            // Arrange
            var cloudFrontConfig = new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Region = "us-east-1"
            };

            var settings = new List<CdnSetting>
            {
                new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.CloudFront,
                    Value = JsonConvert.SerializeObject(cloudFrontConfig)
                }
            };

            var service = new CdnService(settings, mockLogger.Object, mockHttpContext.Object);

            // Act
            var isConfigured = service.IsConfigured();

            // Assert
            Assert.IsTrue(isConfigured);
        }

        [TestMethod]
        public void CdnService_IsConfigured_WithCloudFrontType_ReturnsTrue()
        {
            // Arrange
            var cloudFrontConfig = new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Region = "us-east-1"
            };

            var settings = new List<CdnSetting>
            {
                new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.CloudFront,
                    Value = JsonConvert.SerializeObject(cloudFrontConfig)
                }
            };

            var service = new CdnService(settings, mockLogger.Object, mockHttpContext.Object);

            // Act
            var isConfigured = service.IsConfigured(CdnProviderEnum.CloudFront);

            // Assert
            Assert.IsTrue(isConfigured);
        }

        [TestMethod]
        public void CdnService_IsConfigured_WithoutCloudFront_ReturnsFalse()
        {
            // Arrange
            var settings = new List<CdnSetting>();
            var service = new CdnService(settings, mockLogger.Object, mockHttpContext.Object);

            // Act
            var isConfigured = service.IsConfigured(CdnProviderEnum.CloudFront);

            // Assert
            Assert.IsFalse(isConfigured);
        }

        [TestMethod]
        public async Task CdnService_PurgeCdn_WithCloudFront_CallsCloudFrontDriver()
        {
            // Arrange
            var cloudFrontConfig = new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Region = "us-east-1"
            };

            var settings = new List<CdnSetting>
            {
                new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.CloudFront,
                    Value = JsonConvert.SerializeObject(cloudFrontConfig)
                }
            };

            var service = new CdnService(settings, mockLogger.Object, mockHttpContext.Object);

            // Act
            var results = await service.PurgeCdn(new List<string> { "/test.html" });

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("CloudFront", results[0].ProviderName);
        }

        [TestMethod]
        public async Task CdnService_PurgeCdn_WithMultipleCdns_CallsAllDrivers()
        {
            // Arrange
            var cloudFrontConfig = new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Region = "us-east-1"
            };

            var cloudflareConfig = new CloudflareCdnConfig
            {
                ApiToken = "test-token",
                ZoneId = "test-zone-id"
            };

            var settings = new List<CdnSetting>
            {
                new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.CloudFront,
                    Value = JsonConvert.SerializeObject(cloudFrontConfig)
                },
                new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.Cloudflare,
                    Value = JsonConvert.SerializeObject(cloudflareConfig)
                }
            };

            var service = new CdnService(settings, mockLogger.Object, mockHttpContext.Object);

            // Act
            var results = await service.PurgeCdn(new List<string> { "/test.html" });

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Exists(r => r.ProviderName == "CloudFront"));
            Assert.IsTrue(results.Exists(r => r.ProviderName == "Cloudflare"));
        }

        [TestMethod]
        public async Task CdnService_PurgeCdn_NoParameters_CallsCloudFrontWithWildcard()
        {
            // Arrange
            var cloudFrontConfig = new CloudFrontCdnConfig
            {
                DistributionId = "E1234567890ABC",
                AccessKeyId = "AKIAIOSFODNN7EXAMPLE",
                SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                Region = "us-east-1"
            };

            var settings = new List<CdnSetting>
            {
                new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.CloudFront,
                    Value = JsonConvert.SerializeObject(cloudFrontConfig)
                }
            };

            var service = new CdnService(settings, mockLogger.Object, mockHttpContext.Object);

            // Act
            var results = await service.PurgeCdn();

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("CloudFront", results[0].ProviderName);
        }
    }
}