// <copyright file="SaveArticleSlugNormalizationTests.cs" company="Moonrise Software, LLC">
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
    /// Tests for slug normalization edge cases.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleSlugNormalizationTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        public async Task SaveArticle_TitleWithEmojis_CreatesValidSlug()
        {
            // Arrange
            await Logic.CreateArticle("Root Page", TestUserId); // Prevent root page issue
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "My Article ?? With Emojis ??",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            // Emojis should be stripped/converted to hyphens
            Assert.DoesNotContain("??", savedArticle!.UrlPath);
            Assert.DoesNotContain("??", savedArticle.UrlPath);
            Assert.AreEqual("my-article-with-emojis", savedArticle.UrlPath);
        }

        [TestMethod]
        public async Task SaveArticle_TitleWithDiacritics_NormalizesCorrectly()
        {
            // Arrange
            await Logic.CreateArticle("Root Page", TestUserId);
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Caf\u00e9 R\u00e9sum\u00e9 Na\u00efve",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            Assert.AreEqual("cafe-resume-naive", savedArticle!.UrlPath);
        }

        [TestMethod]
        public async Task SaveArticle_TitleAllSpecialChars_GeneratesValidSlug()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "@#$%^&*()",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            // Should have some valid slug (implementation dependent)
            Assert.IsFalse(string.IsNullOrWhiteSpace(savedArticle!.UrlPath));
        }

        [TestMethod]
        public async Task SaveArticle_TitleStartsWithNumber_HandlesCorrectly()
        {
            // Arrange - Create a dummy root article first to avoid the test article becoming root
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Now create the article we want to test (won't be root)
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "2024 Annual Report",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            Assert.Contains("2024", savedArticle!.UrlPath);
            Assert.AreEqual("2024-annual-report", savedArticle.UrlPath);
        }

        [TestMethod]
        public async Task SaveArticle_TitleWithMultipleSpaces_NormalizesToSingleHyphen()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Multiple    Spaces    Between    Words",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            // Should not have multiple consecutive hyphens
            Assert.DoesNotContain("--", savedArticle!.UrlPath);
        }

        [TestMethod]
        public async Task SaveArticle_TitleWithPunctuation_RemovesPunctuation()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "What's New? Here's the Answer!",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            // Punctuation should be removed or normalized
            Assert.DoesNotContain("?", savedArticle!.UrlPath);
            Assert.DoesNotContain("!", savedArticle.UrlPath);
        }

        [TestMethod]
        public async Task SaveArticle_TitleWithSlashes_NormalizesCorrectly()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Parent/Child/Article",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            // Slashes should be handled appropriately
            Assert.IsNotNull(savedArticle!.UrlPath);
        }

        [TestMethod]
        public async Task SaveArticle_TitleWithAmpersand_NormalizesCorrectly()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Research & Development",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            // Ampersand should be normalized
            Assert.DoesNotContain("&", savedArticle!.UrlPath);
        }

        [TestMethod]
        [DataRow("Hello & Goodbye", "hello-goodbye")]
        [DataRow("Cost: $99.99!", "cost-99-99")]
        [DataRow("C# Programming", "c-programming")]
        [DataRow("2024 Q4 Report", "2024-q4-report")]
        [DataRow("Email: user@domain.com", "email-user-domain-com")]
        [DataRow("50% Off", "50-off")]
        [DataRow("Parent/Child", "parent/child")] // Slash preserved
        [DataRow("caf\u00e9 r\u00e9sum\u00e9", "cafe-resume")]  // ✅ Use Unicode escape sequences
        [DataRow("Hello   World", "hello-world")] // Multiple spaces
        [DataRow("  Trimmed  ", "trimmed")] // Leading/trailing
        public async Task SaveArticle_VariousSpecialCharacters_NormalizesCorrectly(string title, string expectedSlug)
        {
            // Arrange
            await Logic.CreateArticle("Root Page", TestUserId);
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = title,
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            Assert.AreEqual(expectedSlug, savedArticle!.UrlPath, 
                $"Title '{title}' should normalize to '{expectedSlug}'");
        }
    }
}
