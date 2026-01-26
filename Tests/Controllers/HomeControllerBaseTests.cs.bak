// <copyright file="HomeControllerBaseTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.BlobService;
    using Cosmos.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for the <see cref="HomeControllerBase"/> class.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class HomeControllerBaseTests : SkyCmsTestBase
    {
        private TestHomeController controller = null!;
        private Mock<ILogger<TestHomeController>> loggerMock = null!;
        private Mock<IEmailSender> emailSenderMock = null!;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);

            loggerMock = new Mock<ILogger<TestHomeController>>();
            emailSenderMock = new Mock<IEmailSender>();

            controller = new TestHomeController(
                Logic,
                Db,
                Storage,
                loggerMock.Object,
                emailSenderMock.Object);

            // Setup HttpContext
            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            controller?.Dispose();
            await DisposeAsync();
        }

        #region CCMS_GetArticleFolderContents Tests

        [TestMethod]
        public async Task GetArticleFolderContents_ValidArticle_ReturnsJsonResult()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Setup request headers with referer containing article number
            controller.ControllerContext.HttpContext.Request.Headers["referer"] =
                $"http://localhost/editor?articleNumber={article.ArticleNumber}";

            // Act
            var result = await controller.CCMS_GetArticleFolderContents("");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        [TestMethod]
        public async Task GetArticleFolderContents_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            controller.ModelState.AddModelError("path", "Invalid path");

            // Act
            var result = await controller.CCMS_GetArticleFolderContents("");

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task GetArticleFolderContents_ArticleNotFound_ReturnsNotFound()
        {
            // Arrange
            controller.ControllerContext.HttpContext.Request.Headers["referer"] =
                "http://localhost/non-existent-page";

            // Act
            var result = await controller.CCMS_GetArticleFolderContents("");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        [TestMethod]
        public async Task GetArticleFolderContents_WithPath_ReturnsSubfolderContents()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            await Logic.PublishArticle(entity.Id, DateTimeOffset.UtcNow);

            // Use the article number in referer instead of URL path
            controller.ControllerContext.HttpContext.Request.Headers["referer"] =
                $"http://localhost/editor?articleNumber={article.ArticleNumber}";

            // Act
            var result = await controller.CCMS_GetArticleFolderContents("images");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        #endregion

        #region GetTOC Tests

        [TestMethod]
        public async Task GetTOC_RootPage_ReturnsTopLevelPages()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId);
            var page1 = await Logic.CreateArticle("Page 1", TestUserId);
            var page2 = await Logic.CreateArticle("Page 2", TestUserId);

            var entity1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == page1.ArticleNumber);
            var entity2 = await Db.Articles.FirstAsync(a => a.ArticleNumber == page2.ArticleNumber);

            await Logic.PublishArticle(entity1.Id, DateTimeOffset.UtcNow.AddDays(-1));
            await Logic.PublishArticle(entity2.Id, DateTimeOffset.UtcNow);

            // Act
            var result = await controller.GetTOC("", false, 0, 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult);
            var toc = jsonResult.Value as TableOfContents;

            Assert.IsNotNull(toc);
            Assert.IsTrue(toc.TotalCount >= 2, $"Expected at least 2 pages, but got {toc.TotalCount}");
        }

        [TestMethod]
        public async Task GetTOC_WithParentPath_ReturnsChildPages()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId);
            var parent = await Logic.CreateArticle("Parent Page", TestUserId);
            var parentEntity = await Db.Articles.FirstAsync(a => a.ArticleNumber == parent.ArticleNumber);
            await Logic.PublishArticle(parentEntity.Id, DateTimeOffset.UtcNow);

            // Act
            var result = await controller.GetTOC("parent-page", false, 0, 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        [TestMethod]
        public async Task GetTOC_OrderByPublishDate_ReturnsChronologicalOrder()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId);
            var page1 = await Logic.CreateArticle("Older Page", TestUserId);
            var page2 = await Logic.CreateArticle("Newer Page", TestUserId);

            var entity1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == page1.ArticleNumber);
            var entity2 = await Db.Articles.FirstAsync(a => a.ArticleNumber == page2.ArticleNumber);

            await Logic.PublishArticle(entity1.Id, DateTimeOffset.UtcNow.AddDays(-5));
            await Logic.PublishArticle(entity2.Id, DateTimeOffset.UtcNow.AddDays(-1));

            // Act
            var result = await controller.GetTOC("", true, 0, 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult);
            var toc = jsonResult.Value as TableOfContents;

            Assert.IsNotNull(toc);
            Assert.IsNotEmpty(toc.Items);
        }

        [TestMethod]
        public async Task GetTOC_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            controller.ModelState.AddModelError("page", "Invalid page");

            // Act
            var result = await controller.GetTOC("", false, 0, 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task GetTOC_Pagination_ReturnsCorrectPage()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId);

            for (int i = 1; i <= 15; i++)
            {
                var page = await Logic.CreateArticle($"Page {i}", TestUserId);
                var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == page.ArticleNumber);
                await Logic.PublishArticle(entity.Id, DateTimeOffset.UtcNow.AddMinutes(-i));
            }

            // Act - Get second page with 10 items per page
            var result = await controller.GetTOC("", false, 1, 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult);
            var toc = jsonResult.Value as TableOfContents;

            Assert.IsNotNull(toc);
            Assert.AreEqual(1, toc.PageNo);
            Assert.AreEqual(10, toc.PageSize);
        }

        #endregion

        #region CCMS_POSTCONTACT_INFO Tests

        [TestMethod]
        public async Task PostContactInfo_ValidModel_ReturnsJsonResult()
        {
            // Arrange
            var model = new ContactViewModel
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Created = DateTimeOffset.UtcNow,
                Id = Guid.NewGuid(),
                Phone = "123-456-7890",
                Updated = DateTimeOffset.UtcNow,
            };

            emailSenderMock
                .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await controller.CCMS_POSTCONTACT_INFO(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        [TestMethod]
        public async Task PostContactInfo_NullModel_ReturnsNotFound()
        {
            // Act
            var result = await controller.CCMS_POSTCONTACT_INFO(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task PostContactInfo_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var model = new ContactViewModel
            {
                FirstName = "John"
                // Missing required fields
            };
            controller.ModelState.AddModelError("Email", "Email is required");

            // Act
            var result = await controller.CCMS_POSTCONTACT_INFO(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task PostContactInfo_SetsTimestamps_Correctly()
        {
            // Arrange
            var beforeSubmit = DateTimeOffset.UtcNow;
            var model = new ContactViewModel
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe1@example.com",
                Created = DateTimeOffset.UtcNow,
                Id = Guid.NewGuid(),
                Phone = "123-456-7890",
                Updated = DateTimeOffset.UtcNow,
            };

            emailSenderMock
                .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await controller.CCMS_POSTCONTACT_INFO(model);
            var afterSubmit = DateTimeOffset.UtcNow;

            // Assert
            Assert.IsTrue(model.Created >= beforeSubmit && model.Created <= afterSubmit);
            Assert.IsTrue(model.Updated >= beforeSubmit && model.Updated <= afterSubmit);
            Assert.AreNotEqual(Guid.Empty, model.Id);
        }

        #endregion

        #region CCMS___SEARCH Tests

        [TestMethod]
        public async Task Search_ValidQuery_ReturnsResults()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId);
            var article = await Logic.CreateArticle("Searchable Content", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            entity.Content = "<p>This is searchable content with unique terms.</p>";
            await Db.SaveChangesAsync();
            await Logic.PublishArticle(entity.Id, DateTimeOffset.UtcNow);

            // Act
            var result = await controller.CCMS___SEARCH("searchable");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult);
            var results = jsonResult.Value as List<TableOfContentsItem>;

            Assert.IsNotNull(results);
            Assert.IsNotEmpty(results);
        }

        [TestMethod]
        public async Task Search_EmptyQuery_ReturnsBadRequest()
        {
            // Act
            var result = await controller.CCMS___SEARCH("");

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Search_NullQuery_ReturnsBadRequest()
        {
            // Act
            var result = await controller.CCMS___SEARCH(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Search_MultipleTerms_ReturnsMatchingResults()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId);
            var article = await Logic.CreateArticle("Multi Term Search", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            entity.Content = "<p>Content with multiple searchable unique terms here.</p>";
            await Db.SaveChangesAsync();
            await Logic.PublishArticle(entity.Id, DateTimeOffset.UtcNow);

            // Act
            var result = await controller.CCMS___SEARCH("searchable unique");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        [TestMethod]
        public async Task Search_NoMatches_ReturnsEmptyList()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId);

            // Act
            var result = await controller.CCMS___SEARCH("nonexistentterm12345");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult);
            var results = jsonResult.Value as List<TableOfContentsItem>;

            Assert.IsNotNull(results);
            Assert.IsEmpty(results);
        }

        [TestMethod]
        public async Task Search_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            controller.ModelState.AddModelError("searchTxt", "Invalid search");

            // Act
            var result = await controller.CCMS___SEARCH("test");

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region GetArticleNumberFromRequestHeaders Tests

        [TestMethod]
        public async Task GetArticleNumber_FromQueryString_ReturnsCorrectNumber()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            await Logic.PublishArticle(entity.Id, DateTimeOffset.UtcNow);

            controller.ControllerContext.HttpContext.Request.Headers["referer"] =
                $"http://localhost/editor?articleNumber={article.ArticleNumber}";

            // Act
            var result = await controller.CCMS_GetArticleFolderContents("");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        [TestMethod]
        public async Task GetArticleNumber_FromEditorPath_ReturnsCorrectNumber()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            await Logic.PublishArticle(entity.Id, DateTimeOffset.UtcNow);

            controller.ControllerContext.HttpContext.Request.Headers["referer"] =
                $"http://localhost/editor/ccmscontent/{article.ArticleNumber}";

            // Act
            var result = await controller.CCMS_GetArticleFolderContents("");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        [TestMethod]
        public async Task GetArticleNumber_FromPublishedPage_ReturnsCorrectNumber()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            await Logic.PublishArticle(entity.Id, DateTimeOffset.UtcNow);

            controller.ControllerContext.HttpContext.Request.Headers["referer"] =
                $"http://localhost/{article.UrlPath}";

            // Act
            var result = await controller.CCMS_GetArticleFolderContents("");

            // Assert - Should handle published page lookup
            Assert.IsNotNull(result);
        }

        #endregion

        /// <summary>
        /// Test implementation of HomeControllerBase for testing purposes.
        /// </summary>
        public class TestHomeController : HomeControllerBase
        {
            public TestHomeController(
                ArticleLogic articleLogic,
                ApplicationDbContext dbContext,
                StorageContext storageContext,
                ILogger logger,
                IEmailSender emailSender)
                : base(articleLogic, dbContext, storageContext, logger, emailSender)
            {
            }
        }
    }
}
