// <copyright file="PostSetupInitializationServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Editor.Services.Setup
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Services.Setup;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CommonMediator = Cosmos.Common.Features.Shared.IMediator;

    /// <summary>
    /// Unit tests for <see cref="PostSetupInitializationService"/>.
    /// </summary>
    [TestClass]
    public class PostSetupInitializationServiceTests
    {
        [TestMethod]
        [TestCategory("PostSetupInitializationService")]
        public async Task StartAsync_MultiTenantMode_ReturnsWithoutProcessing()
        {
            // Arrange
            var configuration = BuildConfiguration(isMultiTenant: true);
            var services = new ServiceCollection();
            services.AddSingleton(configuration);
            var serviceProvider = services.BuildServiceProvider();

            var loggerMock = new Mock<ILogger<PostSetupInitializationService>>();
            var service = new PostSetupInitializationService(serviceProvider, loggerMock.Object);

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert
            // No exception means the service returned without resolving DbContext or mediator.
        }

        [TestMethod]
        [TestCategory("PostSetupInitializationService")]
        public async Task StartAsync_SingleTenant_NoPendingSettings_DoesNothing()
        {
            // Arrange
            var configuration = BuildConfiguration(isMultiTenant: false);
            var mediatorMock = new Mock<IMediator>();
            var (provider, dbContext, dbPath) = BuildServiceProviderWithDb(configuration, mediatorMock.Object);
            var loggerMock = new Mock<ILogger<PostSetupInitializationService>>();
            var service = new PostSetupInitializationService(provider, loggerMock.Object);

            try
            {
                // Act
                await service.StartAsync(CancellationToken.None);

                // Assert
                Assert.AreEqual(0, await dbContext.Settings.CountAsync());
                mediatorMock.Verify(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                CleanupProvider(provider, dbContext, dbPath);
            }
        }

        [TestMethod]
        [TestCategory("PostSetupInitializationService")]
        public async Task StartAsync_PendingHomePageCreation_MissingUserId_DoesNotClearSettings()
        {
            // Arrange
            var configuration = BuildConfiguration(isMultiTenant: false);
            var mediatorMock = new Mock<IMediator>();
            var (provider, dbContext, dbPath) = BuildServiceProviderWithDb(configuration, mediatorMock.Object);
            var loggerMock = new Mock<ILogger<PostSetupInitializationService>>();
            var service = new PostSetupInitializationService(provider, loggerMock.Object);

            try
            {
                AddSetupSetting(dbContext, "PendingHomePageCreation", "true");
                AddSetupSetting(dbContext, "HomePageUserId", "not-a-guid");
                AddSetupSetting(dbContext, "HomePageTitle", "Home");
                await dbContext.SaveChangesAsync();

                // Act
                await service.StartAsync(CancellationToken.None);

                // Assert
                var remaining = await dbContext.Settings.Where(s => s.Group == "SETUP").ToListAsync();
                Assert.IsTrue(remaining.Any(s => s.Name == "PendingHomePageCreation"), "Pending flag should remain when settings are invalid");
                mediatorMock.Verify(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                CleanupProvider(provider, dbContext, dbPath);
            }
        }

        [TestMethod]
        [TestCategory("PostSetupInitializationService")]
        public async Task StartAsync_PendingHomePageCreation_ExistingHomePage_SkipsCreationAndClearsSettings()
        {
            // Arrange
            var configuration = BuildConfiguration(isMultiTenant: false);
            var mediatorMock = new Mock<IMediator>();
            var (provider, dbContext, dbPath) = BuildServiceProviderWithDb(configuration, mediatorMock.Object);
            var loggerMock = new Mock<ILogger<PostSetupInitializationService>>();
            var service = new PostSetupInitializationService(provider, loggerMock.Object);
            var userId = Guid.NewGuid();

            try
            {
                AddSetupSetting(dbContext, "PendingHomePageCreation", "true");
                AddSetupSetting(dbContext, "HomePageUserId", userId.ToString());
                AddSetupSetting(dbContext, "HomePageTitle", "Home");
                AddSetupSetting(dbContext, "HomePageTemplateId", Guid.NewGuid().ToString());

                dbContext.Articles.Add(new Article
                {
                    ArticleNumber = 1,
                    UrlPath = "root",
                    Title = "Existing Home",
                    Content = "",
                    StatusCode = (int)StatusCodeEnum.Active,
                    VersionNumber = 1
                });

                await dbContext.SaveChangesAsync();

                // Act
                await service.StartAsync(CancellationToken.None);

                // Assert
                Assert.AreEqual(0, await dbContext.Settings.CountAsync(), "Setup flags should be cleared after processing");
                mediatorMock.Verify(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                CleanupProvider(provider, dbContext, dbPath);
            }
        }

        [TestMethod]
        [TestCategory("PostSetupInitializationService")]
        public async Task StartAsync_PendingHomePageCreation_NoExistingHomePage_CreatesHomePageAndClearsSettings()
        {
            // Arrange
            var configuration = BuildConfiguration(isMultiTenant: false);
            var mediatorMock = new Mock<IMediator>();
            var (provider, dbContext, dbPath) = BuildServiceProviderWithDb(configuration, mediatorMock.Object);
            var loggerMock = new Mock<ILogger<PostSetupInitializationService>>();
            var service = new PostSetupInitializationService(provider, loggerMock.Object);
            var userId = Guid.NewGuid();
            var templateId = Guid.NewGuid();

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<ArticleViewModel>.Success(CreateArticleViewModel(1)));

            try
            {
                AddSetupSetting(dbContext, "PendingHomePageCreation", "true");
                AddSetupSetting(dbContext, "HomePageUserId", userId.ToString());
                AddSetupSetting(dbContext, "HomePageTitle", "Home");
                AddSetupSetting(dbContext, "HomePageTemplateId", templateId.ToString());
                await dbContext.SaveChangesAsync();

                // Act
                await service.StartAsync(CancellationToken.None);

                // Assert
                Assert.AreEqual(0, await dbContext.Settings.CountAsync(), "Setup flags should be cleared after processing");
                mediatorMock.Verify(m => m.SendAsync(
                        It.Is<CreateArticleCommand>(c =>
                            c.UserId == userId &&
                            c.Title == "Home" &&
                            c.UrlPathOverride == "root" &&
                            c.StatusCode == StatusCodeEnum.Active &&
                            c.TemplateId == templateId &&
                            c.ArticleType == ArticleType.General),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            }
            finally
            {
                CleanupProvider(provider, dbContext, dbPath);
            }
        }

        [TestMethod]
        [TestCategory("PostSetupInitializationService")]
        public async Task StartAsync_PendingHomePageCreation_CreateFails_ClearsSettings()
        {
            // Arrange
            var configuration = BuildConfiguration(isMultiTenant: false);
            var mediatorMock = new Mock<IMediator>();
            var (provider, dbContext, dbPath) = BuildServiceProviderWithDb(configuration, mediatorMock.Object);
            var loggerMock = new Mock<ILogger<PostSetupInitializationService>>();
            var service = new PostSetupInitializationService(provider, loggerMock.Object);
            var userId = Guid.NewGuid();

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult<ArticleViewModel>.Failure(new Dictionary<string, string[]>
                {
                    ["Title"] = new[] { "Title is required" }
                }));

            try
            {
                AddSetupSetting(dbContext, "PendingHomePageCreation", "true");
                AddSetupSetting(dbContext, "HomePageUserId", userId.ToString());
                AddSetupSetting(dbContext, "HomePageTitle", "Home");
                await dbContext.SaveChangesAsync();

                // Act
                await service.StartAsync(CancellationToken.None);

                // Assert
                Assert.AreEqual(0, await dbContext.Settings.CountAsync(), "Setup flags should be cleared even on failure");
                mediatorMock.Verify(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                CleanupProvider(provider, dbContext, dbPath);
            }
        }

        [TestMethod]
        [TestCategory("PostSetupInitializationService")]
        public async Task StartAsync_MediatorThrows_ExceptionIsSwallowed()
        {
            // Arrange
            var configuration = BuildConfiguration(isMultiTenant: false);
            var mediatorMock = new Mock<IMediator>();
            var (provider, dbContext, dbPath) = BuildServiceProviderWithDb(configuration, mediatorMock.Object);
            var loggerMock = new Mock<ILogger<PostSetupInitializationService>>();
            var service = new PostSetupInitializationService(provider, loggerMock.Object);
            var userId = Guid.NewGuid();

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Mediator failure"));

            try
            {
                AddSetupSetting(dbContext, "PendingHomePageCreation", "true");
                AddSetupSetting(dbContext, "HomePageUserId", userId.ToString());
                AddSetupSetting(dbContext, "HomePageTitle", "Home");
                await dbContext.SaveChangesAsync();

                // Act
                await service.StartAsync(CancellationToken.None);

                // Assert
                var remaining = await dbContext.Settings.Where(s => s.Group == "SETUP").ToListAsync();
                Assert.IsTrue(remaining.Any(s => s.Name == "PendingHomePageCreation"), "Pending flag should remain when exception is thrown");
                mediatorMock.Verify(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                CleanupProvider(provider, dbContext, dbPath);
            }
        }

        private static void AddSetupSetting(ApplicationDbContext dbContext, string name, string value)
        {
            dbContext.Settings.Add(new Setting
            {
                Group = "SETUP",
                Name = name,
                Value = value,
                IsRequired = true,
                Description = "Test setting"
            });
        }

        private static IConfiguration BuildConfiguration(bool? isMultiTenant)
        {
            var values = new Dictionary<string, string>();
            if (isMultiTenant.HasValue)
            {
                values["MultiTenantEditor"] = isMultiTenant.Value.ToString();
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        private static (ServiceProvider Provider, ApplicationDbContext DbContext, string DbPath) BuildServiceProviderWithDb(
            IConfiguration configuration,
            CommonMediator mediator)
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"skycms-postsetup-{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={dbPath}";

            var services = new ServiceCollection();
            services.AddSingleton(configuration);
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
            services.AddSingleton(mediator);

            var provider = services.BuildServiceProvider();
            var dbContext = provider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();

            return (provider, dbContext, dbPath);
        }

        private static ArticleViewModel CreateArticleViewModel(int articleNumber)
        {
            return new ArticleViewModel
            {
                ArticleNumber = articleNumber,
                Title = "Home",
                UrlPath = "root",
                Content = string.Empty,
                Layout = new LayoutViewModel
                {
                    LayoutName = string.Empty,
                    Notes = string.Empty,
                    Head = string.Empty,
                    HtmlHeader = string.Empty,
                    FooterHtmlContent = string.Empty
                }
            };
        }

        private static void CleanupProvider(ServiceProvider provider, ApplicationDbContext dbContext, string dbPath)
        {
            dbContext.Dispose();
            provider.Dispose();

            try
            {
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
            }
            catch
            {
                // Ignore cleanup failures in tests
            }
        }
    }
}
