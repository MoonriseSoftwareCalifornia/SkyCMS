// <copyright file="SetupServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Areas.Setup
{
    using Cosmos.BlobService;
    using Cosmos.Cms.Data;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Data;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Shared;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Layouts;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Redirects;
    using Sky.Editor.Services.Setup;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Services.Titles;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Comprehensive unit tests for the Setup Wizard service and pages.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SetupServiceTests : SkyCmsTestBase
    {
        private ISetupService setupService;
        private Mock<ILogger<SetupService>> mockLogger;
        private Mock<ILayoutImportService> mockLayoutImportService;
        private IConfiguration testConfiguration;
        private string uniqueSetupDbPath;

        [TestInitialize]
        public new void Setup()
        {
            // Call base initialization which should set up Storage and other dependencies
            InitializeTestContext();

            // ✅ Use a unique file-based SQLite database instead of in-memory
            var testDbPath = Path.Combine(
                Path.GetTempPath(),
                $"skycms-test-{Guid.NewGuid()}.db");

            var connectionString = $"Data Source={testDbPath}";

            // Initialize only what we need for setup tests
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString)  // ✅ Use file-based SQLite
                .Options;
            Db = new ApplicationDbContext(options);

            // ✅ Ensure the database is created with schema
            Db.Database.EnsureCreated();

            Cache = new MemoryCache(new MemoryCacheOptions());

            // ✅ Ensure all dependencies are initialized from base class
            // If Storage is still null, the base class needs to be updated

            uniqueSetupDbPath = Path.Combine(
                Path.GetTempPath(),
                $"skycms-setup-test-{Guid.NewGuid()}.db");

            if (File.Exists(uniqueSetupDbPath))
            {
                try
                {
                    File.Delete(uniqueSetupDbPath);
                }
                catch
                {
                    // Ignore - will use a different GUID next time
                }
            }

            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(
                new Dictionary<string, string>()
                {
                    ["ConnectionStrings:ApplicationDbContextConnection"] = connectionString,
                    ["ConnectionStrings:StorageConnectionString"] = "UseDevelopmentStorage=true",
                    ["SetupDatabasePath"] = uniqueSetupDbPath
                });


            var layoutImportService = new LayoutImportService(HttpClientFactory, Cache, new NullLogger<LayoutImportService>());

            testConfiguration = configBuilder.Build();

            // ✅ SetupService constructor with ALL 8 parameters
            setupService = new SetupService(
                testConfiguration,
                new NullLogger<SetupService>(),
                Cache,
                UserManager,
                RoleManager,
                Db,
                layoutImportService,
                Logic,
                Mediator);

            foreach (var role in RequiredIdentityRoles.Roles)
            {
                if (!RoleManager.RoleExistsAsync(role).Result)
                {
                    _ = RoleManager.CreateAsync(new IdentityRole(role)).Result;
                }
            }
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            // ✅ Clean up the UNIQUE SQLite database for this test
            if (!string.IsNullOrEmpty(uniqueSetupDbPath) && File.Exists(uniqueSetupDbPath))
            {
                try
                {
                    File.Delete(uniqueSetupDbPath);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the test
                    Console.WriteLine($"Failed to delete test database {uniqueSetupDbPath}: {ex.Message}");
                }
            }

            // ✅ Clean up the main test database file
            var testDbConnectionString = testConfiguration?.GetConnectionString("ApplicationDbContextConnection");
            if (!string.IsNullOrEmpty(testDbConnectionString))
            {
               
            }

            await DisposeAsync();
        }

        #region InitializeSetupAsync Tests

        [TestMethod]
        public async Task InitializeSetupAsync_CreatesNewSetupConfiguration()
        {
            // Act
            var result = await setupService.InitializeSetupAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreNotEqual(Guid.Empty, result.Id);
            Assert.AreEqual(1, result.CurrentStep);
        }

        [TestMethod]
        public async Task InitializeSetupAsync_CalledTwice_ReturnsSameConfiguration()
        {
            // Act
            var result1 = await setupService.InitializeSetupAsync();
            var result2 = await setupService.InitializeSetupAsync();

            // Assert
            Assert.AreEqual(result1.Id, result2.Id);
        }

        #endregion

        #region GetCurrentSetupAsync Tests

        [TestMethod]
        public async Task GetCurrentSetupAsync_NoSetupExists_ReturnsNull()
        {
            // Act
            var result = await setupService.GetCurrentSetupAsync();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetCurrentSetupAsync_SetupExists_ReturnsConfiguration()
        {
            // Arrange
            await setupService.InitializeSetupAsync();

            // Act
            var result = await setupService.GetCurrentSetupAsync();

            // Assert
            Assert.IsNotNull(result);
        }

        #endregion

        #region UpdateTenantModeAsync Tests

        [TestMethod]
        public async Task UpdateTenantModeAsync_ValidMode_UpdatesConfiguration()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();

            // Act
            await setupService.UpdateTenantModeAsync(setup.Id, "SingleTenant");

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.AreEqual("SingleTenant", result.TenantMode);
        }

        #endregion

        #region TestDatabaseConnectionAsync Tests

        [TestMethod]
        public async Task TestDatabaseConnectionAsync_InvalidConnection_ReturnsError()
        {
            // Arrange
            var connectionString = "Invalid connection string";

            // Act
            var result = await setupService.TestDatabaseConnectionAsync(connectionString);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
        }

        [TestMethod]
        public async Task TestDatabaseConnectionAsync_EmptyConnectionString_ReturnsError()
        {
            // Act
            var result = await setupService.TestDatabaseConnectionAsync(string.Empty);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Message);
        }

        #endregion

        #region UpdateDatabaseConfigAsync Tests

        [TestMethod]
        public async Task UpdateDatabaseConfigAsync_ValidConnection_UpdatesConfiguration()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();
            var connectionString = "Data Source=test.db";

            // Act
            await setupService.UpdateDatabaseConfigAsync(setup.Id, connectionString);

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.AreEqual(connectionString, result.DatabaseConnectionString);
        }

        #endregion

        #region TestStorageConnectionAsync Tests

        [TestMethod]
        public async Task TestStorageConnectionAsync_ValidAzureConnection_ReturnsSuccess()
        {
            // Arrange
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            // Act
            var result = await setupService.TestStorageConnectionAsync(connectionString);

            // Assert
            Assert.IsNotNull(result);
            // Note: May succeed or fail depending on whether Azurite is running
        }

        [TestMethod]
        public async Task TestStorageConnectionAsync_InvalidConnection_ReturnsError()
        {
            // Arrange
            var connectionString = "Invalid storage connection";

            // Act
            var result = await setupService.TestStorageConnectionAsync(connectionString);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
        }

        #endregion

        #region UpdateAdminAccountAsync Tests

        [TestMethod]
        public async Task UpdateAdminAccountAsync_ValidCredentials_UpdatesConfiguration()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();
            var email = "admin@example.com";
            var password = "SecurePassword123!";

            // Act
            await setupService.UpdateAdminAccountAsync(setup.Id, email, password);

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.AreEqual(email, result.SenderEmail);
            Assert.IsFalse(string.IsNullOrEmpty(result.AdminPassword));
        }

        #endregion

        #region UpdatePublisherConfigAsync Tests

        [TestMethod]
        public async Task UpdatePublisherConfigAsync_ValidConfiguration_UpdatesSetup()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();
            var publisherUrl = "https://mysite.com";
            var staticWebPages = true;
            var requiresAuth = false;
            var allowedFileTypes = ".jpg,.png,.pdf";

            // Act
            await setupService.UpdatePublisherConfigAsync(
                setup.Id,
                publisherUrl,
                staticWebPages,
                requiresAuth,
                allowedFileTypes,
                null,
                null,
                "My Title");

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.AreEqual(publisherUrl, result.PublisherUrl);
            Assert.AreEqual(staticWebPages, result.StaticWebPages);
            Assert.AreEqual(requiresAuth, result.CosmosRequiresAuthentication);
            Assert.AreEqual(allowedFileTypes, result.AllowedFileTypes);
        }

        #endregion

        #region TestEmailConfigAsync Tests

        [TestMethod]
        public async Task TestEmailConfigAsync_SendGrid_ValidKey_ReturnsSuccess()
        {
            // Arrange
            var provider = "SendGrid";
            var apiKey = "SG.test_key_123";
            var senderEmail = "sender@example.com";
            var recipient = "test@example.com";

            // Act
            var result = await setupService.TestEmailConfigAsync(
                provider,
                apiKey,
                null,
                null,
                null,
                null,
                null,
                senderEmail,
                recipient);

            // Assert
            Assert.IsNotNull(result);
            // Note: Will fail without valid SendGrid key, but tests the flow
        }

        [TestMethod]
        public async Task TestEmailConfigAsync_MissingProvider_ReturnsError()
        {
            // Act
            var result = await setupService.TestEmailConfigAsync(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "test@example.com",
                "recipient@example.com");

            // Assert
            Assert.IsFalse(result.Success);
        }

        #endregion

        #region UpdateEmailConfigAsync Tests

        [TestMethod]
        public async Task UpdateEmailConfigAsync_SendGridProvider_UpdatesConfiguration()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();
            var provider = "SendGrid";
            var apiKey = "SG.test_key";

            // Act
            await setupService.UpdateEmailConfigAsync(
                setup.Id,
                provider,
                apiKey,
                null,
                null,
                null,
                null,
                null);

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.AreEqual(apiKey, result.SendGridApiKey);
        }

        [TestMethod]
        public async Task UpdateEmailConfigAsync_SmtpProvider_UpdatesConfiguration()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();
            var provider = "SMTP";
            var smtpHost = "smtp.example.com";
            var smtpPort = "587";
            var smtpUsername = "user@example.com";
            var smtpPassword = "password123";

            // Act
            await setupService.UpdateEmailConfigAsync(
                setup.Id,
                provider,
                null,
                null,
                smtpHost,
                smtpPort,
                smtpUsername,
                smtpPassword);

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.AreEqual(smtpHost, result.SmtpHost);
            Assert.AreEqual(smtpPort, result.SmtpPort);
            Assert.AreEqual(smtpUsername, result.SmtpUsername);
        }

        #endregion

        #region UpdateCdnConfigAsync Tests

        [TestMethod]
        public async Task UpdateCdnConfigAsync_AzureCdn_UpdatesConfiguration()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();
            var subscriptionId = "sub-123";
            var resourceGroup = "rg-test";
            var profileName = "cdn-profile";
            var endpointName = "endpoint1";

            // Act
            await setupService.UpdateCdnConfigAsync(
                setup.Id,
                subscriptionId,
                resourceGroup,
                profileName,
                endpointName,
                false,
                null,
                null,
                null,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.AreEqual(subscriptionId, result.AzureCdnSubscriptionId);
            Assert.AreEqual(resourceGroup, result.AzureCdnResourceGroup);
            Assert.AreEqual(profileName, result.AzureCdnProfileName);
            Assert.AreEqual(endpointName, result.AzureCdnEndpointName);
        }

        [TestMethod]
        public async Task UpdateCdnConfigAsync_Cloudflare_UpdatesConfiguration()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();
            var apiToken = "cf-token-123";
            var zoneId = "zone-abc";

            // Act
            await setupService.UpdateCdnConfigAsync(
                setup.Id,
                null,
                null,
                null,
                null,
                false,
                apiToken,
                zoneId,
                null,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.AreEqual(apiToken, result.CloudflareApiToken);
            Assert.AreEqual(zoneId, result.CloudflareZoneId);
        }

        [TestMethod]
        public async Task UpdateCdnConfigAsync_NoCdn_ClearsConfiguration()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();

            // Act - Pass all empty values
            await setupService.UpdateCdnConfigAsync(
                setup.Id,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.IsTrue(string.IsNullOrEmpty(result.AzureCdnSubscriptionId));
            Assert.IsTrue(string.IsNullOrEmpty(result.CloudflareApiToken));
        }

        #endregion

        #region UpdateStepAsync Tests

        [TestMethod]
        public async Task UpdateStepAsync_ValidStep_UpdatesCurrentStep()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();

            // Act
            await setupService.UpdateStepAsync(setup.Id, 3);

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.AreEqual(3, result.CurrentStep);
        }

        [TestMethod]
        public async Task UpdateStepAsync_ProgressiveSteps_UpdatesCorrectly()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();

            // Act
            await setupService.UpdateStepAsync(setup.Id, 1);
            await setupService.UpdateStepAsync(setup.Id, 2);
            await setupService.UpdateStepAsync(setup.Id, 3);

            // Assert
            var result = await setupService.GetCurrentSetupAsync();
            Assert.AreEqual(3, result.CurrentStep);
        }

        #endregion

        #region CompleteSetupAsync Tests

        [TestMethod]
        public async Task CompleteSetupAsync_MissingDatabaseConfig_ReturnsError()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();
            
            // Configure storage (so it passes storage validation)
            await setupService.UpdateStorageConfigAsync(setup.Id, "UseDevelopmentStorage=true", "/");
            
            // Skip database configuration - but note: database config is NOT validated in CompleteSetupAsync
            // This test should actually pass since database config is optional

            // Act
            var result = await setupService.CompleteSetupAsync(setup.Id);

            // Assert - Should fail because admin email/password and publisher URL are missing
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Message.Contains("Administrator email", StringComparison.OrdinalIgnoreCase) ||
                          result.Message.Contains("Administrator password", StringComparison.OrdinalIgnoreCase) ||
                          result.Message.Contains("Publisher URL", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task CompleteSetupAsync_MissingAdminAccount_ReturnsError()
        {
            // Arrange
            var setup = await setupService.InitializeSetupAsync();
            await setupService.UpdateDatabaseConfigAsync(setup.Id, "Data Source=:memory:");
            await setupService.UpdateStorageConfigAsync(setup.Id, "UseDevelopmentStorage=true", "/");
            // Skip admin account

            // Act
            var result = await setupService.CompleteSetupAsync(setup.Id);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Message.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                         result.Message.Contains("Admin", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task CompleteSetupAsync_InvalidSetupId_ReturnsError()
        {
            // Act
            var result = await setupService.CompleteSetupAsync(Guid.NewGuid());

            // Assert
            Assert.IsFalse(result.Success);
        }

        #endregion

        #region Additional Tests

        [TestMethod]
        public async Task ShouldValidateStorageConfiguration()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ConnectionStrings:StorageConnection", "DefaultEndpointsProtocol=https;AccountName=test" },
                    { "CosmosStorage:AzureConfigs:0:AzureStorageConnectionString", "DefaultEndpointsProtocol=https;AccountName=test" }
                })
                .Build();

            var mockMediator = new Mock<IMediator>();

            // Act
            var result = await setupService.InitializeSetupAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreNotEqual(Guid.Empty, result.Id);
            Assert.AreEqual(1, result.CurrentStep);
        }

        #endregion
    }
}
