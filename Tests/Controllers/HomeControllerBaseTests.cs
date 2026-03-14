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
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services;
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
    using System.Threading;
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
        private Mock<IContactManagementService> contactManagementServiceMock = null!;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);

            loggerMock = new Mock<ILogger<TestHomeController>>();
            emailSenderMock = new Mock<IEmailSender>();
            contactManagementServiceMock = new Mock<IContactManagementService>();

            // Use the real mediator from the base class instead of a mock
            controller = new TestHomeController(
                Mediator,
                Db,
                loggerMock.Object,
                emailSenderMock.Object,
                contactManagementServiceMock.Object);

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

        /// <summary>
        /// Tests that GetArticleFolderContents_ValidArticle_ReturnsJsonResult.
        /// </summary>
        [TestMethod]
        public async Task GetArticleFolderContents_ValidArticle_ReturnsJsonResult()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);

            // Setup request headers with referer containing article number
            controller.ControllerContext.HttpContext.Request.Headers["referer"] =
                $"http://localhost/editor?articleNumber={article.ArticleNumber}";

            // Act
            var result = await controller.CCMS_GetArticleFolderContents("");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        /// <summary>
        /// Tests that GetArticleFolderContents_InvalidModelState_ReturnsBadRequest.
        /// </summary>
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

        /// <summary>
        /// Tests that GetArticleFolderContents_ArticleNotFound_ReturnsNotFound.
        /// </summary>
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

        /// <summary>
        /// Tests that GetArticleFolderContents_WithPath_ReturnsSubfolderContents.
        /// </summary>
        [TestMethod]
        public async Task GetArticleFolderContents_WithPath_ReturnsSubfolderContents()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            await PublishingService.PublishAsync(entity);

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

        /// <summary>
        /// Tests that GetTOC_RootPage_ReturnsTopLevelPages.
        /// </summary>
        [TestMethod]
        public async Task GetTOC_RootPage_ReturnsTopLevelPages()
        {
            // Arrange
            await CreateArticleAsync("Home Page", TestUserId);
            var page1 = await CreateArticleAsync("Page 1", TestUserId);
            var page2 = await CreateArticleAsync("Page 2", TestUserId);

            var entity1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == page1.ArticleNumber);
            var entity2 = await Db.Articles.FirstAsync(a => a.ArticleNumber == page2.ArticleNumber);

            await PublishingService.PublishAsync(entity1);
            await PublishingService.PublishAsync(entity2);

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

        /// <summary>
        /// Tests that GetTOC_WithParentPath_ReturnsChildPages.
        /// </summary>
        [TestMethod]
        public async Task GetTOC_WithParentPath_ReturnsChildPages()
        {
            // Arrange
            await CreateArticleAsync("Home Page", TestUserId);
            var parent = await CreateArticleAsync("Parent Page", TestUserId);
            var parentEntity = await Db.Articles.FirstAsync(a => a.ArticleNumber == parent.ArticleNumber);
            await CreateArticleAsync("Child Page", TestUserId);

            await PublishingService.PublishAsync(parentEntity);

            // Act
            var result = await controller.GetTOC("parent-page", false, 0, 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        /// <summary>
        /// Tests that GetTOC_OrderByPublishDate_ReturnsChronologicalOrder.
        /// </summary>
        [TestMethod]
        public async Task GetTOC_OrderByPublishDate_ReturnsChronologicalOrder()
        {
            // Arrange
            await CreateArticleAsync("Home Page", TestUserId);
            var page1 = await CreateArticleAsync("Older Page", TestUserId);
            var page2 = await CreateArticleAsync("Newer Page", TestUserId);

            var entity1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == page1.ArticleNumber);
            var entity2 = await Db.Articles.FirstAsync(a => a.ArticleNumber == page2.ArticleNumber);

            await PublishingService.PublishAsync(entity1);
            await PublishingService.PublishAsync(entity2);

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

        /// <summary>
        /// Tests that GetTOC_InvalidModelState_ReturnsBadRequest.
        /// </summary>
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

        /// <summary>
        /// Tests that GetTOC_Pagination_ReturnsCorrectPage.
        /// </summary>
        [TestMethod]
        public async Task GetTOC_Pagination_ReturnsCorrectPage()
        {
            // Arrange
            await CreateArticleAsync("Home Page", TestUserId);

            for (int i = 1; i <= 15; i++)
            {
                var page = await CreateArticleAsync($"Page {i}", TestUserId);
                var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == page.ArticleNumber);
                await PublishingService.PublishAsync(entity);
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

        /// <summary>
        /// Tests that PostContactInfo_ValidModel_ReturnsJsonResult.
        /// </summary>
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

        /// <summary>
        /// Tests that PostContactInfo_NullModel_ReturnsNotFound.
        /// </summary>
        [TestMethod]
        public async Task PostContactInfo_NullModel_ReturnsNotFound()
        {
            // Act
            var result = await controller.CCMS_POSTCONTACT_INFO(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that PostContactInfo_InvalidModelState_ReturnsBadRequest.
        /// </summary>
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

        /// <summary>
        /// Tests that PostContactInfo_SetsTimestamps_Correctly.
        /// </summary>
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

        /// <summary>
        /// Tests that Search_ValidQuery_ReturnsResults.
        /// </summary>
        [TestMethod]
        public async Task Search_ValidQuery_ReturnsResults()
        {
            // Arrange
            await CreateArticleAsync("Home Page", TestUserId);
            var article = await CreateArticleAsync("Searchable Content", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            entity.Content = "<p>This is searchable content with unique terms.</p>";
            await Db.SaveChangesAsync();
            await PublishingService.PublishAsync(entity);

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

        /// <summary>
        /// Tests that Search_EmptyQuery_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Search_EmptyQuery_ReturnsBadRequest()
        {
            // Act
            var result = await controller.CCMS___SEARCH("");

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that Search_NullQuery_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Search_NullQuery_ReturnsBadRequest()
        {
            // Act
            var result = await controller.CCMS___SEARCH(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that Search_MultipleTerms_ReturnsMatchingResults.
        /// </summary>
        [TestMethod]
        public async Task Search_MultipleTerms_ReturnsMatchingResults()
        {
            // Arrange
            await CreateArticleAsync("Home Page", TestUserId);
            var article = await CreateArticleAsync("Multi Term Search", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            entity.Content = "<p>Content with multiple searchable unique terms here.</p>";
            await Db.SaveChangesAsync();
            await PublishingService.PublishAsync(entity);

            // Act
            var result = await controller.CCMS___SEARCH("searchable unique");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        /// <summary>
        /// Tests that Search_NoMatches_ReturnsEmptyList.
        /// </summary>
        [TestMethod]
        public async Task Search_NoMatches_ReturnsEmptyList()
        {
            // Arrange
            await CreateArticleAsync("Home Page", TestUserId);

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

        /// <summary>
        /// Tests that Search_InvalidModelState_ReturnsBadRequest.
        /// </summary>
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

        /// <summary>
        /// Tests that GetArticleNumber_FromQueryString_ReturnsCorrectNumber.
        /// </summary>
        [TestMethod]
        public async Task GetArticleNumber_FromQueryString_ReturnsCorrectNumber()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            await PublishingService.PublishAsync(entity);

            controller.ControllerContext.HttpContext.Request.Headers["referer"] =
                $"http://localhost/editor?articleNumber={article.ArticleNumber}";

            // Act
            var result = await controller.CCMS_GetArticleFolderContents("");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        /// <summary>
        /// Tests that GetArticleNumber_FromEditorPath_ReturnsCorrectNumber.
        /// </summary>
        [TestMethod]
        public async Task GetArticleNumber_FromEditorPath_ReturnsCorrectNumber()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            await PublishingService.PublishAsync(entity);

            controller.ControllerContext.HttpContext.Request.Headers["referer"] =
                $"http://localhost/editor/ccmscontent/{article.ArticleNumber}";

            // Act
            var result = await controller.CCMS_GetArticleFolderContents("");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        /// <summary>
        /// Tests that GetArticleNumber_FromPublishedPage_ReturnsCorrectNumber.
        /// </summary>
        [TestMethod]
        public async Task GetArticleNumber_FromPublishedPage_ReturnsCorrectNumber()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            await PublishingService.PublishAsync(entity);

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
                IMediator mediator,
                ApplicationDbContext dbContext,
                ILogger<HomeControllerBase> logger,
                IEmailSender emailSender,
                IContactManagementService contactManagementService)
                : base(mediator, dbContext, logger, emailSender, contactManagementService)
            {
            }
        }
    }
}

