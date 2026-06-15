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
    using System.Linq;
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

        [TestMethod]
        public async Task HandleAsync_MultipleVersions_AllVersionsMarkedDeleted()
        {
            // Arrange — three versions of the same logical article
            var v1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 10,
                Title = "Multi-Version Article",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                UrlPath = "multi-version",
                Updated = DateTimeOffset.UtcNow
            };
            var v2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 10,
                Title = "Multi-Version Article",
                VersionNumber = 2,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                UrlPath = "multi-version",
                Updated = DateTimeOffset.UtcNow
            };
            var v3 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 10,
                Title = "Multi-Version Article",
                VersionNumber = 3,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                UrlPath = "multi-version",
                Updated = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.AddRange(v1, v2, v3);
            await this.dbContext.SaveChangesAsync();

            var command = new DeleteArticleCommand { ArticleNumber = 10 };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var remaining = await this.dbContext.Articles
                .Where(a => a.ArticleNumber == 10)
                .ToListAsync();

            Assert.AreEqual(3, remaining.Count, "All versions must still exist (soft delete)");
            Assert.IsTrue(remaining.All(a => a.StatusCode == (int)StatusCodeEnum.Deleted),
                "Every version must be marked Deleted");
        }

        [TestMethod]
        public async Task HandleAsync_PublishedArticle_RelatedPagesRemoved()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 20,
                Title = "Published Article",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                UrlPath = "published-article",
                Published = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow
            };

            var page = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 20,
                Title = "Published Article",
                UrlPath = "published-article",
                Content = "<p>content</p>",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow,
                StatusCode = (int)StatusCodeEnum.Active
            };

            this.dbContext.Articles.Add(article);
            this.dbContext.Pages.Add(page);
            await this.dbContext.SaveChangesAsync();

            var command = new DeleteArticleCommand { ArticleNumber = 20 };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var remainingPages = await this.dbContext.Pages
                .Where(p => p.ArticleNumber == 20)
                .ToListAsync();
            Assert.AreEqual(0, remainingPages.Count, "Page entries must be removed on soft delete");
        }

        [TestMethod]
        public async Task HandleAsync_CatalogUpsertCalledWithLatestVersion()
        {
            // Arrange — two versions; catalog upsert should receive the highest version number
            var mockCatalog = new Mock<ICatalogService>();
            Article capturedArticle = null;
            mockCatalog
                .Setup(c => c.UpsertAsync(It.IsAny<Article>(), It.IsAny<System.Threading.CancellationToken>()))
                .Callback<Article, System.Threading.CancellationToken>((a, _) => capturedArticle = a)
                .ReturnsAsync(new CatalogEntry());

            var localHandler = new DeleteArticleHandler(
                this.dbContext,
                mockCatalog.Object,
                this.mockPublishingService.Object,
                this.mockStorageContext.Object,
                this.mockEditorSettings.Object,
                this.mockLogger.Object);

            var v1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 30,
                Title = "Versioned Article",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                UrlPath = "versioned-article",
                Updated = DateTimeOffset.UtcNow
            };
            var v2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 30,
                Title = "Versioned Article",
                VersionNumber = 2,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                UrlPath = "versioned-article",
                Updated = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.AddRange(v1, v2);
            await this.dbContext.SaveChangesAsync();

            var command = new DeleteArticleCommand { ArticleNumber = 30 };

            // Act
            var result = await localHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            mockCatalog.Verify(c => c.UpsertAsync(It.IsAny<Article>(), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
            Assert.IsNotNull(capturedArticle, "UpsertAsync must be called with a non-null article");
            Assert.AreEqual(2, capturedArticle.VersionNumber, "UpsertAsync must receive the latest (highest) version");
            Assert.AreEqual((int)StatusCodeEnum.Deleted, capturedArticle.StatusCode, "Article passed to UpsertAsync must already be marked Deleted");
        }

        [TestMethod]
        public async Task HandleAsync_AlreadyDeletedArticle_IsIdempotent()
        {
            // Arrange — article is already in Deleted state
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 40,
                Title = "Already Deleted",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                UrlPath = "already-deleted",
                Updated = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.Add(article);
            await this.dbContext.SaveChangesAsync();

            var command = new DeleteArticleCommand { ArticleNumber = 40 };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert — deleting an already-deleted article is idempotent; handler succeeds without error
            Assert.IsTrue(result.IsSuccess, "Delete must be idempotent when article is already in Deleted state");

            var stillDeleted = await this.dbContext.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == 40);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, stillDeleted.StatusCode, "Article must remain in Deleted state");
        }
    }
}
