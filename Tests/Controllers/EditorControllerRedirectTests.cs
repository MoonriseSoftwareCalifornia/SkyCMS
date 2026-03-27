// <copyright file="EditorControllerRedirectTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for EditorController redirect management functionality.
    /// Covers Redirects (GET), RedirectDelete, and RedirectEdit methods.
    /// </summary>
    [TestClass]
    public class EditorControllerRedirectTests : SkyCmsTestBase
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
                LayoutCacheService,
                DynamicConfigurationProvider);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "editor@example.com"),
                new Claim(ClaimTypes.Role, "Editors")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        #region Redirects GET Tests

        /// <summary>
        /// Tests that Redirects returns view with redirect list.
        /// </summary>
        [TestMethod]
        public async Task Redirects_Get_ReturnsViewWithRedirectList()
        {
            // Act
            var result = await controller.Redirects(
                sortOrder: "asc",
                currentSort: "FromUrl",
                pageNo: 0,
                pageSize: 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
        }

        /// <summary>
        /// Tests that Redirects handles sorting by FromUrl ascending.
        /// </summary>
        [TestMethod]
        public async Task Redirects_Get_SortsByFromUrlAscending()
        {
            // Act
            var result = await controller.Redirects(
                sortOrder: "asc",
                currentSort: "FromUrl");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual("asc", controller.ViewData["sortOrder"]);
            Assert.AreEqual("FromUrl", controller.ViewData["currentSort"]);
        }

        /// <summary>
        /// Tests that Redirects handles sorting by ToUrl descending.
        /// </summary>
        [TestMethod]
        public async Task Redirects_Get_SortsByToUrlDescending()
        {
            // Act
            var result = await controller.Redirects(
                sortOrder: "desc",
                currentSort: "ToUrl");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual("desc", controller.ViewData["sortOrder"]);
            Assert.AreEqual("ToUrl", controller.ViewData["currentSort"]);
        }

        /// <summary>
        /// Tests that Redirects handles paging parameters.
        /// </summary>
        [TestMethod]
        public async Task Redirects_Get_HandlesPaging()
        {
            // Act
            var result = await controller.Redirects(
                sortOrder: "asc",
                currentSort: "FromUrl",
                pageNo: 2,
                pageSize: 5);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual(2, controller.ViewData["pageNo"]);
            Assert.AreEqual(5, controller.ViewData["pageSize"]);
        }

        #endregion

        #region RedirectDelete Tests

        /// <summary>
        /// Tests that RedirectDelete deletes a redirect article.
        /// </summary>
        [TestMethod]
        public async Task RedirectDelete_DeletesRedirectArticle()
        {
            // Arrange - Create a redirect article
            var redirect = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = await GetNextArticleNumber(),
                Title = "Test Redirect",
                UrlPath = "/old-url",
                Content = "/new-url",
                StatusCode = (int)StatusCodeEnum.Redirect,
                VersionNumber = 1,
                Updated = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(redirect);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.RedirectDelete(redirect.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Redirects", redirectResult.ActionName);

            // Verify redirect was deleted (marked as deleted)
            var deletedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.Id == redirect.Id);
            Assert.IsNotNull(deletedArticle);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, deletedArticle.StatusCode);
        }

        /// <summary>
        /// Tests that RedirectDelete redirects back to Redirects action.
        /// </summary>
        [TestMethod]
        public async Task RedirectDelete_RedirectsBackToRedirectsAction()
        {
            // Arrange
            var redirect = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = await GetNextArticleNumber(),
                Title = "Test Redirect",
                UrlPath = "/test-redirect",
                Content = "/test-target",
                StatusCode = (int)StatusCodeEnum.Redirect,
                VersionNumber = 1,
                Updated = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(redirect);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.RedirectDelete(redirect.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Redirects", redirectResult.ActionName);
        }

        #endregion

        #region RedirectEdit Tests

        /// <summary>
        /// Tests that RedirectEdit updates redirect URLs.
        /// </summary>
        [TestMethod]
        public async Task RedirectEdit_UpdatesRedirectUrls()
        {
            // Arrange
            var redirect = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = await GetNextArticleNumber(),
                Title = "Edit Test Redirect",
                UrlPath = "/original-from",
                Content = "/original-to",
                StatusCode = (int)StatusCodeEnum.Redirect,
                VersionNumber = 1,
                Updated = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(redirect);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.RedirectEdit(
                redirect.Id,
                fromUrl: "/updated-from",
                toUrl: "/updated-to");

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            // Verify redirect was updated
            var updatedRedirect = await Db.Articles
                .FirstAsync(a => a.Id == redirect.Id);
            Assert.AreEqual("/updated-from", updatedRedirect.UrlPath);
            Assert.AreEqual("/updated-to", updatedRedirect.Content);
        }

        /// <summary>
        /// Tests that RedirectEdit returns NotFound for non-existent redirect.
        /// </summary>
        [TestMethod]
        public async Task RedirectEdit_ReturnsNotFoundForNonExistentRedirect()
        {
            // Arrange - Use non-existent ID
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await controller.RedirectEdit(
                nonExistentId,
                fromUrl: "/test",
                toUrl: "/test2");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that RedirectEdit returns NotFound for non-redirect article.
        /// </summary>
        [TestMethod]
        public async Task RedirectEdit_ReturnsNotFoundForNonRedirectArticle()
        {
            // Arrange - Create regular article (not a redirect)
            var article = await CreateArticleAsync("Regular Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var dbArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act - Try to edit it as redirect
            var result = await controller.RedirectEdit(
                dbArticle.Id,
                fromUrl: "/test",
                toUrl: "/test2");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that RedirectEdit redirects back to Redirects action.
        /// </summary>
        [TestMethod]
        public async Task RedirectEdit_RedirectsBackToRedirectsAction()
        {
            // Arrange
            var redirect = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = await GetNextArticleNumber(),
                Title = "Test Redirect",
                UrlPath = "/from",
                Content = "/to",
                StatusCode = (int)StatusCodeEnum.Redirect,
                VersionNumber = 1,
                Updated = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(redirect);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.RedirectEdit(
                redirect.Id,
                fromUrl: "/new-from",
                toUrl: "/new-to");

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Redirects", redirectResult.ActionName);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the next available article number.
        /// </summary>
        /// <returns>Next article number.</returns>
        private async Task<int> GetNextArticleNumber()
        {
            var maxNumber = await Db.Articles
                .Select(a => (int?)a.ArticleNumber)
                .MaxAsync();
            return (maxNumber ?? 0) + 1;
        }

        #endregion
    }
}
