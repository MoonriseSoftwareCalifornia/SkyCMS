// <copyright file="TrashArticleHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Features.Articles.Trash
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Features.Articles.Trash;
    using Sky.Editor.Services.Publishing;

    /// <summary>
    /// Tests for <see cref="TrashArticleHandler"/>.
    /// </summary>
    [TestClass]
    public class TrashArticleHandlerTests
    {
        private ApplicationDbContext dbContext;
        private Mock<IPublishingService> mockPublishingService;
        private Mock<IStorageContext> mockStorageContext;
        private Mock<ILogger<TrashArticleHandler>> mockLogger;
        private TrashArticleHandler handler;

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
            this.mockLogger = new Mock<ILogger<TrashArticleHandler>>();

            this.handler = new TrashArticleHandler(
                this.dbContext,
                this.mockPublishingService.Object,
                this.mockStorageContext.Object,
                this.mockLogger.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.dbContext?.Dispose();
        }

        [TestMethod]
        public async Task HandleAsync_DeletedArticle_PermanentlyRemovesArticleAndArtifacts()
        {
            // Arrange
            var articleId = Guid.NewGuid();

            this.dbContext.Articles.Add(new Article
            {
                Id = articleId,
                ArticleNumber = 7,
                Title = "Trashed",
                UrlPath = "trashed",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                Updated = DateTimeOffset.UtcNow,
                Content = "<p>Deleted</p>"
            });

            this.dbContext.ArticleCatalog.Add(new CatalogEntry
            {
                ArticleNumber = 7,
                Title = "Trashed",
                Status = "Deleted",
                Updated = DateTimeOffset.UtcNow,
                UrlPath = "trashed"
            });

            this.dbContext.Pages.Add(new PublishedPage
            {
                ArticleNumber = 7,
                Title = "Trashed",
                UrlPath = "trashed",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Deleted,
                Updated = DateTimeOffset.UtcNow,
                Content = "<p>Deleted</p>"
            });

            this.dbContext.ArticleLocks.Add(new ArticleLock
            {
                ArticleId = articleId,
                ConnectionId = "conn-1",
                UserEmail = "test@example.com",
                LockSetDateTime = DateTimeOffset.UtcNow,
                EditorType = "html",
                FilePath = string.Empty
            });

            this.dbContext.ArticleLogs.Add(new ArticleLog
            {
                ArticleId = articleId,
                ArticleTitle = "Trashed",
                IdentityUserId = "test-user",
                ActivityNotes = "Deleted"
            });

            await this.dbContext.SaveChangesAsync();

            // Act
            var result = await this.handler.HandleAsync(new TrashArticleCommand { ArticleNumber = 7 });

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, await this.dbContext.Articles.CountAsync(a => a.ArticleNumber == 7));
            Assert.AreEqual(0, await this.dbContext.ArticleCatalog.CountAsync(a => a.ArticleNumber == 7));
            Assert.AreEqual(0, await this.dbContext.Pages.CountAsync(a => a.ArticleNumber == 7));
            Assert.AreEqual(0, await this.dbContext.ArticleLocks.CountAsync(a => a.ArticleId == articleId));
            Assert.AreEqual(0, await this.dbContext.ArticleLogs.CountAsync(a => a.ArticleId == articleId));

            this.mockStorageContext.Verify(s => s.DeleteFolderAsync("/pub/articles/7"), Times.Once);
            this.mockPublishingService.Verify(p => p.WriteTocAsync("/"), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_ActiveArticle_ReturnsValidationError()
        {
            // Arrange
            this.dbContext.Articles.Add(new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 11,
                Title = "Active",
                UrlPath = "active",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                Updated = DateTimeOffset.UtcNow
            });
            await this.dbContext.SaveChangesAsync();

            // Act
            var result = await this.handler.HandleAsync(new TrashArticleCommand { ArticleNumber = 11 });

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.ErrorMessage.Contains("deleted state", StringComparison.OrdinalIgnoreCase));
            this.mockStorageContext.Verify(s => s.DeleteFolderAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
