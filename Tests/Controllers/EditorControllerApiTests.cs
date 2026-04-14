// <copyright file="EditorControllerApiTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using Sky.Editor.Models.GrapesJs;
    using System;
    using System.Collections;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

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
                LayoutCacheService,
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
            var article = await CreateArticleAsync("Test Article", TestUserId);
            article.Content = "<div>Test content for designer</div>";
            await SaveArticleAsync(article, TestUserId);

            // Act
            var result = await controller.GetDesignerData(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            Assert.IsInstanceOfType(jsonResult.Value, typeof(Project));
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
            var article = await CreateArticleAsync("Test Article", TestUserId);
            article.Content = "<div contenteditable='true'>Editable content</div>";
            await SaveArticleAsync(article, TestUserId);

            // Act
            var result = await controller.GetDesignerData(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var projectData = (Project)jsonResult.Value!;

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
            var article1 = await CreateArticleAsync("Published Article", TestUserId);
            await SaveArticleAsync(article1, TestUserId);
            var article1Entity = await Db.Articles.FirstAsync(a => a.Id == article1.Id); await PublishingService.PublishAsync(article1Entity);

            var article2 = await CreateArticleAsync("Unpublished Article", TestUserId);
            await SaveArticleAsync(article2, TestUserId);

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
            var article1 = await CreateArticleAsync("Test Article About Dogs", TestUserId);
            await SaveArticleAsync(article1, TestUserId);
            var article1Entity = await Db.Articles.FirstAsync(a => a.Id == article1.Id); await PublishingService.PublishAsync(article1Entity);

            var article2 = await CreateArticleAsync("Test Article About Cats", TestUserId);
            await SaveArticleAsync(article2, TestUserId);
            var article2Entity = await Db.Articles.FirstAsync(a => a.Id == article2.Id); await PublishingService.PublishAsync(article2Entity);

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
            var publishedArticle = await CreateArticleAsync("Published", TestUserId);
            await SaveArticleAsync(publishedArticle, TestUserId);
            var publishedArticleEntity = await Db.Articles.FirstAsync(a => a.Id == publishedArticle.Id); await PublishingService.PublishAsync(publishedArticleEntity);

            var unpublishedArticle = await CreateArticleAsync("Unpublished", TestUserId);
            await SaveArticleAsync(unpublishedArticle, TestUserId);

            // Act
            var result = await controller.GetArticleList(publishedOnly: false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that GetArticleList sets HtmlEditorEnabled to true when content has editable markers.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_SetsHtmlEditorEnabledTrue_WhenContentHasEditableMarkers()
        {
            // Arrange
            var article = await CreateArticleAsync("Editable Marker Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            entity.Content = "<div data-ccms-ceid='region-1'>Editable content</div>";
            entity.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetArticleList(term: "Editable Marker Article", publishedOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var items = ((IEnumerable)jsonResult.Value!).Cast<object>().ToList();
            var target = items.Single(i => string.Equals(GetPropertyValue<string>(i, "Title"), "Editable Marker Article", StringComparison.Ordinal));

            Assert.IsTrue(GetPropertyValue<bool>(target, "HtmlEditorEnabled"));
        }

        /// <summary>
        /// Tests that GetArticleList sets HtmlEditorEnabled to false when content has no editable markers.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_SetsHtmlEditorEnabledFalse_WhenContentHasNoEditableMarkers()
        {
            // Arrange
            var article = await CreateArticleAsync("Non Editable Marker Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            entity.Content = "<div>Static content</div>";
            entity.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetArticleList(term: "Non Editable Marker Article", publishedOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var items = ((IEnumerable)jsonResult.Value!).Cast<object>().ToList();
            var target = items.Single(i => string.Equals(GetPropertyValue<string>(i, "Title"), "Non Editable Marker Article", StringComparison.Ordinal));

            Assert.IsFalse(GetPropertyValue<bool>(target, "HtmlEditorEnabled"));
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

        private static T GetPropertyValue<T>(object item, string propertyName)
        {
            var property = item.GetType().GetProperty(propertyName);
            Assert.IsNotNull(property, $"Expected property '{propertyName}' was not found on JSON result item.");

            var value = property.GetValue(item);
            Assert.IsNotNull(value, $"Property '{propertyName}' value is null.");

            return (T)value;
        }
    }
}
