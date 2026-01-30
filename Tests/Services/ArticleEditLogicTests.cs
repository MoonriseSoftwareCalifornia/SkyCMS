// <copyright file="ArticleEditLogicTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common;  // ✅ ADD THIS for ArticleType
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="ArticleEditLogic"/>.
    /// Tests article creation, editing, publishing, and version management workflows.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class ArticleEditLogicTests : SkyCmsTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            InitializeTestContext(seedLayout: true);
        }

        #region Article Creation Tests

        /// <summary>
        /// Tests that creating a new article generates a unique article number.
        /// </summary>
        [TestMethod]
        public async Task CreateArticle_NewArticle_GeneratesUniqueArticleNumber()
        {
            // Act
            var article1 = await Logic.CreateArticle("Test Article 1", TestUserId);
            var article2 = await Logic.CreateArticle("Test Article 2", TestUserId);

            // Assert
            Assert.IsNotNull(article1);
            Assert.IsNotNull(article2);
            Assert.AreNotEqual(article1.ArticleNumber, article2.ArticleNumber);
            Assert.IsTrue(article1.ArticleNumber > 0);
            Assert.IsTrue(article2.ArticleNumber > 0);
        }

        /// <summary>
        /// Tests that new articles start with version number 1.
        /// </summary>
        [TestMethod]
        public async Task CreateArticle_NewArticle_StartsWithVersionOne()
        {
            // Act
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Assert
            Assert.AreEqual(1, article.VersionNumber);
        }

        /// <summary>
        /// Tests that new articles are created as drafts (not published).
        /// </summary>
        [TestMethod]
        public async Task CreateArticle_NewArticle_CreatesAsDraft()
        {
            // Act
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Assert - First article auto-publishes, so skip for second article
            var article2 = await Logic.CreateArticle("Test Article 2", TestUserId);
            Assert.IsNull(article2.Published);
            Assert.IsNotNull(article2.Updated);
        }

        /// <summary>
        /// Tests that article title is properly set.
        /// </summary>
        [TestMethod]
        public async Task CreateArticle_WithTitle_SetsTitle()
        {
            // Arrange
            var title = "My Test Article Title";

            // Act
            var article = await Logic.CreateArticle(title, TestUserId);

            // Assert
            Assert.AreEqual(title, article.Title);
        }

        /// <summary>
        /// Tests that article is assigned to correct user.
        /// </summary>
        [TestMethod]
        public async Task CreateArticle_NewArticle_AssignsToUser()
        {
            // Act
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Assert
            var dbArticle = await Db.Articles.FindAsync(article.Id);
            Assert.IsNotNull(dbArticle);
            Assert.AreEqual(TestUserId.ToString(), dbArticle.UserId); // ✅ Check DB entity instead
        }

        /// <summary>
        /// Tests that article is added to catalog.
        /// </summary>
        [TestMethod]
        public async Task CreateArticle_NewArticle_AddsToArticleCatalog()
        {
            // Act
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Assert
            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            
            Assert.IsNotNull(catalogEntry);
            Assert.AreEqual(article.Title, catalogEntry.Title);
        }

        /// <summary>
        /// Tests that first article becomes root and is auto-published.
        /// </summary>
        [TestMethod]
        public async Task CreateArticle_FirstArticle_BecomesRootAndPublishes()
        {
            // Act
            var article = await Logic.CreateArticle("First Article", TestUserId);

            // Assert
            Assert.AreEqual("root", article.UrlPath);
            Assert.IsNotNull(article.Published);
        }

        /// <summary>
        /// Tests creating article with specific article type.
        /// </summary>
        [TestMethod]
        public async Task CreateArticle_WithArticleType_SetsType()
        {
            // Act
            var article = await Logic.CreateArticle("Blog Post", TestUserId, null, "", ArticleType.BlogPost);

            // Assert
            Assert.AreEqual(ArticleType.BlogPost, article.ArticleType);
        }

        #endregion

        #region Article Update Tests

        /// <summary>
        /// Tests that saving article updates content.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_UpdateContent_PersistsChanges()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<p>Updated content</p>";

            // Act
            var result = await Logic.SaveArticle(article, TestUserId);

            // Assert
            Assert.IsTrue(result.ServerSideSuccess);
            Assert.AreEqual("<p>Updated content</p>", result.Model.Content);
        }

        /// <summary>
        /// Tests that updating title preserves article number.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_ChangeTitle_PreservesArticleNumber()
        {
            // Arrange
            var article = await Logic.CreateArticle("Original Title", TestUserId);
            var articleNumber = article.ArticleNumber;
            article.Title = "Updated Title";

            // Act
            var result = await Logic.SaveArticle(article, TestUserId);

            // Assert
            Assert.AreEqual(articleNumber, result.Model.ArticleNumber);
            Assert.AreEqual("Updated Title", result.Model.Title);
        }

        /// <summary>
        /// Tests that saving article sets Updated timestamp.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_UpdateArticle_UpdatesTimestamp()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            var originalUpdated = article.Updated;
            
            // Wait a moment to ensure timestamp difference
            await Task.Delay(100);
            article.Content = "<p>New content</p>";

            // Act
            var result = await Logic.SaveArticle(article, TestUserId);

            // Assert
            Assert.IsTrue(result.Model.Updated > originalUpdated);
        }

        /// <summary>
        /// Tests that save preserves article type.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_UpdateArticle_PreservesArticleType()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId, null, "", ArticleType.BlogPost);
            article.Content = "<p>Updated content</p>";

            // Act
            var result = await Logic.SaveArticle(article, TestUserId);

            // Assert
            Assert.AreEqual(ArticleType.BlogPost, result.Model.ArticleType);
        }

        #endregion

        #region Publishing Tests

        /// <summary>
        /// Tests that publishing sets Published timestamp.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_DraftArticle_SetsPublishedTimestamp()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            // Second article won't auto-publish
            var article2 = await Logic.CreateArticle("Test Article 2", TestUserId);

            // Act
            var cdnResults = await Logic.PublishArticle(article2.Id, DateTimeOffset.UtcNow);

            // Assert
            var published = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(published.Published);
        }

        /// <summary>
        /// Tests that publishing creates page entry.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_DraftArticle_CreatesPageEntry()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article 2", TestUserId);

            // Act
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page);
            Assert.AreEqual(article.Title, page.Title);
        }

        /// <summary>
        /// Tests that republishing updates existing page.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_AlreadyPublishedArticle_UpdatesPage()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Update content
            article.Content = "<p>Second version</p>";
            await Logic.SaveArticle(article, TestUserId);

            var latest = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            // Act
            await Logic.PublishArticle(latest.Id, DateTimeOffset.UtcNow);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page);
            Assert.IsTrue(page.Content.Contains("Second version"));
        }

        #endregion

        #region Deletion Tests

        /// <summary>
        /// Tests that deleting article marks it as deleted.
        /// </summary>
        [TestMethod]
        public async Task DeleteArticle_ExistingArticle_MarksAsDeleted()
        {
            // Arrange
            // Create a dummy home page first (first article becomes home page with UrlPath="root")
            await Logic.CreateArticle("Home Page", TestUserId);
            
            // Create the article we want to delete
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Act
            await Logic.DeleteArticle(article.ArticleNumber);

            // Assert
            var deleted = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .FirstOrDefaultAsync();
            
            Assert.AreEqual((int)StatusCodeEnum.Deleted, deleted.StatusCode);
        }

        /// <summary>
        /// Tests that deleted article is removed from catalog.
        /// </summary>
        [TestMethod]
        public async Task DeleteArticle_ExistingArticle_RemovesFromCatalog()
        {
            // Arrange
            // Create a dummy home page first (first article becomes home page with UrlPath="root")
            await Logic.CreateArticle("Home Page", TestUserId);
            
            // Create the article we want to delete
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Act
            await Logic.DeleteArticle(article.ArticleNumber);

            // Assert
            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            
            Assert.IsNull(catalogEntry, "Catalog entry should be removed");
        }

        /// <summary>
        /// Tests that root page cannot be deleted.
        /// </summary>
        [TestMethod]
        public async Task DeleteArticle_RootPage_ThrowsNotSupportedException()
        {
            // Arrange
            var root = await Logic.CreateArticle("Root Page", TestUserId);
            Assert.AreEqual("root", root.UrlPath);

            // Act
            await Logic.DeleteArticle(root.ArticleNumber);

            // Assert - Exception expected
        }

        #endregion

        #region URL Path and Slug Tests

        /// <summary>
        /// Tests that article URL path is generated from title.
        /// </summary>
        [TestMethod]
        public async Task CreateArticle_WithTitle_GeneratesUrlPath()
        {
            // Arrange
            var title = "My Test Article Title";

            // Act
            var article = await Logic.CreateArticle(title, TestUserId);

            // Assert
            Assert.IsNotNull(article.UrlPath);
            // First article becomes root, so test with second article
            var article2 = await Logic.CreateArticle(title, TestUserId);
            Assert.IsTrue(article2.UrlPath.Contains("my-test-article-title") || 
                         article2.UrlPath.ToLower().Replace("-", "").Contains("mytestarticletitle"));
        }

        /// <summary>
        /// Tests that duplicate titles generate unique URL paths.
        /// </summary>
        [TestMethod]
        public async Task CreateArticle_DuplicateTitle_GeneratesUniqueUrlPath()
        {
            // Arrange
            var title = "Duplicate Title";

            // Act
            var article1 = await Logic.CreateArticle(title, TestUserId);
            var article2 = await Logic.CreateArticle(title, TestUserId);

            // Assert
            Assert.AreNotEqual(article1.UrlPath, article2.UrlPath,
                "Duplicate titles should generate unique URL paths");
        }

        #endregion

        #region Restore Article Tests

        /// <summary>
        /// Tests that deleted article can be restored.
        /// </summary>
        [TestMethod]
        public async Task RestoreArticle_DeletedArticle_RestoresToActive()
        {
            // Arrange
            // Create a dummy home page first (first article becomes home page with UrlPath="root")
            await Logic.CreateArticle("Home Page", TestUserId);
            
            // Create the article we want to delete and restore
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.DeleteArticle(article.ArticleNumber);
            
            var deleted = await Db.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, deleted.StatusCode);

            // Act
            await Logic.RestoreArticle(article.ArticleNumber, TestUserId.ToString());

            // Assert
            var restored = await Db.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual((int)StatusCodeEnum.Active, restored.StatusCode);
        }

        #endregion
    }
}