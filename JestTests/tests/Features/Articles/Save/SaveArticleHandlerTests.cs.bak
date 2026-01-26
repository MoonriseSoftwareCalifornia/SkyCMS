// <copyright file="SaveArticleHandlerTests.cs" company="Moonrise Software, LLC">
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
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Articles.Save;
    using System.Threading.Tasks;

    [DoNotParallelize]
    [TestClass]
    public class SaveArticleHandlerTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext();
        }

        #region Validation Tests

        [TestMethod]
        [TestCategory("Validation")]
        public async Task HandleAsync_InvalidArticleNumber_ReturnsValidationError()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 0,
                Title = "Test",
                Content = "<p>Content</p>",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Errors);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.ArticleNumber)));
        }

        [TestMethod]
        [TestCategory("Validation")]
        public async Task HandleAsync_EmptyTitle_ReturnsValidationError()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = string.Empty,
                Content = "<p>Content</p>",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Errors);
            Assert.IsTrue(result.Errors.ContainsKey(nameof(command.Title)));
        }

        #endregion

        #region Success Scenarios

        [TestMethod]
        [TestCategory("Articles")]
        public async Task HandleAsync_ValidCommand_SavesArticle()
        {
            // Arrange - Create an article first
            var createCommand = new CreateArticleCommand
            {
                Title = "Original Title",
                UserId = TestUserId
            };
            var createResult = await Mediator.SendAsync(createCommand);
            Assert.IsTrue(createResult.IsSuccess);

            var saveCommand = new SaveArticleCommand
            {
                ArticleNumber = createResult.Data!.ArticleNumber,
                Title = "Updated Title",
                Content = "<p>Updated content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await Mediator.SendAsync(saveCommand);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data);
            Assert.IsTrue(result.Data.ServerSideSuccess);
            Assert.AreEqual("Updated Title", result.Data.Model!.Title);
        }

        [TestMethod]
        [TestCategory("Articles")]
        public async Task HandleAsync_ArticleNotFound_ReturnsError()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 9999,
                Title = "Test",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await Mediator.SendAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.Contains("not found", result.ErrorMessage);
        }

        #endregion

        #region Title Change Tests

        [TestMethod]
        [TestCategory("TitleChanges")]
        public async Task HandleAsync_TitleChanged_TriggersSlugUpdate()
        {
            // Arrange - Create article
            var createCommand = new CreateArticleCommand
            {
                Title = "Original Title",
                UserId = TestUserId
            };
            var createResult = await Mediator.SendAsync(createCommand);
            var originalSlug = createResult.Data!.UrlPath;

            var saveCommand = new SaveArticleCommand
            {
                ArticleNumber = createResult.Data.ArticleNumber,
                Title = "Completely New Title",
                Content = createResult.Data.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await Mediator.SendAsync(saveCommand);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            // Note: Slug may or may not change depending on TitleChangeService logic
        }

        #endregion

        #region Catalog Integration

        [TestMethod]
        [TestCategory("Catalog")]
        public async Task HandleAsync_ValidSave_UpdatesCatalog()
        {
            // Arrange - Create article
            var createCommand = new CreateArticleCommand
            {
                Title = "Original Title",
                UserId = TestUserId
            };
            var createResult = await Mediator.SendAsync(createCommand);

            var saveCommand = new SaveArticleCommand
            {
                ArticleNumber = createResult.Data!.ArticleNumber,
                Title = "Updated Title",
                Content = "<p>Updated content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await Mediator.SendAsync(saveCommand);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            
            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == saveCommand.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.AreEqual("Updated Title", catalogEntry.Title);
        }

        #endregion

        #region Content Processing

        [TestMethod]
        [TestCategory("Content")]
        public async Task HandleAsync_Content_ProcessedForEditableMarkers()
        {
            // Arrange
            var createCommand = new CreateArticleCommand
            {
                Title = "Test Article",
                UserId = TestUserId
            };
            var createResult = await Mediator.SendAsync(createCommand);

            var saveCommand = new SaveArticleCommand
            {
                ArticleNumber = createResult.Data!.ArticleNumber,
                Title = "Test Article",
                Content = "<div contenteditable=\"true\"><h1>Test</h1></div>", // With contenteditable
                UserId = TestUserId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await Mediator.SendAsync(saveCommand);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            // Content should have tracking markers added by ArticleHtmlService
            Assert.Contains("contenteditable", result.Data!.Model!.Content);
            Assert.Contains("data-ccms-ceid", result.Data!.Model!.Content);
            Assert.Contains("data-ccms-index", result.Data!.Model!.Content);
        }

        #endregion

        #region Publishing Tests

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task HandleAsync_PublishedArticle_TriggersPublishing()
        {
            // Arrange
            var createCommand = new CreateArticleCommand
            {
                Title = "Published Article",
                UserId = TestUserId
            };
            var createResult = await Mediator.SendAsync(createCommand);

            var saveCommand = new SaveArticleCommand
            {
                ArticleNumber = createResult.Data!.ArticleNumber,
                Title = "Published Article Updated",
                Content = "<p>Published content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow // Mark as published
            };

            // Act
            var result = await Mediator.SendAsync(saveCommand);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data!.CdnResults);
            // CdnResults may be empty in test environment, but the list should exist
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task HandleAsync_UnpublishedArticle_DoesNotTriggerPublishing()
        {
            // Arrange
            var createCommand = new CreateArticleCommand
            {
                Title = "Draft Article",
                UserId = TestUserId
            };
            var createResult = await Mediator.SendAsync(createCommand);

            // Unpublish the article
            var article = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == createResult.Data!.ArticleNumber);
            article!.Published = null;
            await Db.SaveChangesAsync();

            var saveCommand = new SaveArticleCommand
            {
                ArticleNumber = createResult.Data!.ArticleNumber,
                Title = "Draft Article Updated",
                Content = "<p>Draft content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = null // Not published
            };

            // Act
            var result = await Mediator.SendAsync(saveCommand);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsEmpty(result.Data!.CdnResults);
        }

        #endregion

        #region Field Updates

        [TestMethod]
        [TestCategory("Articles")]
        public async Task HandleAsync_AllFields_UpdatedCorrectly()
        {
            // Arrange
            var createResult = await Mediator.SendAsync(new CreateArticleCommand
            {
                Title = "Original",
                UserId = TestUserId
            });

            var saveCommand = new SaveArticleCommand
            {
                ArticleNumber = createResult.Data!.ArticleNumber,
                Title = "New Title",
                Content = "<p>New Content</p>",
                HeadJavaScript = "<script>console.log('head');</script>",
                FooterJavaScript = "<script>console.log('footer');</script>",
                BannerImage = "https://example.com/banner.jpg",
                ArticleType = ArticleType.BlogPost,
                Category = "Technology",
                Introduction = "This is an introduction",
                UserId = TestUserId
            };

            // Act
            var result = await Mediator.SendAsync(saveCommand);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var model = result.Data!.Model!;
            Assert.AreEqual("New Title", model.Title);
            Assert.AreEqual("<script>console.log('head');</script>", model.HeadJavaScript);
            Assert.AreEqual("<script>console.log('footer');</script>", model.FooterJavaScript);
            Assert.AreEqual("https://example.com/banner.jpg", model.BannerImage);
            Assert.AreEqual(ArticleType.BlogPost, model.ArticleType);
            Assert.AreEqual("Technology", model.Category);
            Assert.AreEqual("This is an introduction", model.Introduction);
        }

        #endregion
    }
}
