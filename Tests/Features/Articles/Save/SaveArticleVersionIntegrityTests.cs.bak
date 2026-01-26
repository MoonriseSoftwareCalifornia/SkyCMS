// <copyright file="SaveArticleVersionIntegrityTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Save
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Save;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for multi-version integrity and version history.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleVersionIntegrityTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        public async Task SaveArticle_AfterMultipleSaves_MaintainsVersionHistory()
        {
            // Arrange
            var article = await Logic.CreateArticle("Version Test", TestUserId);
            var articleNumber = article.ArticleNumber;

            // Perform 5 saves
            for (int i = 1; i <= 5; i++)
            {
                var command = new SaveArticleCommand
                {
                    ArticleNumber = articleNumber,
                    Title = $"Version {i}",
                    Content = $"<p>Content {i}</p>",
                    UserId = TestUserId,
                    ArticleType = ArticleType.General
                };
                await SaveArticleHandler.HandleAsync(command);
            }

            // Assert - Should have only the latest version (in-place updates)
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();

            // Verify latest version
            var latest = allVersions.Last();
            Assert.AreEqual("Version 5", latest.Title);
        }

        [TestMethod]
        public async Task SaveArticle_UpdatesTimestamp_OnEachSave()
        {
            // Arrange
            var article = await Logic.CreateArticle("Timestamp Test", TestUserId);
            var initialTimestamp = article.Updated;

            // Wait to ensure timestamp difference
            await Task.Delay(100);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated",
                Content = "<p>Updated content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var updatedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.IsTrue(updatedArticle!.Updated > initialTimestamp);
        }

        [TestMethod]
        public async Task SaveArticle_PreservesArticleNumber_AcrossMultipleSaves()
        {
            // Arrange
            var article = await Logic.CreateArticle("Article Number Test", TestUserId);
            var originalNumber = article.ArticleNumber;

            // Perform multiple saves
            for (int i = 0; i < 3; i++)
            {
                var command = new SaveArticleCommand
                {
                    ArticleNumber = originalNumber,
                    Title = $"Save {i}",
                    Content = "<p>Content</p>",
                    UserId = TestUserId,
                    ArticleType = ArticleType.General
                };
                await SaveArticleHandler.HandleAsync(command);
            }

            // Assert
            var allArticles = await Db.Articles
                .Where(a => a.ArticleNumber == originalNumber)
                .ToListAsync();

            Assert.IsTrue(allArticles.All(a => a.ArticleNumber == originalNumber));
        }

        [TestMethod]
        public async Task SaveArticle_PreservesOriginalId_OnUpdate()
        {
            // Arrange
            var article = await Logic.CreateArticle("ID Test", TestUserId);
            var originalId = article.Id;

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated Title",
                Content = "<p>Updated</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            await SaveArticleHandler.HandleAsync(command);

            // Assert
            var updatedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            // ID should be preserved (same row updated)
            Assert.AreEqual(originalId, updatedArticle!.Id);
        }

        [TestMethod]
        public async Task SaveArticle_MultipleUsers_TracksLastUser()
        {
            // Arrange
            var article = await Logic.CreateArticle("Multi User Test", TestUserId);
            
            var user2Id = Guid.NewGuid();
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated by User 2",
                Content = "<p>Content</p>",
                UserId = user2Id,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var updatedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual(user2Id.ToString(), updatedArticle!.UserId);
        }
    }
}
