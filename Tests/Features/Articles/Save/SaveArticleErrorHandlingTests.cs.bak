// <copyright file="SaveArticleErrorHandlingTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Save
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Save;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for error handling and validation edge cases.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleErrorHandlingTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        public async Task SaveArticle_NonExistentArticle_ReturnsNotFound()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 99999, // Does not exist
                Title = "Test",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [TestMethod]
        public async Task SaveArticle_WithInvalidUserId_ReturnsValidationError()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Test",
                Content = "<p>Content</p>",
                UserId = Guid.Empty, // Invalid
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.UserId)));
        }

        [TestMethod]
        public async Task SaveArticle_TitleExceeds254Chars_ReturnsValidationError()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var longTitle = new string('A', 255);
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = longTitle,
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.Title)));
        }

        [TestMethod]
        public async Task SaveArticle_IntroductionExceeds512Chars_ReturnsValidationError()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var longIntro = new string('A', 513);
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Test",
                Content = "<p>Content</p>",
                Introduction = longIntro,
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.Introduction)));
        }

        [TestMethod]
        public async Task SaveArticle_CategoryExceeds64Chars_ReturnsValidationError()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var longCategory = new string('A', 65);
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Test",
                Content = "<p>Content</p>",
                Category = longCategory,
                UserId = TestUserId,
                ArticleType = ArticleType.BlogPost
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.Category)));
        }

        [TestMethod]
        public async Task SaveArticle_NegativeArticleNumber_ReturnsValidationError()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = -1,
                Title = "Test",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.ArticleNumber)));
        }

        [TestMethod]
        public async Task SaveArticle_WhitespaceOnlyTitle_ReturnsValidationError()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "   ", // Whitespace only
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.Title)));
        }

        [TestMethod]
        public async Task SaveArticle_WhitespaceOnlyContent_ReturnsValidationError()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Test",
                Content = "     ", // Whitespace only
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.Content)));
        }

        [TestMethod]
        public async Task SaveArticle_AllFieldsNull_ReturnsMultipleValidationErrors()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 0,
                Title = null!,
                Content = null!,
                UserId = Guid.Empty,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsGreaterThanOrEqualTo(3, result.Errors.Count); // At least ArticleNumber, Title, Content
        }
    }
}
