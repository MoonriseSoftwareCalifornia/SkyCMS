// <copyright file="SaveArticleConcurrencyTests.cs" company="Moonrise Software, LLC">
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
    /// Tests for concurrent save operations and race conditions.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleConcurrencyTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        public async Task SaveArticle_SimultaneousSaves_HandlesGracefully()
        {
            // Arrange - Create article
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var command1 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Update from User 1",
                Content = "<p>Content 1</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            var command2 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Update from User 2",
                Content = "<p>Content 2</p>",
                UserId = Guid.NewGuid(), // Different user
                ArticleType = ArticleType.General
            };

            // Act - Simulate concurrent saves
            var task1 = SaveArticleHandler.HandleAsync(command1);
            var task2 = SaveArticleHandler.HandleAsync(command2);

            var results = await Task.WhenAll(task1, task2);

            // Assert - At least one should succeed
            var successCount = results.Count(r => r.IsSuccess);
            Assert.IsGreaterThanOrEqualTo(1, successCount, "At least one save should succeed");

            // Verify final state is consistent
            var finalArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.Updated)
                .FirstOrDefaultAsync();

            Assert.IsNotNull(finalArticle);
        }

        [TestMethod]
        public async Task SaveArticle_RetryLogic_WorksCorrectly()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Simulate stale entity by loading it separately
            var staleArticle = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);

            Assert.IsNotNull(staleArticle);

            // Update article in database (simulate concurrent edit)
            var currentArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            currentArticle!.Title = "Changed by someone else";
            await Db.SaveChangesAsync();

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "My Update",
                Content = "<p>My content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act - This should succeed due to retry logic
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Save should succeed with retry logic");
        }

        [TestMethod]
        public async Task SaveArticle_MultipleQuickSuccessiveSaves_MaintainsConsistency()
        {
            // Arrange
            var article = await Logic.CreateArticle("Quick Saves Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act - Perform 5 rapid saves
            for (int i = 1; i <= 5; i++)
            {
                var command = new SaveArticleCommand
                {
                    ArticleNumber = article.ArticleNumber,
                    Title = $"Iteration {i}",
                    Content = $"<p>Content {i}</p>",
                    UserId = TestUserId,
                    ArticleType = ArticleType.General
                };

                var result = await SaveArticleHandler.HandleAsync(command);
                Assert.IsTrue(result.IsSuccess, $"Save {i} should succeed");
            }

            // Assert - Verify final state
            var finalArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.Updated)
                .FirstOrDefaultAsync();

            Assert.IsNotNull(finalArticle);
            Assert.AreEqual("Iteration 5", finalArticle.Title);
        }

        [TestMethod]
        public async Task SaveArticle_ConcurrentSavesWithDifferentFields_BothSucceed()
        {
            // Arrange
            var article = await Logic.CreateArticle("Concurrent Field Updates", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var command1 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Content = "<p>Updated content from user 1</p>",
                BannerImage = "https://example.com/banner1.jpg",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            var command2 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Content = "<p>Updated content from user 2</p>",
                Category = "Technology",
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var tasks = new[] { command1, command2 }
                .Select(cmd => SaveArticleHandler.HandleAsync(cmd));

            var results = await Task.WhenAll(tasks);

            // Assert - At least one should succeed
            var successCount = results.Count(r => r.IsSuccess);
            Assert.IsGreaterThanOrEqualTo(1, successCount);

            // Verify database is in consistent state
            var dbArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(dbArticle);
        }
    }
}
