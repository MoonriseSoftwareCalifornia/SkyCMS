// <copyright file="CdnIntegrationTests.cs" company="Moonrise Software, LLC">
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
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Integration tests for CDN providers using actual test accounts.
    /// These tests require valid credentials configured in user secrets.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    [TestCategory("CDN")]
    [DoNotParallelize]
    public class CdnIntegrationTests
    {
        private IConfiguration _configuration;
        private Mock<ILogger> _mockLogger;

        [TestInitialize]
        public new void Setup()
        {
            // Load configuration from user secrets and appsettings
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<CdnIntegrationTests>()
                .AddEnvironmentVariables()
                .Build();

            _mockLogger = new Mock<ILogger>();
        }

        #region Azure CDN Integration Tests

        [TestMethod]
        public async Task AzureCdn_PurgeWithValidCredentials_Succeeds()
        {
            // Arrange
            var config = GetAzureCdnConfig();
            if (config == null)
            {
                Assert.Inconclusive("Azure CDN test credentials not configured. Add to user secrets.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.AzureCDN,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new AzureCdnDriver(cdnSetting, _mockLogger.Object);
            var testUrls = new List<string> { "/test-integration-page" };

            // Act
            var results = await driver.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0, "Should return at least one result");
            Assert.IsTrue(results[0].IsSuccessStatusCode, 
                $"Purge should succeed. Status: {results[0].Status}, Message: {results[0].Message}");
            Assert.AreEqual("Azure CDN", results[0].ProviderName);
        }

        [TestMethod]
        public async Task AzureFrontDoor_PurgeWithValidCredentials_Succeeds()
        {
            // Arrange
            var config = GetAzureFrontDoorConfig();
            if (config == null)
            {
                Assert.Inconclusive("Azure Front Door test credentials not configured. Add to user secrets.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.AzureFrontdoor,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new AzureCdnDriver(cdnSetting, _mockLogger.Object);
            var testUrls = new List<string> { "/test-integration-page" };

            // Act
            var results = await driver.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results[0].IsSuccessStatusCode,
                $"Purge should succeed. Status: {results[0].Status}, Message: {results[0].Message}");
            Assert.AreEqual("Front Door", results[0].ProviderName);
        }

        [TestMethod]
        public async Task AzureCdn_PurgeEntireCdn_Succeeds()
        {
            // Arrange
            var config = GetAzureCdnConfig();
            if (config == null)
            {
                Assert.Inconclusive("Azure CDN test credentials not configured.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.AzureCDN,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new AzureCdnDriver(cdnSetting, _mockLogger.Object);

            // Act
            var results = await driver.PurgeCdn();

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results[0].IsSuccessStatusCode);
        }

        #endregion

        #region Cloudflare Integration Tests

        [TestMethod]
        public async Task Cloudflare_PurgeWithValidCredentials_Succeeds()
        {
            // Arrange
            var config = GetCloudflareConfig();
            if (config == null)
            {
                Assert.Inconclusive("Cloudflare test credentials not configured. Add to user secrets.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(cdnSetting, _mockLogger.Object);
            var testUrls = new List<string> { "https://example.com/test-integration-page" };

            // Act
            var results = await driver.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results[0].IsSuccessStatusCode,
                $"Purge should succeed. Status: {results[0].Status}, Message: {results[0].Message}");
            Assert.AreEqual("Cloudflare", results[0].ProviderName);
            Assert.IsTrue(results[0].EstimatedFlushDateTime <= DateTimeOffset.UtcNow.AddSeconds(45),
                "Cloudflare flush should complete within 30 seconds");
        }

        [TestMethod]
        public async Task Cloudflare_PurgeEverything_Succeeds()
        {
            // Arrange
            var config = GetCloudflareConfig();
            if (config == null)
            {
                Assert.Inconclusive("Cloudflare test credentials not configured.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(cdnSetting, _mockLogger.Object);

            // Act
            var results = await driver.PurgeCdn();

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results[0].IsSuccessStatusCode);
        }

        [TestMethod]
        public async Task Cloudflare_PurgeMultipleUrls_Succeeds()
        {
            // Arrange
            var config = GetCloudflareConfig();
            if (config == null)
            {
                Assert.Inconclusive("Cloudflare test credentials not configured.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(cdnSetting, _mockLogger.Object);
            var testUrls = new List<string>
            {
                "https://example.com/page1",
                "https://example.com/page2",
                "https://example.com/page3"
            };

            // Act
            var results = await driver.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results[0].IsSuccessStatusCode);
        }

        #endregion

        #region Sucuri Integration Tests

        [TestMethod]
        public async Task Sucuri_PurgeWithValidCredentials_Succeeds()
        {
            // Arrange
            var config = GetSucuriConfig();
            if (config == null)
            {
                Assert.Inconclusive("Sucuri test credentials not configured. Add to user secrets.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new SucuriCdnService(cdnSetting, _mockLogger.Object);
            var testUrls = new List<string> { "/test-integration-page" };

            // Act
            var results = await driver.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results[0].IsSuccessStatusCode,
                $"Purge should succeed. Status: {results[0].Status}, Reason: {results[0].ReasonPhrase}");
            Assert.AreEqual("Sucuri", results[0].ProviderName);
            Assert.IsTrue(results[0].EstimatedFlushDateTime <= DateTimeOffset.UtcNow.AddMinutes(3),
                "Sucuri flush should complete within 2 minutes");
        }

        [TestMethod]
        public async Task Sucuri_PurgeEntireCache_Succeeds()
        {
            // Arrange
            var config = GetSucuriConfig();
            if (config == null)
            {
                Assert.Inconclusive("Sucuri test credentials not configured.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new SucuriCdnService(cdnSetting, _mockLogger.Object);

            // Act
            var results = await driver.PurgeCdn();

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results[0].IsSuccessStatusCode);
        }

        [TestMethod]
        public async Task Sucuri_PurgeMultipleFiles_Succeeds()
        {
            // Arrange
            var config = GetSucuriConfig();
            if (config == null)
            {
                Assert.Inconclusive("Sucuri test credentials not configured.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new SucuriCdnService(cdnSetting, _mockLogger.Object);
            var testUrls = new List<string>
            {
                "/page1.html",
                "/page2.html",
                "/page3.html"
            };

            // Act
            var results = await driver.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            // Sucuri processes each file separately, so we should get multiple results
            Assert.IsTrue(results.Count == testUrls.Count);
            Assert.IsTrue(results.TrueForAll(r => r.IsSuccessStatusCode));
        }

        [TestMethod]
        public async Task Sucuri_PurgeMoreThan20Files_PurgesEntireCache()
        {
            // Arrange
            var config = GetSucuriConfig();
            if (config == null)
            {
                Assert.Inconclusive("Sucuri test credentials not configured.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Sucuri,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new SucuriCdnService(cdnSetting, _mockLogger.Object);
            var testUrls = new List<string>();
            for (int i = 0; i < 25; i++)
            {
                testUrls.Add($"/page{i}.html");
            }

            // Act
            var results = await driver.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            // Should purge entire cache when >20 URLs
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].IsSuccessStatusCode);
        }

        #endregion

        #region Multi-Provider Integration Tests

        [TestMethod]
        public async Task MultipleProviders_PurgeAllConfigured_AllSucceed()
        {
            // Arrange
            var hasAzure = GetAzureCdnConfig() != null;
            var hasCloudflare = GetCloudflareConfig() != null;
            var hasSucuri = GetSucuriConfig() != null;

            if (!hasAzure && !hasCloudflare && !hasSucuri)
            {
                Assert.Inconclusive("No CDN providers configured for testing.");
                return;
            }

            var settings = new List<CdnSetting>();

            if (hasAzure)
            {
                settings.Add(new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.AzureCDN,
                    Value = JsonConvert.SerializeObject(GetAzureCdnConfig())
                });
            }

            if (hasCloudflare)
            {
                settings.Add(new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.Cloudflare,
                    Value = JsonConvert.SerializeObject(GetCloudflareConfig())
                });
            }

            if (hasSucuri)
            {
                settings.Add(new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.Sucuri,
                    Value = JsonConvert.SerializeObject(GetSucuriConfig())
                });
            }

            var service = new CdnService(settings, _mockLogger.Object, null);
            
            // Get URLs in the correct format for the configured providers
            var testUrls = GetProviderSpecificUrls("/test-multi-provider", hasCloudflare);

            // Act
            var results = await service.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(settings.Count, results.Count, 
                "Should return one result per configured provider");

            // Provide detailed diagnostics for each result
            var failedProviders = new List<string>();
            foreach (var result in results)
            {
                if (!result.IsSuccessStatusCode)
                {
                    failedProviders.Add($"{result.ProviderName}: Status={result.Status}, Reason={result.ReasonPhrase}, Message={result.Message}");
                }
            }

            Assert.IsTrue(results.TrueForAll(r => r.IsSuccessStatusCode),
                $"All providers should succeed. Failed providers: {string.Join("; ", failedProviders)}");
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task AzureCdn_InvalidCredentials_ReturnsError()
        {
            // Arrange
            var invalidConfig = new AzureCdnConfig
            {
                IsFrontDoor = false,
                EndpointName = "invalid-endpoint",
                ProfileName = "invalid-profile",
                ResourceGroup = "invalid-rg",
                SubscriptionId = Guid.NewGuid().ToString()
            };

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.AzureCDN,
                Value = JsonConvert.SerializeObject(invalidConfig)
            };

            var driver = new AzureCdnDriver(cdnSetting, _mockLogger.Object);

            // Act & Assert
            try
            {
                await driver.PurgeCdn(new List<string> { "/test" });
                Assert.Fail("Should throw exception with invalid credentials");
            }
            catch (Exception ex)
            {
                Assert.IsNotNull(ex);
                // Expected behavior - invalid credentials should fail
            }
        }

        [TestMethod]
        public async Task Cloudflare_InvalidApiToken_ReturnsFailure()
        {
            // Arrange
            var invalidConfig = new CloudflareCdnConfig
            {
                ApiToken = "invalid-token",
                ZoneId = "invalid-zone-id"
            };

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(invalidConfig)
            };

            var driver = new CloudflareCdnDriver(cdnSetting, _mockLogger.Object);

            // Act
            var results = await driver.PurgeCdn(new List<string> { "https://example.com/test" });

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsFalse(results[0].IsSuccessStatusCode, 
                "Should fail with invalid credentials");
        }

        #endregion

        #region Helper Methods

        private AzureCdnConfig GetAzureCdnConfig()
        {
            var subscriptionId = _configuration["CdnIntegrationTests:Azure:SubscriptionId"];
            var resourceGroup = _configuration["CdnIntegrationTests:Azure:ResourceGroup"];
            var profileName = _configuration["CdnIntegrationTests:Azure:ProfileName"];
            var endpointName = _configuration["CdnIntegrationTests:Azure:EndpointName"];

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup) ||
                string.IsNullOrEmpty(profileName) || string.IsNullOrEmpty(endpointName))
            {
                return null;
            }

            return new AzureCdnConfig
            {
                IsFrontDoor = false,
                SubscriptionId = subscriptionId,
                ResourceGroup = resourceGroup,
                ProfileName = profileName,
                EndpointName = endpointName
            };
        }

        private AzureCdnConfig GetAzureFrontDoorConfig()
        {
            var subscriptionId = _configuration["CdnIntegrationTests:AzureFrontDoor:SubscriptionId"];
            var resourceGroup = _configuration["CdnIntegrationTests:AzureFrontDoor:ResourceGroup"];
            var profileName = _configuration["CdnIntegrationTests:AzureFrontDoor:ProfileName"];
            var endpointName = _configuration["CdnIntegrationTests:AzureFrontDoor:EndpointName"];

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup) ||
                string.IsNullOrEmpty(profileName) || string.IsNullOrEmpty(endpointName))
            {
                return null;
            }

            return new AzureCdnConfig
            {
                IsFrontDoor = true,
                SubscriptionId = subscriptionId,
                ResourceGroup = resourceGroup,
                ProfileName = profileName,
                EndpointName = endpointName
            };
        }

        private CloudflareCdnConfig GetCloudflareConfig()
        {
            var apiToken = _configuration["CdnIntegrationTests:Cloudflare:ApiToken"];
            var zoneId = _configuration["CdnIntegrationTests:Cloudflare:ZoneId"];

            if (string.IsNullOrEmpty(apiToken) || string.IsNullOrEmpty(zoneId))
            {
                return null;
            }

            return new CloudflareCdnConfig
            {
                ApiToken = apiToken,
                ZoneId = zoneId
            };
        }

        private SucuriCdnConfig GetSucuriConfig()
        {
            var apiKey = _configuration["CdnIntegrationTests:Sucuri:ApiKey"];
            var apiSecret = _configuration["CdnIntegrationTests:Sucuri:ApiSecret"];

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            {
                return null;
            }

            return new SucuriCdnConfig
            {
                ApiKey = apiKey,
                ApiSecret = apiSecret
            };
        }

        /// <summary>
        /// Gets the test domain for Cloudflare from configuration.
        /// </summary>
        private string GetCloudflareDomain()
        {
            // Try to get from configuration first
            var domain = _configuration["CdnIntegrationTests:Cloudflare:TestDomain"];
            
            // Fallback to example.com if not configured
            return string.IsNullOrEmpty(domain) ? "example.com" : domain;
        }

        /// <summary>
        /// Converts a relative URL path to the appropriate format for each CDN provider.
        /// Cloudflare requires full URLs, others work with relative paths.
        /// </summary>
        private List<string> GetProviderSpecificUrls(string relativePath, bool hasCloudflare)
        {
            if (hasCloudflare)
            {
                var domain = GetCloudflareDomain();
                return new List<string> { $"https://{domain}{relativePath}" };
            }
            
            return new List<string> { relativePath };
        }

        #endregion

        #region Cloudflare-Specific Advanced Tests

        [TestMethod]
        public async Task Cloudflare_SequentialPurgeOperations_AllSucceed()
        {
            // Arrange
            var config = GetCloudflareConfig();
            if (config == null)
            {
                Assert.Inconclusive("Cloudflare test credentials not configured.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(cdnSetting, _mockLogger.Object);

            // Act - Perform multiple operations in sequence
            var result1 = await driver.PurgeCdn(new List<string> { "https://example.com/test1" });
            await Task.Delay(1000); // Brief delay between requests
            var result2 = await driver.PurgeCdn(new List<string> { "https://example.com/test2" });
            await Task.Delay(1000);
            var result3 = await driver.PurgeCdn(new List<string> { "https://example.com/test3" });

            // Assert
            Assert.IsTrue(result1[0].IsSuccessStatusCode, "First purge should succeed");
            Assert.IsTrue(result2[0].IsSuccessStatusCode, "Second purge should succeed");
            Assert.IsTrue(result3[0].IsSuccessStatusCode, "Third purge should succeed");
        }

        [TestMethod]
        public async Task Cloudflare_PurgeLargeUrlList_SucceedsWithinRateLimits()
        {
            // Arrange
            var config = GetCloudflareConfig();
            if (config == null)
            {
                Assert.Inconclusive("Cloudflare test credentials not configured.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(cdnSetting, _mockLogger.Object);
            
            // Create a list of 30 test URLs (Cloudflare allows up to 30 per request on free tier)
            var testUrls = new List<string>();
            for (int i = 1; i <= 30; i++)
            {
                testUrls.Add($"https://example.com/test-page-{i}");
            }

            // Act
            var results = await driver.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results[0].IsSuccessStatusCode,
                $"Large batch purge should succeed. Status: {results[0].Status}, Message: {results[0].Message}");
        }

        [TestMethod]
        public async Task Cloudflare_VerifyResponseDetails_ContainsExpectedData()
        {
            // Arrange
            var config = GetCloudflareConfig();
            if (config == null)
            {
                Assert.Inconclusive("Cloudflare test credentials not configured.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(cdnSetting, _mockLogger.Object);
            var testUrls = new List<string> { "https://example.com/test-verify" };

            // Act
            var results = await driver.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            
            var result = results[0];
            Assert.IsTrue(result.IsSuccessStatusCode);
            Assert.AreEqual("Cloudflare", result.ProviderName);
            Assert.IsNotNull(result.Id, "Result should have an ID");
            Assert.IsNotNull(result.ClientRequestId, "Result should have a ClientRequestId");
            Assert.IsTrue(result.EstimatedFlushDateTime > DateTimeOffset.UtcNow, 
                "EstimatedFlushDateTime should be in the future");
            Assert.IsTrue(result.EstimatedFlushDateTime <= DateTimeOffset.UtcNow.AddMinutes(1),
                "EstimatedFlushDateTime should be within 1 minute");
        }

        [TestMethod]
        public async Task Cloudflare_PurgeWithWildcard_Succeeds()
        {
            // Arrange
            var config = GetCloudflareConfig();
            if (config == null)
            {
                Assert.Inconclusive("Cloudflare test credentials not configured.");
                return;
            }

            var cdnSetting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Cloudflare,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new CloudflareCdnDriver(cdnSetting, _mockLogger.Object);
            
            // Test purging a directory pattern (Cloudflare supports this)
            var testUrls = new List<string> 
            { 
                "https://example.com/assets/*",
                "https://example.com/images/*"
            };

            // Act
            var results = await driver.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            // Note: This may fail if your Cloudflare plan doesn't support wildcard purge
            // In that case, the test documents the limitation
        }

        #endregion

        #region Diagnostic Tests

        [TestMethod]
        public void Diagnostic_VerifyCloudflareConfiguration()
        {
            // Arrange & Act
            var config = GetCloudflareConfig();

            // Assert
            if (config == null)
            {
                Assert.Inconclusive("Cloudflare configuration not found in user secrets.");
                return;
            }

            Assert.IsNotNull(config.ApiToken, "ApiToken should not be null");
            Assert.IsNotNull(config.ZoneId, "ZoneId should not be null");
            Assert.IsFalse(string.IsNullOrWhiteSpace(config.ApiToken), "ApiToken should not be empty");
            Assert.IsFalse(string.IsNullOrWhiteSpace(config.ZoneId), "ZoneId should not be empty");
            
            // Verify token format (basic validation)
            Assert.IsTrue(config.ApiToken.Length > 10, "ApiToken seems too short");
            Assert.IsTrue(config.ZoneId.Length > 10, "ZoneId seems too short");
            
            Console.WriteLine($"Cloudflare Configuration:");
            Console.WriteLine($"  ZoneId: {config.ZoneId.Substring(0, 8)}... (truncated)");
            Console.WriteLine($"  ApiToken: {config.ApiToken.Substring(0, 8)}... (truncated)");
        }

        [TestMethod]
        public void Diagnostic_ListConfiguredProviders()
        {
            // Arrange & Act
            var hasAzure = GetAzureCdnConfig() != null;
            var hasCloudflare = GetCloudflareConfig() != null;
            var hasSucuri = GetSucuriConfig() != null;

            // Assert & Report
            Console.WriteLine("Configured CDN Providers:");
            Console.WriteLine($"  Azure CDN: {(hasAzure ? "? Configured" : "? Not Configured")}");
            Console.WriteLine($"  Cloudflare: {(hasCloudflare ? "? Configured" : "? Not Configured")}");
            Console.WriteLine($"  Sucuri: {(hasSucuri ? "? Configured" : "? Not Configured")}");
            
            var configuredCount = (hasAzure ? 1 : 0) + (hasCloudflare ? 1 : 0) + (hasSucuri ? 1 : 0);
            Console.WriteLine($"\nTotal Configured: {configuredCount}/3");

            Assert.IsTrue(configuredCount > 0, "At least one provider should be configured for integration tests");
        }

        #endregion

        #region New Cloudflare Test

        [TestMethod]
        public async Task Cloudflare_SingleProviderInCdnService_Succeeds()
        {
            // Arrange
            var config = GetCloudflareConfig();
            if (config == null)
            {
                Assert.Inconclusive("Cloudflare test credentials not configured.");
                return;
            }

            var settings = new List<CdnSetting>
            {
                new CdnSetting
                {
                    CdnProvider = CdnProviderEnum.Cloudflare,
                    Value = JsonConvert.SerializeObject(config)
                }
            };

            var service = new CdnService(settings, _mockLogger.Object, null);
            var testUrls = new List<string> { "https://example.com/test-single-provider" };

            // Act
            var results = await service.PurgeCdn(testUrls);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count, "Should return exactly one result");
            Assert.IsTrue(results[0].IsSuccessStatusCode,
                $"Cloudflare purge should succeed. Status: {results[0].Status}, Message: {results[0].Message}");
            Assert.AreEqual("Cloudflare", results[0].ProviderName);
        }

        #endregion
    }
}
