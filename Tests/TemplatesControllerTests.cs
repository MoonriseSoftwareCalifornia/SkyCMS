// <copyright file="TemplatesControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.Common.Data;
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
    /// Unit tests for the <see cref="TemplatesController"/> class.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class TemplatesControllerTests : SkyCmsTestBase
    {
        private TemplatesController _controller;
        private Mock<UserManager<IdentityUser>> _mockUserManager;

        /// <summary>
        /// Initializes test context before each test.
        /// </summary>
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);

            // Setup UserManager mock
            var store = new Mock<IUserStore<IdentityUser>>();
            _mockUserManager = new Mock<UserManager<IdentityUser>>(
                store.Object, null, null, null, null, null, null, null, null);

            var testUser = new IdentityUser
            {
                Id = TestUserId.ToString(),
                UserName = "testuser@example.com",
                Email = "testuser@example.com"
            };

            _mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            // Create controller with all dependencies including Mediator
            _controller = new TemplatesController(
                Db,
                _mockUserManager.Object,
                Storage,
                Logic,
                EditorSettings,
                ArticleHtmlService,
                TemplateService,
                Mediator,
                Cache,                           // ? Add memory cache
                DynamicConfigurationProvider);   // ? Add config provider

            // Setup HttpContext for the controller
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Setup TempData (required for controller actions that use it)
            var mockTempDataProvider = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>();
            _controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, mockTempDataProvider.Object);
        }

        /// <summary>
        /// Cleanup after each test.
        /// </summary>
        [TestCleanup]
        public async Task Cleanup()
        {
            await DisposeAsync();
        }

        #region UpdateAllPages Tests

        /// <summary>
        /// Tests that UpdateAllPages updates all pages using the specified template.
        /// </summary>
        [TestMethod]
        public async Task UpdateAllPages_UpdatesAllPagesWithTemplate()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Template Content</div>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Create root article first
            await Logic.CreateArticle("Root", TestUserId);

            // Create two articles using this template
            var article1 = await Logic.CreateArticle("Article 1", TestUserId, template.Id);
            var article2 = await Logic.CreateArticle("Article 2", TestUserId, template.Id);

            // Update catalog entries to reference the template
            var catalog1 = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == article1.ArticleNumber);
            var catalog2 = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == article2.ArticleNumber);
            catalog1.TemplateId = template.Id;
            catalog2.TemplateId = template.Id;
            await Db.SaveChangesAsync();

            // Modify articles to have editable content
            var entity1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article1.ArticleNumber);
            var entity2 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article2.ArticleNumber);
            entity1.Content = "<div data-ccms-ceid='region1'>User Content 1</div>";
            entity2.Content = "<div data-ccms-ceid='region1'>User Content 2</div>";
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Publish(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            // Verify both articles were updated with new versions
            var updatedArticle1 = await Db.Articles
                .Where(a => a.ArticleNumber == article1.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();
            var updatedArticle2 = await Db.Articles
                .Where(a => a.ArticleNumber == article2.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            Assert.Contains("User Content 1", updatedArticle1.Content, "Article 1 should preserve user content");
            Assert.Contains("User Content 2", updatedArticle2.Content, "Article 2 should preserve user content");
            Assert.AreEqual(1, updatedArticle1.VersionNumber, "Article 1 should have version reset to 1");
            Assert.AreEqual(1, updatedArticle2.VersionNumber, "Article 2 should have version reset to 1");
        }

        /// <summary>
        /// Tests that UpdateAllPages handles template not found gracefully.
        /// </summary>
        [TestMethod]
        public async Task UpdateAllPages_WithNonExistentTemplate_DoesNotUpdatePages()
        {
            // Arrange
            var nonExistentTemplateId = Guid.NewGuid();

            // Create root article
            await Logic.CreateArticle("Root", TestUserId);

            var article = await Logic.CreateArticle("Test Article", TestUserId);
            var initialVersionCount = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .CountAsync();

            // Act
            var result = await _controller.Publish(nonExistentTemplateId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            // Verify no new versions were created
            var finalVersionCount = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .CountAsync();

            Assert.AreEqual(initialVersionCount, finalVersionCount, "Should not create new versions when template doesn't exist");
        }

        /// <summary>
        /// Tests that UpdateAllPages handles empty page list.
        /// </summary>
        [TestMethod]
        public async Task UpdateAllPages_WithNoPagesUsingTemplate_CompletesSuccessfully()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Unused Template",
                Content = "<div data-ccms-ceid='region1'>Template Content</div>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Publish(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Pages", redirectResult?.ActionName);
        }

        #endregion

        #region ApplyTemplateChanges Tests

        /// <summary>
        /// Tests that ApplyTemplateChanges adds new editable regions from template.
        /// </summary>
        [TestMethod]
        public async Task ApplyTemplateChanges_AddsNewRegionsFromTemplate()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = @"<div data-ccms-ceid='region1'>Template Content</div>
                           <div data-ccms-ceid='region2'>New Region</div>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            await Logic.CreateArticle("Root", TestUserId);
            var article = await Logic.CreateArticle("Test Article", TestUserId, template.Id);

            // Article has only region1
            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            entity.Content = "<div data-ccms-ceid='region1'>Existing Content</div>";
            await Db.SaveChangesAsync();

            // Update template to have region2
            template.Content = @"<div data-ccms-ceid='region1'>Template Content</div>
                                <div data-ccms-ceid='region2'>Brand New Region</div>";
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.UpdatePage(article.ArticleNumber, template.Id);

            // Assert
            var updatedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            Assert.Contains("data-ccms-ceid='region2'", updatedArticle.Content, "Should add new region");
            Assert.Contains("Brand New Region", updatedArticle.Content, "Should include new region content");
        }

        /// <summary>
        /// Tests that ApplyTemplateChanges handles missing editable regions gracefully.
        /// </summary>
        [TestMethod]
        public async Task ApplyTemplateChanges_HandlesNoEditableRegions()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Static Template",
                Content = "<div>Static Content Only</div>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            await Logic.CreateArticle("Root", TestUserId);
            var article = await Logic.CreateArticle("Test Article", TestUserId, template.Id);

            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            entity.Content = "<div data-ccms-ceid='region1'>User Content</div>";
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.UpdatePage(article.ArticleNumber, template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            var updatedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            Assert.IsNotNull(updatedArticle);
            Assert.AreEqual(1, updatedArticle.VersionNumber);
        }

        /// <summary>
        /// Tests that ApplyTemplateChanges preserves user content in matching regions.
        /// </summary>
        [TestMethod]
        public async Task ApplyTemplateChanges_PreservesUserContentInMatchingRegions()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='main'>Template Main</div>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            await Logic.CreateArticle("Root", TestUserId);
            var article = await Logic.CreateArticle("Test Article", TestUserId, template.Id);

            var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            entity.Content = "<div data-ccms-ceid='main'>User's Important Content</div>";
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.UpdatePage(article.ArticleNumber, template.Id);

            // Assert
            var updatedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            Assert.Contains("User's Important Content", updatedArticle.Content, "Should preserve user content in matching editable region");
        }

        /// <summary>
        /// Tests that ApplyTemplateChanges handles template not found.
        /// </summary>
        [TestMethod]
        public async Task ApplyTemplateChanges_WithNonExistentTemplate_ReturnsNotFound()
        {
            // Arrange
            await Logic.CreateArticle("Root", TestUserId);
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Act
            var result = await _controller.UpdatePage(article.ArticleNumber, Guid.NewGuid());

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        /// <summary>
        /// Tests that UpdatePage redirects to editor after successful update.
        /// </summary>
        [TestMethod]
        public async Task UpdatePage_RedirectsToEditorAfterSuccess()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Template Content</div>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            await Logic.CreateArticle("Root", TestUserId);
            var article = await Logic.CreateArticle("Test Article", TestUserId, template.Id);

            // Act
            var result = await _controller.UpdatePage(article.ArticleNumber, template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Edit", redirectResult?.ActionName);
            Assert.AreEqual("Editor", redirectResult?.ControllerName);
            Assert.AreEqual(article.ArticleNumber, redirectResult?.RouteValues?["id"]);
        }

        #endregion

        #region Edge Cases

        /// <summary>
        /// Tests that UpdateAllPages handles concurrent updates gracefully.
        /// </summary>
        [TestMethod]
        public async Task UpdateAllPages_HandlesConcurrentUpdates()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Template Content</div>",
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            await Logic.CreateArticle("Root", TestUserId);
            var article = await Logic.CreateArticle("Test Article", TestUserId, template.Id);

            var catalog = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == article.ArticleNumber);
            catalog.TemplateId = template.Id;
            await Db.SaveChangesAsync();

            // Act - No exception should be thrown
            var result = await _controller.Publish(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
        }

        /// <summary>
        /// Tests that ApplyTemplateChanges handles invalid HTML gracefully.
        /// </summary>
        [TestMethod]
        public async Task ApplyTemplateChanges_HandlesInvalidHtml()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Template",  // Malformed HTML
                LayoutId = Db.Layouts.First().Id
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            await Logic.CreateArticle("Root", TestUserId);
            var article = await Logic.CreateArticle("Test Article", TestUserId, template.Id);

            // Act - Should not throw exception
            var result = await _controller.UpdatePage(article.ArticleNumber, template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
        }

        #endregion
    }
}
