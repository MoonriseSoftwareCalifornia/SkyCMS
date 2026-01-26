// <copyright file="SaveArticleBlogEdgeCaseTests.cs" company="Moonrise Software, LLC">
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
    /// Tests for blog-specific edge cases during save operations.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleBlogEdgeCaseTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        public async Task SaveArticle_BlogPost_WithCategory_SavesCorrectly()
        {
            // Arrange
            var article = await Logic.CreateArticle("Blog Post", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "My First Blog Post",
                Content = "<p>Blog content</p>",
                ArticleType = ArticleType.BlogPost,
                Category = "Technology",
                Introduction = "A tech blog post",
                UserId = TestUserId
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNotNull(savedArticle);
            Assert.AreEqual("Technology", savedArticle.Category);
            Assert.AreEqual((int)ArticleType.BlogPost, savedArticle.ArticleType);
        }

        [TestMethod]
        public async Task SaveArticle_BlogPost_ChangingCategory_UpdatesCatalog()
        {
            // Arrange
            var article = await Logic.CreateArticle("Blog", TestUserId);
            var command1 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Blog Post",
                Content = "<p>Content</p>",
                ArticleType = ArticleType.BlogPost,
                Category = "Technology",
                UserId = TestUserId
            };
            await SaveArticleHandler.HandleAsync(command1);

            var command2 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Blog Post",
                Content = "<p>Content</p>",
                ArticleType = ArticleType.BlogPost,
                Category = "Science", // Changed category
                UserId = TestUserId
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command2);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.AreEqual("Science", savedArticle!.Category);
        }

        [TestMethod]
        public async Task SaveArticle_BlogPost_WithIntroduction_PreservesIntro()
        {
            // Arrange
            var article = await Logic.CreateArticle("Blog", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Blog with Intro",
                Content = "<p>Full article content goes here</p>",
                ArticleType = ArticleType.BlogPost,
                Introduction = "This is the intro text",
                UserId = TestUserId
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.AreEqual("This is the intro text", savedArticle!.Introduction);
        }

        [TestMethod]
        public async Task SaveArticle_BlogPost_NoCategoryProvided_SavesEmpty()
        {
            // Arrange
            var article = await Logic.CreateArticle("Blog", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Uncategorized Blog",
                Content = "<p>Content</p>",
                ArticleType = ArticleType.BlogPost,
                Category = null, // No category
                UserId = TestUserId
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsTrue(string.IsNullOrEmpty(savedArticle!.Category));
        }

        [TestMethod]
        public async Task SaveArticle_ConvertingToBlogPost_UpdatesArticleType()
        {
            // Arrange
            var article = await Logic.CreateArticle("Regular Article", TestUserId);
            var command1 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Regular Article",
                Content = "<p>Content</p>",
                ArticleType = ArticleType.General,
                UserId = TestUserId
            };
            await SaveArticleHandler.HandleAsync(command1);

            var command2 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Now a Blog Post",
                Content = "<p>Content</p>",
                ArticleType = ArticleType.BlogPost, // Convert to blog
                Category = "Technology",
                UserId = TestUserId
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command2);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.AreEqual((int)ArticleType.BlogPost, savedArticle!.ArticleType);
        }

        [TestMethod]
        public async Task SaveArticle_BlogPost_WithBannerImage_Saves()
        {
            // Arrange
            var article = await Logic.CreateArticle("Blog", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Blog with Banner",
                Content = "<p>Content</p>",
                ArticleType = ArticleType.BlogPost,
                BannerImage = "https://example.com/banner.jpg",
                UserId = TestUserId
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.AreEqual("https://example.com/banner.jpg", savedArticle!.BannerImage);
        }

        [TestMethod]
        public async Task SaveArticle_BlogPost_PublishedWithCategory_IndexesCorrectly()
        {
            // Arrange
            var article = await Logic.CreateArticle("Published Blog", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Published Blog Post",
                Content = "<p>Content</p>",
                ArticleType = ArticleType.BlogPost,
                Category = "News",
                Published = Clock.UtcNow,
                UserId = TestUserId
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.IsNotNull(catalogEntry.Published);
        }
    }
}
