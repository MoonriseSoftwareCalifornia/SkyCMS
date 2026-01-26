// <copyright file="CreateArticleHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Editor.Features.Articles.Create
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Features.Articles.Create;

    [TestClass]
    public class CreateArticleHandlerTests : ArticleTestBase
    {
        private CreateArticleHandler _handler;

        [TestInitialize]
        public new void TestInitialize()
        {
            base.TestInitialize();

            _handler = new CreateArticleHandler(
                DbContext,
                MockHtmlService.Object,
                MockCatalogService.Object,
                MockPublishingService.Object,
                MockTitleChangeService.Object,
                MockTemplateService.Object,
                MockClock.Object,
                Mock.Of<ILogger<CreateArticleHandler>>());
        }

        /// <summary>
        /// Tests that HandleAsync_WithValidCommand_CreatesArticle.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithValidCommand_CreatesArticle()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var command = new CreateArticleCommand
            {
                Title = "Test Article",
                UserId = userId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.IsNotNull(result.Data, "Result data should not be null");
            Assert.AreEqual("Test Article", result.Data.Title, "Title should match");
            Assert.AreEqual(1, result.Data.ArticleNumber, "Should be first article");

            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.IsNotNull(article, "Article should be created in database");
            Assert.AreEqual("Test Article", article.Title, "Database title should match");
        }

        /// <summary>
        /// Tests that HandleAsync_FirstArticle_AutoPublishesAsRoot.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_FirstArticle_AutoPublishesAsRoot()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Home",
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.AreEqual("root", result.Data.UrlPath, "First article should have 'root' URL");
            Assert.IsNotNull(result.Data.Published, "First article should be auto-published");
            Assert.AreEqual(TestNow, result.Data.Published, "Published date should be TestNow");

            MockPublishingService.Verify(
                x => x.PublishAsync(It.IsAny<Article>()),
                Times.Once,
                "Should call PublishAsync once for first article");
        }

        /// <summary>
        /// Tests that HandleAsync_SecondArticle_DoesNotAutoPublish.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_SecondArticle_DoesNotAutoPublish()
        {
            // Arrange
            await SeedArticleAsync("Existing Article", 1);
            await SeedArticleNumberAsync(1);

            var command = new CreateArticleCommand
            {
                Title = "Second Article",
                UserId = Guid.NewGuid()
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.IsNull(result.Data.Published, "Second article should not be auto-published");
            Assert.AreEqual(2, result.Data.ArticleNumber, "Should be second article");
            Assert.AreNotEqual("root", result.Data.UrlPath, "Should not have root URL");

            MockPublishingService.Verify(
                x => x.PublishAsync(It.IsAny<Article>()),
                Times.Never,
                "Should not auto-publish second article");
        }

        /// <summary>
        /// Tests that HandleAsync_WithConflictingTitle_ReturnsFailure.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithConflictingTitle_ReturnsFailure()
        {
            // Arrange
            await SeedArticleAsync("Existing Title", 1);

            MockTitleChangeService
                .Setup(x => x.ValidateTitle("Existing Title", null))
                .ReturnsAsync(false);

            var command = new CreateArticleCommand
            {
                Title = "Existing Title",
                UserId = Guid.NewGuid()
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail");
            Assert.IsTrue(result.Errors.ContainsKey("Title"), "Should have Title error");
            StringAssert.Contains(result.Errors["Title"][0], "conflicts", 
                "Error message should mention conflict");
        }

        /// <summary>
        /// Tests that HandleAsync_WithEmptyTitle_ReturnsFailure.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithEmptyTitle_ReturnsFailure()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = string.Empty,
                UserId = Guid.NewGuid()
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail with empty title");
            Assert.IsTrue(result.Errors.ContainsKey("Title"), "Should have Title error");
        }

        /// <summary>
        /// Tests that HandleAsync_WithTemplate_UsesTemplateContent.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithTemplate_UsesTemplateContent()
        {
            // Arrange
            var template = await SeedTemplateAsync("Test Template", "<div>Template Content</div>");

            var command = new CreateArticleCommand
            {
                Title = "Article With Template",
                UserId = Guid.NewGuid(),
                TemplateId = template.Id
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            
            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.IsNotNull(article, "Article should be created");
            Assert.AreEqual("<div>Template Content</div>", article.Content, 
                "Should use template content");
        }

        /// <summary>
        /// Tests that HandleAsync_WithContentOverride_UsesOverride.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithContentOverride_UsesOverride()
        {
            // Arrange
            var template = await SeedTemplateAsync("Test Template", "<div>Template Content</div>");

            var command = new CreateArticleCommand
            {
                Title = "Article With Override",
                UserId = Guid.NewGuid(),
                TemplateId = template.Id,
                ContentOverride = "<div>Custom Content</div>"
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            
            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.AreEqual("<div>Custom Content</div>", article.Content, 
                "Should use content override instead of template");
        }

        /// <summary>
        /// Tests that HandleAsync_WithUrlPathOverride_UsesOverride.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithUrlPathOverride_UsesOverride()
        {
            // Arrange
            await SeedArticleAsync("Existing", 1); // So this isn't the first article
            await SeedArticleNumberAsync(1); // Track the article number

            var command = new CreateArticleCommand
            {
                Title = "Test Page",
                UserId = Guid.NewGuid(),
                UrlPathOverride = "custom-path"
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.AreEqual("custom-path", result.Data.UrlPath, 
                "Should use URL path override");
        }

        /// <summary>
        /// Tests that HandleAsync_WithPublishedOverride_PublishesImmediately.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithPublishedOverride_PublishesImmediately()
        {
            // Arrange
            await SeedArticleAsync("Existing", 1); // So this isn't the first article
            await SeedArticleNumberAsync(1);

            var publishDate = TestNow.AddDays(-1);
            var command = new CreateArticleCommand
            {
                Title = "Published Article",
                UserId = Guid.NewGuid(),
                Published = publishDate
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.AreEqual(publishDate, result.Data.Published, 
                "Should use explicit publish date");

            MockPublishingService.Verify(
                x => x.PublishAsync(It.IsAny<Article>()),
                Times.Once,
                "Should publish when Published date provided");
        }

        /// <summary>
        /// Tests that HandleAsync_BlogPost_CreatesWithBlogKey.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_BlogPost_CreatesWithBlogKey()
        {
            // Arrange
            var command = new CreateArticleCommand
            {
                Title = "Blog Post",
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.BlogPost,
                BlogKey = "tech-blog"
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            
            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.AreEqual("tech-blog", article.BlogKey, "Should set blog key");
            Assert.AreEqual((int)ArticleType.BlogPost, article.ArticleType, 
                "Should set article type to BlogPost");
        }

        /// <summary>
        /// Tests that HandleAsync_WithAllOptionalProperties_SetsAllProperties.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithAllOptionalProperties_SetsAllProperties()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var publishDate = TestNow.AddDays(-5);

            var command = new CreateArticleCommand
            {
                Title = "Full Article",
                UserId = userId,
                ArticleType = ArticleType.General,
                Category = "Technology",
                Introduction = "Test introduction",
                BannerImage = "/images/banner.jpg",
                HeadJavaScript = "<script>console.log('head');</script>",
                FooterJavaScript = "<script>console.log('footer');</script>",
                Published = publishDate,
                StatusCode = StatusCodeEnum.Active,
                ContentOverride = "<div>Custom</div>",
                UrlPathOverride = "special-path"
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            
            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.AreEqual("Technology", article.Category);
            Assert.AreEqual("Test introduction", article.Introduction);
            Assert.AreEqual("/images/banner.jpg", article.BannerImage);
            Assert.AreEqual("<script>console.log('head');</script>", article.HeaderJavaScript);
            Assert.AreEqual("<script>console.log('footer');</script>", article.FooterJavaScript);
            Assert.AreEqual(publishDate, article.Published);
            Assert.AreEqual((int)StatusCodeEnum.Active, article.StatusCode);
            Assert.AreEqual("<div>Custom</div>", article.Content);
            Assert.AreEqual("special-path", article.UrlPath);
        }
    }
}
