// <copyright file="SaveArticleJavaScriptBlockTests.cs" company="Moonrise Software, LLC">
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
    /// Tests for JavaScript/Head/Footer block edge cases.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleJavaScriptBlockTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        public async Task SaveArticle_WithHeadJavaScript_Saves()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Article with Head JS",
                Content = "<p>Content</p>",
                HeadJavaScript = "<script>console.log('head script');</script>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.Contains("console.log", savedArticle!.HeaderJavaScript);
        }

        [TestMethod]
        public async Task SaveArticle_WithFooterJavaScript_Saves()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Article with Footer JS",
                Content = "<p>Content</p>",
                FooterJavaScript = "<script>console.log('footer script');</script>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.Contains("console.log", savedArticle!.FooterJavaScript);
        }

        [TestMethod]
        public async Task SaveArticle_WithBothHeadAndFooterJS_SavesBoth()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Article with Both Scripts",
                Content = "<p>Content</p>",
                HeadJavaScript = "<script>var headVar = 'test';</script>",
                FooterJavaScript = "<script>console.log(headVar);</script>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.Contains("headVar", savedArticle!.HeaderJavaScript);
            Assert.Contains("console.log", savedArticle.FooterJavaScript);
        }

        [TestMethod]
        public async Task SaveArticle_LargeJavaScriptBlock_Saves()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var largeScript = "<script>\n" +
                string.Concat(System.Linq.Enumerable.Repeat("console.log('test');\n", 1000)) +
                "</script>";

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Article with Large Script",
                Content = "<p>Content</p>",
                HeadJavaScript = largeScript,
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsGreaterThan(10000, savedArticle!.HeaderJavaScript.Length);
        }

        [TestMethod]
        public async Task SaveArticle_EmptyJavaScriptBlocks_SavesEmpty()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Article with No Scripts",
                Content = "<p>Content</p>",
                HeadJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.AreEqual(string.Empty, savedArticle!.HeaderJavaScript);
            Assert.AreEqual(string.Empty, savedArticle.FooterJavaScript);
        }

        [TestMethod]
        public async Task SaveArticle_NullJavaScriptBlocks_SavesAsEmpty()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Article with Null Scripts",
                Content = "<p>Content</p>",
                HeadJavaScript = null,
                FooterJavaScript = null,
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.AreEqual(string.Empty, savedArticle!.HeaderJavaScript);
            Assert.AreEqual(string.Empty, savedArticle.FooterJavaScript);
        }

        [TestMethod]
        public async Task SaveArticle_JavaScriptWithSpecialCharacters_Preserves()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            
            var scriptWithSpecialChars = "<script>var data = {\"key\": \"value's & test\"};</script>";

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Script with Special Chars",
                Content = "<p>Content</p>",
                HeadJavaScript = scriptWithSpecialChars,
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.Contains("value's & test", savedArticle!.HeaderJavaScript);
        }
    }
}
