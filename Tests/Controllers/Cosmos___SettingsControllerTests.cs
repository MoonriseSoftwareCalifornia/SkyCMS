// <copyright file="Cosmos___SettingsControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Editor.Controllers;
    using Sky.Editor.Models;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="Cosmos___SettingsController"/>.
    /// Tests editor settings and CDN configuration management.
    /// Thread-safe for parallel execution using unique in-memory databases per test.
    /// </summary>
    [TestClass]
    public class Cosmos___SettingsControllerTests
    {
        private ApplicationDbContext dbContext;
        private Mock<ILogger<Cosmos___SettingsController>> loggerMock;
        private Mock<IEditorSettings> editorSettingsMock;
        private Mock<ICdnServiceFactory> cdnServiceFactoryMock;
        private Cosmos___SettingsController controller;

        [TestInitialize]
        public void Setup()
        {
            // Setup in-memory database with unique name for parallel test execution
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"SettingsTest_{Guid.NewGuid()}")
                .Options;
            dbContext = new ApplicationDbContext(options);

            // Setup mocks
            loggerMock = new Mock<ILogger<Cosmos___SettingsController>>();
            editorSettingsMock = new Mock<IEditorSettings>();
            cdnServiceFactoryMock = new Mock<ICdnServiceFactory>();

            // Setup default editor settings mock behavior
            editorSettingsMock.Setup(s => s.GetEditorConfigAsync())
                .ReturnsAsync(new EditorConfig
                {
                    BlobPublicUrl = "https://cdn.example.com",
                    StaticWebPages = false
                });

            // Setup CDN service factory mock to return a CdnService with empty settings
            var mockLogger = new Mock<ILogger>();
            cdnServiceFactoryMock.Setup(f => f.CreateCdnServiceAsync(
                    It.IsAny<ApplicationDbContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<HttpContext>()))
                .ReturnsAsync((ApplicationDbContext db, ILogger log, HttpContext ctx) =>
                    new CdnService(new List<CdnSetting>(), log, ctx));

            // Create controller
            controller = new Cosmos___SettingsController(
                dbContext,
                loggerMock.Object,
                editorSettingsMock.Object,
                cdnServiceFactoryMock.Object);

            // Setup HttpContext
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            dbContext?.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new Cosmos___SettingsController(null, loggerMock.Object, editorSettingsMock.Object, cdnServiceFactoryMock.Object));
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new Cosmos___SettingsController(dbContext, null, editorSettingsMock.Object, cdnServiceFactoryMock.Object));
        }

        [TestMethod]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new Cosmos___SettingsController(dbContext, loggerMock.Object, null, cdnServiceFactoryMock.Object));
        }

        [TestMethod]
        public void Constructor_WithNullCdnServiceFactory_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new Cosmos___SettingsController(dbContext, loggerMock.Object, editorSettingsMock.Object, null));
        }

        #endregion

        #region Batch 1: Settings Listing Tests

        [TestMethod]
        public async Task Index_Get_ReturnsViewWithEditorConfig()
        {
            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsNotNull(result);
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.IsInstanceOfType(viewResult.Model, typeof(EditorConfig));

            var model = viewResult.Model as EditorConfig;
            Assert.AreEqual("https://cdn.example.com", model.BlobPublicUrl);
            Assert.IsFalse(model.StaticWebPages);
        }

        [TestMethod]
        public async Task CDN_Get_ReturnsViewWithCdnViewModel()
        {
            // Act
            var result = await controller.CDN();

            // Assert
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.IsInstanceOfType(viewResult.Model, typeof(CdnViewModel));
            Assert.IsNull(viewResult.ViewData["Operation"]);
        }

        [TestMethod]
        public async Task CDN_Get_WithMultipleCdnProviders_LoadsAllProviders()
        {
            // Arrange - Add settings for all CDN providers
            dbContext.Settings.AddRange(
                CreateCdnSettingEntity(CdnProviderEnum.AzureCDN, new AzureCdnConfig
                {
                    ProfileName = "azure-profile",
                    EndpointName = "azure-endpoint"
                }),
                CreateCdnSettingEntity(CdnProviderEnum.Cloudflare, new CloudflareCdnConfig
                {
                    ApiToken = "cf-token",
                    ZoneId = "cf-zone"
                }),
                CreateCdnSettingEntity(CdnProviderEnum.CloudFront, new CloudFrontCdnConfig
                {
                    DistributionId = "cf-dist-123",
                    AccessKeyId = "aws-key"
                }),
                CreateCdnSettingEntity(CdnProviderEnum.Sucuri, new SucuriCdnConfig
                {
                    ApiKey = "sucuri-key",
                    ApiSecret = "sucuri-secret"
                })
            );
            await dbContext.SaveChangesAsync();

            // Act
            var result = await controller.CDN();

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as CdnViewModel;
            Assert.IsNotNull(model.AzureCdn);
            Assert.IsNotNull(model.Cloudflare);
            Assert.IsNotNull(model.CloudFront);
            Assert.IsNotNull(model.Sucuri);
            Assert.AreEqual("azure-profile", model.AzureCdn.ProfileName);
            Assert.AreEqual("cf-token", model.Cloudflare.ApiToken);
            Assert.AreEqual("cf-dist-123", model.CloudFront.DistributionId);
            Assert.AreEqual("sucuri-key", model.Sucuri.ApiKey);
        }

        [TestMethod]
        public async Task CDN_Get_WithAzureFrontDoor_LoadsAsFrontDoor()
        {
            // Arrange
            dbContext.Settings.Add(CreateCdnSettingEntity(CdnProviderEnum.AzureFrontdoor,
                new AzureCdnConfig
                {
                    ProfileName = "frontdoor-profile",
                    EndpointName = "frontdoor-endpoint",
                    IsFrontDoor = true
                }));
            await dbContext.SaveChangesAsync();

            // Act
            var result = await controller.CDN();

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as CdnViewModel;
            Assert.IsNotNull(model.AzureCdn);
            Assert.AreEqual("frontdoor-profile", model.AzureCdn.ProfileName);
            Assert.IsTrue(model.AzureCdn.IsFrontDoor);
        }

        [TestMethod]
        public async Task CDN_Get_WithNullCdnSetting_SkipsEntry()
        {
            // Arrange
            dbContext.Settings.Add(new Setting
            {
                Group = "CDN",
                Name = "NullSetting",
                Value = "null"
            });
            await dbContext.SaveChangesAsync();

            // Act
            var result = await controller.CDN();

            // Assert - Should not throw
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            var model = viewResult.Model as CdnViewModel;
            Assert.IsNotNull(model);
        }

        [TestMethod]
        public async Task CDN_Get_WithInvalidJson_LogsWarning()
        {
            // Arrange
            dbContext.Settings.Add(new Setting
            {
                Group = "CDN",
                Name = "BadJson",
                Value = "{ invalid : json }"
            });
            await dbContext.SaveChangesAsync();

            // Act
            var result = await controller.CDN();

            // Assert - Verify warning was logged
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<JsonException>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        [TestMethod]
        public async Task CDN_Get_WithEmptyDatabase_ReturnsEmptyViewModel()
        {
            // Act
            var result = await controller.CDN();

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as CdnViewModel;
            Assert.IsNotNull(model);
            // CdnViewModel creates default instances, so check they're empty/default
            Assert.IsTrue(string.IsNullOrEmpty(model.AzureCdn?.ProfileName));
            Assert.IsTrue(string.IsNullOrEmpty(model.Cloudflare?.ApiToken));
            Assert.IsTrue(string.IsNullOrEmpty(model.CloudFront?.DistributionId));
            Assert.IsTrue(string.IsNullOrEmpty(model.Sucuri?.ApiKey));
        }

        #endregion

        #region Batch 2: Settings CRUD Tests

        [TestMethod]
        public async Task Index_Post_WithValidModel_SavesSettings()
        {
            // Arrange
            var model = new EditorConfig
            {
                BlobPublicUrl = "https://newcdn.example.com",
                StaticWebPages = false
            };

            // Act
            var result = await controller.Index(model);

            // Assert
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(model, viewResult.Model);

            // Verify setting was saved
            var savedSetting = await dbContext.Settings
                .FirstOrDefaultAsync(s => s.Group == "EDITORSETTINGS");
            Assert.IsNotNull(savedSetting);

            var savedConfig = JsonConvert.DeserializeObject<EditorConfig>(savedSetting.Value);
            Assert.AreEqual("https://newcdn.example.com", savedConfig.BlobPublicUrl);
        }

        [TestMethod]
        public async Task Index_Post_WithStaticWebPages_SetsBlobUrlToSlash()
        {
            // Arrange
            var model = new EditorConfig
            {
                BlobPublicUrl = "https://cdn.example.com",
                StaticWebPages = true
            };

            // Act
            var result = await controller.Index(model);

            // Assert
            var returnedModel = (result as ViewResult).Model as EditorConfig;
            Assert.AreEqual("/", returnedModel.BlobPublicUrl);

            // Verify saved value
            var savedSetting = await dbContext.Settings
                .FirstOrDefaultAsync(s => s.Group == "EDITORSETTINGS");
            var savedConfig = JsonConvert.DeserializeObject<EditorConfig>(savedSetting.Value);
            Assert.AreEqual("/", savedConfig.BlobPublicUrl);
        }

        [TestMethod]
        public async Task Index_Post_WithInvalidModel_ReturnsViewWithModel()
        {
            // Arrange
            var model = new EditorConfig();
            controller.ModelState.AddModelError("BlobPublicUrl", "Required");

            // Act
            var result = await controller.Index(model);

            // Assert
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(model, viewResult.Model);
            Assert.IsFalse(controller.ModelState.IsValid);
        }

        [TestMethod]
        public async Task Index_Post_CreatesNewSettingIfNotExists()
        {
            // Arrange
            var model = new EditorConfig { BlobPublicUrl = "https://test.com" };

            // Act
            await controller.Index(model);

            // Assert
            var settings = await dbContext.Settings.ToListAsync();
            Assert.AreEqual(1, settings.Count);
            Assert.AreEqual("EDITORSETTINGS", settings[0].Group);
            Assert.AreEqual("EditorSettings", settings[0].Name);
        }

        [TestMethod]
        public async Task Index_Post_UpdatesExistingSettingIfExists()
        {
            // Arrange
            var existingSetting = new Setting
            {
                Group = "EDITORSETTINGS",
                Name = "EditorSettings",
                Value = JsonConvert.SerializeObject(new EditorConfig { BlobPublicUrl = "https://old.com" }),
                Description = "Settings used by the Cosmos Editor"
            };
            dbContext.Settings.Add(existingSetting);
            await dbContext.SaveChangesAsync();

            var model = new EditorConfig { BlobPublicUrl = "https://updated.com" };

            // Act
            await controller.Index(model);

            // Assert
            var settings = await dbContext.Settings.ToListAsync();
            Assert.AreEqual(1, settings.Count);
            var savedConfig = JsonConvert.DeserializeObject<EditorConfig>(settings[0].Value);
            Assert.AreEqual("https://updated.com", savedConfig.BlobPublicUrl);
        }

        [TestMethod]
        public async Task CDN_Post_WithValidAzureCdn_SavesSettings()
        {
            // Arrange
            var model = new CdnViewModel
            {
                AzureCdn = new AzureCdnConfig
                {
                    ProfileName = "test-profile",
                    EndpointName = "test-endpoint"
                }
            };

            // Act
            await controller.CDN(model);

            // Assert
            var savedSettings = await dbContext.Settings
                .Where(s => s.Group == "CDN")
                .ToListAsync();
            Assert.AreEqual(1, savedSettings.Count);
            Assert.AreEqual("AzureCDN", savedSettings[0].Name);
        }

        [TestMethod]
        public async Task CDN_Post_WithValidCloudflare_SavesSettings()
        {
            // Arrange
            var model = new CdnViewModel
            {
                Cloudflare = new CloudflareCdnConfig
                {
                    ApiToken = "cf-token",
                    ZoneId = "zone123"
                }
            };

            // Act
            await controller.CDN(model);

            // Assert
            var savedSettings = await dbContext.Settings
                .Where(s => s.Group == "CDN")
                .ToListAsync();
            Assert.AreEqual(1, savedSettings.Count);
            Assert.AreEqual("Cloudflare", savedSettings[0].Name);
        }

        [TestMethod]
        public async Task CDN_Post_ClearsExistingCdnSettings()
        {
            // Arrange
            dbContext.Settings.AddRange(
                CreateCdnSettingEntity(CdnProviderEnum.AzureCDN, new AzureCdnConfig { ProfileName = "old" }),
                CreateCdnSettingEntity(CdnProviderEnum.Cloudflare, new CloudflareCdnConfig { ApiToken = "old" })
            );
            await dbContext.SaveChangesAsync();

            var model = new CdnViewModel
            {
                CloudFront = new CloudFrontCdnConfig { DistributionId = "new-dist" }
            };

            // Act
            await controller.CDN(model);

            // Assert
            var remainingSettings = await dbContext.Settings
                .Where(s => s.Group == "CDN")
                .ToListAsync();
            Assert.AreEqual(1, remainingSettings.Count);
            Assert.AreEqual("CloudFront", remainingSettings[0].Name);
        }

        [TestMethod]
        public async Task CDN_Post_SkipsNullConfig()
        {
            // Arrange
            var model = new CdnViewModel { AzureCdn = null };

            // Act
            await controller.CDN(model);

            // Assert
            var settings = await dbContext.Settings.Where(s => s.Group == "CDN").ToListAsync();
            Assert.AreEqual(0, settings.Count);
        }

        [TestMethod]
        public async Task CDN_Post_SkipsEmptyProfileName()
        {
            // Arrange
            var model = new CdnViewModel
            {
                AzureCdn = new AzureCdnConfig { ProfileName = string.Empty }
            };

            // Act
            await controller.CDN(model);

            // Assert
            var settings = await dbContext.Settings.Where(s => s.Group == "CDN").ToListAsync();
            Assert.AreEqual(0, settings.Count);
        }

        [TestMethod]
        public async Task CDN_Post_WithFrontDoor_SetsCorrectProvider()
        {
            // Arrange
            var model = new CdnViewModel
            {
                AzureCdn = new AzureCdnConfig
                {
                    ProfileName = "frontdoor",
                    IsFrontDoor = true
                }
            };

            // Act
            await controller.CDN(model);

            // Assert
            var setting = await dbContext.Settings.FirstOrDefaultAsync(s => s.Group == "CDN");
            var cdnSetting = JsonConvert.DeserializeObject<CdnSetting>(setting.Value);
            Assert.AreEqual(CdnProviderEnum.AzureFrontdoor, cdnSetting.CdnProvider);
        }

        [TestMethod]
        public async Task Remove_ClearsCdnSettingsAndRedirects()
        {
            // Arrange
            dbContext.Settings.AddRange(
                CreateCdnSettingEntity(CdnProviderEnum.AzureCDN, new AzureCdnConfig { ProfileName = "test" }),
                CreateCdnSettingEntity(CdnProviderEnum.Cloudflare, new CloudflareCdnConfig { ApiToken = "test" })
            );
            await dbContext.SaveChangesAsync();

            // Act
            var result = await controller.Remove();

            // Assert
            var redirectResult = result as RedirectToActionResult;
            Assert.IsNotNull(redirectResult);
            Assert.AreEqual("Index", redirectResult.ActionName);

            var remainingSettings = await dbContext.Settings
                .Where(s => s.Group == "CDN")
                .ToListAsync();
            Assert.AreEqual(0, remainingSettings.Count);
        }

        [TestMethod]
        public async Task Remove_WithNoExistingSettings_SucceedsWithoutError()
        {
            // Act
            var result = await controller.Remove();

            // Assert
            var redirectResult = result as RedirectToActionResult;
            Assert.IsNotNull(redirectResult);
            Assert.AreEqual("Index", redirectResult.ActionName);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper method to create a CDN setting entity for testing.
        /// Thread-safe for parallel execution.
        /// </summary>
        private Setting CreateCdnSettingEntity<T>(CdnProviderEnum provider, T config)
        {
            return new Setting
            {
                Group = "CDN",
                Name = provider.ToString(),
                Value = JsonConvert.SerializeObject(new CdnSetting
                {
                    CdnProvider = provider,
                    Value = JsonConvert.SerializeObject(config)
                }),
                Description = $"{provider} configuration"
            };
        }

        #endregion
    }
}
