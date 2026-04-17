// <copyright file="EditorControllerSecurityTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Controllers;
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for EditorController security-related functionality.
    /// Covers permissions management and URL validation for redirect protection.
    /// </summary>
    [TestClass]
    public class EditorControllerSecurityTests : SkyCmsTestBase
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

            // Setup administrator user context
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

            // Setup IUrlHelper for URL validation in PublishPage
            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(x => x.IsLocalUrl(It.IsAny<string>()))
                .Returns((string url) => url != null && url.StartsWith("/"));
            controller.Url = urlHelper.Object;
        }

        #region Permissions GET Tests

        /// <summary>
        /// Tests that Permissions GET returns view with article permissions.
        /// </summary>
        [TestMethod]
        public async Task Permissions_Get_ReturnsViewWithPermissions()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // Act
            var result = await controller.Permissions(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
        }

        /// <summary>
        /// Tests that Permissions GET handles for roles correctly.
        /// </summary>
        [TestMethod]
        public async Task Permissions_Get_WithForRoles_ReturnsRolesList()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // Ensure we have at least one role
            var roleName = "TestRole_" + Guid.NewGuid().ToString().Substring(0, 8);
            await RoleManager.CreateAsync(new IdentityRole(roleName));

            // Act
            var result = await controller.Permissions(article.ArticleNumber, forRoles: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsTrue((bool)controller.ViewData["showingRoles"]!);
        }

        /// <summary>
        /// Tests that Permissions GET handles for users correctly.
        /// </summary>
        [TestMethod]
        public async Task Permissions_Get_WithForUsers_ReturnsUsersList()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // Act
            var result = await controller.Permissions(article.ArticleNumber, forRoles: false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsFalse((bool)controller.ViewData["showingRoles"]!);
        }

        /// <summary>
        /// Tests that Permissions GET handles sorting and paging.
        /// </summary>
        [TestMethod]
        public async Task Permissions_Get_HandlesSortingAndPaging()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // Act
            var result = await controller.Permissions(
                article.ArticleNumber,
                forRoles: true,
                sortOrder: "desc",
                currentSort: "Name",
                pageNo: 0,
                pageSize: 5);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual("desc", controller.ViewData["sortOrder"]);
            Assert.AreEqual("Name", controller.ViewData["currentSort"]);
            Assert.AreEqual(0, controller.ViewData["pageNo"]);
            Assert.AreEqual(5, controller.ViewData["pageSize"]);
        }

        #endregion

        #region Permissions POST Tests

        /// <summary>
        /// Tests that Permissions POST updates article permissions with roles.
        /// </summary>
        [TestMethod]
        public async Task Permissions_Post_UpdatesPermissionsWithRoles()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // Create test roles
            var role1Name = "TestRole1_" + Guid.NewGuid().ToString().Substring(0, 8);
            var role2Name = "TestRole2_" + Guid.NewGuid().ToString().Substring(0, 8);
            var role1 = await RoleManager.CreateAsync(new IdentityRole(role1Name));
            var role2 = await RoleManager.CreateAsync(new IdentityRole(role2Name));

            var role1Id = (await RoleManager.FindByNameAsync(role1Name))!.Id;
            var role2Id = (await RoleManager.FindByNameAsync(role2Name))!.Id;

            var identityObjectIds = new[] { role1Id, role2Id };

            // Act
            var result = await controller.Permissions(article.ArticleNumber, identityObjectIds);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Verify permissions were saved
            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.IsNotNull(catalogEntry.ArticlePermissions);
            Assert.AreEqual(2, catalogEntry.ArticlePermissions.Count);
            Assert.IsTrue(catalogEntry.ArticlePermissions.All(p => p.IsRoleObject));
        }

        /// <summary>
        /// Tests that Permissions POST updates article permissions with users.
        /// </summary>
        [TestMethod]
        public async Task Permissions_Post_UpdatesPermissionsWithUsers()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // Create test user
            var testUser = new IdentityUser
            {
                UserName = "testuser@example.com",
                Email = "testuser@example.com"
            };
            await UserManager.CreateAsync(testUser, "TestPassword123!");
            var userId = (await UserManager.FindByEmailAsync("testuser@example.com"))!.Id;

            var identityObjectIds = new[] { userId };

            // Act
            var result = await controller.Permissions(article.ArticleNumber, identityObjectIds);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Verify permissions were saved
            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.IsNotNull(catalogEntry.ArticlePermissions);
            Assert.AreEqual(1, catalogEntry.ArticlePermissions.Count);
            Assert.IsFalse(catalogEntry.ArticlePermissions.First().IsRoleObject);
        }

        /// <summary>
        /// Tests that Permissions POST clears existing permissions.
        /// </summary>
        [TestMethod]
        public async Task Permissions_Post_ClearsExistingPermissions()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // Create and set initial permissions
            var role1Name = "TestRole1_" + Guid.NewGuid().ToString().Substring(0, 8);
            await RoleManager.CreateAsync(new IdentityRole(role1Name));
            var role1Id = (await RoleManager.FindByNameAsync(role1Name))!.Id;

            await controller.Permissions(article.ArticleNumber, new[] { role1Id });

            // Act - Set new permissions (should clear old ones)
            var role2Name = "TestRole2_" + Guid.NewGuid().ToString().Substring(0, 8);
            await RoleManager.CreateAsync(new IdentityRole(role2Name));
            var role2Id = (await RoleManager.FindByNameAsync(role2Name))!.Id;

            var result = await controller.Permissions(article.ArticleNumber, new[] { role2Id });

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.AreEqual(1, catalogEntry.ArticlePermissions.Count);
            Assert.AreEqual(role2Id, catalogEntry.ArticlePermissions.First().IdentityObjectId);
        }

        /// <summary>
        /// Tests that Permissions POST handles empty permission list.
        /// </summary>
        [TestMethod]
        public async Task Permissions_Post_HandlesEmptyPermissionList()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var identityObjectIds = Array.Empty<string>();

            // Act
            var result = await controller.Permissions(article.ArticleNumber, identityObjectIds);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.AreEqual(0, catalogEntry.ArticlePermissions.Count);
        }

        #endregion

        #region PublishPage URL Validation Tests

        /// <summary>
        /// Tests that PublishPage accepts valid editor URLs.
        /// </summary>
        [TestMethod]
        public async Task PublishPage_AcceptsValidEditorUrl()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act
            var result = await controller.PublishPage(
                dbArticle.Id,
                DateTimeOffset.UtcNow,
                "/Editor/Index");

            // Assert
            Assert.IsInstanceOfType(result, typeof(LocalRedirectResult));
            var redirectResult = (LocalRedirectResult)result;
            Assert.AreEqual("/Editor/Index", redirectResult.Url);
        }

        /// <summary>
        /// Tests that PublishPage accepts valid versions URL.
        /// </summary>
        [TestMethod]
        public async Task PublishPage_AcceptsValidVersionsUrl()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act
            var result = await controller.PublishPage(
                dbArticle.Id,
                DateTimeOffset.UtcNow,
                "/Editor/Versions?id=1");

            // Assert
            Assert.IsInstanceOfType(result, typeof(LocalRedirectResult));
            var redirectResult = (LocalRedirectResult)result;
            Assert.AreEqual("/Editor/Versions?id=1", redirectResult.Url);
        }

        /// <summary>
        /// Tests that PublishPage rejects unauthorized URL paths (open redirect protection).
        /// </summary>
        [TestMethod]
        public async Task PublishPage_RejectsUnauthorizedUrl()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act - Try to redirect to an unauthorized path
            var result = await controller.PublishPage(
                dbArticle.Id,
                DateTimeOffset.UtcNow,
                "/UnauthorizedController/Action");

            // Assert - Should redirect to safe default (Editor/Index)
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Index", redirectResult.ActionName);
            Assert.AreEqual("Editor", redirectResult.ControllerName);
        }

        /// <summary>
        /// Tests that PublishPage rejects external URLs (open redirect protection).
        /// </summary>
        [TestMethod]
        public async Task PublishPage_RejectsExternalUrl()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act - Try to redirect to an external URL
            var result = await controller.PublishPage(
                dbArticle.Id,
                DateTimeOffset.UtcNow,
                "https://evil.com/phishing");

            // Assert - Should redirect to safe default (Editor/Index)
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Index", redirectResult.ActionName);
        }

        /// <summary>
        /// Tests that PublishPage rejects invalid URL format.
        /// </summary>
        [TestMethod]
        public async Task PublishPage_RejectsInvalidUrlFormat()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act - Try with invalid URL format
            var result = await controller.PublishPage(
                dbArticle.Id,
                DateTimeOffset.UtcNow,
                "not a valid url <script>");

            // Assert - Should redirect to safe default
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Index", redirectResult.ActionName);
        }

        /// <summary>
        /// Tests that PublishPage accepts null editorUrl.
        /// </summary>
        [TestMethod]
        public async Task PublishPage_AcceptsNullEditorUrl()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act
            var result = await controller.PublishPage(
                dbArticle.Id,
                DateTimeOffset.UtcNow,
                null!);

            // Assert - Should not throw and redirect to null URL
            Assert.IsInstanceOfType(result, typeof(LocalRedirectResult));
        }

        /// <summary>
        /// Tests that PublishPage accepts templates controller URLs.
        /// </summary>
        [TestMethod]
        public async Task PublishPage_AcceptsTemplatesControllerUrl()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act
            var result = await controller.PublishPage(
                dbArticle.Id,
                DateTimeOffset.UtcNow,
                "/Templates/EditCode");

            // Assert
            Assert.IsInstanceOfType(result, typeof(LocalRedirectResult));
            var redirectResult = (LocalRedirectResult)result;
            Assert.AreEqual("/Templates/EditCode", redirectResult.Url);
        }

        #endregion
    }
}
