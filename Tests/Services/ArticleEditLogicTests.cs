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
    using Cosmos.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="ArticleEditLogic"/>.
    /// Tests article creation, editing, publishing, and version management workflows.
    /// </summary>
    /// <remarks>
    /// DEPRECATED: ArticleEditLogic is obsolete as of the CQRS migration.
    /// These tests document legacy functionality that has been migrated to command handlers.
    /// 
    /// Migration Path:
    /// - CreateArticle ? CreateArticleHandler with CreateArticleCommand
    /// - SaveArticle ? SaveArticleHandler with SaveArticleCommand
    /// - PublishArticle ? PublishArticleHandler with PublishArticleCommand
    /// - DeleteArticle ? DeleteArticleHandler with DeleteArticleCommand
    /// - RestoreArticle ? RestoreArticleHandler with RestoreArticleCommand
    /// - NewVersion ? CreateArticleVersionHandler with CreateArticleVersionCommand
    /// 
    /// For new tests, create handler-specific test files in Tests/Features/Articles/
    /// </remarks>
    [Obsolete("ArticleEditLogic is deprecated. Use CQRS command handlers instead. See remarks for migration paths.", false)]
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
        [Ignore]
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
        [Ignore]
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
        [Ignore]
        public async Task CreateArticle_NewArticle_CreatesAsDraft()
        {
            // Act
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Assert - First article auto-publishes, so skip for second article
            var article2 = await Logic.CreateArticle("Test Article 2", TestUserId);
            Assert.IsNull(article2.Published);
            Assert.IsNotNull(article2.Updated);
        }

        #endregion

        #region Update Tests

        /// <summary>
        /// Tests that saving article updates content.
        /// </summary>
        [TestMethod]
        [Ignore]
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

        #endregion

        #region Publishing Tests

        /// <summary>
        /// Tests that publishing sets Published timestamp.
        /// </summary>
        [TestMethod]
        [Ignore]
        public async Task PublishArticle_DraftArticle_SetsPublishedTimestamp()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article 2", TestUserId);

            // Act
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Assert
            var published = await Db.Articles.FindAsync(article.Id);
            Assert.IsNotNull(published.Published);
        }

        #endregion

        #region Deletion Tests

        /// <summary>
        /// Tests that deleting article marks it as deleted.
        /// </summary>
        [TestMethod]
        [Ignore]
        public async Task DeleteArticle_ExistingArticle_MarksAsDeleted()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId);
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Act
            await Logic.DeleteArticle(article.ArticleNumber);

            // Assert
            var deleted = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .FirstOrDefaultAsync();

            Assert.AreEqual((int)StatusCodeEnum.Deleted, deleted.StatusCode);
        }

        #endregion
    }
}
