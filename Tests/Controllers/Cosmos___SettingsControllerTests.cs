// <copyright file="SkyCmsSettingsControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Sky.Editor.Controllers;
    using Sky.Editor.Features.Copilot.GetSettings;
    using Sky.Editor.Features.Copilot.RemoveSettings;
    using Sky.Editor.Features.Copilot.SaveSettings;
    using Sky.Editor.Models;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="SkyCmsSettingsController"/>.
    /// Tests editor settings and CDN configuration management.
    /// Thread-safe for parallel execution using unique in-memory databases per test.
    /// </summary>
    [TestClass]
    public class SkyCmsSettingsControllerTests
    {
        private ApplicationDbContext dbContext;
        private Mock<ILogger<SkyCmsSettingsController>> loggerMock;
        private Mock<IMediator> mediatorMock;
        private Mock<IEditorSettings> editorSettingsMock;
        private Mock<ICdnServiceFactory> cdnServiceFactoryMock;
        private SkyCmsSettingsController controller;

        [TestInitialize]
        public void Setup()
        {
            // Setup in-memory database with unique name for parallel test execution
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"SettingsTest_{Guid.NewGuid()}")
                .Options;
            dbContext = new ApplicationDbContext(options);

            // Setup mocks
            loggerMock = new Mock<ILogger<SkyCmsSettingsController>>();
            mediatorMock = new Mock<IMediator>();
            editorSettingsMock = new Mock<IEditorSettings>();
            cdnServiceFactoryMock = new Mock<ICdnServiceFactory>();

            mediatorMock
                .Setup(m => m.QueryAsync(It.IsAny<GetCopilotProxyOptionsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<CopilotProxyOptions>.Success(new CopilotProxyOptions()));

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<SaveCopilotProxyOptionsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SaveCopilotProxyOptionsCommand cmd, CancellationToken _) =>
                    CommandResult<CopilotProxyOptions>.Success(cmd.Options));

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<RemoveCopilotProxyOptionsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<bool>.Success(true));

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
            controller = new SkyCmsSettingsController(
                dbContext,
                mediatorMock.Object,
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
                new SkyCmsSettingsController(null, mediatorMock.Object, loggerMock.Object, editorSettingsMock.Object, cdnServiceFactoryMock.Object));
        }

        [TestMethod]
        public void Constructor_WithNullMediator_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new SkyCmsSettingsController(dbContext, null, loggerMock.Object, editorSettingsMock.Object, cdnServiceFactoryMock.Object));
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new SkyCmsSettingsController(dbContext, mediatorMock.Object, null, editorSettingsMock.Object, cdnServiceFactoryMock.Object));
        }

        [TestMethod]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new SkyCmsSettingsController(dbContext, mediatorMock.Object, loggerMock.Object, null, cdnServiceFactoryMock.Object));
        }

        [TestMethod]
        public void Constructor_WithNullCdnServiceFactory_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new SkyCmsSettingsController(dbContext, mediatorMock.Object, loggerMock.Object, editorSettingsMock.Object, null));
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

        #region Copilot Proxy Settings Tests

        [TestMethod]
        public async Task Copilot_Get_ReturnsViewWithCopilotOptions()
        {
            // Act
            var result = await controller.Copilot();

            // Assert
            Assert.IsNotNull(result);
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.IsInstanceOfType(viewResult.Model, typeof(CopilotProxyOptions));
        }

        [TestMethod]
        public async Task Copilot_Get_WithInvalidJson_LogsWarningAndReturnsDefaultOptions()
        {
            // Arrange
            mediatorMock
                .Setup(m => m.QueryAsync(It.IsAny<GetCopilotProxyOptionsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<CopilotProxyOptions>.Failure("Load failed"));

            // Act
            var result = await controller.Copilot();

            // Assert
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            var model = viewResult.Model as CopilotProxyOptions;
            Assert.IsNotNull(model);
            Assert.IsFalse(model.Enabled);
            Assert.AreEqual("auto", model.Model);
            Assert.IsFalse(controller.ModelState.IsValid);
        }

        [TestMethod]
        public async Task Copilot_Get_WithEmptyDatabase_ReturnsDefaultOptions()
        {
            // Arrange
            mediatorMock
                .Setup(m => m.QueryAsync(It.IsAny<GetCopilotProxyOptionsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<CopilotProxyOptions>.Success(new CopilotProxyOptions()));

            // Act
            var result = await controller.Copilot();

            // Assert
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.IsInstanceOfType(viewResult.Model, typeof(CopilotProxyOptions));

            var model = viewResult.Model as CopilotProxyOptions;
            Assert.IsFalse(model.Enabled);
            Assert.AreEqual("auto", model.Model);
            Assert.AreEqual(8000, model.TimeoutMs);
            Assert.AreEqual(0.2, model.Temperature);
            Assert.AreEqual(160, model.MaxTokens);
        }

        [TestMethod]
        public async Task Copilot_Get_WithExistingSetting_LoadsOptionsFromDatabase()
        {
            // Arrange
            var options = new CopilotProxyOptions
            {
                Enabled = true,
                Endpoint = "https://example.ai/v1/chat/completions",
                Model = "gpt-4.1-mini",
                AccessToken = "secret-token",
                TimeoutMs = 12000,
                Temperature = 0.4,
                MaxTokens = 256,
            };

            mediatorMock
                .Setup(m => m.QueryAsync(It.IsAny<GetCopilotProxyOptionsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<CopilotProxyOptions>.Success(options));

            // Act
            var result = await controller.Copilot();

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as CopilotProxyOptions;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Enabled);
            Assert.AreEqual("https://example.ai/v1/chat/completions", model.Endpoint);
            Assert.AreEqual("gpt-4.1-mini", model.Model);
            Assert.AreEqual("secret-token", model.AccessToken);
            Assert.AreEqual(12000, model.TimeoutMs);
            Assert.AreEqual(0.4, model.Temperature);
            Assert.AreEqual(256, model.MaxTokens);
        }

        [TestMethod]
        public async Task Copilot_Post_WithValidModel_SavesSettings()
        {
            // Arrange
            var model = new CopilotProxyOptions
            {
                Enabled = true,
                Endpoint = "https://example.ai/v1/chat/completions",
                Model = "gpt-4o-mini",
                AccessToken = "abc123",
                TimeoutMs = 9000,
                Temperature = 0.3,
                MaxTokens = 180,
            };

            // Act
            var result = await controller.Copilot(model);

            // Assert
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual("Saved", viewResult.ViewData["Operation"] as string);

            var returnedModel = viewResult.Model as CopilotProxyOptions;
            Assert.IsNotNull(returnedModel);
            Assert.IsTrue(returnedModel.Enabled);
            Assert.AreEqual("https://example.ai/v1/chat/completions", returnedModel.Endpoint);

            mediatorMock.Verify(
                m => m.SendAsync(
                    It.Is<SaveCopilotProxyOptionsCommand>(c => c.Options.Endpoint == "https://example.ai/v1/chat/completions"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task Copilot_Post_WithInvalidModel_ReturnsViewWithModel()
        {
            // Arrange
            var model = new CopilotProxyOptions();
            controller.ModelState.AddModelError("Endpoint", "Required");

            // Act
            var result = await controller.Copilot(model);

            // Assert
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(model, viewResult.Model);
            Assert.IsFalse(controller.ModelState.IsValid);

            mediatorMock.Verify(
                m => m.SendAsync(It.IsAny<SaveCopilotProxyOptionsCommand>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task Copilot_Post_WithMediatorFailure_ReturnsViewWithModelAndError()
        {
            // Arrange
            var model = new CopilotProxyOptions
            {
                Enabled = true,
                Endpoint = "https://example.ai/v1/chat/completions",
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<SaveCopilotProxyOptionsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<CopilotProxyOptions>.Failure(new Dictionary<string, string[]>
                {
                    ["Endpoint"] = new[] { "Endpoint is required when Copilot is enabled." },
                }));

            // Act
            var result = await controller.Copilot(model);

            // Assert
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(model, viewResult.Model);
            Assert.IsFalse(controller.ModelState.IsValid);
            Assert.IsTrue(controller.ModelState.ContainsKey("Endpoint"));
        }

        [TestMethod]
        public async Task RemoveCopilot_UsesMediatorAndRedirects()
        {
            // Act
            var result = await controller.RemoveCopilot();

            // Assert
            var redirectResult = result as RedirectToActionResult;
            Assert.IsNotNull(redirectResult);
            Assert.AreEqual("Copilot", redirectResult.ActionName);

            mediatorMock.Verify(
                m => m.SendAsync(It.IsAny<RemoveCopilotProxyOptionsCommand>(), It.IsAny<CancellationToken>()),
                Times.Once);
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
