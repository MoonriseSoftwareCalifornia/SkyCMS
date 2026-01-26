// <copyright file="SaveArticleCatalogTests.cs" company="Moonrise Software, LLC">
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
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for catalog (index) update integration during save operations.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleCatalogTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        public async Task SaveArticle_UpdatesCatalogTitle()
        {
            // Arrange
            var article = await Logic.CreateArticle("Original", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated Catalog Title",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.AreEqual("Updated Catalog Title", catalogEntry.Title);
        }

        [TestMethod]
        public async Task SaveArticle_UpdatesCatalogIntroduction()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Test",
                Content = "<p>First paragraph for intro.</p><p>Second paragraph.</p>",
                Introduction = "Custom introduction text",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.AreEqual("Custom introduction text", catalogEntry.Introduction);
        }

        [TestMethod]
        public async Task SaveArticle_CatalogReflectsLatestVersion()
        {
            // Arrange
            var article = await Logic.CreateArticle("Version Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Create multiple versions
            for (int i = 1; i <= 3; i++)
            {
                var command = new SaveArticleCommand
                {
                    ArticleNumber = article.ArticleNumber,
                    Title = $"Version {i}",
                    Content = $"<p>Content {i}</p>",
                    UserId = TestUserId,
                    ArticleType = ArticleType.General
                };

                await SaveArticleHandler.HandleAsync(command);
            }

            // Assert
            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.AreEqual("Version 3", catalogEntry.Title);
        }

        [TestMethod]
        public async Task SaveArticle_UpdatesCatalogUrlPath()
        {
            // Arrange - Create a dummy root article first to avoid the test article becoming root
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Now create the article we want to test (won't be root)
            var article = await Logic.CreateArticle("Original Path", TestUserId);
            await Logic.SaveArticle(article, TestUserId);
            var originalPath = article.UrlPath;

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "New Path Title",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.AreNotEqual(originalPath, catalogEntry.UrlPath);
            Assert.AreEqual("new-path-title", catalogEntry.UrlPath);
        }

        [TestMethod]
        public async Task SaveArticle_UpdatesCatalogBannerImage()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Test",
                Content = "<p>Content</p>",
                BannerImage = "https://example.com/new-banner.jpg",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.AreEqual("https://example.com/new-banner.jpg", catalogEntry.BannerImage);
        }

        [TestMethod]
        public async Task SaveArticle_CatalogUpdatedTimestamp_ReflectsLatestSave()
        {
            // Arrange
            var article = await Logic.CreateArticle("Timestamp Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var initialCatalog = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            var initialUpdated = initialCatalog!.Updated;

            // Wait a moment to ensure timestamp difference
            await Task.Delay(100);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated Timestamp",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var updatedCatalog = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(updatedCatalog);
            Assert.IsTrue(updatedCatalog.Updated > initialUpdated);
        }

        [TestMethod]
        public async Task SaveArticle_CatalogEntry_ExistsForAllArticleTypes()
        {
            // Test that catalog entries are created for all article types
            var articleTypes = new[] 
            { 
                ArticleType.General, 
                ArticleType.BlogPost,
                ArticleType.BlogStream 
            };

            foreach (var articleType in articleTypes)
            {
                // Arrange
                var article = await Logic.CreateArticle($"Article {articleType}", TestUserId);
                
                var command = new SaveArticleCommand
                {
                    ArticleNumber = article.ArticleNumber,
                    Title = $"Article {articleType}",
                    Content = "<p>Content</p>",
                    UserId = TestUserId,
                    ArticleType = articleType
                };

                // Act
                var result = await SaveArticleHandler.HandleAsync(command);

                // Assert
                Assert.IsTrue(result.IsSuccess, $"Save failed for {articleType}");

                var catalogEntry = await Db.ArticleCatalog
                    .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
                Assert.IsNotNull(catalogEntry, $"Catalog entry missing for {articleType}");
            }
        }
    }
}
