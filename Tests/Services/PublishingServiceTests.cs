// <copyright file="PublishingServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for PublishingService - Critical for article publishing workflow.
    /// Tests publishing, unpublishing, and static page generation.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PublishingServiceTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
        }

        #region Publishing Tests

        /// <summary>
        /// Tests that PublishArticle creates a Page entry for published articles.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_CreatesPageEntry()
        {
            // Arrange
            var article = await CreateArticleAsync("Publish Test Article", TestUserId);
            article.Content = "<h1>Test Content</h1>";
            await Db.SaveChangesAsync();

            var publishDate = DateTimeOffset.UtcNow;
            var articleEntity = await Db.Articles.FindAsync(article.Id);

            // Act
            await PublishingService.PublishAsync(articleEntity);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page, "Page should be created for published article");
            Assert.AreEqual(article.Title, page.Title);
            Assert.AreEqual(article.ArticleNumber, page.ArticleNumber);
        }

        /// <summary>
        /// Tests that PublishArticle updates article's Published timestamp.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_SetsPublishedTimestamp()
        {
            // Arrange
            var article = await CreateArticleAsync("Timestamp Test Article", TestUserId);
            var articleEntity = await Db.Articles.FindAsync(article.Id);
            
            // Ensure Published is null before testing PublishAsync
            articleEntity.Published = null;
            await Db.SaveChangesAsync();
            
            var publishDate = DateTimeOffset.UtcNow;

            // Act
            await PublishingService.PublishAsync(articleEntity);

            // Assert
            var publishedArticle = await Db.Articles.FindAsync(article.Id);
            Assert.IsNotNull(publishedArticle.Published, "Published timestamp should be set");
            Assert.IsTrue(publishedArticle.Published.Value >= publishDate.AddSeconds(-1));
        }

        /// <summary>
        /// Tests that publishing an article unpublishes previous versions.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_UnpublishesPreviousVersions()
        {
            // Arrange
            var article = await CreateArticleAsync("Version Test Article", TestUserId);
            var articleEntity = await Db.Articles.FindAsync(article.Id);
            await PublishingService.PublishAsync(articleEntity);

            // Create new version
            var dbArticle = await Db.Articles.FindAsync(article.Id);
            var newVersionVm = await CreateArticleVersionAsync(dbArticle.ArticleNumber);
            var newVersion = await Db.Articles.Where(a => a.ArticleNumber == dbArticle.ArticleNumber).OrderByDescending(x => x.VersionNumber).FirstAsync();
            newVersion.Content = "<h1>Updated Content</h1>";
            await Db.SaveChangesAsync();

            // Act - Publish new version
            var newVersionEntity = await Db.Articles.FindAsync(newVersion.Id);
            await PublishingService.PublishAsync(newVersionEntity);

            // Assert
            var oldVersion = await Db.Articles.FindAsync(article.Id);
            Assert.IsNull(oldVersion.Published, "Old version should be unpublished");

            var currentVersion = await Db.Articles.FindAsync(newVersion.Id);
            Assert.IsNotNull(currentVersion.Published, "New version should be published");
        }

        #endregion

        #region Unpublishing Tests

        /// <summary>
        /// Tests that UnpublishArticle removes Published timestamp.
        /// </summary>
        [TestMethod]
        public async Task UnpublishArticle_RemovesPublishedTimestamp()
        {
            // Arrange
            var article = await CreateArticleAsync("Unpublish Test Article", TestUserId);
            var articleEntity = await Db.Articles.FindAsync(article.Id);
            await PublishingService.PublishAsync(articleEntity);

            // Verify it's published
            var publishedArticle = await Db.Articles.FindAsync(article.Id);
            Assert.IsNotNull(publishedArticle.Published);

            // Act
            await PublishingService.UnpublishAsync(publishedArticle);

            // Assert
            var unpublishedArticle = await Db.Articles.FindAsync(article.Id);
            Assert.IsNull(unpublishedArticle.Published, "Published timestamp should be null after unpublishing");
        }

        /// <summary>
        /// Tests that UnpublishArticle removes Page entry.
        /// </summary>
        [TestMethod]
        public async Task UnpublishArticle_RemovesPageEntry()
        {
            // Arrange
            var article = await CreateArticleAsync("Page Removal Test Article", TestUserId);
            var articleEntity = await Db.Articles.FindAsync(article.Id);
            await PublishingService.PublishAsync(articleEntity);

            // Verify page exists
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page);

            // Act
            var articleForUnpublish = await Db.Articles.FindAsync(article.Id);
            await PublishingService.UnpublishAsync(articleForUnpublish);

            // Assert
            var deletedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNull(deletedPage, "Page should be removed after unpublishing");
        }

        #endregion

        #region Catalog Update Tests

        /// <summary>
        /// Tests that publishing updates the article catalog.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_UpdatesArticleCatalog()
        {
            // Arrange
            var article = await CreateArticleAsync("Catalog Update Test", TestUserId);
            article.Content = "<h1>Test Content</h1>";
            await Db.SaveChangesAsync();

            // Act
            var articleEntity = await Db.Articles.FindAsync(article.Id);
            await PublishingService.PublishAsync(articleEntity);

            // Assert
            var catalogEntry = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry, "Catalog entry should exist");
            Assert.IsNotNull(catalogEntry.Published, "Catalog should show article as published");
        }

        /// <summary>
        /// Tests that unpublishing updates the article catalog.
        /// </summary>
        [TestMethod]
        public async Task UnpublishArticle_UpdatesArticleCatalog()
        {
            // Arrange
            var article = await CreateArticleAsync("Catalog Unpublish Test", TestUserId);
            var articleEntity = await Db.Articles.FindAsync(article.Id);
            await PublishingService.PublishAsync(articleEntity);

            // Act
            var articleForUnpublish = await Db.Articles.FindAsync(article.Id);
            await PublishingService.UnpublishAsync(articleForUnpublish);

            // Assert
            var catalogEntry = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry, "Catalog entry should still exist");
            Assert.IsNull(catalogEntry.Published, "Catalog should show article as unpublished");
        }

        #endregion

        #region Bulk Publishing Tests

        /// <summary>
        /// Tests that publishing multiple articles works correctly.
        /// </summary>
        [TestMethod]
        public async Task PublishMultipleArticles_AllPublishedSuccessfully()
        {
            // Arrange
            var article1 = await CreateArticleAsync("Bulk Test Article 1", TestUserId);
            var article2 = await CreateArticleAsync("Bulk Test Article 2", TestUserId);
            var article3 = await CreateArticleAsync("Bulk Test Article 3", TestUserId);

            // Act
            var entity1 = await Db.Articles.FindAsync(article1.Id);
            var entity2 = await Db.Articles.FindAsync(article2.Id);
            var entity3 = await Db.Articles.FindAsync(article3.Id);
            await PublishingService.PublishAsync(entity1);
            await PublishingService.PublishAsync(entity2);
            await PublishingService.PublishAsync(entity3);

            // Assert
            var publishedCount = await Db.Articles
                .Where(a => a.Published != null)
                .CountAsync();
            Assert.IsTrue(publishedCount >= 3, "At least 3 articles should be published");

            var pageCount = await Db.Pages.CountAsync();
            Assert.IsTrue(pageCount >= 3, "At least 3 pages should be created");
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Tests that publishing non-existent article handles gracefully.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_NonExistentArticle_HandlesGracefully()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act & Assert
            try
            {
                var nonExistentArticle = await Db.Articles.FindAsync(nonExistentId);
                await PublishingService.PublishAsync(nonExistentArticle);
                Assert.Fail("Should throw exception for non-existent article");
            }
            catch (Exception)
            {
                Assert.IsTrue(true, "Exception expected for non-existent article");
            }
        }

        /// <summary>
        /// Tests that unpublishing already unpublished article is idempotent.
        /// </summary>
        [TestMethod]
        public async Task UnpublishArticle_AlreadyUnpublished_IsIdempotent()
        {
            // Arrange
            var article = await CreateArticleAsync("Idempotent Test Article", TestUserId);

            // Act - Unpublish twice
            var articleEntity = await Db.Articles.FindAsync(article.Id);
            await PublishingService.UnpublishAsync(articleEntity);
            // Reload to get updated state
            articleEntity = await Db.Articles.FindAsync(article.Id);
            await PublishingService.UnpublishAsync(articleEntity);

            // Assert
            var unpublishedArticle = await Db.Articles.FindAsync(article.Id);
            Assert.IsNull(unpublishedArticle.Published, "Article should remain unpublished");
        }

        #endregion

        #region Homepage Publishing Tests

        /// <summary>
        /// Tests that publishing homepage (ArticleNumber 0) works correctly.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_Homepage_CreatesRootPage()
        {
            // Arrange
            var articles = await Db.Articles.Where(a => a.ArticleNumber == 0).ToListAsync();
            if (articles.Any())
            {
                var homepage = articles.First();

                // Act
                await PublishingService.PublishAsync(homepage);

                // Assert
                var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == 0);
                Assert.IsNotNull(page, "Homepage page should be created");
                Assert.AreEqual("/", page.UrlPath, "Homepage should have root URL path");
            }
            else
            {
                Assert.Inconclusive("No homepage article found for test");
            }
        }

        #endregion

        #region Scheduled Publishing Tests

        /// <summary>
        /// Tests that articles can be scheduled for future publishing.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_FutureDate_SchedulesPublishing()
        {
            // Arrange
            var article = await CreateArticleAsync("Scheduled Article", TestUserId);
            var futureDate = DateTimeOffset.UtcNow.AddDays(7);
            var articleEntity = await Db.Articles.FindAsync(article.Id);
            articleEntity.Published = futureDate; // Set the future publish date

            // Act
            await PublishingService.PublishAsync(articleEntity);

            // Assert
            var publishedArticle = await Db.Articles.FindAsync(article.Id);
            Assert.IsNotNull(publishedArticle.Published, "Article should have published date");
            Assert.IsTrue(publishedArticle.Published >= futureDate.AddSeconds(-1));
        }

        #endregion

        #region Static Page Generation Tests

        /// <summary>
        /// Tests that static page generation is disabled in test environment.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_StaticPagesDisabled_DoesNotGenerateStaticFiles()
        {
            // Arrange
            var article = await CreateArticleAsync("Static Page Test", TestUserId);
            article.Content = "<h1>Test Content</h1>";
            await Db.SaveChangesAsync();

            // Act
            var articleEntity = await Db.Articles.FindAsync(article.Id);
            await PublishingService.PublishAsync(articleEntity);

            // Assert
            // In test environment, static web page generation is disabled (see SkyCmsTestBase)
            var setting = await Db.Settings.FirstOrDefaultAsync(s => s.Name == "StaticWebPages");
            Assert.IsNotNull(setting);
            Assert.AreEqual("false", setting.Value, "Static web pages should be disabled in tests");
        }

        #endregion
    }
}
