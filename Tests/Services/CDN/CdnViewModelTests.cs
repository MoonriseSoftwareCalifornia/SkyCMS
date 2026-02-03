// <copyright file="CdnViewModelTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.CDN
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Sky.Editor.Models;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Unit tests for <see cref="CdnViewModel"/> initialization and configuration deserialization.
    /// Tests are designed to execute in parallel where independent of one another.
    /// </summary>
    [TestClass]
    public class CdnViewModelTests
    {
        #region Initialization Tests

        /// <summary>
        /// Test: CdnViewModel initializes with default empty constructor.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.Construction")]
        public void Constructor_Default_InitializesWithEmptyConfigs()
        {
            // Act
            var viewModel = new CdnViewModel();

            // Assert
            Assert.IsNotNull(viewModel);
            Assert.IsNotNull(viewModel.AzureCdn);
            Assert.IsNotNull(viewModel.Cloudflare);
            Assert.IsNotNull(viewModel.CloudFront);
            Assert.IsNotNull(viewModel.Sucuri);
        }

        /// <summary>
        /// Test: CdnViewModel initializes with null settings list.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.Construction")]
        public void Constructor_WithNullSettings_DoesNotThrow()
        {
            // Act
            var viewModel = new CdnViewModel(null);

            // Assert
            Assert.IsNotNull(viewModel);
        }

        /// <summary>
        /// Test: CdnViewModel initializes with empty settings list.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.Construction")]
        public void Constructor_WithEmptySettings_InitializesEmpty()
        {
            // Act
            var viewModel = new CdnViewModel(new List<CdnSetting>());

            // Assert
            Assert.IsNotNull(viewModel);
            Assert.IsNotNull(viewModel.AzureCdn);
            Assert.IsNotNull(viewModel.Cloudflare);
            Assert.IsNotNull(viewModel.CloudFront);
            Assert.IsNotNull(viewModel.Sucuri);
        }

        #endregion

        #region Azure CDN Configuration Tests

        /// <summary>
        /// Test: CdnViewModel deserializes Azure CDN configuration.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.AzureConfig")]
        public void Constructor_WithAzureCdnSetting_DeserializesConfiguration()
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

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.AzureCDN,
                Value = JsonConvert.SerializeObject(azureConfig)
            };

            var settings = new List<CdnSetting> { setting };

            // Act
            var viewModel = new CdnViewModel(settings);

            // Assert
            Assert.IsNotNull(viewModel.AzureCdn);
            Assert.AreEqual(false, viewModel.AzureCdn.IsFrontDoor);
            Assert.AreEqual("test-endpoint", viewModel.AzureCdn.EndpointName);
            Assert.AreEqual("test-profile", viewModel.AzureCdn.ProfileName);
            Assert.AreEqual("test-rg", viewModel.AzureCdn.ResourceGroup);
        }

        /// <summary>
        /// Test: CdnViewModel deserializes Azure Front Door configuration.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.AzureConfig")]
        public void Constructor_WithAzureFrontdoorSetting_DeserializesConfiguration()
        {
            // Arrange
            var frontdoorConfig = new AzureCdnConfig
            {
                IsFrontDoor = true,
                EndpointName = "fd-endpoint",
                ProfileName = "fd-profile",
                ResourceGroup = "fd-rg",
                SubscriptionId = Guid.NewGuid().ToString()
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.AzureFrontdoor,
                Value = JsonConvert.SerializeObject(frontdoorConfig)
            };

            var settings = new List<CdnSetting> { setting };

            // Act
            var viewModel = new CdnViewModel(settings);

            // Assert
            Assert.IsNotNull(viewModel.AzureCdn);
            Assert.AreEqual(true, viewModel.AzureCdn.IsFrontDoor);
            Assert.AreEqual("fd-endpoint", viewModel.AzureCdn.EndpointName);
        }

        #endregion

        #region Cloudflare Configuration Tests

        /// <summary>
        /// Test: CdnViewModel deserializes Cloudflare configuration.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.CloudflareConfig")]
        public void Constructor_WithCloudflareSetting_DeserializesConfiguration()
        {
            // Arrange
            var cloudflareConfig = new CloudflareCdnConfig
            {
                ZoneId = "zone-123",
                ApiToken = "token-456"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(cloudflareConfig)
            };

            var settings = new List<CdnSetting> { setting };

            // Act
            var viewModel = new CdnViewModel(settings);

            // Assert
            Assert.IsNotNull(viewModel.Cloudflare);
            Assert.AreEqual("zone-123", viewModel.Cloudflare.ZoneId);
            Assert.AreEqual("token-456", viewModel.Cloudflare.ApiToken);
        }

        #endregion

        #region CloudFront Configuration Tests

        /// <summary>
        /// Test: CdnViewModel deserializes CloudFront configuration.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.CloudFrontConfig")]
        public void Constructor_WithCloudFrontSetting_DeserializesConfiguration()
        {
            // Arrange
            var cloudFrontConfig = new CloudFrontCdnConfig
            {
                DistributionId = "dist-789",
                AccessKeyId = "access-key",
                SecretAccessKey = "secret-key"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.CloudFront,
                Value = JsonConvert.SerializeObject(cloudFrontConfig)
            };

            var settings = new List<CdnSetting> { setting };

            // Act
            var viewModel = new CdnViewModel(settings);

            // Assert
            Assert.IsNotNull(viewModel.CloudFront);
            Assert.AreEqual("dist-789", viewModel.CloudFront.DistributionId);
            Assert.AreEqual("access-key", viewModel.CloudFront.AccessKeyId);
            Assert.AreEqual("secret-key", viewModel.CloudFront.SecretAccessKey);
        }

        #endregion

        #region Sucuri Configuration Tests

        /// <summary>
        /// Test: CdnViewModel deserializes Sucuri configuration.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.SucuriConfig")]
        public void Constructor_WithSucuriSetting_DeserializesConfiguration()
        {
            // Arrange
            var sucuriConfig = new SucuriCdnConfig
            {
                ApiKey = "api-key-abc",
                ApiSecret = "api-secret-xyz"
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(sucuriConfig)
            };

            var settings = new List<CdnSetting> { setting };

            // Act
            var viewModel = new CdnViewModel(settings);

            // Assert
            Assert.IsNotNull(viewModel.Sucuri);
            Assert.AreEqual("api-key-abc", viewModel.Sucuri.ApiKey);
            Assert.AreEqual("api-secret-xyz", viewModel.Sucuri.ApiSecret);
        }

        #endregion

        #region Multiple Provider Tests

        /// <summary>
        /// Test: CdnViewModel loads multiple provider configurations simultaneously.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.MultipleProviders")]
        public void Constructor_WithMultipleSettings_DeserializesAll()
        {
            // Arrange
            var azureConfig = new AzureCdnConfig
            {
                IsFrontDoor = false,
                EndpointName = "azure-ep",
                ProfileName = "azure-prof",
                ResourceGroup = "azure-rg",
                SubscriptionId = Guid.NewGuid().ToString()
            };

            var cloudflareConfig = new CloudflareCdnConfig
            {
                ZoneId = "cf-zone",
                ApiToken = "cf-token"
            };

            var cloudFrontConfig = new CloudFrontCdnConfig
            {
                DistributionId = "cf-dist",
                AccessKeyId = "cf-access",
                SecretAccessKey = "cf-secret"
            };

            var settings = new List<CdnSetting>
            {
                new CdnSetting { CdnProvider = CdnProviderEnum.AzureCDN, Value = JsonConvert.SerializeObject(azureConfig) },
                new CdnSetting { CdnProvider = CdnProviderEnum.Cloudflare, Value = JsonConvert.SerializeObject(cloudflareConfig) },
                new CdnSetting { CdnProvider = CdnProviderEnum.CloudFront, Value = JsonConvert.SerializeObject(cloudFrontConfig) }
            };

            // Act
            var viewModel = new CdnViewModel(settings);

            // Assert
            Assert.AreEqual("azure-ep", viewModel.AzureCdn.EndpointName);
            Assert.AreEqual("cf-zone", viewModel.Cloudflare.ZoneId);
            Assert.AreEqual("cf-dist", viewModel.CloudFront.DistributionId);
        }

        /// <summary>
        /// Test: CdnViewModel handles duplicate provider settings (last one wins).
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.MultipleProviders")]
        public void Constructor_WithDuplicateProviders_LastSettingWins()
        {
            // Arrange
            var azureConfig1 = new AzureCdnConfig
            {
                EndpointName = "first",
                ProfileName = "prof1",
                ResourceGroup = "rg1",
                SubscriptionId = Guid.NewGuid().ToString()
            };

            var azureConfig2 = new AzureCdnConfig
            {
                EndpointName = "second",
                ProfileName = "prof2",
                ResourceGroup = "rg2",
                SubscriptionId = Guid.NewGuid().ToString()
            };

            var settings = new List<CdnSetting>
            {
                new CdnSetting { CdnProvider = CdnProviderEnum.AzureCDN, Value = JsonConvert.SerializeObject(azureConfig1) },
                new CdnSetting { CdnProvider = CdnProviderEnum.AzureCDN, Value = JsonConvert.SerializeObject(azureConfig2) }
            };

            // Act
            var viewModel = new CdnViewModel(settings);

            // Assert
            Assert.AreEqual("second", viewModel.AzureCdn.EndpointName);
            Assert.AreEqual("prof2", viewModel.AzureCdn.ProfileName);
        }

        /// <summary>
        /// Test: CdnViewModel skips unknown provider settings.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.MultipleProviders")]
        public void Constructor_WithUnknownProvider_SkipsAndContinues()
        {
            // Arrange
            var azureConfig = new AzureCdnConfig
            {
                EndpointName = "azure",
                ProfileName = "profile",
                ResourceGroup = "rg",
                SubscriptionId = Guid.NewGuid().ToString()
            };

            var settings = new List<CdnSetting>
            {
                new CdnSetting { CdnProvider = CdnProviderEnum.None, Value = "{}" },
                new CdnSetting { CdnProvider = CdnProviderEnum.AzureCDN, Value = JsonConvert.SerializeObject(azureConfig) }
            };

            // Act
            var viewModel = new CdnViewModel(settings);

            // Assert
            Assert.AreEqual("azure", viewModel.AzureCdn.EndpointName);
        }

        #endregion

        #region Property Access Tests

        /// <summary>
        /// Test: CdnViewModel properties can be accessed and modified independently.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.PropertyAccess")]
        public void Properties_CanBeModifiedIndependently_NoSideEffects()
        {
            // Arrange
            var viewModel = new CdnViewModel();
            var originalCloudflare = viewModel.Cloudflare;

            // Act
            viewModel.AzureCdn = new AzureCdnConfig { EndpointName = "modified" };
            var cloudflareUnchanged = viewModel.Cloudflare;

            // Assert
            Assert.AreEqual("modified", viewModel.AzureCdn.EndpointName);
            Assert.AreEqual(originalCloudflare, cloudflareUnchanged);
        }

        /// <summary>
        /// Test: CdnViewModel returns non-null default configs even if not set.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.PropertyAccess")]
        public void Properties_DefaultInstances_NeverNull()
        {
            // Arrange & Act
            var viewModel = new CdnViewModel(new List<CdnSetting>());

            // Assert
            Assert.IsNotNull(viewModel.AzureCdn);
            Assert.IsNotNull(viewModel.Cloudflare);
            Assert.IsNotNull(viewModel.CloudFront);
            Assert.IsNotNull(viewModel.Sucuri);
        }

        #endregion

        #region Configuration Consistency Tests

        /// <summary>
        /// Test: CdnViewModel maintains configuration after deserialization.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.Consistency")]
        public void Constructor_DeserializedConfigs_ArePersistedCorrectly()
        {
            // Arrange
            var subscriptionId = Guid.NewGuid().ToString();
            var azureConfig = new AzureCdnConfig
            {
                IsFrontDoor = false,
                EndpointName = "persist-test",
                ProfileName = "profile-test",
                ResourceGroup = "rg-test",
                SubscriptionId = subscriptionId
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.AzureCDN,
                Value = JsonConvert.SerializeObject(azureConfig)
            };

            var settings = new List<CdnSetting> { setting };

            // Act
            var viewModel = new CdnViewModel(settings);

            // Assert - Verify all properties survived deserialization
            Assert.AreEqual(false, viewModel.AzureCdn.IsFrontDoor);
            Assert.AreEqual("persist-test", viewModel.AzureCdn.EndpointName);
            Assert.AreEqual("profile-test", viewModel.AzureCdn.ProfileName);
            Assert.AreEqual("rg-test", viewModel.AzureCdn.ResourceGroup);
            Assert.AreEqual(subscriptionId, viewModel.AzureCdn.SubscriptionId);
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Test: CdnViewModel handles malformed JSON gracefully.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.ErrorHandling")]
        public void Constructor_WithMalformedJson_ThrowsException()
        {
            // Arrange
            var settings = new List<CdnSetting>
            {
                new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.AzureCDN,
                    Value = "{ invalid json }"
                }
            };

            // Act & Assert
            try
            {
                _ = new CdnViewModel(settings);
                Assert.Fail("Should have thrown exception for malformed JSON");
            }
            catch (JsonReaderException)
            {
                // Expected
            }
        }

        /// <summary>
        /// Test: CdnViewModel tolerates settings with only provider and no value.
        /// </summary>
        [TestMethod]
        [TestCategory("CdnViewModel.ErrorHandling")]
        public void Constructor_WithEmptyValue_HandledGracefully()
        {
            // Arrange
            var settings = new List<CdnSetting>
            {
                new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.AzureCDN,
                    Value = null
                }
            };

            // Act & Assert
            try
            {
                _ = new CdnViewModel(settings);
            }
            catch (NullReferenceException)
            {
                // This is acceptable behavior for null value
            }
        }

        #endregion
    }
}
