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
    using System.Linq;
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

        [TestMethod]
        public async Task HandleAsync_MultipleVersions_AllVersionsRestoredToActive()
        {
            // Arrange — three deleted versions of the same logical article
            var v1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 50,
                Title = "Multi-Version Restore",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                UrlPath = "multi-version-restore",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow
            };
            var v2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 50,
                Title = "Multi-Version Restore",
                VersionNumber = 2,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                UrlPath = "multi-version-restore",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow
            };
            var v3 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 50,
                Title = "Multi-Version Restore",
                VersionNumber = 3,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                UrlPath = "multi-version-restore",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.AddRange(v1, v2, v3);
            await this.dbContext.SaveChangesAsync();

            var command = new RestoreArticleCommand { ArticleNumber = 50, UserId = "test-user" };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var restored = await this.dbContext.Articles
                .Where(a => a.ArticleNumber == 50)
                .ToListAsync();

            Assert.AreEqual(3, restored.Count, "All versions must still exist");
            Assert.IsTrue(restored.All(a => a.StatusCode == (int)StatusCodeEnum.Active),
                "Every version must be restored to Active");
            Assert.IsTrue(restored.All(a => a.Published == null),
                "Published date must be cleared on every version");
        }

        [TestMethod]
        public async Task HandleAsync_TitleConflict_UrlSlugAlsoRenamed()
        {
            // Arrange — trashed article; active article with the same title occupies the slug
            var trashed = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 60,
                Title = "Slug Conflict Article",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                UrlPath = "slug-conflict-article",
                Updated = DateTimeOffset.UtcNow,
                Published = null
            };

            var active = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 61,
                Title = "Slug Conflict Article",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = "test-user",
                UrlPath = "slug-conflict-article",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.AddRange(trashed, active);
            this.dbContext.ArticleCatalog.Add(new CatalogEntry
            {
                ArticleNumber = active.ArticleNumber,
                Title = active.Title,
                UrlPath = active.UrlPath,
                StatusCode = (int)StatusCodeEnum.Active,
                Status = "Active",
                Updated = active.Updated
            });
            await this.dbContext.SaveChangesAsync();

            var command = new RestoreArticleCommand { ArticleNumber = 60, UserId = "test-user" };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var restoredVersions = await this.dbContext.Articles
                .Where(a => a.ArticleNumber == 60)
                .ToListAsync();

            foreach (var v in restoredVersions)
            {
                Assert.AreNotEqual("slug-conflict-article", v.UrlPath,
                    "UrlPath must be renamed when a title conflict is detected");
                Assert.AreNotEqual("Slug Conflict Article", v.Title,
                    "Title must be renamed when a conflict is detected");
            }
        }

        [TestMethod]
        public async Task HandleAsync_Restore_CatalogUpsertCalledWithLatestVersion()
        {
            // Arrange
            var capturedArticle = (Article)null;
            this.mockCatalogService
                .Setup(c => c.UpsertAsync(It.IsAny<Article>(), It.IsAny<System.Threading.CancellationToken>()))
                .Callback<Article, System.Threading.CancellationToken>((a, _) => capturedArticle = a)
                .ReturnsAsync(new CatalogEntry());

            var v1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 70,
                Title = "Catalog Upsert Test",
                VersionNumber = 1,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                UrlPath = "catalog-upsert-test",
                Updated = DateTimeOffset.UtcNow
            };
            var v2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 70,
                Title = "Catalog Upsert Test",
                VersionNumber = 2,
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = "test-user",
                UrlPath = "catalog-upsert-test",
                Updated = DateTimeOffset.UtcNow
            };

            this.dbContext.Articles.AddRange(v1, v2);
            await this.dbContext.SaveChangesAsync();

            var command = new RestoreArticleCommand { ArticleNumber = 70, UserId = "test-user" };

            // Act
            var result = await this.handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            this.mockCatalogService.Verify(
                c => c.UpsertAsync(It.IsAny<Article>(), It.IsAny<System.Threading.CancellationToken>()),
                Times.Once,
                "CatalogService.UpsertAsync must be called exactly once on restore");
            Assert.IsNotNull(capturedArticle);
            Assert.AreEqual(2, capturedArticle.VersionNumber, "UpsertAsync must receive the latest (highest) version");
            Assert.AreEqual((int)StatusCodeEnum.Active, capturedArticle.StatusCode,
                "Article passed to UpsertAsync must already be restored to Active");
        }
    }
}
