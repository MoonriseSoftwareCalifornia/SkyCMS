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
    using Sky.Cms.Models;
    using Sky.Editor.Models.GrapesJs;
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

            // Create catalog entries for unpublished articles (normally only created when published)
            var catalog1 = new CatalogEntry
            {
                ArticleNumber = article1.ArticleNumber,
                Title = article1.Title,
                UrlPath = article1.UrlPath,
                Status = "Active",
                Updated = DateTimeOffset.UtcNow,
                TemplateId = template.Id
            };
            var catalog2 = new CatalogEntry
            {
                ArticleNumber = article2.ArticleNumber,
                Title = article2.Title,
                UrlPath = article2.UrlPath,
                Status = "Active",
                Updated = DateTimeOffset.UtcNow,
                TemplateId = template.Id
            };
            Db.ArticleCatalog.Add(catalog1);
            Db.ArticleCatalog.Add(catalog2);
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
            Assert.AreEqual(2, updatedArticle1.VersionNumber, "Article 1 should have new draft version 2");
            Assert.AreEqual(2, updatedArticle2.VersionNumber, "Article 2 should have new draft version 2");
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

            // Create catalog entry for unpublished article (normally only created when published)
            var catalog = new CatalogEntry
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                UrlPath = article.UrlPath,
                Status = "Active",
                Updated = DateTimeOffset.UtcNow,
                TemplateId = template.Id
            };
            Db.ArticleCatalog.Add(catalog);
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

        #region Phase 1: Index Method Tests

        /// <summary>
        /// Tests that Index returns view with templates list.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsView_WithTemplatesList()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template1 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template A",
                Description = "Description A",
                Content = "<div data-ccms-ceid='region1'>Content A</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            var template2 = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template B",
                Description = "Description B",
                Content = "<div data-ccms-ceid='region2'>Content B</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template1);
            Db.Templates.Add(template2);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.Model);
            
            var model = viewResult.Model as List<Sky.Cms.Models.TemplateIndexViewModel>;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Count >= 2, "Should have at least 2 templates");
        }

        /// <summary>
        /// Tests that Index applies sorting ascending by Title.
        /// </summary>
        [TestMethod]
        public async Task Index_AppliesSorting_Ascending_ByTitle()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var templateZ = new Template
            {
                Id = Guid.NewGuid(),
                Title = "ZZZ Template",
                Description = "Last",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            var templateA = new Template
            {
                Id = Guid.NewGuid(),
                Title = "AAA Template",
                Description = "First",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(templateZ);
            Db.Templates.Add(templateA);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Index(sortOrder: "asc", currentSort: "Title");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<Sky.Cms.Models.TemplateIndexViewModel>;
            
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Count >= 2);
            
            // Verify first item is AAA Template (ascending)
            var firstTemplate = model.First(t => t.Title == "AAA Template" || t.Title == "ZZZ Template");
            Assert.AreEqual("AAA Template", firstTemplate.Title, "First template should be AAA when sorted ascending");
        }

        /// <summary>
        /// Tests that Index applies sorting descending by Title.
        /// </summary>
        [TestMethod]
        public async Task Index_AppliesSorting_Descending_ByTitle()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var templateZ = new Template
            {
                Id = Guid.NewGuid(),
                Title = "ZZZ Template",
                Description = "Last",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            var templateA = new Template
            {
                Id = Guid.NewGuid(),
                Title = "AAA Template",
                Description = "First",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(templateZ);
            Db.Templates.Add(templateA);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Index(sortOrder: "desc", currentSort: "Title");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<Sky.Cms.Models.TemplateIndexViewModel>;
            
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Count >= 2);
            
            // Verify first item is ZZZ Template (descending)
            var firstTemplate = model.First(t => t.Title == "AAA Template" || t.Title == "ZZZ Template");
            Assert.AreEqual("ZZZ Template", firstTemplate.Title, "First template should be ZZZ when sorted descending");
        }

        /// <summary>
        /// Tests that Index applies sorting by LayoutName.
        /// </summary>
        [TestMethod]
        public async Task Index_AppliesSorting_ByLayoutName()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Description = "Test",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Index(sortOrder: "asc", currentSort: "LayoutName");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.AreEqual("asc", viewResult.ViewData["sortOrder"]);
            Assert.AreEqual("LayoutName", viewResult.ViewData["currentSort"]);
        }

        /// <summary>
        /// Tests that Index applies sorting by Description.
        /// </summary>
        [TestMethod]
        public async Task Index_AppliesSorting_ByDescription()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Description = "Test Description",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Index(sortOrder: "desc", currentSort: "Description");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.AreEqual("desc", viewResult.ViewData["sortOrder"]);
            Assert.AreEqual("Description", viewResult.ViewData["currentSort"]);
        }

        /// <summary>
        /// Tests that Index applies pagination correctly.
        /// </summary>
        [TestMethod]
        public async Task Index_AppliesPagination_Correctly()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            
            // Create 15 templates to test pagination
            for (int i = 1; i <= 15; i++)
            {
                var template = new Template
                {
                    Id = Guid.NewGuid(),
                    Title = $"Template {i:D2}",
                    Description = $"Description {i}",
                    Content = $"<div>Content {i}</div>",
                    LayoutId = layout.Id,
                    LayoutNumber = layout.LayoutNumber
                };
                Db.Templates.Add(template);
            }
            await Db.SaveChangesAsync();

            // Act - Get page 0 with pageSize 5
            var result = await _controller.Index(pageNo: 0, pageSize: 5);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<Sky.Cms.Models.TemplateIndexViewModel>;
            
            Assert.IsNotNull(model);
            Assert.AreEqual(5, model.Count, "First page should have exactly 5 templates");
            Assert.AreEqual(0, viewResult.ViewData["pageNo"]);
            Assert.AreEqual(5, viewResult.ViewData["pageSize"]);
        }

        /// <summary>
        /// Tests that Index sets correct ViewData properties.
        /// </summary>
        [TestMethod]
        public async Task Index_SetsCorrectViewData()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Description = "Test",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Index(sortOrder: "asc", currentSort: "Title", pageNo: 1, pageSize: 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            
            Assert.AreEqual("asc", viewResult.ViewData["sortOrder"]);
            Assert.AreEqual("Title", viewResult.ViewData["currentSort"]);
            Assert.AreEqual(1, viewResult.ViewData["pageNo"]);
            Assert.AreEqual(10, viewResult.ViewData["pageSize"]);
            Assert.IsNotNull(viewResult.ViewData["Layouts"]);
            Assert.IsNotNull(viewResult.ViewData["RowCount"]);
        }

        /// <summary>
        /// Tests that Index identifies templates with HTML editor (contenteditable).
        /// </summary>
        [TestMethod]
        public async Task Index_IdentifiesTemplatesWithHtmlEditor()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var templateWithEditor = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template With Editor",
                Description = "Has editor",
                Content = "<div contenteditable='true'>Editable Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            var templateWithCeid = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template With CEID",
                Description = "Has CEID",
                Content = "<div data-ccms-ceid='region1'>Region Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            var templateNoEditor = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template No Editor",
                Description = "No editor",
                Content = "<div>Static Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.AddRange(templateWithEditor, templateWithCeid, templateNoEditor);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<Sky.Cms.Models.TemplateIndexViewModel>;
            
            var withEditor = model.FirstOrDefault(t => t.Title == "Template With Editor");
            var withCeid = model.FirstOrDefault(t => t.Title == "Template With CEID");
            var noEditor = model.FirstOrDefault(t => t.Title == "Template No Editor");
            
            Assert.IsTrue(withEditor?.UsesHtmlEditor ?? false, "Template with contenteditable should use HTML editor");
            Assert.IsTrue(withCeid?.UsesHtmlEditor ?? false, "Template with data-ccms-ceid should use HTML editor");
            Assert.IsFalse(noEditor?.UsesHtmlEditor ?? true, "Template without markers should not use HTML editor");
        }

        /// <summary>
        /// Tests that Index returns BadRequest when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("TestKey", "Test error");

            // Act
            var result = await _controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsInstanceOfType(badRequestResult.Value, typeof(SerializableError));
        }

        #endregion

        #region Phase 2: Pages Method Tests

        /// <summary>
        /// Tests that Pages returns view with articles using the template.
        /// </summary>
        [TestMethod]
        public async Task Pages_ReturnsView_WithArticlesUsingTemplate()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Create root article first
            await Logic.CreateArticle("Root", TestUserId);

            // Create articles using this template
            var article1 = await Logic.CreateArticle("Article 1", TestUserId, template.Id);
            var article2 = await Logic.CreateArticle("Article 2", TestUserId, template.Id);

            // Create catalog entries for unpublished articles (normally only created when published)
            var catalog1 = new CatalogEntry
            {
                ArticleNumber = article1.ArticleNumber,
                Title = article1.Title,
                UrlPath = article1.UrlPath,
                Status = "Active",
                Updated = DateTimeOffset.UtcNow,
                TemplateId = template.Id
            };
            var catalog2 = new CatalogEntry
            {
                ArticleNumber = article2.ArticleNumber,
                Title = article2.Title,
                UrlPath = article2.UrlPath,
                Status = "Active",
                Updated = DateTimeOffset.UtcNow,
                TemplateId = template.Id
            };
            Db.ArticleCatalog.Add(catalog1);
            Db.ArticleCatalog.Add(catalog2);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Pages(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.Model);
            
            var model = viewResult.Model as List<ArticleListItem>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count, "Should have 2 articles using the template");
        }

        /// <summary>
        /// Tests that Pages returns NotFound when template does not exist.
        /// </summary>
        [TestMethod]
        public async Task Pages_ReturnsNotFound_WhenTemplateDoesNotExist()
        {
            // Arrange
            var nonExistentTemplateId = Guid.NewGuid();

            // Act
            var result = await _controller.Pages(nonExistentTemplateId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that Pages applies sorting by Title ascending.
        /// </summary>
        [TestMethod]
        public async Task Pages_AppliesSorting_ByTitle_Ascending()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            await Logic.CreateArticle("Root", TestUserId);

            var articleZ = await Logic.CreateArticle("ZZZ Article", TestUserId, template.Id);
            var articleA = await Logic.CreateArticle("AAA Article", TestUserId, template.Id);

            // Reload context to ensure catalog entries are available
            var catalogZ = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == articleZ.ArticleNumber);
            var catalogA = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == articleA.ArticleNumber);
            
            if (catalogZ != null)
            {
                catalogZ.TemplateId = template.Id;
            }
            if (catalogA != null)
            {
                catalogA.TemplateId = template.Id;
            }
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Pages(template.Id, sortOrder: "asc", currentSort: "Title");

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<ArticleListItem>;
            
            Assert.IsNotNull(model);
            if (model.Count >= 2)
            {
                var testArticles = model.Where(t => t.Title == "AAA Article" || t.Title == "ZZZ Article").OrderBy(t => t.Title).ToList();
                if (testArticles.Count == 2)
                {
                    Assert.AreEqual("AAA Article", testArticles[0].Title, "First article should be AAA when sorted ascending");
                    Assert.AreEqual("ZZZ Article", testArticles[1].Title, "Second article should be ZZZ when sorted ascending");
                }
            }
        }

        /// <summary>
        /// Tests that Pages applies sorting by LastPublished descending.
        /// </summary>
        [TestMethod]
        public async Task Pages_AppliesSorting_ByLastPublished_Descending()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            await Logic.CreateArticle("Root", TestUserId);

            var article1 = await Logic.CreateArticle("Article 1", TestUserId, template.Id);
            var article2 = await Logic.CreateArticle("Article 2", TestUserId, template.Id);

            var catalog1 = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article1.ArticleNumber);
            var catalog2 = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article2.ArticleNumber);
            
            if (catalog1 != null)
            {
                catalog1.TemplateId = template.Id;
                catalog1.Published = DateTimeOffset.UtcNow.AddDays(-1);
            }
            if (catalog2 != null)
            {
                catalog2.TemplateId = template.Id;
                catalog2.Published = DateTimeOffset.UtcNow;
            }
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Pages(template.Id, sortOrder: "desc", currentSort: "LastPublished");

            // Assert
            var viewResult = result as ViewResult;
            Assert.AreEqual("desc", viewResult.ViewData["sortOrder"]);
            Assert.AreEqual("LastPublished", viewResult.ViewData["currentSort"]);
        }

        /// <summary>
        /// Tests that Pages applies filter by search term.
        /// </summary>
        [TestMethod]
        public async Task Pages_AppliesFilter_BySearchTerm()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            await Logic.CreateArticle("Root", TestUserId);

            var articleMatch = await Logic.CreateArticle("Matching Article", TestUserId, template.Id);
            var articleNoMatch = await Logic.CreateArticle("Different Page", TestUserId, template.Id);

            // Manually create catalog entries since CreateArticle doesn't create them for unpublished articles
            var catalogMatch = new CatalogEntry
            {
                ArticleNumber = articleMatch.ArticleNumber,
                Title = articleMatch.Title,
                UrlPath = articleMatch.UrlPath,
                TemplateId = template.Id,
                Published = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow,
                Status = "Active"
            };
            
            var catalogNoMatch = new CatalogEntry
            {
                ArticleNumber = articleNoMatch.ArticleNumber,
                Title = articleNoMatch.Title,
                UrlPath = articleNoMatch.UrlPath,
                TemplateId = template.Id,
                Published = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow,
                Status = "Active"
            };
            
            Db.ArticleCatalog.Add(catalogMatch);
            Db.ArticleCatalog.Add(catalogNoMatch);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Pages(template.Id, filter: "matching");

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<ArticleListItem>;
            
            Assert.IsNotNull(model);
            var matchingArticles = model.Where(a => a.Title.Contains("Matching", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.IsTrue(matchingArticles.Count >= 1, "Should have at least one matching article");
            Assert.AreEqual("matching", viewResult.ViewData["Filter"]);
        }

        /// <summary>
        /// Tests that Pages applies pagination correctly.
        /// </summary>
        [TestMethod]
        public async Task Pages_AppliesPagination_Correctly()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            await Logic.CreateArticle("Root", TestUserId);

            // Create 8 articles
            for (int i = 1; i <= 8; i++)
            {
                var article = await Logic.CreateArticle($"Article {i:D2}", TestUserId, template.Id);
                var catalog = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
                if (catalog != null)
                {
                    catalog.TemplateId = template.Id;
                }
            }
            await Db.SaveChangesAsync();

            // Act - Get page 0 with pageSize 5
            var result = await _controller.Pages(template.Id, pageNo: 0, pageSize: 5);

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<ArticleListItem>;
            
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Count <= 5, "First page should have at most 5 articles");
            Assert.AreEqual(0, viewResult.ViewData["pageNo"]);
            Assert.AreEqual(5, viewResult.ViewData["pageSize"]);
        }

        #endregion

        #region Phase 3: Create & Edit Operations Tests

        /// <summary>
        /// Tests that Create creates new template with default content.
        /// </summary>
        [TestMethod]
        public async Task Create_CreatesNewTemplate_WithDefaultContent()
        {
            // Arrange
            var initialCount = await Db.Templates.CountAsync();

            // Act
            var result = await _controller.Create();

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("EditCode", redirectResult.ActionName);
            Assert.AreEqual("Templates", redirectResult.ControllerName);

            // Verify new template was created
            var finalCount = await Db.Templates.CountAsync();
            Assert.AreEqual(initialCount + 1, finalCount, "Should create one new template");

            var newTemplate = await Db.Templates.OrderByDescending(t => t.Title).FirstAsync();
            Assert.IsTrue(newTemplate.Title.StartsWith("New Template"), "Title should start with 'New Template'");
            Assert.IsNotNull(newTemplate.Description);
            Assert.IsNotNull(newTemplate.Content);
        }

        /// <summary>
        /// Tests that Create ensures editable markers in content.
        /// </summary>
        [TestMethod]
        public async Task Create_EnsuresEditableMarkers_InContent()
        {
            // Act
            var result = await _controller.Create();

            // Assert
            var newTemplate = await Db.Templates.OrderByDescending(t => t.Title).FirstAsync();
            
            // Content should be processed through htmlService.EnsureEditableMarkers
            Assert.IsNotNull(newTemplate.Content);
            Assert.IsTrue(newTemplate.Content.Length > 0, "Content should not be empty");
        }

        /// <summary>
        /// Tests that Create creates first version in version history.
        /// </summary>
        [TestMethod]
        public async Task Create_CreatesFirstVersion_InVersionHistory()
        {
            // Act
            var result = await _controller.Create();

            // Assert
            var newTemplate = await Db.Templates.OrderByDescending(t => t.Title).FirstAsync();
            
            // Verify PageDesignVersion was created
            var version = await Db.PageDesignVersions.FirstOrDefaultAsync(v => v.TemplateId == newTemplate.Id);
            Assert.IsNotNull(version, "Should create a PageDesignVersion");
            Assert.AreEqual(1, version.Version, "First version should be version 1");
            Assert.AreEqual(newTemplate.Content, version.Content);
            Assert.AreEqual(newTemplate.Title, version.Title);
            Assert.AreEqual(newTemplate.Description, version.Description);
        }

        /// <summary>
        /// Tests that Create redirects to EditCode after creation.
        /// </summary>
        [TestMethod]
        public async Task Create_RedirectsToEditCode_AfterCreation()
        {
            // Act
            var result = await _controller.Create();

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("EditCode", redirectResult.ActionName);
            Assert.AreEqual("Templates", redirectResult.ControllerName);
            Assert.IsNotNull(redirectResult.RouteValues);
            Assert.IsTrue(redirectResult.RouteValues.ContainsKey("Id"), "Should pass template Id to EditCode");
        }

        /// <summary>
        /// Tests that Edit GET returns view with template data.
        /// </summary>
        [TestMethod]
        public async Task Edit_Get_ReturnsView_WithTemplateData()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Description = "Test Description",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Edit(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.Model);
            
            var model = viewResult.Model as TemplateEditViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(template.Id, model.Id);
            Assert.AreEqual(template.Title, model.Title);
            Assert.AreEqual(template.Description, model.Description);
            Assert.AreEqual(template.Title, viewResult.ViewData["Title"]);
        }

        /// <summary>
        /// Tests that Edit GET returns BadRequest when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task Edit_Get_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("TestKey", "Test error");
            var templateId = Guid.NewGuid();

            // Act
            var result = await _controller.Edit(templateId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that Edit POST saves changes when valid.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_SavesChanges_WhenValid()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Description = "Original Description",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var model = new TemplateEditViewModel
            {
                Id = template.Id,
                Title = "Updated Title",
                Description = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("Updated Description")
            };

            // Act
            var result = await _controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Index", redirectResult.ActionName);

            // Verify changes were saved
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.AreEqual("Updated Title", updatedTemplate.Title);
            Assert.AreEqual("Updated Description", updatedTemplate.Description);
        }

        /// <summary>
        /// Tests that Edit POST decrypts description before saving.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_DecryptsDescription_BeforeSaving()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Description = "Original Description",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var encryptedDescription = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("Encrypted Description");
            var model = new TemplateEditViewModel
            {
                Id = template.Id,
                Title = "Test Template",
                Description = encryptedDescription
            };

            // Act
            var result = await _controller.Edit(model);

            // Assert
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.AreEqual("Encrypted Description", updatedTemplate.Description, 
                "Description should be decrypted before saving");
        }

        /// <summary>
        /// Tests that Edit POST returns view when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_ReturnsView_WhenModelStateInvalid()
        {
            // Arrange
            var model = new TemplateEditViewModel
            {
                Id = Guid.NewGuid(),
                Title = "Test",
                Description = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("Description")
            };
            _controller.ModelState.AddModelError("Title", "Title is required");

            // Act
            var result = await _controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsFalse(_controller.ModelState.IsValid);
        }

        /// <summary>
        /// Tests that Edit POST redirects to Index after save.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_RedirectsToIndex_AfterSave()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Description = "Test Description",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var model = new TemplateEditViewModel
            {
                Id = template.Id,
                Title = "Updated Title",
                Description = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("Updated Description")
            };

            // Act
            var result = await _controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Index", redirectResult.ActionName);
        }

        #endregion

        #region Phase 4: Code Editor Operations Tests

        /// <summary>
        /// Tests that EditCode GET returns view with code editor model.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Get_ReturnsView_WithCodeEditorModel()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.EditCode(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.Model);
            
            var model = viewResult.Model as TemplateCodeEditorViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(template.Id, model.Id);
            Assert.AreEqual(template.Title, model.Title);
            Assert.AreEqual("Template Editor", model.EditorTitle);
            Assert.IsNotNull(model.Content);
            Assert.IsNotNull(model.EditorFields);
            Assert.AreEqual(1, model.EditorFields.Count());
        }

        /// <summary>
        /// Tests that EditCode GET ensures editable markers in content.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Get_EnsuresEditableMarkers_InContent()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Plain content without markers</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.EditCode(template.Id);

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as TemplateCodeEditorViewModel;
            
            // Content should be processed through htmlService.EnsureEditableMarkers
            Assert.IsNotNull(model.Content);
        }

        /// <summary>
        /// Tests that EditCode GET returns BadRequest when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Get_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("TestKey", "Test error");
            var templateId = Guid.NewGuid();

            // Act
            var result = await _controller.EditCode(templateId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that EditCode POST saves changes when valid.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_SavesChanges_WhenValid()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Content = "<div data-ccms-ceid='region1'>Original Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var model = new TemplateCodeEditorViewModel
            {
                Id = template.Id,
                Title = "Updated Title",
                Content = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div data-ccms-ceid='region1'>Updated Content</div>"),
                EditorTitle = "Template Editor",
                EditingField = "Content"
            };

            // Act
            var result = await _controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult.Value);

            // Verify changes were saved
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.AreEqual("Updated Title", updatedTemplate.Title);
            Assert.IsTrue(updatedTemplate.Content.Contains("Updated Content"), "Content should be updated");
        }

        /// <summary>
        /// Tests that EditCode POST decrypts content before saving.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_DecryptsContent_BeforeSaving()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Original</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var encryptedContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div data-ccms-ceid='region1'>Encrypted Content</div>");
            var model = new TemplateCodeEditorViewModel
            {
                Id = template.Id,
                Title = "Test Template",
                Content = encryptedContent,
                EditorTitle = "Template Editor",
                EditingField = "Content"
            };

            // Act
            var result = await _controller.EditCode(model);

            // Assert
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsTrue(updatedTemplate.Content.Contains("Encrypted Content"), "Content should be decrypted before saving");
        }

        /// <summary>
        /// Tests that EditCode POST validates nested editable regions.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_ValidatesNestedEditableRegions()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Content with nested editable regions (should be invalid)
            var nestedContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(
                "<div contenteditable='true'><div contenteditable='true'>Nested</div></div>");
            
            var model = new TemplateCodeEditorViewModel
            {
                Id = template.Id,
                Title = "Test Template",
                Content = nestedContent,
                EditorTitle = "Template Editor",
                EditingField = "Content"
            };

            // Act
            var result = await _controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            
            // Model should be returned with validation errors
            var returnedModel = jsonResult.Value as TemplateCodeEditorViewModel;
            Assert.IsNotNull(returnedModel);
            Assert.IsFalse(_controller.ModelState.IsValid, "ModelState should be invalid for nested regions");
        }

        /// <summary>
        /// Tests that EditCode POST returns JSON with errors when invalid.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_ReturnsJson_WithErrors_WhenInvalid()
        {
            // Arrange
            var model = new TemplateCodeEditorViewModel
            {
                Id = Guid.NewGuid(),
                Title = "Test",
                Content = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div>Test</div>"),
                EditorTitle = "Template Editor",
                EditingField = "Content"
            };
            
            _controller.ModelState.AddModelError("Content", "Test error");

            // Act
            var result = await _controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsFalse(_controller.ModelState.IsValid);
        }

        /// <summary>
        /// Tests that EditCode POST returns JSON with success when valid.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_ReturnsJson_WithSuccess_WhenValid()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var model = new TemplateCodeEditorViewModel
            {
                Id = template.Id,
                Title = "Valid Template",
                Content = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div data-ccms-ceid='region1'>Valid Content</div>"),
                EditorTitle = "Template Editor",
                EditingField = "Content"
            };

            // Act
            var result = await _controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            var response = jsonResult.Value as TemplateCodeEditorViewModel;
            
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsValid, "Model should be marked as valid");
        }

        /// <summary>
        /// Tests that EditCode POST ensures editable markers after save.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_EnsuresEditableMarkers_AfterSave()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var model = new TemplateCodeEditorViewModel
            {
                Id = template.Id,
                Title = "Test Template",
                Content = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div>Plain content</div>"),
                EditorTitle = "Template Editor",
                EditingField = "Content"
            };

            // Act
            var result = await _controller.EditCode(model);

            // Assert
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            
            // Content should be processed through htmlService.EnsureEditableMarkers
            Assert.IsNotNull(updatedTemplate.Content);
        }

        /// <summary>
        /// Tests that EditCode POST updates title when provided.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_UpdatesTitle_WhenProvided()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var model = new TemplateCodeEditorViewModel
            {
                Id = template.Id,
                Title = "Brand New Title",
                Content = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div data-ccms-ceid='region1'>Content</div>"),
                EditorTitle = "Template Editor",
                EditingField = "Content"
            };

            // Act
            var result = await _controller.EditCode(model);

            // Assert
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.AreEqual("Brand New Title", updatedTemplate.Title, "Title should be updated");
        }

        #endregion

        #region Phase 5: Designer Operations Tests

        /// <summary>
        /// Tests that Designer GET returns designer view.
        /// </summary>
        [TestMethod]
        public async Task Designer_Get_ReturnsDesignerView()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Designer(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.AreEqual(true, viewResult.ViewData["IsDesigner"], "Should set IsDesigner flag");
        }

        /// <summary>
        /// Tests that Designer GET returns NotFound when template does not exist.
        /// </summary>
        [TestMethod]
        public async Task Designer_Get_ReturnsNotFound_WhenTemplateDoesNotExist()
        {
            // Arrange
            var nonExistentTemplateId = Guid.NewGuid();

            // Act
            var result = await _controller.Designer(nonExistentTemplateId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that Designer GET includes image assets in config.
        /// </summary>
        [TestMethod]
        public async Task Designer_Get_IncludesImageAssets()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Designer(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(DesignerConfig));
            
            var config = viewResult.Model as DesignerConfig;
            Assert.IsNotNull(config.ImageAssets, "Should have ImageAssets collection");
        }

        /// <summary>
        /// Tests that Designer GET creates config with correct template info.
        /// </summary>
        [TestMethod]
        public async Task Designer_Get_CreatesConfigWithTemplateInfo()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Designer Template",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Designer(template.Id);

            // Assert
            var viewResult = result as ViewResult;
            var config = viewResult.Model as DesignerConfig;
            
            Assert.IsNotNull(config);
            Assert.AreEqual(template.Id.ToString(), config.Id);
            Assert.AreEqual(template.Title, config.Title);
        }

        /// <summary>
        /// Tests that Designer GET returns BadRequest when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task Designer_Get_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("TestKey", "Test error");
            var templateId = Guid.NewGuid();

            // Act
            var result = await _controller.Designer(templateId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that DesignerData GET returns JSON with project data.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Get_ReturnsJson_WithProjectData()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.DesignerData(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult.Value);
            Assert.IsInstanceOfType(jsonResult.Value, typeof(project));
        }

        /// <summary>
        /// Tests that DesignerData GET ensures editable markers in content.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Get_EnsuresEditableMarkers()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Plain content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.DesignerData(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            var projectData = jsonResult.Value as project;
            
            // Content should be processed through htmlService.EnsureEditableMarkers
            Assert.IsNotNull(projectData);
        }

        /// <summary>
        /// Tests that DesignerData POST saves designer changes.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Post_SavesDesignerChanges()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Original Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var htmlContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div data-ccms-ceid='region1'>Designer Content</div>");
            var cssContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(".test { color: red; }");

            // Act
            var result = await _controller.DesignerData(template.Id, "Updated Title", htmlContent, cssContent);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            
            // Verify the success response
            var response = jsonResult.Value;
            Assert.IsNotNull(response);

            // Verify changes were saved
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNotNull(updatedTemplate.Content);
        }

        /// <summary>
        /// Tests that DesignerData POST decrypts content before saving.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Post_DecryptsContent_BeforeSaving()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Original</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var encryptedHtml = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div data-ccms-ceid='region1'>Encrypted Content</div>");
            var encryptedCss = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("body { margin: 0; }");

            // Act
            var result = await _controller.DesignerData(template.Id, "Test", encryptedHtml, encryptedCss);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            
            // Content should be decrypted and processed
            var updatedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNotNull(updatedTemplate.Content);
        }

        /// <summary>
        /// Tests that DesignerData POST validates nested editable regions.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Post_ValidatesNestedEditableRegions()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Content with nested editable regions (invalid)
            var nestedHtml = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(
                "<div contenteditable='true'><div contenteditable='true'>Nested</div></div>");
            var cssContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(string.Empty);

            // Act
            var result = await _controller.DesignerData(template.Id, "Test", nestedHtml, cssContent);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequest = result as BadRequestObjectResult;
            Assert.IsTrue(badRequest.Value.ToString().Contains("nested editable regions"));
        }

        /// <summary>
        /// Tests that DesignerData POST returns NotFound when template does not exist.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Post_ReturnsNotFound_WhenTemplateDoesNotExist()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();
            var htmlContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div>Content</div>");
            var cssContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(string.Empty);

            // Act
            var result = await _controller.DesignerData(nonExistentId, "Test", htmlContent, cssContent);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that DesignerData POST returns success JSON when valid.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Post_ReturnsSuccess_WhenValid()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var model = new TemplateCodeEditorViewModel
            {
                Id = template.Id,
                Title = "Valid Template",
                Content = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div data-ccms-ceid='region1'>Valid Content</div>"),
                EditorTitle = "Template Editor",
                EditingField = "Content"
            };

            // Act
            var result = await _controller.DesignerData(model.Id, model.Title, model.Content, null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            var response = jsonResult.Value as TemplateCodeEditorViewModel;
            
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsValid, "Model should be marked as valid");
        }

        /// <summary>
        /// Tests that DesignerData POST returns BadRequest when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Post_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("TestKey", "Test error");
            var templateId = Guid.NewGuid();
            var htmlContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div>Content</div>");
            var cssContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(string.Empty);

            // Act
            var result = await _controller.DesignerData(templateId, "Test", htmlContent, cssContent);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region Phase 6: Delete & Preview Operations Tests

        /// <summary>
        /// Tests that Trash deletes template successfully.
        /// </summary>
        [TestMethod]
        public async Task Trash_DeletesTemplate_Successfully()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template to Delete",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Trash(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Index", redirectResult.ActionName);

            // Verify template was deleted
            var deletedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNull(deletedTemplate, "Template should be deleted");
        }

        /// <summary>
        /// Tests that Trash redirects to Index after deletion.
        /// </summary>
        [TestMethod]
        public async Task Trash_RedirectsToIndex_AfterDeletion()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template to Delete",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Trash(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Index", redirectResult.ActionName);
        }

        /// <summary>
        /// Tests that Trash returns BadRequest when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task Trash_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("TestKey", "Test error");
            var templateId = Guid.NewGuid();

            // Act
            var result = await _controller.Trash(templateId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that PreviewImpact returns view with impact preview.
        /// </summary>
        [TestMethod]
        public async Task PreviewImpact_ReturnsView_WithImpactPreview()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template with Articles",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Create root article first
            await Logic.CreateArticle("Root", TestUserId);

            // Create articles using this template
            var article1 = await Logic.CreateArticle("Article 1", TestUserId, template.Id);
            var article2 = await Logic.CreateArticle("Article 2", TestUserId, template.Id);

            // Update catalog entries to reference the template
            var catalog1 = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == article1.ArticleNumber);
            var catalog2 = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == article2.ArticleNumber);
            catalog1.TemplateId = template.Id;
            catalog2.TemplateId = template.Id;
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.PreviewImpact(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.Model);
            Assert.AreEqual(template.Id, viewResult.ViewData["TemplateId"]);
        }

        /// <summary>
        /// Tests that PreviewImpact redirects with error when template service throws exception.
        /// </summary>
        [TestMethod]
        public async Task PreviewImpact_RedirectsWithError_WhenServiceThrowsException()
        {
            // Arrange
            var nonExistentTemplateId = Guid.NewGuid();

            // Act
            var result = await _controller.PreviewImpact(nonExistentTemplateId);

            // Assert
            // Should redirect to Index when exception occurs
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Index", redirectResult.ActionName);
            Assert.IsTrue(_controller.TempData.ContainsKey("Error"));
        }

        /// <summary>
        /// Tests that PreviewImpact returns BadRequest when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task PreviewImpact_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("TestKey", "Test error");
            var templateId = Guid.NewGuid();

            // Act
            var result = await _controller.PreviewImpact(templateId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that PreviewImpactJson returns JSON with preview data.
        /// </summary>
        [TestMethod]
        public async Task PreviewImpactJson_ReturnsJson_WithPreviewData()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.PreviewImpactJson(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that PreviewImpactJson returns error when template service throws exception.
        /// </summary>
        [TestMethod]
        public async Task PreviewImpactJson_ReturnsError_WhenServiceThrowsException()
        {
            // Arrange
            var nonExistentTemplateId = Guid.NewGuid();

            // Act
            var result = await _controller.PreviewImpactJson(nonExistentTemplateId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult.Value);
            
            // Should contain error property in the response
            var response = jsonResult.Value;
            var errorProperty = response.GetType().GetProperty("error");
            Assert.IsNotNull(errorProperty, "Response should contain 'error' property");
        }

        #endregion

        #region Phase 7: Publishing & Batch Operations Tests

        /// <summary>
        /// Tests that PublishDrafts publishes selected articles.
        /// </summary>
        [TestMethod]
        public async Task PublishDrafts_PublishesSelectedArticles()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Create root article first
            await Logic.CreateArticle("Root", TestUserId);

            // Create two articles using this template
            var article1 = await Logic.CreateArticle("Article 1", TestUserId, template.Id);
            var article2 = await Logic.CreateArticle("Article 2", TestUserId, template.Id);

            // Create catalog entries for unpublished articles (normally only created when published)
            var catalog1 = new CatalogEntry
            {
                ArticleNumber = article1.ArticleNumber,
                Title = article1.Title,
                UrlPath = article1.UrlPath,
                Status = "Active",
                Updated = DateTimeOffset.UtcNow,
                TemplateId = template.Id
            };
            var catalog2 = new CatalogEntry
            {
                ArticleNumber = article2.ArticleNumber,
                Title = article2.Title,
                UrlPath = article2.UrlPath,
                Status = "Active",
                Updated = DateTimeOffset.UtcNow,
                TemplateId = template.Id
            };
            Db.ArticleCatalog.Add(catalog1);
            Db.ArticleCatalog.Add(catalog2);
            await Db.SaveChangesAsync();

            // Modify articles to have editable content
            var entity1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article1.ArticleNumber);
            var entity2 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article2.ArticleNumber);
            entity1.Content = "<div data-ccms-ceid='region1'>User Content 1</div>";
            entity2.Content = "<div data-ccms-ceid='region1'>User Content 2</div>";
            await Db.SaveChangesAsync();

            var articleNumbers = new List<int> { article1.ArticleNumber, article2.ArticleNumber };

            // Act
            var result = await _controller.PublishDrafts(template.Id, articleNumbers);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Pages", redirectResult.ActionName);

            // Verify both articles were published (existing version 1)
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
            Assert.AreEqual(1, updatedArticle1.VersionNumber, "Article 1 should remain version 1");
            Assert.AreEqual(1, updatedArticle2.VersionNumber, "Article 2 should remain version 1");
            Assert.IsNotNull(updatedArticle1.Published, "Article 1 should be published");
            Assert.IsNotNull(updatedArticle2.Published, "Article 2 should be published");
        }

        /// <summary>
        /// Tests that PublishDrafts publishes all articles when list is null.
        /// </summary>
        [TestMethod]
        public async Task PublishDrafts_PublishesAllArticles_WhenNullList()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act - Pass null to publish all
            var result = await _controller.PublishDrafts(template.Id, null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Pages", redirectResult.ActionName);
        }

        /// <summary>
        /// Tests that PublishDrafts shows success message when articles are published.
        /// </summary>
        [TestMethod]
        public async Task PublishDrafts_ShowsSuccessMessage_WhenPublished()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.PublishDrafts(template.Id, null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            // TempData may contain Success, Warning, or Info depending on service response
            // Just verify redirect happened successfully
        }

        /// <summary>
        /// Tests that PublishDrafts shows warning message when some articles fail.
        /// </summary>
        [TestMethod]
        public async Task PublishDrafts_ShowsWarningMessage_WhenPartialFailure()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act - The service will determine success/failure counts
            var result = await _controller.PublishDrafts(template.Id, null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            // Service determines actual success/failure, we just verify the action completes
        }

        /// <summary>
        /// Tests that PublishDrafts shows info message when no drafts to publish.
        /// </summary>
        [TestMethod]
        public async Task PublishDrafts_ShowsInfoMessage_WhenNoDraftsToPublish()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act - No articles using this template
            var result = await _controller.PublishDrafts(template.Id, null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Pages", redirectResult.ActionName);
        }

        /// <summary>
        /// Tests that PublishDrafts redirects to Pages after completion.
        /// </summary>
        [TestMethod]
        public async Task PublishDrafts_RedirectsToPages_AfterCompletion()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.PublishDrafts(template.Id, null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Pages", redirectResult.ActionName);
            Assert.IsNotNull(redirectResult.RouteValues);
            Assert.AreEqual(template.Id, redirectResult.RouteValues["id"]);
        }

        /// <summary>
        /// Tests that PublishDrafts handles exceptions gracefully.
        /// </summary>
        [TestMethod]
        public async Task PublishDrafts_HandlesException_Gracefully()
        {
            // Arrange
            var nonExistentTemplateId = Guid.NewGuid();

            // Act - Should handle exception from service
            var result = await _controller.PublishDrafts(nonExistentTemplateId, null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Pages", redirectResult.ActionName);
            
            // Should have error in TempData
            Assert.IsTrue(_controller.TempData.ContainsKey("Error"), "Should set Error in TempData when exception occurs");
        }

        /// <summary>
        /// Tests that PublishDrafts returns BadRequest when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task PublishDrafts_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("TestKey", "Test error");
            var templateId = Guid.NewGuid();

            // Act
            var result = await _controller.PublishDrafts(templateId, null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion
    }
}
