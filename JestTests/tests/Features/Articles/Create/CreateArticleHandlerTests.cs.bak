// <copyright file="CreateArticleHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Create
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Create;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for the <see cref="CreateArticleHandler"/> class.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class CreateArticleHandlerTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext();
        }

        #region Validation Tests

        [TestMethod]
        [TestCategory("Validation")]
        public async Task HandleAsync_EmptyTitle_ReturnsValidationError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = string.Empty,
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Errors);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.Title)));
            Assert.AreEqual(0, await ArticleCountAsync());
        }

        [TestMethod]
        [TestCategory("Validation")]
        public async Task HandleAsync_TitleTooLong_ReturnsValidationError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = new string('A', 255),
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Errors);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.Title)));
        }

        [TestMethod]
        [TestCategory("Validation")]
        public async Task HandleAsync_EmptyUserId_ReturnsValidationError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Valid Title",
                UserId = Guid.Empty
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Errors);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.UserId)));
        }

        #endregion

        #region Success Scenarios

        [TestMethod]
        [TestCategory("Articles")]
        public async Task HandleAsync_ValidCommand_ReturnsSuccess()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Test Article",
                UserId = TestUserId,
                BlogKey = "default",
                ArticleType = ArticleType.General
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.IsNull(result.ErrorMessage);
            Assert.IsNull(result.Errors);
        }

        [TestMethod]
        [TestCategory("Articles")]
        public async Task HandleAsync_ValidCommand_CreatesArticleInDatabase()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Test Article",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.AreEqual(1, await ArticleCountAsync());
            var savedArticle = await Db.Articles.FirstAsync();
            Assert.AreEqual(command.Title, savedArticle.Title);
            Assert.AreEqual(command.UserId.ToString(), savedArticle.UserId);
        }

        #endregion

        #region First Article Behavior

        [TestMethod]
        [TestCategory("Articles")]
        public async Task HandleAsync_FirstArticle_BecomesRoot()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "First Article",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("root", result.Data!.UrlPath);
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task HandleAsync_FirstArticle_IsPublishedAutomatically()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "First Article",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data!.Published);
            Assert.IsTrue(result.Data.Published <= Clock.UtcNow);
        }

        #endregion
    }
}
