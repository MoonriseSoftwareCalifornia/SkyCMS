// <copyright file="EditorSettingsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.EditorSettings
{
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Models;
    using Sky.Editor.Services.EditorSettings;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Comprehensive unit tests for the <see cref="EditorSettings"/> class.
    /// </summary>
    [TestClass]
    public class EditorSettingsTests : SkyCmsTestBase
    {
        private Mock<IHttpContextAccessor> mockHttpContextAccessor;
        private Mock<IDynamicConfigurationProvider> mockDynamicConfigProvider;
        private IMemoryCache memoryCache;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext();

            mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            mockDynamicConfigProvider = new Mock<IDynamicConfigurationProvider>();
            memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            memoryCache?.Dispose();
            await DisposeAsync();
        }

        #region Constructor Tests

        /// <summary>
        /// Tests that Constructor_WithValidParameters_CreatesInstance.
        /// </summary>
        [TestMethod]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false"
            });

            var services = new ServiceCollection().BuildServiceProvider();

            // Act
            var editorSettings = new EditorSettings(
                config,
                Db,
                mockHttpContextAccessor.Object,
                memoryCache,
                services);

            // Assert
            Assert.IsNotNull(editorSettings);
            Assert.IsFalse(editorSettings.IsMultiTenantEditor);
        }

        /// <summary>
        /// Tests that Constructor_MultiTenantMode_RequiresDynamicConfigProvider.
        /// </summary>
        [TestMethod]
        public void Constructor_MultiTenantMode_RequiresDynamicConfigProvider()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true"
            });

            var services = new ServiceCollection()
                .AddScoped<IDynamicConfigurationProvider>(_ => mockDynamicConfigProvider.Object)
                .BuildServiceProvider();

            // Act
            var editorSettings = new EditorSettings(
                config,
                Db,
                mockHttpContextAccessor.Object,
                memoryCache,
                services);

            // Assert
            Assert.IsNotNull(editorSettings);
            Assert.IsTrue(editorSettings.IsMultiTenantEditor);
        }

        /// <summary>
        /// Tests that Constructor_BackupStorageConnectionString_IsSet.
        /// </summary>
        [TestMethod]
        public void Constructor_BackupStorageConnectionString_IsSet()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["ConnectionStrings:BackupStorageConnectionString"] = "backup-connection-string"
            });

            var services = new ServiceCollection().BuildServiceProvider();

            // Act
            var editorSettings = new EditorSettings(
                config,
                Db,
                mockHttpContextAccessor.Object,
                memoryCache,
                services);

            // Assert
            Assert.AreEqual("backup-connection-string", editorSettings.BackupStorageConnectionString);
        }

        #endregion

        #region Single-Tenant Configuration Priority Tests

        /// <summary>
        /// Tests that GetEditorConfigAsync_SingleTenant_NoSetup_UsesConfigurationOnly.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_SingleTenant_NoSetup_UsesConfigurationOnly()
        {
            // Arrange - No database setup completed
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://config.example.com",
                ["AzureBlobStorageEndPoint"] = "/config-blob",
                ["CosmosStaticWebPages"] = "true"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://config.example.com", result.PublisherUrl);
            Assert.AreEqual("/config-blob", result.BlobPublicUrl);
            Assert.IsTrue(result.StaticWebPages);
        }

        /// <summary>
        /// Tests that GetEditorConfigAsync_SingleTenant_SetupComplete_UsesDatabase.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_SingleTenant_SetupComplete_UsesDatabase()
        {
            // Arrange - Setup completed, database has settings
            await CreateCompletedSetupInDatabase();

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://config.example.com" // This should be ignored
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://config.example.com", result.PublisherUrl);
            Assert.AreEqual("/", result.BlobPublicUrl);
            Assert.IsFalse(result.AllowSetup);
        }

        /// <summary>
        /// Tests that GetEditorConfigAsync_SingleTenant_EnvironmentOverridesDatabase.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_SingleTenant_EnvironmentOverridesDatabase()
        {
            // Arrange - Both database and environment have settings
            await CreateCompletedSetupInDatabase();

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://environment-override.example.com",
                ["CosmosStaticWebPages"] = "false"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert - Environment variables should override database
            Assert.AreEqual("https://environment-override.example.com", result.PublisherUrl);
            Assert.IsFalse(result.StaticWebPages);
            // BlobPublicUrl should come from database since not in environment
            Assert.AreEqual("/", result.BlobPublicUrl);
        }

        /// <summary>
        /// Tests that GetEditorConfigAsync_SingleTenant_UsesDefaultsWhenNothingConfigured.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_SingleTenant_UsesDefaultsWhenNothingConfigured()
        {
            // Arrange - No configuration at all
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert - Should use hardcoded defaults
            Assert.AreEqual("/", result.BlobPublicUrl);
            Assert.IsFalse(result.AllowSetup);
            Assert.IsFalse(result.CosmosRequiresAuthentication);
            Assert.AreEqual(string.Empty, result.PublisherUrl);
        }

        #endregion

        #region Database Loading Tests

        /// <summary>
        /// Tests that LoadConfigFromDatabase_SetupNotComplete_ReturnsNull.
        /// </summary>
        [TestMethod]
        public async Task LoadConfigFromDatabase_SetupNotComplete_ReturnsNull()
        {
            // Arrange - SYSTEM.AllowSetup is not "false"
            Db.Settings.Add(new Setting
            {
                Group = "SYSTEM",
                Name = "AllowSetup",
                Value = "true"
            });
            await Db.SaveChangesAsync();

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://fallback.com"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert - Should fall back to configuration
            Assert.AreEqual("https://fallback.com", result.PublisherUrl);
        }

        /// <summary>
        /// Tests that LoadConfigFromDatabase_NoSettings_ReturnsNull.
        /// </summary>
        [TestMethod]
        public async Task LoadConfigFromDatabase_NoSettings_ReturnsNull()
        {
            // Arrange - Setup complete but no settings
            Db.Settings.Add(new Setting
            {
                Group = "SYSTEM",
                Name = "AllowSetup",
                Value = "false"
            });
            await Db.SaveChangesAsync();

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://fallback.com"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert - Should fall back to configuration
            Assert.AreEqual("https://fallback.com", result.PublisherUrl);
        }

        /// <summary>
        /// Tests that LoadConfigFromDatabase_ValidSettings_ReturnsConfig.
        /// </summary>
        [TestMethod]
        public async Task LoadConfigFromDatabase_ValidSettings_ReturnsConfig()
        {
            // Arrange
            await CreateCompletedSetupInDatabase();

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("https://database.example.com", result.PublisherUrl);
            Assert.AreEqual("/", result.BlobPublicUrl);
            Assert.IsFalse(result.CosmosRequiresAuthentication);
            Assert.AreEqual("app-id-123", result.MicrosoftAppId);
        }

        /// <summary>
        /// Tests that LoadConfigFromDatabase_BooleanSettings_ParsedCorrectly.
        /// </summary>
        [TestMethod]
        public async Task LoadConfigFromDatabase_BooleanSettings_ParsedCorrectly()
        {
            // Arrange
            Db.Settings.AddRange(
                new Setting { Group = "SYSTEM", Name = "AllowSetup", Value = "false" },
                new Setting { Group = "PUBLISHER", Name = "CosmosRequiresAuthentication", Value = "true" },
                new Setting { Group = "PUBLISHER", Name = "StaticWebPages", Value = "false" },
                new Setting { Group = "PUBLISHER", Name = "PublisherUrl", Value = "https://test.com" }
            );
            await Db.SaveChangesAsync();

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert
            Assert.IsTrue(result.CosmosRequiresAuthentication);
            Assert.IsFalse(result.StaticWebPages);
        }

        /// <summary>
        /// Tests that LoadConfigFromDatabase_InvalidBooleanValue_ReturnsNull.
        /// </summary>
        [TestMethod]
        public async Task LoadConfigFromDatabase_InvalidBooleanValue_ReturnsNull()
        {
            // Arrange
            Db.Settings.AddRange(
                new Setting { Group = "SYSTEM", Name = "AllowSetup", Value = "false" },
                new Setting { Group = "PUBLISHER", Name = "CosmosRequiresAuthentication", Value = "invalid-bool" },
                new Setting { Group = "PUBLISHER", Name = "PublisherUrl", Value = "https://test.com" }
            );
            await Db.SaveChangesAsync();

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert - Should use default value when parsing fails
            Assert.IsFalse(result.CosmosRequiresAuthentication);
        }

        #endregion

        #region Caching Tests

        /// <summary>
        /// Tests that GetEditorConfigAsync_CalledTwice_ReturnsCachedValue.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_CalledTwice_ReturnsCachedValue()
        {
            // Arrange
            await CreateCompletedSetupInDatabase();

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result1 = await editorSettings.GetEditorConfigAsync();

            // Modify database after first call
            var setting = await Db.Settings.FirstAsync(s => s.Group == "PUBLISHER" && s.Name == "PublisherUrl");
            setting.Value = "https://modified.com";
            await Db.SaveChangesAsync();

            var result2 = await editorSettings.GetEditorConfigAsync();

            // Assert - Second call should return cached value
            Assert.AreEqual(result1.PublisherUrl, result2.PublisherUrl);
            Assert.AreEqual("https://database.example.com", result2.PublisherUrl);
        }

        /// <summary>
        /// Tests that GetEditorConfigAsync_CacheExpiration_ReloadsFromDatabase.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_CacheExpiration_ReloadsFromDatabase()
        {
            // Arrange
            await CreateCompletedSetupInDatabase();

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act - First call
            var result1 = await editorSettings.GetEditorConfigAsync();
            Assert.AreEqual("https://database.example.com", result1.PublisherUrl);

            // Simulate cache expiration by clearing cache
            memoryCache.Remove("edsetting-");

            // Modify database
            var setting = await Db.Settings.FirstAsync(s => s.Group == "PUBLISHER" && s.Name == "PublisherUrl");
            setting.Value = "https://updated.com";
            await Db.SaveChangesAsync();

            // Second call with new cache
            var newCache = new MemoryCache(new MemoryCacheOptions());
            var newEditorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, newCache, services);
            var result2 = await newEditorSettings.GetEditorConfigAsync();

            // Assert - Should reload from database
            Assert.AreEqual("https://updated.com", result2.PublisherUrl);

            newCache.Dispose();
        }

        /// <summary>
        /// Tests that GetEditorConfigAsync_DifferentDomains_UseDifferentCacheKeys.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_DifferentDomains_UseDifferentCacheKeys()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://default.com"
            });

            var services = new ServiceCollection().BuildServiceProvider();

            var editorSettings1 = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services, "domain1.com");
            var editorSettings2 = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services, "domain2.com");

            // Act
            var result1 = await editorSettings1.GetEditorConfigAsync();
            var result2 = await editorSettings2.GetEditorConfigAsync();

            // Assert - Both should work independently
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
        }

        #endregion

        #region Multi-Tenant Tests

        /// <summary>
        /// Tests that GetEditorConfigAsync_MultiTenant_LoadsFromDynamicProvider.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_MultiTenant_LoadsFromDynamicProvider()
        {
            // Arrange
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant1.com" },
                WebsiteUrl = "https://tenant1.com",
                BlobPublicUrl = "/tenant1-blob",
                PublisherRequiresAuthentication = true,
                MicrosoftAppId = "tenant1-app-id",
                PublisherMode = "Static",
                AllowSetup = false
            };

            mockDynamicConfigProvider
                .Setup(x => x.GetTenantConnectionAsync("tenant1.com", default))
                .ReturnsAsync(connection);

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true"
            });

            var services = new ServiceCollection()
                .AddScoped<IDynamicConfigurationProvider>(_ => mockDynamicConfigProvider.Object)
                .BuildServiceProvider();

            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services, "tenant1.com");

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert
            Assert.IsTrue(result.IsMultiTenantEditor);
            Assert.AreEqual("https://tenant1.com", result.PublisherUrl);
            Assert.AreEqual("/tenant1-blob", result.BlobPublicUrl);
            Assert.IsTrue(result.CosmosRequiresAuthentication);
            Assert.AreEqual("tenant1-app-id", result.MicrosoftAppId);
            Assert.IsTrue(result.StaticWebPages);
            Assert.IsFalse(result.AllowSetup);
        }

        /// <summary>
        /// Tests that GetEditorConfigAsync_MultiTenant_NullConnection_ThrowsException.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_MultiTenant_NullConnection_ThrowsException()
        {
            // Arrange
            mockDynamicConfigProvider
                .Setup(x => x.GetTenantConnectionAsync("nonexistent.com", default))
                .ReturnsAsync((Connection)null);

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true"
            });

            var services = new ServiceCollection()
                .AddScoped<IDynamicConfigurationProvider>(_ => mockDynamicConfigProvider.Object)
                .BuildServiceProvider();

            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services, "nonexistent.com");

            // Act & Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                await editorSettings.GetEditorConfigAsync());
        }

        /// <summary>
        /// Tests that GetEditorConfigAsync_MultiTenant_NoDomainName_UsesHttpContext.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_MultiTenant_NoDomainName_UsesHttpContext()
        {
            // Arrange
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant2.com" },
                WebsiteUrl = "https://tenant2.com",
                BlobPublicUrl = "/",
                PublisherMode = "Dynamic"
            };

            mockDynamicConfigProvider
                .Setup(x => x.GetTenantDomainNameFromRequest())
                .Returns("tenant2.com");

            mockDynamicConfigProvider
                .Setup(x => x.GetTenantConnectionAsync("tenant2.com", default))
                .ReturnsAsync(connection);

            var httpContext = new DefaultHttpContext();
            mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true"
            });

            var services = new ServiceCollection()
                .AddScoped<IDynamicConfigurationProvider>(_ => mockDynamicConfigProvider.Object)
                .BuildServiceProvider();

            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert
            Assert.AreEqual("https://tenant2.com", result.PublisherUrl);
            Assert.IsFalse(result.StaticWebPages); // PublisherMode = "Dynamic"
        }

        /// <summary>
        /// Tests that GetEditorConfigAsync_MultiTenant_NoHttpContext_ThrowsException.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_MultiTenant_NoHttpContext_ThrowsException()
        {
            // Arrange
            mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true"
            });

            var services = new ServiceCollection()
                .AddScoped<IDynamicConfigurationProvider>(_ => mockDynamicConfigProvider.Object)
                .BuildServiceProvider();

            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act & Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                await editorSettings.GetEditorConfigAsync());
        }

        #endregion

        #region Property Accessor Tests

        /// <summary>
        /// Tests that AllowSetup_ReturnsCorrectValue.
        /// </summary>
        [TestMethod]
        public async Task AllowSetup_ReturnsCorrectValue()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosAllowSetup"] = "true"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = editorSettings.AllowSetup;

            // Assert
            Assert.IsTrue(result);
        }

        /// <summary>
        /// Tests that PublisherUrl_ReturnsCorrectValue.
        /// </summary>
        [TestMethod]
        public async Task PublisherUrl_ReturnsCorrectValue()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://publisher.example.com"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = editorSettings.PublisherUrl;

            // Assert
            Assert.AreEqual("https://publisher.example.com", result);
        }

        /// <summary>
        /// Tests that BlobPublicUrl_ReturnsCorrectValue.
        /// </summary>
        [TestMethod]
        public async Task BlobPublicUrl_ReturnsCorrectValue()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["AzureBlobStorageEndPoint"] = "/blobs"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = editorSettings.BlobPublicUrl;

            // Assert
            Assert.AreEqual("/blobs", result);
        }

        /// <summary>
        /// Tests that AllowedFileTypes_WithCustomValue_ReturnsCustomValue.
        /// </summary>
        [TestMethod]
        public async Task AllowedFileTypes_WithCustomValue_ReturnsCustomValue()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["AllowedFileTypes"] = ".jpg,.png,.pdf"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = editorSettings.AllowedFileTypes;

            // Assert
            Assert.AreEqual(".jpg,.png,.pdf", result);
        }

        /// <summary>
        /// Tests that GetBlobAbsoluteUrl_AbsoluteUrl_ReturnsAsUri.
        /// </summary>
        [TestMethod]
        public async Task GetBlobAbsoluteUrl_AbsoluteUrl_ReturnsAsUri()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["AzureBlobStorageEndPoint"] = "https://cdn.example.com"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = editorSettings.GetBlobAbsoluteUrl();

            // Assert
            Assert.AreEqual("https://cdn.example.com/", result.ToString());
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Tests that LoadConfigFromDatabase_DatabaseConnectionFails_ReturnsNull.
        /// </summary>
        [TestMethod]
        public async Task LoadConfigFromDatabase_DatabaseConnectionFails_ReturnsNull()
        {
            // Arrange - Use a disposed context to simulate connection failure
            var disposedDb = CreateDisposedDbContext();

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://fallback.com"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, disposedDb, mockHttpContextAccessor.Object, memoryCache, services);

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert - Should fall back to configuration
            Assert.AreEqual("https://fallback.com", result.PublisherUrl);
        }

        /// <summary>
        /// Tests that LoadConfigFromDatabase_ExceptionThrown_FallsBackToConfiguration.
        /// </summary>
        [TestMethod]
        public async Task LoadConfigFromDatabase_ExceptionThrown_FallsBackToConfiguration()
        {
            // Arrange - Database throws exception
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://fallback.com"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Dispose database to cause exception
            await Db.DisposeAsync();

            // Act
            var result = await editorSettings.GetEditorConfigAsync();

            // Assert - Should fall back to configuration without throwing
            Assert.IsNotNull(result);
            Assert.AreEqual("https://fallback.com", result.PublisherUrl);
        }

        #endregion

        #region Thread Safety Tests

        /// <summary>
        /// Tests that EnsureConfigLoaded_ConcurrentAccess_LoadsOnlyOnce.
        /// </summary>
        [TestMethod]
        public async Task EnsureConfigLoaded_ConcurrentAccess_LoadsOnlyOnce()
        {
            // Arrange
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://test.com"
            });

            var services = new ServiceCollection().BuildServiceProvider();
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act - Access properties concurrently
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() => editorSettings.PublisherUrl)).ToArray();
            var results = await Task.WhenAll(tasks);

            // Assert - All should return the same value
            Assert.AreEqual(10, results.Length);
            Assert.IsTrue(results.All(r => r == "https://test.com"));
        }

        #endregion

        #region Helper Methods

        private IConfiguration CreateConfiguration(Dictionary<string, string> values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        private async Task CreateCompletedSetupInDatabase()
        {
            Db.Settings.AddRange(
                new Setting
                {
                    Group = "SYSTEM",
                    Name = "AllowSetup",
                    Value = "false"
                },
                new Setting
                {
                    Group = "PUBLISHER",
                    Name = "PublisherUrl",
                    Value = "https://database.example.com"
                },
                new Setting
                {
                    Group = "STORAGE",
                    Name = "BlobPublicUrl",
                    Value = "/database-blob"
                },
                new Setting
                {
                    Group = "PUBLISHER",
                    Name = "StaticWebPages",
                    Value = "true"
                },
                new Setting
                {
                    Group = "PUBLISHER",
                    Name = "CosmosRequiresAuthentication",
                    Value = "false"
                },
                new Setting
                {
                    Group = "OAUTH",
                    Name = "MicrosoftAppId",
                    Value = "app-id-123"
                },
                new Setting
                {
                    Group = "PUBLISHER",
                    Name = "AllowedFileTypes",
                    Value = ".jpg,.png,.gif"
                }
            );
            await Db.SaveChangesAsync();
        }

        private ApplicationDbContext CreateDisposedDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"DisposedDb_{Guid.NewGuid()}")
                .Options;

            var context = new ApplicationDbContext(options);
            context.Dispose();
            return context;
        }

        #endregion

        #region Cache Isolation Tests

        /// <summary>
        /// Verifies that two different tenant domain names produce independent cache entries.
        /// A config change for tenant A must not affect tenant B's cached value.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_TwoDifferentTenantDomains_CacheEntriesAreIsolated()
        {
            // Arrange — two tenant instances sharing one IMemoryCache
            var connectionA = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant-a.com" },
                WebsiteUrl = "https://tenant-a.com",
                BlobPublicUrl = "/blob-a",
                PublisherMode = "Dynamic"
            };
            var connectionB = new Connection
            {
                Id = Guid.NewGuid(),
                DomainNames = new[] { "tenant-b.com" },
                WebsiteUrl = "https://tenant-b.com",
                BlobPublicUrl = "/blob-b",
                PublisherMode = "Static"
            };

            mockDynamicConfigProvider
                .Setup(x => x.GetTenantConnectionAsync("tenant-a.com", default))
                .ReturnsAsync(connectionA);
            mockDynamicConfigProvider
                .Setup(x => x.GetTenantConnectionAsync("tenant-b.com", default))
                .ReturnsAsync(connectionB);

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true"
            });

            var services = new ServiceCollection()
                .AddScoped<IDynamicConfigurationProvider>(_ => mockDynamicConfigProvider.Object)
                .BuildServiceProvider();

            var settingsA = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services, "tenant-a.com");
            var settingsB = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services, "tenant-b.com");

            // Act
            var resultA = await settingsA.GetEditorConfigAsync();
            var resultB = await settingsB.GetEditorConfigAsync();

            // Assert — each tenant sees only its own values
            Assert.AreEqual("https://tenant-a.com", resultA.PublisherUrl, "Tenant A should have its own publisher URL");
            Assert.AreEqual("https://tenant-b.com", resultB.PublisherUrl, "Tenant B should have its own publisher URL");
            Assert.AreNotEqual(resultA.PublisherUrl, resultB.PublisherUrl, "Tenants must not share cache entries");
            Assert.IsFalse(resultA.StaticWebPages, "Tenant A mode: Dynamic");
            Assert.IsTrue(resultB.StaticWebPages, "Tenant B mode: Static");
        }

        /// <summary>
        /// Verifies that single-tenant mode always uses a stable, well-known cache key
        /// regardless of whether a domain name is explicitly passed.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_SingleTenant_AlwaysUsesStableCacheKey()
        {
            // Arrange — two instances: one with no domain, one with explicit domain
            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "false",
                ["CosmosPublisherUrl"] = "https://stable.example.com"
            });

            var services = new ServiceCollection().BuildServiceProvider();

            // Instance 1: no domain name (empty string path)
            var settingsNoDomain = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);
            // Instance 2: explicit domain name (non-empty)
            var settingsWithDomain = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services, "some-domain.com");

            // Act
            var resultNoDomain = await settingsNoDomain.GetEditorConfigAsync();
            // A second instance with a different domain should NOT receive the first one's cache,
            // because single-tenant uses "single-tenant" key only when domain is empty.
            var resultWithDomain = await settingsWithDomain.GetEditorConfigAsync();

            // Assert — both resolve the same publisher URL (single-tenant env var wins)
            Assert.AreEqual("https://stable.example.com", resultNoDomain.PublisherUrl);
            Assert.AreEqual("https://stable.example.com", resultWithDomain.PublisherUrl);

            // The empty-domain instance should be cached under "edsetting-single-tenant"
            Assert.IsTrue(memoryCache.TryGetValue("edsetting-single-tenant", out EditorConfig _),
                "Single-tenant with no domain must write to the 'edsetting-single-tenant' cache key");
        }

        /// <summary>
        /// Verifies that multi-tenant mode with an empty domain resolved from the HTTP context
        /// throws an <see cref="InvalidOperationException"/> rather than silently producing a shared key.
        /// </summary>
        [TestMethod]
        public async Task GetEditorConfigAsync_MultiTenant_EmptyDomainFromRequest_ThrowsInvalidOperation()
        {
            // Arrange — no domain name in constructor and provider returns empty string
            mockDynamicConfigProvider
                .Setup(x => x.GetTenantDomainNameFromRequest())
                .Returns(string.Empty);

            var httpContext = new DefaultHttpContext();
            mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var config = CreateConfiguration(new Dictionary<string, string>
            {
                ["MultiTenantEditor"] = "true"
            });

            var services = new ServiceCollection()
                .AddScoped<IDynamicConfigurationProvider>(_ => mockDynamicConfigProvider.Object)
                .BuildServiceProvider();

            // No explicit domain name — must resolve from request context
            var editorSettings = new EditorSettings(config, Db, mockHttpContextAccessor.Object, memoryCache, services);

            // Act & Assert — empty domain must not produce a shared cache key
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await editorSettings.GetEditorConfigAsync(),
                "An empty domain from the request must throw, not produce a shared cache key");
        }

        #endregion
    }
}

