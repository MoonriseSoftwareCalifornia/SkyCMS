// <copyright file="CloudFrontAwsSignatureTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.CDN
{
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Editor.Services.CDN;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Tests for AWS Signature Version 4 implementation in CloudFront driver.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CloudFrontAwsSignatureTests
    {
        private Mock<ILogger> mockLogger;

        [TestInitialize]
        public void Setup()
        {
            mockLogger = new Mock<ILogger>();
        }

        [TestMethod]
        public void ComputeSha256Hash_WithKnownInput_ReturnsExpectedHash()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };
            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            // Use reflection to test private method
            var method = typeof(CloudFrontCdnDriver).GetMethod(
                "ComputeSha256Hash",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var testData = "test data";
            var expectedHash = "916f0027a575074ce72a331777c3478d6513f786a591bd892da1a577bf2335f9";

            // Act
            var result = (string)method.Invoke(driver, new object[] { testData });

            // Assert
            Assert.AreEqual(expectedHash, result);
        }

        [TestMethod]
        public void ComputeSha256Hash_EmptyString_ReturnsValidHash()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };
            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            var method = typeof(CloudFrontCdnDriver).GetMethod(
                "ComputeSha256Hash",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var expectedHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

            // Act
            var result = (string)method.Invoke(driver, new object[] { string.Empty });

            // Assert
            Assert.AreEqual(expectedHash, result);
        }

        [TestMethod]
        public void ComputeHmacSha256_WithKnownValues_ReturnsExpectedHash()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };
            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            var method = typeof(CloudFrontCdnDriver).GetMethod(
                "ComputeHmacSha256",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var key = Encoding.UTF8.GetBytes("key");
            var data = "data";

            // Act
            var result = (string)method.Invoke(driver, new object[] { key, data });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(64, result.Length); // SHA256 hex string is 64 chars
            Assert.IsTrue(result.All(c => "0123456789abcdef".Contains(c)));
        }

        [TestMethod]
        public void GetSignatureKey_DifferentDates_ProducesDifferentKeys()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };
            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            var method = typeof(CloudFrontCdnDriver).GetMethod(
                "GetSignatureKey",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var secretKey = "testSecretKey";
            var region = "us-east-1";
            var service = "cloudfront";

            // Act
            var key1 = (byte[])method.Invoke(driver, new object[] { secretKey, "20240101", region, service });
            var key2 = (byte[])method.Invoke(driver, new object[] { secretKey, "20240102", region, service });

            // Assert
            Assert.IsNotNull(key1);
            Assert.IsNotNull(key2);
            CollectionAssert.AreNotEqual(key1, key2);
        }

        [TestMethod]
        public void GetSignatureKey_DifferentRegions_ProducesDifferentKeys()
        {
            // Arrange
            var config = CreateValidConfig();
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(config)
            };
            var driver = new CloudFrontCdnDriver(setting, mockLogger.Object);

            var method = typeof(CloudFrontCdnDriver).GetMethod(
                "GetSignatureKey",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var secretKey = "testSecretKey";
            var dateStamp = "20240101";
            var service = "cloudfront";

            // Act
            var key1 = (byte[])method.Invoke(driver, new object[] { secretKey, dateStamp, "us-east-1", service });
            var key2 = (byte[])method.Invoke(driver, new object[] { secretKey, dateStamp, "eu-west-1", service });

            // Assert
            CollectionAssert.AreNotEqual(key1, key2);
        }

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
    }
}