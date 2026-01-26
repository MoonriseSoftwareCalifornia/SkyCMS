// <copyright file="SaveArticleValidatorTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Save
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Save;
    using System;

    [TestClass]
    public class SaveArticleValidatorTests
    {
        private SaveArticleValidator validator = null!;

        [TestInitialize]
        public new void Setup()
        {
            validator = new SaveArticleValidator();
        }

        [TestMethod]
        public void Validate_ValidCommand_ReturnsNoErrors()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "Valid Title",
                Content = "<p>Valid content</p>",
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsEmpty(errors);
        }

        [TestMethod]
        public void Validate_ZeroArticleNumber_ReturnsError()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 0,
                Title = "Valid Title",
                Content = "<p>Valid content</p>",
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.ArticleNumber)));
        }

        [TestMethod]
        public void Validate_EmptyTitle_ReturnsError()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = string.Empty,
                Content = "<p>Valid content</p>",
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.Title)));
        }

        [TestMethod]
        public void Validate_TitleTooLong_ReturnsError()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = new string('A', 255),
                Content = "<p>Valid content</p>",
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.Title)));
        }

        [TestMethod]
        public void Validate_EmptyContent_ReturnsError()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "Valid Title",
                Content = string.Empty,
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.Content)));
        }

        [TestMethod]
        public void Validate_EmptyUserId_ReturnsError()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "Valid Title",
                Content = "<p>Valid content</p>",
                UserId = Guid.Empty
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.UserId)));
        }
    }
}
