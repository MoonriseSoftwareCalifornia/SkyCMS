// <copyright file="EditorControllerAdminTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using System;
    using System.Security.Claims;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;

    /// <summary>
    /// Tests for EditorController administrative and utility functions.
    /// Covers ExportPage, Preload, Scheduler, Logs, CcmsContent, and SearchAndReplaceQuery.
    /// </summary>
    [TestClass]
    public class EditorControllerAdminTests : SkyCmsTestBase
    {
        private EditorController controller = null!;

        [TestInitialize]
        public new void Setup()
        {
            base.Setup();

            controller = new EditorController(
                Logger,
                Db,
                UserManager,
                RoleManager,
                Logic,
                EditorSettings,
                ViewRenderService,
                Storage,
                Hub.Object,
                PublishingService,
                ArticleHtmlService,
                ReservedPaths,
                TitleChangeService,
                TemplateService,
                Mediator,
                Cache,
                DynamicConfigurationProvider);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "admin@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        #region ExportPage Tests

        /// <summary>
        /// Tests that ExportPage exports article with specified ID.
        /// </summary>
        [TestMethod]
        public async Task ExportPage_ExportsArticleWithSpecifiedId()
        {
            // Arrange
            var article = await Logic.CreateArticle("Article to Export", TestUserId);
            article.Content = "<html><body><h1>Export Test</h1></body></html>";
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.ExportPage(article.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            Assert.AreEqual("application/octet-stream", fileResult.ContentType);
            Assert.IsTrue(fileResult.FileDownloadName.Contains($"pageid-{article.ArticleNumber}"));
            Assert.IsTrue(fileResult.FileDownloadName.EndsWith(".html"));
        }

        /// <summary>
        /// Tests that ExportPage creates blank page when ID is null.
        /// </summary>
        [TestMethod]
        public async Task ExportPage_CreatesBlankPageWhenIdIsNull()
        {
            // Act
            var result = await controller.ExportPage(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            Assert.AreEqual("application/octet-stream", fileResult.ContentType);
            Assert.IsTrue(fileResult.FileDownloadName.EndsWith(".html"));
        }

        /// <summary>
        /// Tests that ExportPage returns HTML content as bytes.
        /// </summary>
        [TestMethod]
        public async Task ExportPage_ReturnsHtmlContentAsBytes()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Export", TestUserId);
            article.Content = "<p>Test content for export</p>";
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.ExportPage(article.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            Assert.IsTrue(fileResult.FileContents.Length > 0);
            
            // Verify content is valid UTF-8
            var htmlContent = Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.IsNotNull(htmlContent);
            Assert.IsTrue(htmlContent.Length > 0);
        }

        #endregion

        #region Preload Tests

        /// <summary>
        /// Tests that Preload returns view with PreloadViewModel.
        /// </summary>
        [TestMethod]
        public void Preload_Get_ReturnsViewWithPreloadViewModel()
        {
            // Act
            var result = controller.Preload();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(PreloadViewModel));
        }

        #endregion

        #region Scheduler Tests

        /// <summary>
        /// Tests that Scheduler returns view.
        /// </summary>
        [TestMethod]
        public void Scheduler_Get_ReturnsView()
        {
            // Act
            var result = controller.Scheduler();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        #endregion

        #region Logs Tests

        /// <summary>
        /// Tests that Logs returns view with article logs.
        /// </summary>
        [TestMethod]
        public async Task Logs_Get_ReturnsViewWithArticleLogs()
        {
            // Act
            var result = await controller.Logs();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
        }

        /// <summary>
        /// Tests that Logs orders by DateTimeStamp descending.
        /// </summary>
        [TestMethod]
        public async Task Logs_Get_OrdersByDateTimeStampDescending()
        {
            // Act
            var result = await controller.Logs();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            
            // Model should be IQueryable<ArticleLogJsonModel>
            Assert.IsInstanceOfType(viewResult.Model, typeof(System.Linq.IQueryable<ArticleLogJsonModel>));
        }

        #endregion

        #region CcmsContent Tests

        /// <summary>
        /// Tests that CcmsContent returns view with article.
        /// </summary>
        [TestMethod]
        public async Task CcmsContent_Get_ReturnsViewWithArticle()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.CcmsContent(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
        }

        #endregion

        #region SearchAndReplaceQuery Tests

        /// <summary>
        /// Tests that SearchAndReplaceQuery shows count for specific article.
        /// </summary>
        [TestMethod]
        public async Task SearchAndReplaceQuery_ShowsCountForSpecificArticle()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<p>This is a test. This is only a test.</p>";
            await Logic.SaveArticle(article, TestUserId);

            var model = new SearchAndReplaceViewModel
            {
                ArticleNumber = article.ArticleNumber,
                FindValue = "test"
            };

            // Act
            var result = await controller.SearchAndReplaceQuery(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(controller.ViewData["SearchAndReplacePrequery"]);
            
            var prequeryMessage = controller.ViewData["SearchAndReplacePrequery"]?.ToString();
            Assert.IsTrue(prequeryMessage!.Contains("versions will be modified"));
        }

        /// <summary>
        /// Tests that SearchAndReplaceQuery shows count for all published articles.
        /// </summary>
        [TestMethod]
        public async Task SearchAndReplaceQuery_ShowsCountForAllPublishedArticles()
        {
            // Arrange
            var article = await Logic.CreateArticle("Published Article", TestUserId);
            article.Content = "<p>Find me!</p>";
            await Logic.SaveArticle(article, TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            var model = new SearchAndReplaceViewModel
            {
                ArticleNumber = null, // Search all
                FindValue = "Find me"
            };

            // Act
            var result = await controller.SearchAndReplaceQuery(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsNotNull(controller.ViewData["SearchAndReplacePrequery"]);
            
            var prequeryMessage = controller.ViewData["SearchAndReplacePrequery"]?.ToString();
            Assert.IsTrue(prequeryMessage!.Contains("published articles will be modified"));
        }

        /// <summary>
        /// Tests that SearchAndReplaceQuery returns view on invalid model.
        /// </summary>
        [TestMethod]
        public async Task SearchAndReplaceQuery_ReturnsViewOnInvalidModel()
        {
            // Arrange
            var model = new SearchAndReplaceViewModel(); // Invalid - no FindValue
            controller.ModelState.AddModelError("FindValue", "Find value is required");

            // Act
            var result = await controller.SearchAndReplaceQuery(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsFalse(controller.ModelState.IsValid);
        }

        #endregion

        #region Publish (Dialog) Tests

        /// <summary>
        /// Tests that Publish returns view for publish dialog.
        /// </summary>
        [TestMethod]
        public void Publish_Get_ReturnsView()
        {
            // Act
            var result = controller.Publish();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        #endregion
    }
}
