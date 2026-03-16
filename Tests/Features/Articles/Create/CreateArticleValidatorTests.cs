// <copyright file="CreateArticleValidatorTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Create
{
    using Cosmos.Cms.Common;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Create;
    using System;
    using System.Linq;

    /// <summary>
    /// Unit tests for the <see cref="CreateArticleValidator"/> class.
    /// </summary>
    [TestClass]
    public class CreateArticleValidatorTests
    {
        private CreateArticleValidator validator = null!;

        [TestInitialize]
        public void Setup()
        {
            validator = new CreateArticleValidator();
        }

        #region Title Validation Tests

        /// <summary>
        /// Tests that Validate_EmptyTitle_ReturnsError.
        /// </summary>
        [TestMethod]
        public void Validate_EmptyTitle_ReturnsError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = string.Empty,
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.Title)));
            Assert.AreEqual("Title is required.", errors[nameof(command.Title)][0]);
        }

        /// <summary>
        /// Tests that Validate_WhitespaceOnlyTitle_ReturnsError.
        /// </summary>
        [TestMethod]
        public void Validate_WhitespaceOnlyTitle_ReturnsError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "   ",
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.Title)));
            Assert.AreEqual("Title is required.", errors[nameof(command.Title)][0]);
        }

        /// <summary>
        /// Tests that Validate_NullTitle_ReturnsError.
        /// </summary>
        [TestMethod]
        public void Validate_NullTitle_ReturnsError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = null!,
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.Title)));
            Assert.AreEqual("Title is required.", errors[nameof(command.Title)][0]);
        }

        /// <summary>
        /// Tests that Validate_TitleExceeds254Characters_ReturnsError.
        /// </summary>
        [TestMethod]
        public void Validate_TitleExceeds254Characters_ReturnsError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = new string('A', 255),
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.Title)));
            Assert.AreEqual("Title must not exceed 254 characters.", errors[nameof(command.Title)][0]);
        }

        /// <summary>
        /// Tests that Validate_TitleExactly254Characters_NoError.
        /// </summary>
        [TestMethod]
        public void Validate_TitleExactly254Characters_NoError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = new string('A', 254),
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsFalse(errors.ContainsKey(nameof(command.Title)));
        }

        /// <summary>
        /// Tests that Validate_ValidTitle_NoError.
        /// </summary>
        [TestMethod]
        public void Validate_ValidTitle_NoError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Valid Article Title",
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsFalse(errors.ContainsKey(nameof(command.Title)));
        }

        #endregion

        #region UserId Validation Tests

        /// <summary>
        /// Tests that Validate_EmptyUserId_ReturnsError.
        /// </summary>
        [TestMethod]
        public void Validate_EmptyUserId_ReturnsError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Valid Title",
                UserId = Guid.Empty
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.UserId)));
            Assert.AreEqual("UserId is required.", errors[nameof(command.UserId)][0]);
        }

        /// <summary>
        /// Tests that Validate_ValidUserId_NoError.
        /// </summary>
        [TestMethod]
        public void Validate_ValidUserId_NoError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Valid Title",
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsFalse(errors.ContainsKey(nameof(command.UserId)));
        }

        #endregion

        #region BlogKey Validation Tests

        /// <summary>
        /// Tests that Validate_BlogKeyExceeds128Characters_ReturnsError.
        /// </summary>
        [TestMethod]
        public void Validate_BlogKeyExceeds128Characters_ReturnsError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Valid Title",
                UserId = Guid.NewGuid(),
                BlogKey = new string('B', 129)
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsTrue(errors.ContainsKey(nameof(command.BlogKey)));
            Assert.AreEqual("BlogKey must not exceed 128 characters.", errors[nameof(command.BlogKey)][0]);
        }

        /// <summary>
        /// Tests that Validate_BlogKeyExactly128Characters_NoError.
        /// </summary>
        [TestMethod]
        public void Validate_BlogKeyExactly128Characters_NoError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Valid Title",
                UserId = Guid.NewGuid(),
                BlogKey = new string('B', 128)
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsFalse(errors.ContainsKey(nameof(command.BlogKey)));
        }

        /// <summary>
        /// Tests that Validate_EmptyBlogKey_NoError.
        /// </summary>
        [TestMethod]
        public void Validate_EmptyBlogKey_NoError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Valid Title",
                UserId = Guid.NewGuid(),
                BlogKey = string.Empty
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsFalse(errors.ContainsKey(nameof(command.BlogKey)));
        }

        /// <summary>
        /// Tests that Validate_DefaultBlogKey_NoError.
        /// </summary>
        [TestMethod]
        public void Validate_DefaultBlogKey_NoError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Valid Title",
                UserId = Guid.NewGuid(),
                BlogKey = "default"
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsFalse(errors.ContainsKey(nameof(command.BlogKey)));
        }

        #endregion

        #region Multiple Errors Tests

        /// <summary>
        /// Tests that Validate_MultipleErrors_ReturnsAllErrors.
        /// </summary>
        [TestMethod]
        public void Validate_MultipleErrors_ReturnsAllErrors()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = string.Empty,
                UserId = Guid.Empty,
                BlogKey = new string('B', 129)
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.HasCount(3, errors);
            Assert.IsTrue(errors.ContainsKey(nameof(command.Title)));
            Assert.IsTrue(errors.ContainsKey(nameof(command.UserId)));
            Assert.IsTrue(errors.ContainsKey(nameof(command.BlogKey)));
        }

        /// <summary>
        /// Tests that Validate_AllFieldsValid_ReturnsNoErrors.
        /// </summary>
        [TestMethod]
        public void Validate_AllFieldsValid_ReturnsNoErrors()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Valid Article Title",
                UserId = Guid.NewGuid(),
                TemplateId = Guid.NewGuid(),
                BlogKey = "tech-blog",
                ArticleType = ArticleType.BlogPost
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsEmpty(errors);
        }

        #endregion

        #region Edge Cases

        /// <summary>
        /// Tests that Validate_TitleWithSpecialCharacters_NoError.
        /// </summary>
        [TestMethod]
        public void Validate_TitleWithSpecialCharacters_NoError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Article: With <Special> & \"Characters\"!",
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsFalse(errors.ContainsKey(nameof(command.Title)));
        }

        /// <summary>
        /// Tests that Validate_TitleWithUnicodeCharacters_NoError.
        /// </summary>
        [TestMethod]
        public void Validate_TitleWithUnicodeCharacters_NoError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "???? - Article Title - ?????????",
                UserId = Guid.NewGuid()
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsFalse(errors.ContainsKey(nameof(command.Title)));
        }

        /// <summary>
        /// Tests that Validate_OptionalTemplateId_NoError.
        /// </summary>
        [TestMethod]
        public void Validate_OptionalTemplateId_NoError()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Valid Title",
                UserId = Guid.NewGuid(),
                TemplateId = null
            };

            // Act
            var errors = validator.Validate(command);

            // Assert
            Assert.IsFalse(errors.ContainsKey(nameof(command.TemplateId)));
        }

        /// <summary>
        /// Tests that Validate_AllArticleTypes_NoError.
        /// </summary>
        [TestMethod]
        public void Validate_AllArticleTypes_NoError()
        {
            // Arrange
            var articleTypes = Enum.GetValues<ArticleType>();

            foreach (var articleType in articleTypes)
            {
                var command = new CreateArticleCommand
                {
                    Title = "Valid Title",
                    UserId = Guid.NewGuid(),
                    ArticleType = articleType
                };

                // Act
                var errors = validator.Validate(command);

                // Assert
                Assert.IsFalse(errors.Any(), $"ArticleType {articleType} should be valid");
            }
        }

        #endregion
    }
}

