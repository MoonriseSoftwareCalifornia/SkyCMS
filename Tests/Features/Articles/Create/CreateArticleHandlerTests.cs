// <copyright file="CreateArticleHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Features.Articles.Create
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Tests.Editor.Features.Articles;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

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

        [TestCleanup]
        public void Cleanup()
        {
            DbContext?.Dispose();
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

            // Verify article was created in database
            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.IsNotNull(article, "Article should be created in database");
            Assert.AreEqual("Test Article", article.Title, "Database title should match");
            Assert.AreEqual((int)ArticleType.General, article.ArticleType, "ArticleType should be General");
            Assert.AreEqual(userId.ToString(), article.UserId, "UserId should be stored in entity");
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
                Title = "Home Page",
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
            Assert.AreEqual(1, result.Data.ArticleNumber, "Should be article number 1");

            // Verify in database
            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.AreEqual("root", article.UrlPath, "Database UrlPath should be 'root'");
            Assert.IsNotNull(article.Published, "Database Published should not be null");
            Assert.AreEqual((int)StatusCodeEnum.Active, article.StatusCode, "StatusCode should be Active");

            // Verify catalog service was called
            MockCatalogService.Verify(
                x => x.UpsertAsync(It.IsAny<Cosmos.Common.Data.Article>(), It.IsAny<CancellationToken>()),
                Times.Once,
                "Should update catalog for first article");

            // Verify publishing service was called
            MockPublishingService.Verify(
                x => x.PublishAsync(It.IsAny<Cosmos.Common.Data.Article>()),
                Times.Once,
                "Should auto-publish first article");
        }

        /// <summary>
        /// Tests that HandleAsync_SecondArticle_DoesNotAutoPublish.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_SecondArticle_DoesNotAutoPublish()
        {
            // Arrange
            // Create first article to ensure we're testing the second one
            await SeedArticleAsync("First Article", 1, urlPath: "root", published: true);
            await SeedArticleNumberAsync(1);

            var command = new CreateArticleCommand
            {
                Title = "Second Article",
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.IsNull(result.Data.Published, "Second article should NOT be auto-published");
            Assert.AreEqual(2, result.Data.ArticleNumber, "Should be article number 2");
            Assert.AreNotEqual("root", result.Data.UrlPath, "Should not have root URL");

            // UrlPath should be generated from title
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.UrlPath), "UrlPath should be generated");

            // Verify in database
            var article = await DbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == 2);
            Assert.IsNotNull(article, "Second article should exist in database");
            Assert.IsNull(article.Published, "Database Published should be null");

            // Verify publishing service was NOT called
            MockPublishingService.Verify(
                x => x.PublishAsync(It.Is<Cosmos.Common.Data.Article>(a => a.ArticleNumber == 2)),
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
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail with conflicting title");
            Assert.IsTrue(result.Errors.ContainsKey("Title"), "Should have Title error");
            StringAssert.Contains(result.Errors["Title"][0], "conflicts",
                "Error message should mention conflict");

            // Verify no duplicate article was created
            var count = await DbContext.Articles
                .CountAsync(a => a.Title == "Existing Title");
            Assert.AreEqual(1, count, "Should still have only one article with that title");
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
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail with empty title");
            Assert.IsTrue(result.Errors.ContainsKey("Title"), "Should have Title error");
            Assert.IsTrue(result.Errors["Title"].Any(), "Should have at least one error message");

            // Verify no article was created
            var count = await DbContext.Articles.CountAsync();
            Assert.AreEqual(0, count, "No article should be created with empty title");
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
                TemplateId = template.Id,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.IsNotNull(result.Data, "Result data should not be null");

            // Verify template content was used
            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.IsNotNull(article, "Article should be created in database");
            StringAssert.Contains(article.Content, "Template Content",
                "Should use template content");
            Assert.AreEqual(template.Id, article.TemplateId,
                "TemplateId should be stored");
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
                ContentOverride = "<div>Custom Content Override</div>",
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");

            // Verify ContentOverride takes precedence over template
            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.IsNotNull(article, "Article should be created");
            StringAssert.Contains(article.Content, "Custom Content Override",
                "Should use content override instead of template");
            Assert.IsFalse(article.Content.Contains("Template Content"),
                "Should NOT use template content when override is provided");
        }

        /// <summary>
        /// Tests that HandleAsync_WithUrlPathOverride_UsesOverride.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithUrlPathOverride_UsesOverride()
        {
            // Arrange
            // Seed first article so this isn't auto-assigned "root"
            await SeedArticleAsync("First Article", 1, urlPath: "root", published: true);
            await SeedArticleNumberAsync(1);

            var command = new CreateArticleCommand
            {
                Title = "Custom URL Article",
                UserId = Guid.NewGuid(),
                UrlPathOverride = "custom-special-path",
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.AreEqual("custom-special-path", result.Data.UrlPath,
                "Should use URL path override");

            // Verify in database
            var article = await DbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == 2);
            Assert.IsNotNull(article, "Article should exist");
            Assert.AreEqual("custom-special-path", article.UrlPath,
                "Database should have custom URL path");
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
                Title = "My First Blog Post",
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.BlogPost,
                BlogKey = "tech-blog",
                Category = "Technology",
                Introduction = "This is a test blog post"
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.AreEqual((int)ArticleType.BlogPost, (int)result.Data.ArticleType,
                "Should be BlogPost type");

            // Verify in database
            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.IsNotNull(article, "Article should be created");
            Assert.AreEqual("tech-blog", article.BlogKey, "BlogKey should be set");
            Assert.AreEqual((int)ArticleType.BlogPost, article.ArticleType,
                "Database ArticleType should be BlogPost");
            Assert.AreEqual("Technology", article.Category, "Category should be set");
            Assert.AreEqual("This is a test blog post", article.Introduction,
                "Introduction should be set");
        }

        /// <summary>
        /// Tests that HandleAsync_WithPublishedOverride_PublishesImmediately.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithPublishedOverride_PublishesImmediately()
        {
            // Arrange
            // Seed first article so this is the second one (normally unpublished)
            await SeedArticleAsync("First Article", 1, urlPath: "root", published: true);
            await SeedArticleNumberAsync(1);

            var publishDate = TestNow.AddDays(-5);
            var command = new CreateArticleCommand
            {
                Title = "Pre-Published Article",
                UserId = Guid.NewGuid(),
                Published = publishDate,  // Explicit publish date
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.AreEqual(publishDate, result.Data.Published,
                "Should use explicit publish date");
            Assert.AreEqual(2, result.Data.ArticleNumber, "Should be second article");

            // Verify in database
            var article = await DbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == 2);
            Assert.IsNotNull(article, "Article should exist");
            Assert.AreEqual(publishDate, article.Published,
                "Database should have explicit publish date");

            // Verify publishing service was called (even for second article when Published is set)
            MockPublishingService.Verify(
                x => x.PublishAsync(It.IsAny<Cosmos.Common.Data.Article>()),
                Times.Once,
                "Should publish when Published date is explicitly provided");
        }

        /// <summary>
        /// Tests that HandleAsync_WithAllOptionalProperties_SetsAllProperties.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithAllOptionalProperties_SetsAllProperties()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var publishDate = TestNow.AddDays(-3);
            var template = await SeedTemplateAsync("Full Template", "<div>Template</div>");

            var command = new CreateArticleCommand
            {
                Title = "Full Featured Article",
                UserId = userId,
                ArticleType = ArticleType.General,
                TemplateId = template.Id,
                Category = "Technology",
                Introduction = "A comprehensive test article",
                BannerImage = "/images/banner.jpg",
                HeadJavaScript = "<script>console.log('head');</script>",
                FooterJavaScript = "<script>console.log('footer');</script>",
                Published = publishDate,
                StatusCode = StatusCodeEnum.Active,
                ContentOverride = "<div>Custom Full Content</div>",
                UrlPathOverride = "special-article-path",
                BlogKey = "tech-blog"
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.IsNotNull(result.Data, "Result should contain article");

            // Verify ALL properties were set correctly
            var article = await DbContext.Articles.FirstOrDefaultAsync();
            Assert.IsNotNull(article, "Article should exist in database");

            // Basic properties
            Assert.AreEqual("Full Featured Article", article.Title);
            Assert.AreEqual(userId.ToString(), article.UserId);
            Assert.AreEqual((int)ArticleType.General, article.ArticleType);

            // Template and content
            Assert.AreEqual(template.Id, article.TemplateId);
            StringAssert.Contains(article.Content, "Custom Full Content",
                "Should use content override");

            // Metadata
            Assert.AreEqual("Technology", article.Category);
            Assert.AreEqual("A comprehensive test article", article.Introduction);
            Assert.AreEqual("/images/banner.jpg", article.BannerImage);

            // Scripts
            Assert.AreEqual("<script>console.log('head');</script>", article.HeaderJavaScript);
            Assert.AreEqual("<script>console.log('footer');</script>", article.FooterJavaScript);

            // Publishing
            Assert.AreEqual(publishDate, article.Published);
            Assert.AreEqual((int)StatusCodeEnum.Active, article.StatusCode);

            // URL and blog
            Assert.AreEqual("special-article-path", article.UrlPath);
            Assert.AreEqual("tech-blog", article.BlogKey);
        }
    }
}

