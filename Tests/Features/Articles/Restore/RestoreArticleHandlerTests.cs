// <copyright file="RestoreArticleHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Features.Articles.Restore
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Features.Articles.Restore;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.Slugs;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for RestoreArticleHandler.
    /// </summary>
    [TestClass]
    public class RestoreArticleHandlerTests
    {
        private ApplicationDbContext dbContext;
        private Mock<ICatalogService> mockCatalogService;
        private Mock<ISlugService> mockSlugService;
        private Mock<ILogger<RestoreArticleHandler>> mockLogger;
        private RestoreArticleHandler handler;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            this.dbContext = new ApplicationDbContext(options);

            this.mockCatalogService = new Mock<ICatalogService>();
            this.mockSlugService = new Mock<ISlugService>();
            this.mockLogger = new Mock<ILogger<RestoreArticleHandler>>();

            this.mockSlugService
                .Setup(s => s.Normalize(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((input, blogKey) => input?.ToLower().Replace(" ", "-") ?? string.Empty);

            this.handler = new RestoreArticleHandler(
                this.dbContext,
                this.mockCatalogService.Object,
                this.mockSlugService.Object,
                this.mockLogger.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.dbContext?.Dispose();
        }

        [TestMethod]
        public async Task HandleAsync_DeletedArticle_SuccessfullyRestores()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                Title = "Test Article",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                UrlPath = "test-article",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.Add(article);
            await this.dbContext.SaveChangesAsync();

            var command = new RestoreArticleCommand { ArticleNumber = 1, UserId = "test-user" };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var restoredArticle = await this.dbContext.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == 1);
            Assert.AreEqual((int)StatusCodeEnum.Active, restoredArticle.StatusCode);
        }

        [TestMethod]
        public async Task HandleAsync_ArticleNotFound_ReturnsError()
        {
            // Arrange
            var command = new RestoreArticleCommand { ArticleNumber = 999, UserId = "test-user" };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.ErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public async Task HandleAsync_ActiveArticle_ReturnsError()
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

            var command = new RestoreArticleCommand { ArticleNumber = 1, UserId = "test-user" };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        public async Task HandleAsync_TitleConflictsWithActiveArticle_RestoresWithRenamedTitle()
        {
            // Arrange — article 1 is trashed, article 2 is active with the same title
            var trashedArticle = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                Title = "My Article",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                UrlPath = "my-article",
                Updated = DateTimeOffset.UtcNow,
                Published = null
            };

            var activeArticle = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                Title = "My Article",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                UrlPath = "my-article",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.AddRange(trashedArticle, activeArticle);

            // Seed the catalog entry for the active article so title-conflict detection
            // (which now queries ArticleCatalog) finds the occupied title.
            this.dbContext.ArticleCatalog.Add(new CatalogEntry
            {
                ArticleNumber = activeArticle.ArticleNumber,
                Title = activeArticle.Title,
                UrlPath = activeArticle.UrlPath,
                StatusCode = (int)StatusCodeEnum.Active,
                Status = "Active",
                Updated = activeArticle.Updated
            });

            await this.dbContext.SaveChangesAsync();

            var command = new RestoreArticleCommand { ArticleNumber = 1, UserId = "test-user" };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var restored = await this.dbContext.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == 1);
            Assert.AreEqual((int)StatusCodeEnum.Active, restored.StatusCode);
            Assert.IsNull(restored.Published, "Restored article must not be published.");
            StringAssert.Contains(restored.Title, "my article", StringComparison.OrdinalIgnoreCase);
            Assert.AreNotEqual("My Article", restored.Title, "Title should have been renamed to avoid conflict.");
        }

        [TestMethod]
        public async Task HandleAsync_NoTitleConflict_RestoresWithOriginalTitle()
        {
            // Arrange — article is trashed, no other article has the same title
            var trashedArticle = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 5,
                Title = "Unique Title",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                UrlPath = "unique-title",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.Add(trashedArticle);
            await this.dbContext.SaveChangesAsync();

            var command = new RestoreArticleCommand { ArticleNumber = 5, UserId = "test-user" };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var restored = await this.dbContext.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == 5);
            Assert.AreEqual((int)StatusCodeEnum.Active, restored.StatusCode);
            Assert.AreEqual("Unique Title", restored.Title, "Title should be unchanged when there is no conflict.");
            Assert.IsNull(restored.Published, "Restored article must not be published.");
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
