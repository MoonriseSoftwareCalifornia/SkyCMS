// <copyright file="CatalogServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Catalog
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.Html;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="CatalogService"/>, covering status-code projection,
    /// Published-date suppression, introduction derivation, and upsert-replaces-existing semantics.
    /// </summary>
    [TestClass]
    public class CatalogServiceTests
    {
        private ApplicationDbContext db;
        private Mock<IArticleHtmlService> mockHtml;
        private Mock<IClock> mockClock;
        private CatalogService service;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"CatalogServiceTests_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            this.db = new ApplicationDbContext(options);
            this.mockHtml = new Mock<IArticleHtmlService>();
            this.mockClock = new Mock<IClock>();
            this.mockClock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

            // Default: extraction returns empty string unless overridden per-test
            this.mockHtml.Setup(h => h.ExtractIntroduction(It.IsAny<string>())).Returns(string.Empty);

            this.service = new CatalogService(
                this.db,
                this.mockHtml.Object,
                this.mockClock.Object,
                new NullLogger<CatalogService>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.db?.Dispose();
        }

        // ----------------------------------------------------------------
        // Status-code projection tests
        // ----------------------------------------------------------------

        [TestMethod]
        public async Task UpsertAsync_ActiveArticle_StatusIsActiveAndPublishedPreserved()
        {
            // Arrange
            var published = DateTimeOffset.UtcNow.AddDays(-1);
            var article = BuildArticle(1, StatusCodeEnum.Active, published);
            this.db.Articles.Add(article);
            await this.db.SaveChangesAsync();

            // Act
            var entry = await this.service.UpsertAsync(article, CancellationToken.None);

            // Assert
            Assert.AreEqual("Active", entry.Status);
            Assert.AreEqual((int)StatusCodeEnum.Active, entry.StatusCode);
            Assert.IsNotNull(entry.Published, "Published date must be preserved for Active articles");
            Assert.AreEqual(published, entry.Published);
        }

        [TestMethod]
        public async Task UpsertAsync_InactiveArticle_StatusIsInactiveAndPublishedPreserved()
        {
            // Arrange
            var published = DateTimeOffset.UtcNow.AddDays(-2);
            var article = BuildArticle(2, StatusCodeEnum.Inactive, published);
            this.db.Articles.Add(article);
            await this.db.SaveChangesAsync();

            // Act
            var entry = await this.service.UpsertAsync(article, CancellationToken.None);

            // Assert
            Assert.AreEqual("Inactive", entry.Status);
            Assert.AreEqual((int)StatusCodeEnum.Inactive, entry.StatusCode);
            Assert.IsNotNull(entry.Published, "Published date must be preserved for Inactive articles");
        }

        [TestMethod]
        public async Task UpsertAsync_DeletedArticle_StatusIsDeletedAndPublishedForcedNull()
        {
            // Arrange — article was published before being deleted
            var article = BuildArticle(3, StatusCodeEnum.Deleted, publishedDate: DateTimeOffset.UtcNow);
            this.db.Articles.Add(article);
            await this.db.SaveChangesAsync();

            // Act
            var entry = await this.service.UpsertAsync(article, CancellationToken.None);

            // Assert
            Assert.AreEqual("Deleted", entry.Status);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, entry.StatusCode);
            Assert.IsNull(entry.Published, "Published must be forced to null for Deleted articles so they never surface in published queries");
        }

        [TestMethod]
        public async Task UpsertAsync_RedirectArticle_StatusIsRedirectAndPublishedForcedNull()
        {
            // Arrange
            var article = BuildArticle(4, StatusCodeEnum.Redirect, publishedDate: DateTimeOffset.UtcNow);
            this.db.Articles.Add(article);
            await this.db.SaveChangesAsync();

            // Act
            var entry = await this.service.UpsertAsync(article, CancellationToken.None);

            // Assert
            Assert.AreEqual("Redirect", entry.Status);
            Assert.AreEqual((int)StatusCodeEnum.Redirect, entry.StatusCode);
            Assert.IsNull(entry.Published, "Published must be forced to null for Redirect articles");
        }

        // ----------------------------------------------------------------
        // Upsert-replaces-existing semantics
        // ----------------------------------------------------------------

        [TestMethod]
        public async Task UpsertAsync_ExistingEntry_IsReplacedNotDuplicated()
        {
            // Arrange — seed an existing catalog entry
            var existing = new CatalogEntry
            {
                ArticleNumber = 5,
                Title = "Old Title",
                UrlPath = "old-title",
                Status = "Active",
                StatusCode = (int)StatusCodeEnum.Active,
                Updated = DateTimeOffset.UtcNow.AddDays(-10)
            };
            this.db.ArticleCatalog.Add(existing);
            await this.db.SaveChangesAsync();

            var updatedArticle = BuildArticle(5, StatusCodeEnum.Active, publishedDate: null);
            updatedArticle.Title = "New Title";
            updatedArticle.UrlPath = "new-title";
            this.db.Articles.Add(updatedArticle);
            await this.db.SaveChangesAsync();

            // Act
            var entry = await this.service.UpsertAsync(updatedArticle, CancellationToken.None);

            // Assert
            var allEntries = await this.db.ArticleCatalog
                .Where(c => c.ArticleNumber == 5)
                .ToListAsync();

            Assert.AreEqual(1, allEntries.Count, "There must be exactly one catalog row per article number after upsert");
            Assert.AreEqual("New Title", allEntries[0].Title, "Title must reflect the updated article");
            Assert.AreEqual("new-title", allEntries[0].UrlPath, "UrlPath must reflect the updated article");
        }

        // ----------------------------------------------------------------
        // Introduction derivation tests
        // ----------------------------------------------------------------

        [TestMethod]
        public async Task UpsertAsync_IntroductionBlank_DerivedFromContent()
        {
            // Arrange
            var article = BuildArticle(6, StatusCodeEnum.Active, publishedDate: null);
            article.Introduction = string.Empty;
            this.db.Articles.Add(article);
            await this.db.SaveChangesAsync();

            const string extractedIntro = "This is the extracted introduction.";
            this.mockHtml
                .Setup(h => h.ExtractIntroduction(It.IsAny<string>()))
                .Returns(extractedIntro);

            // Act
            var entry = await this.service.UpsertAsync(article, CancellationToken.None);

            // Assert
            Assert.AreEqual(extractedIntro, entry.Introduction, "Introduction must be derived from content when the article has none");
            this.mockHtml.Verify(h => h.ExtractIntroduction(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task UpsertAsync_IntroductionAlreadySet_NotOverwrittenByExtraction()
        {
            // Arrange
            var article = BuildArticle(7, StatusCodeEnum.Active, publishedDate: null);
            article.Introduction = "Manually written introduction.";
            this.db.Articles.Add(article);
            await this.db.SaveChangesAsync();

            this.mockHtml
                .Setup(h => h.ExtractIntroduction(It.IsAny<string>()))
                .Returns("Should NOT be used.");

            // Act
            var entry = await this.service.UpsertAsync(article, CancellationToken.None);

            // Assert
            Assert.AreEqual("Manually written introduction.", entry.Introduction,
                "Existing introduction must not be overwritten by HTML extraction");
        }

        // ----------------------------------------------------------------
        // Helper
        // ----------------------------------------------------------------

        private static Article BuildArticle(
            int articleNumber,
            StatusCodeEnum status,
            DateTimeOffset? publishedDate)
        {
            return new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumber,
                Title = $"Article {articleNumber}",
                UrlPath = $"article-{articleNumber}",
                VersionNumber = 1,
                StatusCode = (int)status,
                Published = publishedDate,
                Updated = DateTimeOffset.UtcNow,
                UserId = "test-user",
                Content = "<p>Some content for article.</p>"
            };
        }

        // Needed for LINQ .Where() in assertion
        private Microsoft.EntityFrameworkCore.DbSet<CatalogEntry> CatalogSet => this.db.ArticleCatalog;

        private async Task<System.Collections.Generic.List<CatalogEntry>> GetAllCatalogEntries(int articleNumber)
            => await this.db.ArticleCatalog
                .Where(c => c.ArticleNumber == articleNumber)
                .ToListAsync();
    }
}
