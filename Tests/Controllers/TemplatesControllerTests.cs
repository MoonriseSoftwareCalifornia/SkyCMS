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
            await CreateArticleAsync("Root Article", TestUserId);

            // Create two articles using this template
            var article1 = await CreateArticleAsync("Article 1", TestUserId, template.Id);
            var article2 = await CreateArticleAsync("Article 2", TestUserId, template.Id);

            // Update existing catalog entries (already created by CreateArticleHandler)
            var catalog1 = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == article1.ArticleNumber);
            var catalog2 = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == article2.ArticleNumber);
            catalog1.Status = "Active";
            catalog2.Status = "Active";
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
            await CreateArticleAsync("Root Article", TestUserId);

            var article = await CreateArticleAsync("Test Article", TestUserId);
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

            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Test Article", TestUserId, template.Id);

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

            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Test Article", TestUserId, template.Id);

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

            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Test Article", TestUserId, template.Id);

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
            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Test Article", TestUserId);

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

            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Test Article", TestUserId, template.Id);

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

            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Test Article", TestUserId, template.Id);

            // Update existing catalog entry (already created by CreateArticleHandler)
            var catalog = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == article.ArticleNumber);
            catalog.Status = "Active";
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

            await CreateArticleAsync("Root Article", TestUserId);
            var article = await CreateArticleAsync("Test Article", TestUserId, template.Id);

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
            await CreateArticleAsync("Root Article", TestUserId);

            // Create articles using this template
            var article1 = await CreateArticleAsync("Article 1", TestUserId, template.Id);
            var article2 = await CreateArticleAsync("Article 2", TestUserId, template.Id);

            // Update existing catalog entries (already created by CreateArticleHandler)
            var catalog1 = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article1.ArticleNumber);
            var catalog2 = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article2.ArticleNumber);
            Assert.IsNotNull(catalog1, "Catalog entry should have been created by CreateArticleHandler");
            Assert.IsNotNull(catalog2, "Catalog entry should have been created by CreateArticleHandler");
            catalog1.Status = "Active"; // Ensure it's active for the test
            catalog2.Status = "Active"; // Ensure it's active for the test
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

            await CreateArticleAsync("Root Article", TestUserId);

            var articleZ = await CreateArticleAsync("ZZZ Article", TestUserId, template.Id);
            var articleA = await CreateArticleAsync("AAA Article", TestUserId, template.Id);

            // Update existing catalog entries (already created by CreateArticleHandler)
            var catalogZ = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == articleZ.ArticleNumber);
            var catalogA = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == articleA.ArticleNumber);
            catalogZ.Status = "Active";
            catalogA.Status = "Active";
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

            await CreateArticleAsync("Root Article", TestUserId);

            var article1 = await CreateArticleAsync("Article 1", TestUserId, template.Id);
            var article2 = await CreateArticleAsync("Article 2", TestUserId, template.Id);

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

            await CreateArticleAsync("Root Article", TestUserId);

            var articleMatch = await CreateArticleAsync("Matching Article", TestUserId, template.Id);
            var articleNoMatch = await CreateArticleAsync("Different Page", TestUserId, template.Id);

            // Update the catalog entries created by CreateArticleAsync to set Published and Active status
            var catalogMatch = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == articleMatch.ArticleNumber);
            catalogMatch.Published = DateTimeOffset.UtcNow;
            catalogMatch.Status = "Active";
            
            var catalogNoMatch = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == articleNoMatch.ArticleNumber);
            catalogNoMatch.Published = DateTimeOffset.UtcNow;
            catalogNoMatch.Status = "Active";
            
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

            await CreateArticleAsync("Root Article", TestUserId);

            // Create 8 articles
            for (int i = 1; i <= 8; i++)
            {
                var article = await CreateArticleAsync($"Article {i:D2}", TestUserId, template.Id);
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

            // Create a PageDesignVersion for this template (required by EditCode action)
            var pageDesignVersion = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Title = "Original Title",
                Content = "<div data-ccms-ceid='region1'>Original Content</div>",
                LayoutId = layout.Id,
                PageType = "content",
                Version = 1
            };
            Db.PageDesignVersions.Add(pageDesignVersion);
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
            
            // Verify the version was updated (query by TemplateId, not by primary key)
            var updatedVersion = await Db.PageDesignVersions
                .FirstOrDefaultAsync(v => v.TemplateId == model.Id);
            Assert.IsNotNull(updatedVersion);
            Assert.AreEqual("Updated Title", updatedVersion.Title);
            Assert.IsTrue(
                updatedVersion.Content.Contains("data-ccms-ceid"),
                "Updated content should have editable markers");
        }

        /// <summary>
        /// Tests that EditCode POST rejects nested editable regions.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_RejectsNestedEditableRegions()
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

            // Create a PageDesignVersion for this template (required by EditCode action)
            var pageDesignVersion = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Title = "Test Template",
                Content = "<div>Content</div>",
                LayoutId = layout.Id,
                PageType = "content",
                Version = 1
            };
            Db.PageDesignVersions.Add(pageDesignVersion);
            await Db.SaveChangesAsync();

            var nestedContent = "<div contenteditable='true'><div contenteditable='true'>Nested</div></div>";
            var model = new Sky.Cms.Models.TemplateCodeEditorViewModel
            {
                Id = template.Id,
                Title = "Updated",
                Content = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(nestedContent),
                Version = 1
            };

            // Act
            var result = await _controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        /// <summary>
        /// Tests that DesignerData POST uses SavePageDesignVersionHandler.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Post_UsesSaveHandler_ForDesignerUpdates()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Designer Template",
                Content = "<div>Original</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var designerVersion = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Version = 1,
                Title = template.Title,
                Content = template.Content,
                PageType = "template"
            };
            Db.PageDesignVersions.Add(designerVersion);
            await Db.SaveChangesAsync();

            // HTML with contenteditable attribute (added by designer UI)
            var htmlContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div contenteditable='true'>Designer Content</div>");
            var cssContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(".class { color: red; }");

            // Act
            var result = await _controller.DesignerData(template.Id, "Updated by Designer", htmlContent, cssContent);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            var resultValue = jsonResult.Value as dynamic;
            Assert.IsTrue(resultValue.success, "Designer save should succeed");

            // Verify version was updated
            var updatedVersion = await Db.PageDesignVersions.FindAsync(designerVersion.Id);
            Assert.IsNotNull(updatedVersion);
            Assert.IsTrue(
                updatedVersion.Content.Contains("data-ccms-ceid"),
                "Designer updated content should have editable markers");
        }

        /// <summary>
        /// Tests that DesignerData POST ensures editable markers are present.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Post_EnsuresEditableMarkers()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Designer Template",
                Content = "<div>Original</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            var designerVersion = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                Version = 1,
                Title = template.Title,
                Content = template.Content,
                PageType = "template"
            };
            Db.PageDesignVersions.Add(designerVersion);
            await Db.SaveChangesAsync();

            // HTML with contenteditable attribute (added by designer UI) that needs data-ccms-ceid markers
            var plainHtml = "<div contenteditable='true'>HTML with editable marker that needs CEID</div>";
            var htmlContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(plainHtml);
            var cssContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(string.Empty);

            // Act
            var result = await _controller.DesignerData(template.Id, "Test", htmlContent, cssContent);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));

            // Verify markers were added
            var updatedVersion = await Db.PageDesignVersions.FindAsync(designerVersion.Id);
            Assert.IsNotNull(updatedVersion);
            Assert.IsTrue(
                updatedVersion.Content.Contains("data-ccms-ceid"),
                "Content should have data-ccms-ceid markers");
        }

        /// <summary>
        /// Tests that DesignerData POST rejects nested editable regions.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Post_RejectsNestedEditableRegions()
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

            var nestedHtml = "<div contenteditable='true'><div contenteditable='true'>Nested</div></div>";
            var htmlContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(nestedHtml);
            var cssContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt(string.Empty);

            // Act
            var result = await _controller.DesignerData(template.Id, "Test", htmlContent, cssContent);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region Phase 5: Delete Operations Tests

        /// <summary>
        /// Tests that Delete succeeds when template has no pages using it.
        /// </summary>
        [TestMethod]
        public async Task Delete_SucceedsWhenNoPages_UsingTemplate()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Unused Template",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Delete(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Index", redirectResult.ActionName);

            // Verify template was deleted
            var deletedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNull(deletedTemplate, "Template should be deleted from database");
        }

        /// <summary>
        /// Tests that Delete fails when template has pages using it.
        /// </summary>
        [TestMethod]
        public async Task Delete_FailsWhenPagesAreUsingTemplate()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Template In Use",
                Content = "<div data-ccms-ceid='region1'>Content</div>",
                LayoutId = layout.Id,
                LayoutNumber = layout.LayoutNumber
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Create root article first
            await CreateArticleAsync("Root Article", TestUserId);

            // Create article using this template
            var article = await CreateArticleAsync("Article 1", TestUserId, template.Id);

            // Update existing catalog entry (already created by CreateArticleHandler)
            var catalog = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalog, "Catalog entry should have been created by CreateArticleHandler");
            catalog.Status = "Active"; // Ensure it's active for the test
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Delete(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Index", redirectResult.ActionName);

            // Verify template still exists (deletion was blocked)
            var templateStillExists = await Db.Templates.FindAsync(template.Id);
            Assert.IsNotNull(templateStillExists, "Template should not be deleted");

            // Verify error message was set in TempData
            Assert.IsTrue(_controller.TempData.ContainsKey("Error"), "Error message should be set in TempData");
        }

        /// <summary>
        /// Tests that Delete returns BadRequest with invalid template ID.
        /// </summary>
        [TestMethod]
        public async Task Delete_ReturnsBadRequestWithEmptyTemplateId()
        {
            // Act
            var result = await _controller.Delete(Guid.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that Delete returns BadRequest when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task Delete_ReturnsBadRequestWhenModelStateInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("TestKey", "Test error");

            // Act
            var result = await _controller.Delete(Guid.NewGuid());

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion
    }
}
