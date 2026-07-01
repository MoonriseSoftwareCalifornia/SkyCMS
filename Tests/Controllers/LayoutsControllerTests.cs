// <copyright file="LayoutsControllerTests.cs" company="Moonrise Software, LLC">
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
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for LayoutsController.
    /// </summary>
    [TestClass]
    public class LayoutsControllerTests : SkyCmsTestBase
    {
        private LayoutsController controller = null!;

        [TestInitialize]
        public new void Setup()
        {
            base.Setup();

            var layoutVersioningService = new Sky.Editor.Services.Layouts.LayoutVersioningService(
                Db,
                ArticleHtmlService,
                NullLogger<Sky.Editor.Services.Layouts.LayoutVersioningService>.Instance);

            // Create controller with all dependencies using real services from base
            controller = new LayoutsController(
                Db,
                UserManager,
                Mediator,  // Use real Mediator from base class
                EditorSettings,
                Storage,
                ViewRenderService,
                NullLogger<LayoutsController>.Instance,
                LayoutImportService,  // Use real LayoutImportService from base class
                layoutVersioningService,
                LayoutCacheService,
                DynamicConfigurationProvider);

            // Setup user context
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "test@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region Phase 1: Core CRUD Operations

        /// <summary>
        /// Test that Index returns view with layouts list.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsView_WithLayoutsList()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                Notes = "Test notes",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);

            var model = viewResult.Model as System.Collections.Generic.List<LayoutIndexViewModel>;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Count > 0, "Should have at least one layout");
        }

        /// <summary>
        /// Test that Index shows CreateFirstLayout when no layouts exist.
        /// </summary>
        [TestMethod]
        public async Task Index_ShowsCreateFirstLayout_WhenNoLayoutsExist()
        {
            // Arrange - Ensure no layouts exist
            var existingLayouts = await Db.Layouts.ToListAsync();
            Db.Layouts.RemoveRange(existingLayouts);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsTrue((bool)viewResult.ViewData["ShowCreateFirstLayout"],
                "Should show create first layout button");
        }

        /// <summary>
        /// Test that Create creates new layout with incremented layout number.
        /// </summary>
        [TestMethod]
        public async Task Create_CreatesNewLayout_WithIncrementedLayoutNumber()
        {
            // Arrange - Clear any existing layouts from base setup
            var existingLayouts = await Db.Layouts.ToListAsync();
            Db.Layouts.RemoveRange(existingLayouts);
            await Db.SaveChangesAsync();

            var existingLayout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Existing Layout",
                IsDefault = true,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(existingLayout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Create();

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = (RedirectToActionResult)result;
            Assert.AreEqual("EditCode", redirect.ActionName);

            // Verify new layout was created
            var layouts = await Db.Layouts.OrderBy(l => l.LayoutNumber).ToListAsync();
            Assert.AreEqual(2, layouts.Count, "Should have 2 layouts now");
            Assert.AreEqual(2, layouts[1].LayoutNumber, "New layout should have LayoutNumber = 2");
            Assert.AreEqual(1, layouts[1].Version, "New layout should have Version = 1");
        }

        /// <summary>
        /// Test that Delete deletes layout when not default.
        /// </summary>
        [TestMethod]
        public async Task Delete_DeletesLayout_WhenNotDefault()
        {
            // Arrange
            var defaultLayout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Default Layout",
                IsDefault = true,
                LayoutNumber = 1,
                Version = 1
            };
            var layoutToDelete = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Layout to Delete",
                IsDefault = false,
                LayoutNumber = 2,
                Version = 1
            };
            Db.Layouts.Add(defaultLayout);
            Db.Layouts.Add(layoutToDelete);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Delete(layoutToDelete.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            // Verify layout was deleted
            var deletedLayout = await Db.Layouts.FindAsync(layoutToDelete.Id);
            Assert.IsNull(deletedLayout, "Layout should be deleted");
        }

        /// <summary>
        /// Test that Delete returns BadRequest when deleting default layout.
        /// </summary>
        [TestMethod]
        public async Task Delete_ReturnsBadRequest_WhenDeletingDefaultLayout()
        {
            // Arrange
            var defaultLayout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Default Layout",
                IsDefault = true,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(defaultLayout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Delete(defaultLayout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequest = (BadRequestObjectResult)result;
            Assert.IsTrue(badRequest.Value.ToString().Contains("Cannot delete the default layout"));
        }

        /// <summary>
        /// Test that Delete deletes associated templates when deleting layout.
        /// </summary>
        [TestMethod]
        public async Task Delete_DeletesAssociatedTemplates_WhenDeletingLayout()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Layout with Templates",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Associated Template",
                LayoutId = layout.Id,
                Content = "<div>Test</div>"
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Delete(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            // Verify template was deleted
            var deletedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNull(deletedTemplate, "Associated template should be deleted");
        }

        /// <summary>
        /// Test that GetLayouts returns JSON list of layouts.
        /// </summary>
        [TestMethod]
        public async Task GetLayouts_ReturnsJsonList_OfLayouts()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = true,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetLayouts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);

            var layouts = jsonResult.Value as System.Collections.Generic.List<LayoutIndexViewModel>;
            Assert.IsNotNull(layouts);
            Assert.IsTrue(layouts.Count > 0, "Should have at least one layout");
        }

        /// <summary>
        /// Test that GetLayouts initializes versions when needed.
        /// </summary>
        [TestMethod]
        public async Task GetLayouts_InitializesVersions_WhenNeeded()
        {
            // Arrange
            var layoutWithoutVersion = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Layout Without Version",
                IsDefault = true,
                LayoutNumber = 1,
                Version = null // Uninitialized version
            };
            Db.Layouts.Add(layoutWithoutVersion);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetLayouts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));

            // Verify version was initialized
            var updatedLayout = await Db.Layouts.FindAsync(layoutWithoutVersion.Id);
            Assert.IsTrue(updatedLayout.Version.HasValue, "Version should be initialized");
            Assert.IsTrue(updatedLayout.Version > 0, "Version should be greater than 0");
        }

        #endregion

        #region Phase 2: Edit Operations

        /// <summary>
        /// Test that EditCode GET returns view model with layout data.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Get_ReturnsViewModel_WithLayoutData()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                Head = "<style>body { margin: 0; }</style>",
                HtmlHeader = "<header>Header</header>",
                FooterHtmlContent = "<footer>Footer</footer>",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.EditCode(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(LayoutCodeViewModel));

            var model = (LayoutCodeViewModel)viewResult.Model;
            Assert.AreEqual(layout.Id, model.Id);
            Assert.AreEqual(layout.Head, model.Head);
        }

        /// <summary>
        /// Test that EditCode GET returns NotFound when layout does not exist.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Get_ReturnsNotFound_WhenLayoutDoesNotExist()
        {
            // Act
            var result = await controller.EditCode(Guid.NewGuid());

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        /// <summary>
        /// Test that EditCode POST saves layout changes.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_SavesLayoutChanges()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Original Layout",
                Head = "<style>body { margin: 0; }</style>",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            var model = new LayoutCodeViewModel
            {
                Id = layout.Id,
                EditorTitle = "Updated Layout",
                Head = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<style>body { margin: 10px; }</style>"),
                HtmlHeader = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<header>New Header</header>"),
                FooterHtmlContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<footer>New Footer</footer>"),
                BodyHtmlAttributes = string.Empty
            };

            // Act
            var result = await controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));

            // Verify changes were saved
            var updatedLayout = await Db.Layouts.FindAsync(layout.Id);
            Assert.IsNotNull(updatedLayout);
            Assert.Contains("margin: 10px", updatedLayout.Head);
            Assert.Contains("New Header", updatedLayout.HtmlHeader);
        }

        /// <summary>
        /// Test that EditCode POST decrypts content before saving.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_DecryptsContent_BeforeSaving()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            var encryptedContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<div>Encrypted Content</div>");
            var model = new LayoutCodeViewModel
            {
                Id = layout.Id,
                EditorTitle = "Test",
                Head = encryptedContent,
                HtmlHeader = encryptedContent,
                FooterHtmlContent = encryptedContent,
                BodyHtmlAttributes = string.Empty
            };

            // Act
            var result = await controller.EditCode(model);

            // Assert
            var updatedLayout = await Db.Layouts.FindAsync(layout.Id);
            Assert.IsNotNull(updatedLayout);
            Assert.Contains("Encrypted Content", updatedLayout.Head,
                "Content should be decrypted and saved");
        }

        /// <summary>
        /// Test that EditNotes GET returns view model.
        /// </summary>
        [TestMethod]
        public async Task EditNotes_Get_ReturnsViewModel()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                Notes = "Test notes",
                IsDefault = true,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.EditNotes();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(LayoutIndexViewModel));
        }

        /// <summary>
        /// Test that EditNotes POST saves notes when valid.
        /// </summary>
        [TestMethod]
        public async Task EditNotes_Post_SavesNotes_WhenValid()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                Notes = "Original notes",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            var model = new LayoutIndexViewModel
            {
                Id = layout.Id,
                LayoutName = "Updated Layout Name",
                Notes = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("Updated notes"),
                IsDefault = false
            };

            // Act
            var result = await controller.EditNotes(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            // Verify notes were saved
            var updatedLayout = await Db.Layouts.FindAsync(layout.Id);
            Assert.IsNotNull(updatedLayout);
            Assert.AreEqual("Updated notes", updatedLayout.Notes);
            Assert.AreEqual("Updated Layout Name", updatedLayout.LayoutName);
        }

        /// <summary>
        /// Test that EditNotes POST validates HTML in notes.
        /// </summary>
        [TestMethod]
        public async Task EditNotes_Post_ValidatesHtml_InNotes()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                Notes = "Original notes",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            var model = new LayoutIndexViewModel
            {
                Id = layout.Id,
                LayoutName = "Test",
                Notes = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<p>Valid HTML notes</p>"),
                IsDefault = false
            };

            // Act
            var result = await controller.EditNotes(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            // Verify notes were saved
            var updatedLayout = await Db.Layouts.FindAsync(layout.Id);
            Assert.Contains("Valid HTML notes", updatedLayout.Notes);
        }

        #endregion

        #region Phase 4: Publishing & Versioning

        /// <summary>
        /// Test that Publish sets layout as default.
        /// </summary>
        [TestMethod]
        public async Task Publish_SetsLayoutAsDefault()
        {
            // Arrange
            var layout1 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Current Default",
                IsDefault = true,
                LayoutNumber = 1,
                Version = 1,
                Published = DateTimeOffset.UtcNow
            };
            var layout2 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "New Default",
                IsDefault = false,
                LayoutNumber = 2,
                Version = 1
            };
            Db.Layouts.Add(layout1);
            Db.Layouts.Add(layout2);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Publish(layout2.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            // Verify layout2 is now default
            var updatedLayout2 = await Db.Layouts.FindAsync(layout2.Id);
            Assert.IsTrue(updatedLayout2.IsDefault, "Layout should be set as default");
            Assert.IsNotNull(updatedLayout2.Published, "Published date should be set");
        }

        /// <summary>
        /// Test that Publish unpublishes other layouts when publishing.
        /// </summary>
        [TestMethod]
        public async Task Publish_UnpublishesOtherLayouts_WhenPublishing()
        {
            // Arrange
            var layout1 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Current Default",
                IsDefault = true,
                LayoutNumber = 1,
                Version = 1,
                Published = DateTimeOffset.UtcNow
            };
            var layout2 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "New Default",
                IsDefault = false,
                LayoutNumber = 2,
                Version = 1
            };
            Db.Layouts.Add(layout1);
            Db.Layouts.Add(layout2);
            await Db.SaveChangesAsync();

            // Act
            await controller.Publish(layout2.Id);

            // Assert
            var updatedLayout1 = await Db.Layouts.FindAsync(layout1.Id);
            Assert.IsFalse(updatedLayout1.IsDefault, "Old default should be unpublished");
            Assert.IsNull(updatedLayout1.Published, "Published date should be cleared");
        }

        /// <summary>
        /// Test that Promote creates new version with incremented version.
        /// </summary>
        [TestMethod]
        public async Task Promote_CreatesNewVersion_WithIncrementedVersion()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1,
                Head = "<style>body {}</style>"
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Promote(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;

            // Verify new version was created
            var layouts = await Db.Layouts.Where(l => l.LayoutNumber == layout.LayoutNumber).OrderByDescending(l => l.Version).ToListAsync();
            Assert.AreEqual(2, layouts.Count, "Should have 2 versions now");
            Assert.AreEqual(2, layouts[0].Version, "New version should have Version = 2");
            Assert.AreEqual(2, (int)jsonResult.Value, "Should return new version number");
        }

        /// <summary>
        /// Test that Promote preserves LayoutNumber across versions.
        /// </summary>
        [TestMethod]
        public async Task Promote_PreservesLayoutNumber_AcrossVersions()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = false,
                LayoutNumber = 5,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            await controller.Promote(layout.Id);

            // Assert
            var layouts = await Db.Layouts.Where(l => l.LayoutNumber == 5).ToListAsync();
            Assert.AreEqual(2, layouts.Count, "Should have 2 versions with same LayoutNumber");
            Assert.IsTrue(layouts.All(l => l.LayoutNumber == 5),
                "All versions should have LayoutNumber = 5");
        }

        #endregion

        #region Phase 5: Import/Export & Community Features

        /// <summary>
        /// Test that ExportLayout returns HTML file.
        /// </summary>
        [TestMethod]
        public async Task ExportLayout_ReturnsHtmlFile()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Export Test Layout",
                Head = "<style>body {}</style>",
                HtmlHeader = "<header>Header</header>",
                FooterHtmlContent = "<footer>Footer</footer>",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Create a root article for export
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Home",
                UrlPath = "root",
                Content = "<p>Test content</p>",
                Published = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.ExportLayout(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            Assert.AreEqual("application/octet-stream", fileResult.ContentType);
            Assert.IsTrue(fileResult.FileDownloadName.Contains("layout-"));
            Assert.IsTrue(fileResult.FileContents.Length > 0);
        }

        /// <summary>
        /// Test that CommunityLayouts returns view with catalog.
        /// </summary>
        [TestMethod]
        public async Task CommunityLayouts_ReturnsView_WithCatalog()
        {
            // Act
            var result = await controller.CommunityLayouts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
        }

        /// <summary>
        /// Test that Create sets version to 1 for new layout.
        /// </summary>
        [TestMethod]
        public async Task Create_SetsVersionToOne_ForNewLayout()
        {
            // Act
            var result = await controller.Create();

            // Assert
            var layouts = await Db.Layouts.ToListAsync();
            var newLayout = layouts.OrderByDescending(l => l.LastModified).First();
            Assert.AreEqual(1, newLayout.Version, "New layout should have Version = 1");
        }

        /// <summary>
        /// Test that Import imports community layout successfully.
        /// NOTE: This test requires network access to fetch community layouts and is currently disabled.
        /// TODO: Mock ILayoutImportService properly or make this an integration test.
        /// </summary>
        [TestMethod]
        [Ignore("Requires network access to community catalog - needs proper mocking or integration test setup")]
        public async Task Import_ImportsCommunityLayout_Successfully()
        {
            // This test is currently disabled because it would require real HTTP calls
            // to the community layout catalog. It should either be:
            // 1. Converted to an integration test with proper setup
            // 2. Refactored to properly mock the ILayoutImportService
            Assert.Inconclusive("Test requires refactoring to work with real LayoutImportService");
        }

        /// <summary>
        /// Test that Import returns BadRequest when layout already exists.
        /// </summary>
        [TestMethod]
        public async Task Import_ReturnsBadRequest_WhenLayoutAlreadyExists()
        {
            // Arrange
            var communityLayoutId = "test-layout-1";
            var existingLayout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Existing Community Layout",
                CommunityLayoutId = communityLayoutId,
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(existingLayout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Import(communityLayoutId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequest = (BadRequestObjectResult)result;
            Assert.IsTrue(badRequest.Value.ToString().Contains("already loaded"));
        }

        /// <summary>
        /// Test that Import returns BadRequest when ID is null or empty.
        /// </summary>
        [TestMethod]
        public async Task Import_ReturnsBadRequest_WhenIdNullOrEmpty()
        {
            // Act
            var result = await controller.Import(string.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Test that Import sets layout as default when no default exists.
        /// NOTE: This test requires network access and is currently disabled.
        /// </summary>
        [TestMethod]
        [Ignore("Requires network access - needs integration test setup")]
        public async Task Import_SetsLayoutAsDefault_WhenNoDefaultExists()
        {
            // Test disabled - requires network access to community catalog
            Assert.Inconclusive("Test requires refactoring for integration testing");
        }

        /// <summary>
        /// Test that Import imports templates with layout.
        /// NOTE: This test requires network access and is currently disabled.
        /// </summary>
        [TestMethod]
        [Ignore("Requires network access - needs integration test setup")]
        public async Task Import_ImportsTemplates_WithLayout()
        {
            // Test disabled - requires network access to community catalog
            Assert.Inconclusive("Test requires refactoring for integration testing");
        }

        #endregion

        #region Phase 6: Preview & Helper Methods

        /// <summary>
        /// Test that EditPreview returns view with layout preview.
        /// </summary>
        [TestMethod]
        public async Task EditPreview_ReturnsView_WithLayoutPreview()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Preview Layout",
                Head = "<style>body {}</style>",
                HtmlHeader = "<header>Header</header>",
                FooterHtmlContent = "<footer>Footer</footer>",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Create a root article for preview
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Home",
                UrlPath = "root",
                Content = "<p>Preview content</p>",
                Published = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.EditPreview(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("~/Views/Home/Index.cshtml", viewResult.ViewName);
        }

        /// <summary>
        /// Test that EditPreview returns NotFound when layout does not exist.
        /// </summary>
        [TestMethod]
        public async Task EditPreview_ReturnsNotFound_WhenLayoutDoesNotExist()
        {
            // Act
            var result = await controller.EditPreview(Guid.NewGuid());

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        /// <summary>
        /// Test that EditPreview returns BadRequest for invalid ID.
        /// </summary>
        [TestMethod]
        public async Task EditPreview_ReturnsBadRequest_ForInvalidId()
        {
            // Act
            var result = await controller.EditPreview(Guid.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region Phase 7: Edge Cases & Validation

        /// <summary>
        /// Test that EditCode POST returns BadRequest when model is invalid.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_ReturnsBadRequest_WhenModelInvalid()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            var model = new LayoutCodeViewModel
            {
                Id = layout.Id,
                EditorTitle = "Test",
                Head = null, // Invalid
                HtmlHeader = null,
                FooterHtmlContent = null
            };

            // Add model error manually
            controller.ModelState.AddModelError("Head", "Required");

            // Act
            var result = await controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            Assert.IsFalse(controller.ModelState.IsValid);

            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            Assert.IsInstanceOfType(jsonResult.Value, typeof(SaveCodeResultJsonModel));
            var payload = (SaveCodeResultJsonModel)jsonResult.Value;
            Assert.IsFalse(payload.IsValid);
            Assert.IsTrue(payload.ErrorCount > 0);
        }

        /// <summary>
        /// Test that Delete returns NotFound when layout does not exist.
        /// </summary>
        [TestMethod]
        public async Task Delete_ReturnsNotFound_WhenLayoutDoesNotExist()
        {
            // Act
            var result = await controller.Delete(Guid.NewGuid());

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        /// <summary>
        /// Test that Index validates pagination parameters.
        /// </summary>
        [TestMethod]
        public async Task Index_ValidatesPaginationParameters()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = true,
                LayoutNumber = 1,
                Version = 1
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act - Pass negative pageNo and invalid pageSize
            var result = await controller.Index(pageNo: -1, pageSize: 200);

            // Assert - Should still return view but with corrected parameters
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;

            // Verify corrected pagination
            Assert.AreEqual(0, viewResult.ViewData["pageNo"], "Negative pageNo should be corrected to 0");
            Assert.AreEqual(10, viewResult.ViewData["pageSize"], "Invalid pageSize should be corrected to 10");
        }

        /// <summary>
        /// Test that CommunityLayouts validates pagination parameters.
        /// </summary>
        [TestMethod]
        public async Task CommunityLayouts_ValidatesPaginationParameters()
        {
            // Act - Pass negative pageNo and invalid pageSize
            var result = await controller.CommunityLayouts(pageNo: -1, pageSize: 200);

            // Assert - Should still return view but with corrected parameters
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;

            // Verify corrected pagination
            Assert.AreEqual(0, viewResult.ViewData["pageNo"], "Negative pageNo should be corrected to 0");
            Assert.AreEqual(10, viewResult.ViewData["pageSize"], "Invalid pageSize should be corrected to 10");
        }

        /// <summary>
        /// Test that layout ID actions return BadRequest for empty GUID.
        /// </summary>
        [TestMethod]
        public async Task LayoutIdActions_ReturnBadRequest_ForEmptyGuid()
        {
            var scenarios = new (string Name, Func<Task<IActionResult>> Action)[]
            {
                ("Delete", () => controller.Delete(Guid.Empty)),
                ("Promote", () => controller.Promote(Guid.Empty)),
                ("Publish", () => controller.Publish(Guid.Empty)),
                ("ExportLayout", () => controller.ExportLayout(Guid.Empty)),
            };

            foreach (var scenario in scenarios)
            {
                var result = await scenario.Action();
                Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), $"{scenario.Name} should return BadRequest for empty GUID.");
            }
        }

        /// <summary>
        /// Test that Promote, Publish, and ExportLayout return NotFound for missing layouts.
        /// </summary>
        [TestMethod]
        public async Task LayoutIdActions_ReturnNotFound_WhenLayoutDoesNotExist()
        {
            var missingId = Guid.NewGuid();
            var scenarios = new (string Name, Func<Task<IActionResult>> Action)[]
            {
                ("Promote", () => controller.Promote(missingId)),
                ("Publish", () => controller.Publish(missingId)),
                ("ExportLayout", () => controller.ExportLayout(missingId)),
            };

            foreach (var scenario in scenarios)
            {
                var result = await scenario.Action();
                Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult), $"{scenario.Name} should return NotFound for missing layout.");
            }
        }

        /// <summary>
        /// Test that Publish returns OK when layout is already default.
        /// </summary>
        [TestMethod]
        public async Task Publish_ReturnsOk_WhenLayoutAlreadyDefault()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Already Default",
                IsDefault = true,
                LayoutNumber = 1,
                Version = 1,
                Published = DateTimeOffset.UtcNow
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Publish(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        /// <summary>
        /// Test that Publish returns RedirectToAction when layout is newly published.
        /// </summary>
        [TestMethod]
        public async Task Publish_ReturnsRedirectToAction_WhenLayoutIsNewlyPublished()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Not Default Yet",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1,
                Published = null
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Publish(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = result as RedirectToActionResult;
            Assert.AreEqual("Publish", redirect.ActionName);
            Assert.AreEqual("Editor", redirect.ControllerName);
        }

        /// <summary>
        /// Test that EditCode POST returns NotFound when layout does not exist.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_ReturnsNotFound_WhenLayoutDoesNotExist()
        {
            // Arrange
            var model = new LayoutCodeViewModel
            {
                Id = Guid.NewGuid(),
                EditorTitle = "Test",
                Head = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<style>body {}</style>"),
                HtmlHeader = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<header>Header</header>"),
                FooterHtmlContent = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<footer>Footer</footer>"),
                BodyHtmlAttributes = string.Empty
            };

            // Act
            var result = await controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        /// <summary>
        /// Test that EditCode POST returns BadRequest when model is null.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_ReturnsBadRequest_WhenModelNull()
        {
            // Act
            var result = await controller.EditCode((LayoutCodeViewModel)null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Test that EditCode POST returns BadRequest for empty GUID.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_ReturnsBadRequest_ForEmptyGuid()
        {
            // Arrange
            var model = new LayoutCodeViewModel
            {
                Id = Guid.Empty,
                EditorTitle = "Test",
                Head = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("<style>body {}</style>"),
                HtmlHeader = string.Empty,
                FooterHtmlContent = string.Empty,
                BodyHtmlAttributes = string.Empty
            };

            // Act
            var result = await controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Test that EditCode GET returns BadRequest for empty GUID.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Get_ReturnsBadRequest_ForEmptyGuid()
        {
            // Act
            var result = await controller.EditCode(Guid.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Test that EditNotes POST returns BadRequest when model is null.
        /// </summary>
        [TestMethod]
        public async Task EditNotes_Post_ReturnsBadRequest_WhenModelNull()
        {
            // Act
            var result = await controller.EditNotes((LayoutIndexViewModel)null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Test that EditNotes POST returns BadRequest for empty GUID.
        /// </summary>
        [TestMethod]
        public async Task EditNotes_Post_ReturnsBadRequest_ForEmptyGuid()
        {
            // Arrange
            var model = new LayoutIndexViewModel
            {
                Id = Guid.Empty,
                LayoutName = "Test",
                Notes = "Test notes",
                IsDefault = false
            };

            // Act
            var result = await controller.EditNotes(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Test that EditNotes POST returns NotFound when layout does not exist.
        /// </summary>
        [TestMethod]
        public async Task EditNotes_Post_ReturnsNotFound_WhenLayoutDoesNotExist()
        {
            // Arrange
            var model = new LayoutIndexViewModel
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test",
                Notes = Cosmos.Common.Services.CryptoJsDecryption.Encrypt("Test notes"),
                IsDefault = false
            };

            // Act
            var result = await controller.EditNotes(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        /// <summary>
        /// Test that Promote copies all layout properties to new version.
        /// </summary>
        [TestMethod]
        public async Task Promote_CopiesAllProperties_ToNewVersion()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout With Properties",
                IsDefault = false,
                LayoutNumber = 1,
                Version = 1,
                Head = "<style>body { margin: 0; }</style>",
                HtmlHeader = "<header>Test Header</header>",
                FooterHtmlContent = "<footer>Test Footer</footer>",
                BodyHtmlAttributes = "class='test-body'",
                Notes = "Test notes",
                CommunityLayoutId = "test-community-id"
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Promote(layout.Id);

            // Assert
            var layouts = await Db.Layouts.Where(l => l.LayoutNumber == layout.LayoutNumber)
                .OrderByDescending(l => l.Version)
                .ToListAsync();

            Assert.AreEqual(2, layouts.Count);
            var newVersion = layouts[0];

            Assert.AreEqual(layout.LayoutName, newVersion.LayoutName, "LayoutName should be copied");
            Assert.AreEqual(layout.Head, newVersion.Head, "Head should be copied");
            Assert.AreEqual(layout.HtmlHeader, newVersion.HtmlHeader, "HtmlHeader should be copied");
            Assert.AreEqual(layout.FooterHtmlContent, newVersion.FooterHtmlContent, "FooterHtmlContent should be copied");
            Assert.AreEqual(layout.BodyHtmlAttributes, newVersion.BodyHtmlAttributes, "BodyHtmlAttributes should be copied");
            Assert.AreEqual(layout.Notes, newVersion.Notes, "Notes should be copied");
            Assert.AreEqual(layout.CommunityLayoutId, newVersion.CommunityLayoutId, "CommunityLayoutId should be copied");
            Assert.IsFalse(newVersion.IsDefault, "New version should not be default");
            Assert.IsNull(newVersion.Published, "New version should not be published");
        }

        /// <summary>
        /// Test that Create increments LayoutNumber correctly with gaps.
        /// </summary>
        [TestMethod]
        public async Task Create_IncrementsLayoutNumber_WithGaps()
        {
            // Arrange - Clear existing layouts and create layouts with gaps
            var existingLayouts = await Db.Layouts.ToListAsync();
            Db.Layouts.RemoveRange(existingLayouts);
            await Db.SaveChangesAsync();

            var layout1 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Layout 1",
                IsDefault = true,
                LayoutNumber = 1,
                Version = 1
            };
            var layout5 = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Layout 5",
                IsDefault = false,
                LayoutNumber = 5,
                Version = 1
            };
            Db.Layouts.Add(layout1);
            Db.Layouts.Add(layout5);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Create();

            // Assert
            var layouts = await Db.Layouts.OrderByDescending(l => l.LayoutNumber).ToListAsync();
            var newLayout = layouts.First();
            Assert.AreEqual(6, newLayout.LayoutNumber, "New layout should have LayoutNumber = 6 (max + 1)");
        }

        /// <summary>
        /// Test that Index returns correct pagination data.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsCorrectPagination_WithMultipleLayouts()
        {
            // Arrange - Create 15 layouts
            var existingLayouts = await Db.Layouts.ToListAsync();
            Db.Layouts.RemoveRange(existingLayouts);
            await Db.SaveChangesAsync();

            for (int i = 1; i <= 15; i++)
            {
                Db.Layouts.Add(new Layout
                {
                    Id = Guid.NewGuid(),
                    LayoutName = $"Layout {i}",
                    IsDefault = i == 1,
                    LayoutNumber = i,
                    Version = 1
                });
            }
            await Db.SaveChangesAsync();

            // Act - Get page 1 with page size 10
            var result = await controller.Index(pageNo: 1, pageSize: 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as System.Collections.Generic.List<LayoutIndexViewModel>;

            Assert.IsNotNull(model);
            Assert.AreEqual(5, model.Count, "Page 2 should have 5 layouts (15 total - 10 on page 1)");
            Assert.AreEqual(15, (int)viewResult.ViewData["RowCount"], "RowCount should be 15");
        }

        /// <summary>
        /// Test that GetLayouts handles exception gracefully.
        /// </summary>
        [TestMethod]
        public async Task GetLayouts_ReturnsInternalServerError_WhenExceptionThrown()
        {
            // Arrange - Dispose the database to force an exception
            await Db.DisposeAsync();

            // Act
            var result = await controller.GetLayouts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(500, objectResult.StatusCode);
            Assert.IsTrue(objectResult.Value.ToString().Contains("error"));
        }

        #endregion
    }
}

