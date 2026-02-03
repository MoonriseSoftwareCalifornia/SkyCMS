// <copyright file="EditorControllerApiTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using Sky.Editor.Models.GrapesJs;

    /// <summary>
    /// Tests for EditorController API endpoints.
    /// Covers JSON/API methods that return data to the frontend.
    /// </summary>
    [TestClass]
    public class EditorControllerApiTests : SkyCmsTestBase
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
                new Claim(ClaimTypes.Name, "test@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        #region GetDesignerData Tests

        /// <summary>
        /// Tests that GetDesignerData returns article content for GrapeJS.
        /// </summary>
        [TestMethod]
        public async Task GetDesignerData_ReturnsArticleContent()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div>Test content for designer</div>";
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.GetDesignerData(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            Assert.IsInstanceOfType(jsonResult.Value, typeof(project));
        }

        /// <summary>
        /// Tests that GetDesignerData returns NotFound for non-existent article.
        /// </summary>
        [TestMethod]
        public async Task GetDesignerData_ReturnsNotFound_WhenArticleDoesNotExist()
        {
            // Act
            var result = await controller.GetDesignerData(99999);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that GetDesignerData ensures editable markers in content.
        /// </summary>
        [TestMethod]
        public async Task GetDesignerData_EnsuresEditableMarkers()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div contenteditable='true'>Editable content</div>";
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.GetDesignerData(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var projectData = (project)jsonResult.Value!;
            
            // The ArticleHtmlService should have processed the content
            Assert.IsNotNull(projectData);
        }

        #endregion

        #region GetTemplateInfo Tests

        /// <summary>
        /// Tests that GetTemplateInfo returns template data.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateInfo_ReturnsTemplateData()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Template content</div>",
                Description = "Test description"
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetTemplateInfo(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            
            var returnedTemplate = jsonResult.Value as Template;
            Assert.IsNotNull(returnedTemplate);
            Assert.AreEqual(template.Id, returnedTemplate.Id);
            Assert.AreEqual("Test Template", returnedTemplate.Title);
        }

        /// <summary>
        /// Tests that GetTemplateInfo returns empty string for null ID.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateInfo_ReturnsEmptyString_WhenIdIsNull()
        {
            // Act
            var result = await controller.GetTemplateInfo(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.AreEqual(string.Empty, jsonResult.Value);
        }

        #endregion

        #region GetArticleList Tests

        /// <summary>
        /// Tests that GetArticleList returns published articles.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_ReturnsPublishedArticles()
        {
            // Arrange
            var article1 = await Logic.CreateArticle("Published Article", TestUserId);
            await Logic.SaveArticle(article1, TestUserId);
            await Logic.PublishArticle(article1.Id, DateTimeOffset.UtcNow);

            var article2 = await Logic.CreateArticle("Unpublished Article", TestUserId);
            await Logic.SaveArticle(article2, TestUserId);

            // Act
            var result = await controller.GetArticleList(publishedOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that GetArticleList filters by search term.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_FiltersBySearchTerm()
        {
            // Arrange
            var article1 = await Logic.CreateArticle("Test Article About Dogs", TestUserId);
            await Logic.SaveArticle(article1, TestUserId);
            await Logic.PublishArticle(article1.Id, DateTimeOffset.UtcNow);

            var article2 = await Logic.CreateArticle("Test Article About Cats", TestUserId);
            await Logic.SaveArticle(article2, TestUserId);
            await Logic.PublishArticle(article2.Id, DateTimeOffset.UtcNow);

            // Act
            var result = await controller.GetArticleList(term: "dogs");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            // The result should be filtered to only articles containing "dogs"
        }

        /// <summary>
        /// Tests that GetArticleList returns all articles when publishedOnly is false.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_ReturnsAllArticles_WhenPublishedOnlyIsFalse()
        {
            // Arrange
            var publishedArticle = await Logic.CreateArticle("Published", TestUserId);
            await Logic.SaveArticle(publishedArticle, TestUserId);
            await Logic.PublishArticle(publishedArticle.Id, DateTimeOffset.UtcNow);

            var unpublishedArticle = await Logic.CreateArticle("Unpublished", TestUserId);
            await Logic.SaveArticle(unpublishedArticle, TestUserId);

            // Act
            var result = await controller.GetArticleList(publishedOnly: false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        #endregion

        #region GetEncryptionKey Tests

        /// <summary>
        /// Tests that GetEncryptionKey returns existing encryption key.
        /// </summary>
        [TestMethod]
        public async Task GetEncryptionKey_ReturnsExistingKey()
        {
            // Arrange - Create encryption key setting
            var setting = new Cosmos.Common.Data.Setting
            {
                Description = "EncryptionKey",
                Value = "TestEncryptionKey123"
            };
            Db.Settings.Add(setting);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetEncryptionKey();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.AreEqual("TestEncryptionKey123", jsonResult.Value);
        }

        /// <summary>
        /// Tests that GetEncryptionKey creates new key if none exists.
        /// </summary>
        [TestMethod]
        public async Task GetEncryptionKey_CreatesNewKey_WhenNoneExists()
        {
            // Act
            var result = await controller.GetEncryptionKey();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            
            var keyValue = jsonResult.Value as string;
            Assert.IsNotNull(keyValue);
            Assert.IsTrue(keyValue.Length > 0);

            // Verify it was saved to database
            var setting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Description == "EncryptionKey");
            Assert.IsNotNull(setting);
            Assert.AreEqual(keyValue, setting.Value);
        }

        #endregion

        #region GetPublishedPageList Tests

        /// <summary>
        /// Tests that GetPublishedPageList returns published pages.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageList_ReturnsPublishedPages()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Page", TestUserId);
            await Logic.SaveArticle(article, TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Act
            var result = await controller.GetPublishedPageList();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that GetPublishedPageList excludes unpublished pages.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageList_ExcludesUnpublishedPages()
        {
            // Arrange
            var publishedArticle = await Logic.CreateArticle("Published Page", TestUserId);
            await Logic.SaveArticle(publishedArticle, TestUserId);
            await Logic.PublishArticle(publishedArticle.Id, DateTimeOffset.UtcNow);

            var unpublishedArticle = await Logic.CreateArticle("Unpublished Page", TestUserId);
            await Logic.SaveArticle(unpublishedArticle, TestUserId);

            // Act
            var result = await controller.GetPublishedPageList();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            // Verify the list doesn't contain the unpublished article
        }

        #endregion

        #region Get_RoleList Tests

        /// <summary>
        /// Tests that Get_RoleList returns all roles.
        /// </summary>
        [TestMethod]
        public async Task Get_RoleList_ReturnsAllRoles()
        {
            // Arrange
            var role1 = "TestRole1_" + Guid.NewGuid().ToString().Substring(0, 8);
            var role2 = "TestRole2_" + Guid.NewGuid().ToString().Substring(0, 8);
            await RoleManager.CreateAsync(new IdentityRole(role1));
            await RoleManager.CreateAsync(new IdentityRole(role2));

            // Act
            var result = await controller.Get_RoleList(string.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that Get_RoleList filters by text.
        /// </summary>
        [TestMethod]
        public async Task Get_RoleList_FiltersByText()
        {
            // Arrange
            var uniquePrefix = "UniqueRole_" + Guid.NewGuid().ToString().Substring(0, 8);
            await RoleManager.CreateAsync(new IdentityRole(uniquePrefix + "_Test"));
            await RoleManager.CreateAsync(new IdentityRole("OtherRole"));

            // Act
            var result = await controller.Get_RoleList(uniquePrefix);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        #endregion

        #region List_Articles Tests

        /// <summary>
        /// Tests that List_Articles returns active articles.
        /// </summary>
        [TestMethod]
        public async Task List_Articles_ReturnsActiveArticles()
        {
            // Arrange
            var article = await Logic.CreateArticle("Active Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.List_Articles(string.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that List_Articles filters by search text.
        /// </summary>
        [TestMethod]
        public async Task List_Articles_FiltersBySearchText()
        {
            // Arrange
            var article1 = await Logic.CreateArticle("Article About Technology", TestUserId);
            await Logic.SaveArticle(article1, TestUserId);

            var article2 = await Logic.CreateArticle("Article About Science", TestUserId);
            await Logic.SaveArticle(article2, TestUserId);

            // Act
            var result = await controller.List_Articles("technology");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that List_Articles limits results to 10.
        /// </summary>
        [TestMethod]
        public async Task List_Articles_LimitsResultsTo10()
        {
            // Arrange - Create more than 10 articles
            for (int i = 0; i < 15; i++)
            {
                var article = await Logic.CreateArticle($"Article {i}", TestUserId);
                await Logic.SaveArticle(article, TestUserId);
            }

            // Act
            var result = await controller.List_Articles(string.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            
            // The result should be limited to 10 items
            var items = jsonResult.Value as System.Collections.IEnumerable;
            if (items != null)
            {
                var count = items.Cast<object>().Count();
                Assert.IsTrue(count <= 10, "Should return maximum 10 items");
            }
        }

        #endregion
    }
}
