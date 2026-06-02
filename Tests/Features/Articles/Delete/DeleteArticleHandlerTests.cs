// <copyright file="DeleteArticleHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Features.Articles.Delete
{
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Features.Articles.Delete;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Publishing;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for DeleteArticleHandler.
    /// </summary>
    [TestClass]
    public class DeleteArticleHandlerTests
    {
        private ApplicationDbContext dbContext;
        private Mock<IPublishingService> mockPublishingService;
        private Mock<IStorageContext> mockStorageContext;
        private Mock<IEditorSettings> mockEditorSettings;
        private Mock<ILogger<DeleteArticleHandler>> mockLogger;
        private DeleteArticleHandler handler;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            this.dbContext = new ApplicationDbContext(options);

            this.mockPublishingService = new Mock<IPublishingService>();
            this.mockStorageContext = new Mock<IStorageContext>();
            this.mockEditorSettings = new Mock<IEditorSettings>();
            this.mockLogger = new Mock<ILogger<DeleteArticleHandler>>();

            this.mockEditorSettings.Setup(s => s.StaticWebPages).Returns(false);

            this.handler = new DeleteArticleHandler(
                this.dbContext,
                new Mock<ICatalogService>().Object,
                this.mockPublishingService.Object,
                this.mockStorageContext.Object,
                this.mockEditorSettings.Object,
                this.mockLogger.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.dbContext?.Dispose();
        }

        [TestMethod]
        public async Task HandleAsync_ValidArticle_SuccessfullyDeletes()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                Title = "Test Article",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                UrlPath = "test-article",
                Updated = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.Add(article);
            await this.dbContext.SaveChangesAsync();

            var command = new DeleteArticleCommand { ArticleNumber = 1 };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var deletedArticle = await this.dbContext.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == 1);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, deletedArticle.StatusCode);
        }

        [TestMethod]
        public async Task HandleAsync_RootPage_ReturnsError()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                Title = "Home",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                UrlPath = "root",
                Updated = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.Add(article);
            await this.dbContext.SaveChangesAsync();

            var command = new DeleteArticleCommand { ArticleNumber = 1 };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        public async Task HandleAsync_ArticleNotFound_ReturnsError()
        {
            // Arrange
            var command = new DeleteArticleCommand { ArticleNumber = 999 };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.ErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task HandleAsync_NullCommand_ReturnsError()
        {
            // Act
            var result = await this.handler.HandleAsync(null);

            // Assert
            Assert.IsFalse(result.IsSuccess);
        }
    }
}
