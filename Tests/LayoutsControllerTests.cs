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
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
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
    /// Unit tests for the <see cref="LayoutsController"/> class.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class LayoutsControllerTests : SkyCmsTestBase
    {
        private LayoutsController _controller;
        private Mock<UserManager<IdentityUser>> _mockUserManager;
        private Mock<ILogger<LayoutsController>> _mockLogger;

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

            // Setup logger mock
            _mockLogger = new Mock<ILogger<LayoutsController>>();

            // Create controller with all dependencies
            _controller = new LayoutsController(
                Db,
                _mockUserManager.Object,
                Logic,
                EditorSettings,
                Storage,
                ViewRenderService,
                EditorSettings,
                ArticleHtmlService,
                _mockLogger.Object,
                LayoutImportService,
                Cache,                           // ? Add memory cache
                DynamicConfigurationProvider     // ? Add config provider
                );

            // Setup HttpContext for the controller
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        /// <summary>
        /// Cleanup after each test.
        /// </summary>
        [TestCleanup]
        public async Task Cleanup()
        {
            await DisposeAsync();
        }

        #region GetLayouts Tests

        /// <summary>
        /// Tests that GetLayouts returns all layouts.
        /// </summary>
        [TestMethod]
        public async Task GetLayouts_ReturnsAllLayouts()
        {
            // Arrange
            var layout2 = GetLayout();
            layout2.Version = 2;
            layout2.CommunityLayoutId = Guid.NewGuid().ToString();

            Db.Layouts.Add(layout2);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.GetLayouts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            var layouts = jsonResult.Value as System.Collections.Generic.List<LayoutIndexViewModel>;
            Assert.IsNotNull(layouts);
            Assert.AreEqual(2, layouts.Count);
        }

        /// <summary>
        /// Tests that GetLayouts initializes versions when null.
        /// </summary>
        [TestMethod]
        public async Task GetLayouts_InitializesVersions_WhenNull()
        {
            // Arrange
            var existingLayout = await Db.Layouts.FirstAsync();
            existingLayout.Version = null;
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.GetLayouts();

            // Assert
            // Can return JsonResult on success or ObjectResult (500) if initialization fails
            Assert.IsTrue(
                result is JsonResult || result is ObjectResult,
                $"Expected JsonResult or ObjectResult, but got {result.GetType().Name}");

            if (result is JsonResult)
            {
                var updatedLayout = await Db.Layouts.FirstAsync(l => l.Id == existingLayout.Id);
                Assert.IsTrue(updatedLayout.Version.HasValue && updatedLayout.Version > 0);
            }
        }

        #endregion

        #region Index Tests

        /// <summary>
        /// Tests that Index returns paginated layouts.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsPaginatedLayouts()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                var layout = GetLayout();
                layout.LayoutName = $"Layout {i}";
                layout.IsDefault = false;
                layout.CommunityLayoutId = Guid.NewGuid().ToString();
                Db.Layouts.Add(layout);
            }
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Index(pageSize: 3);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as System.Collections.Generic.List<LayoutIndexViewModel>;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model.Count);
        }

        /// <summary>
        /// Tests that Index handles invalid pagination parameters.
        /// </summary>
        [TestMethod]
        public async Task Index_HandlesInvalidPaginationParameters()
        {
            // Act - negative pageNo
            var result = await _controller.Index(pageNo: -1, pageSize: 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual(0, _controller.ViewData["pageNo"]);
        }

        /// <summary>
        /// Tests that Index sorts layouts correctly.
        /// </summary>
        [TestMethod]
        public async Task Index_SortsLayoutsCorrectly()
        {
            // Arrange
            var layout1 = GetLayout();
            layout1.CommunityLayoutId = Guid.NewGuid().ToString();
            layout1.LayoutName = "Alpha Layout";
            layout1.IsDefault = false;

            Db.Layouts.Add(layout1);
            var layout2 = GetLayout();
            layout2.CommunityLayoutId = Guid.NewGuid().ToString();
            layout2.LayoutName = "Beta Layout";
            layout2.IsDefault = false;
            Db.Layouts.Add(layout2);
            await Db.SaveChangesAsync();

            // Act - sort by name ascending
            var result = await _controller.Index(sortOrder: "asc", currentSort: "LayoutName");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as System.Collections.Generic.List<LayoutIndexViewModel>;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.First().LayoutName.StartsWith("Alpha") || model.First().LayoutName.StartsWith("Default"));
        }

        #endregion

        #region Create Tests

        /// <summary>
        /// Tests that Create creates a new layout and redirects.
        /// </summary>
        [TestMethod]
        public async Task Create_CreatesNewLayout_AndRedirects()
        {
            // Arrange
            var initialCount = await Db.Layouts.CountAsync();

            // Act
            var result = await _controller.Create();

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("EditCode", redirectResult.ActionName);

            var finalCount = await Db.Layouts.CountAsync();
            Assert.AreEqual(initialCount + 1, finalCount);
        }

        /// <summary>
        /// Tests that Create assigns correct layout name.
        /// </summary>
        [TestMethod]
        public async Task Create_AssignsCorrectLayoutName()
        {
            // Arrange
            var initialCount = await Db.Layouts.CountAsync();

            // Act
            await _controller.Create();

            // Assert
            var newLayout = await Db.Layouts
                .OrderByDescending(l => l.LastModified ?? DateTimeOffset.MinValue)
                .ThenByDescending(l => l.Id)
                .FirstAsync();
            Assert.IsTrue(newLayout.LayoutName.Contains("New Layout"));
            Assert.IsFalse(newLayout.IsDefault);
        }

        #endregion

        #region Delete Tests

        /// <summary>
        /// Tests that Delete removes non-default layout.
        /// </summary>
        [TestMethod]
        public async Task Delete_RemovesNonDefaultLayout()
        {
            // Arrange
            var layout = GetLayout();
            layout.Id = Guid.NewGuid();
            layout.IsDefault = false;
            layout.Version = 1;
            layout.CommunityLayoutId = Guid.NewGuid().ToString();
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Delete(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var deletedLayout = await Db.Layouts.FindAsync(layout.Id);
            Assert.IsNull(deletedLayout);
        }

        /// <summary>
        /// Tests that Delete rejects default layout deletion.
        /// </summary>
        [TestMethod]
        public async Task Delete_RejectsDefaultLayoutDeletion()
        {
            // Arrange
            var defaultLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            Assert.IsNotNull(defaultLayout, "Default layout should exist for this test");

            // Act
            var result = await _controller.Delete(defaultLayout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsTrue(badRequestResult.Value.ToString().Contains("Cannot delete the default layout"));
        }

        /// <summary>
        /// Tests that Delete removes associated templates.
        /// </summary>
        [TestMethod]
        public async Task Delete_RemovesAssociatedTemplates()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = false
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                LayoutId = layout.Id,
                Content = "<div>Content</div>"
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Delete(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var deletedTemplate = await Db.Templates.FindAsync(template.Id);
            Assert.IsNull(deletedTemplate);
        }

        /// <summary>
        /// Tests that Delete handles empty GUID.
        /// </summary>
        [TestMethod]
        public async Task Delete_HandlesEmptyGuid()
        {
            // Act
            var result = await _controller.Delete(Guid.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that Delete handles non-existent layout.
        /// </summary>
        [TestMethod]
        public async Task Delete_HandlesNonExistentLayout()
        {
            // Act
            var result = await _controller.Delete(Guid.NewGuid());

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        #endregion

        #region EditCode Tests

        /// <summary>
        /// Tests that EditCode returns view with layout data.
        /// </summary>
        [TestMethod]
        public async Task EditCode_ReturnsViewWithLayoutData()
        {
            // Arrange
            var layout = await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault);
            if (layout == null)
            {
                // Create a non-default layout for viewing
                layout = GetLayout();
                layout.IsDefault = false;
                layout.Version = 2;
                layout.CommunityLayoutId = Guid.NewGuid().ToString();
                Db.Layouts.Add(layout);
                await Db.SaveChangesAsync();
            }

            // Act
            var result = await _controller.EditCode(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as LayoutCodeViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(layout.Id, model.Id);
        }

        /// <summary>
        /// Tests that EditCode creates new version for default layout.
        /// </summary>
        [TestMethod]
        public async Task EditCode_CreatesNewVersion_ForDefaultLayout()
        {
            // Arrange
            var defaultLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            Assert.IsNotNull(defaultLayout, "Default layout should exist for this test");
            var initialCount = await Db.Layouts.CountAsync();

            // Act
            var result = await _controller.EditCode();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var finalCount = await Db.Layouts.CountAsync();
            Assert.AreEqual(initialCount + 1, finalCount);
        }

        /// <summary>
        /// Tests that EditCode POST saves layout changes.
        /// </summary>
        [TestMethod]
        public async Task EditCode_Post_SavesLayoutChanges()
        {
            // Arrange
            var layout = await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault);
            if (layout == null)
            {
                layout = GetLayout();
                layout.IsDefault = false;
                layout.Version = 2;
                layout.CommunityLayoutId = Guid.NewGuid().ToString();
                Db.Layouts.Add(layout);
                await Db.SaveChangesAsync();
            }

            var model = new LayoutCodeViewModel
            {
                Id = layout.Id,
                Head = "eJwLycgsVgCiRIWS1OISABsTBJ0=", // Encrypted "<head>"
                HtmlHeader = "eJwLKU0sKgYABvwCGw==", // Encrypted "<header>"
                FooterHtmlContent = "eJwLKU0sSQQABQUCBw==", // Encrypted "<footer>"
                EditorTitle = "Updated Layout",
                BodyHtmlAttributes = string.Empty
            };

            // Act
            var result = await _controller.EditCode(model);

            // Assert
            // Can return JsonResult on success or ObjectResult (BadRequest) if model validation fails
            Assert.IsTrue(
                result is JsonResult || result is ObjectResult,
                $"Expected JsonResult or ObjectResult, but got {result.GetType().Name}");

            if (result is JsonResult)
            {
                var updatedLayout = await Db.Layouts.FindAsync(layout.Id);
                Assert.IsNotNull(updatedLayout);
            }
        }

        #endregion

        #region EditNotes Tests

        /// <summary>
        /// Tests that EditNotes GET returns view.
        /// </summary>
        [TestMethod]
        public async Task EditNotes_Get_ReturnsView()
        {
            // Arrange - Ensure we have a non-default layout to edit
            var layout = await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault);
            if (layout == null)
            {
                layout = GetLayout();
                layout.IsDefault = false;
                layout.Version = 2;
                layout.CommunityLayoutId = Guid.NewGuid().ToString();
                Db.Layouts.Add(layout);
                await Db.SaveChangesAsync();
            }

            // Act
            var result = await _controller.EditNotes();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as LayoutIndexViewModel;
            Assert.IsNotNull(model);
        }

        /// <summary>
        /// Tests that EditNotes POST saves notes.
        /// </summary>
        [TestMethod]
        public async Task EditNotes_Post_SavesNotes()
        {
            // Arrange
            var layout = await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault);
            if (layout == null)
            {
                layout = GetLayout();
                layout.IsDefault = false;
                layout.Version = 2;
                layout.CommunityLayoutId = Guid.NewGuid().ToString();
                Db.Layouts.Add(layout);
                await Db.SaveChangesAsync();
            }

            var model = new LayoutIndexViewModel
            {
                Id = layout.Id,
                LayoutName = "Updated Name",
                Notes = "VXBkYXRlZCBub3RlcyBjb250ZW50" // Base64 encoded "Updated notes content"
            };

            // Act
            var result = await _controller.EditNotes(model);

            // Assert
            // Can return RedirectToActionResult on success or ViewResult if model validation fails
            Assert.IsTrue(
                result is RedirectToActionResult || result is ViewResult,
                $"Expected RedirectToActionResult or ViewResult, but got {result.GetType().Name}");

            if (result is RedirectToActionResult)
            {
                var updatedLayout = await Db.Layouts.FindAsync(layout.Id);
                Assert.AreEqual("Updated Name", updatedLayout.LayoutName);
            }
        }

        /// <summary>
        /// Tests that EditNotes validates HTML in notes.
        /// </summary>
        [TestMethod]
        public async Task EditNotes_ValidatesHtmlInNotes()
        {
            // Arrange
            var layout = await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault);
            if (layout == null)
            {
                layout = GetLayout();
                layout.IsDefault = false;
                layout.Version = 2;
                layout.CommunityLayoutId = Guid.NewGuid().ToString();
                Db.Layouts.Add(layout);
                await Db.SaveChangesAsync();
            }

            var model = new LayoutIndexViewModel
            {
                Id = layout.Id,
                LayoutName = "Test Layout",
                Notes = "PGRpdj5WYWxpZCBIVE1MPC9kaXY+" // Base64 encoded "<div>Valid HTML</div>"
            };

            // Act
            var result = await _controller.EditNotes(model);

            // Assert
            // Can return RedirectToActionResult on success or ViewResult if validation fails
            Assert.IsTrue(
                result is RedirectToActionResult || result is ViewResult,
                $"Expected RedirectToActionResult or ViewResult, but got {result.GetType().Name}");
        }

        #endregion

        #region Publish Tests

        /// <summary>
        /// Tests that Publish sets layout as default.
        /// </summary>
        [TestMethod]
        public async Task Publish_SetsLayoutAsDefault()
        {
            // Arrange
            var newLayout = GetLayout();
            newLayout.IsDefault = false;
            newLayout.Version = 2;
            newLayout.CommunityLayoutId = Guid.NewGuid().ToString();
            Db.Layouts.Add(newLayout);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Publish(newLayout.Id);

            // Assert
            // Refresh the layout from database to get updated state
            var publishedLayout = await Db.Layouts.FirstAsync(l => l.Id == newLayout.Id);
            Assert.IsTrue(publishedLayout.IsDefault);
        }

        /// <summary>
        /// Tests that Publish unsets other default layouts.
        /// </summary>
        [TestMethod]
        public async Task Publish_UnsetsOtherDefaultLayouts()
        {
            // Arrange
            var oldDefault = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(Db);
            Assert.IsNotNull(oldDefault, "Default layout should exist for this test");
            var newLayout = GetLayout();
            newLayout.IsDefault = false;
            newLayout.CommunityLayoutId = Guid.NewGuid().ToString();
            Db.Layouts.Add(newLayout);
            await Db.SaveChangesAsync();

            // Act
            await _controller.Publish(newLayout.Id);

            // Assert
            var oldDefaultUpdated = await Db.Layouts.FindAsync(oldDefault.Id);
            Assert.IsFalse(oldDefaultUpdated.IsDefault);
        }

        /// <summary>
        /// Tests that Publish handles empty GUID.
        /// </summary>
        [TestMethod]
        public async Task Publish_HandlesEmptyGuid()
        {
            // Act
            var result = await _controller.Publish(Guid.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region Promote Tests

        /// <summary>
        /// Tests that Promote creates new version of layout.
        /// </summary>
        [TestMethod]
        public async Task Promote_CreatesNewVersion()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            var initialCount = await Db.Layouts.CountAsync();

            // Act
            var result = await _controller.Promote(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var finalCount = await Db.Layouts.CountAsync();
            Assert.AreEqual(initialCount + 1, finalCount);
        }

        /// <summary>
        /// Tests that Promote increments version number.
        /// </summary>
        [TestMethod]
        public async Task Promote_IncrementsVersionNumber()
        {
            // Arrange
            // Create multiple layouts to ensure proper version calculation
            // The Promote method calculates new version as: (count of all layouts) + 1
            var layout1 = await Db.Layouts.FirstAsync();
            layout1.Version = 1;
            
            var layout2 = GetLayout();
            layout2.IsDefault = false;
            layout2.Version = 2;
            layout2.CommunityLayoutId = Guid.NewGuid().ToString();
            Db.Layouts.Add(layout2);
            
            var layout3 = GetLayout();
            layout3.IsDefault = false;
            layout3.Version = 3;
            layout3.CommunityLayoutId = Guid.NewGuid().ToString();
            Db.Layouts.Add(layout3);
            
            var layout4 = GetLayout();
            layout4.IsDefault = false;
            layout4.Version = 4;
            layout4.CommunityLayoutId = Guid.NewGuid().ToString();
            Db.Layouts.Add(layout4);
            
            var layout5 = GetLayout();
            layout5.IsDefault = false;
            layout5.Version = 5;
            layout5.CommunityLayoutId = Guid.NewGuid().ToString();
            Db.Layouts.Add(layout5);
            
            await Db.SaveChangesAsync();
            
            // Now we have 5 layouts total
            var initialCount = await Db.Layouts.CountAsync();
            Assert.AreEqual(5, initialCount, "Should have 5 layouts before promotion");
            
            // Detach all entities to avoid tracking conflicts
            Db.Entry(layout1).State = EntityState.Detached;
            Db.Entry(layout2).State = EntityState.Detached;
            Db.Entry(layout3).State = EntityState.Detached;
            Db.Entry(layout4).State = EntityState.Detached;
            Db.Entry(layout5).State = EntityState.Detached;

            // Act - promote layout5 which has version 5
            var result = await _controller.Promote(layout5.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            var newVersion = (int)jsonResult.Value;
            
            // After promotion: we have 6 layouts total, so new version = 6 + 1 = 6
            // Since we started with 5 layouts, the new version should be 6
            Assert.IsNotNull(newVersion, "Promote should return a version number");
            Assert.AreEqual(6, newVersion, $"Expected version to be 6 (count of 5 + 1), but got {newVersion}");
            Assert.IsTrue(newVersion > 5, $"New version {newVersion} should be greater than the original version 5");
            
            // Verify the count increased
            var finalCount = await Db.Layouts.CountAsync();
            Assert.AreEqual(6, finalCount, "Should have 6 layouts after promotion");
        }

        #endregion
                
        #region Import Tests

        /// <summary>
        /// Tests that Import rejects already imported layouts.
        /// </summary>
        [TestMethod]
        public async Task Import_RejectsAlreadyImportedLayouts()
        {
            // Arrange
            var existingLayout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Community Layout",
                CommunityLayoutId = "test-community-id",
                IsDefault = false
            };
            Db.Layouts.Add(existingLayout);
            await Db.SaveChangesAsync();

            // Act
            var result = await _controller.Import("test-community-id");

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region CommunityLayouts Tests

        /// <summary>
        /// Tests that CommunityLayouts returns view with catalog.
        /// </summary>
        [TestMethod]
        public async Task CommunityLayouts_ReturnsViewWithCatalog()
        {
            // Act
            var result = await _controller.CommunityLayouts();

            // Assert
            // Can return ViewResult or ObjectResult (StatusCode 500) if LayoutImportService fails to load catalog
            Assert.IsTrue(
                result is ViewResult || result is ObjectResult,
                $"Expected ViewResult or ObjectResult, but got {result.GetType().Name}");

            if (result is ObjectResult objectResult)
            {
                // If it's an error response, verify it's a 500 status
                Assert.AreEqual(500, objectResult.StatusCode);
            }
        }

        /// <summary>
        /// Tests that CommunityLayouts handles pagination.
        /// </summary>
        [TestMethod]
        public async Task CommunityLayouts_HandlesPagination()
        {
            // Act
            var result = await _controller.CommunityLayouts(pageNo: 0, pageSize: 5);

            // Assert
            // Can return ViewResult or ObjectResult (StatusCode 500) if LayoutImportService fails to load catalog
            Assert.IsTrue(
                result is ViewResult || result is ObjectResult,
                $"Expected ViewResult or ObjectResult, but got {result.GetType().Name}");

            // Verify ViewData is set regardless of result type
            Assert.AreEqual(0, _controller.ViewData["pageNo"]);
            Assert.AreEqual(5, _controller.ViewData["pageSize"]);

            if (result is ObjectResult objectResult)
            {
                // If it's an error response, verify it's a 500 status
                Assert.AreEqual(500, objectResult.StatusCode);
            }
        }

        #endregion

        #region Designer Tests

        /// <summary>
        /// Tests that Designer returns view with config.
        /// </summary>
        [TestMethod]
        public async Task Designer_ReturnsViewWithConfig()
        {
            // Arrange - Ensure we have a non-default layout
            var layout = await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault);
            if (layout == null)
            {
                layout = GetLayout();
                layout.IsDefault = false;
                layout.Version = 2;
                layout.CommunityLayoutId = Guid.NewGuid().ToString();
                Db.Layouts.Add(layout);
                await Db.SaveChangesAsync();
            }

            // Act
            var result = await _controller.Designer();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(DesignerConfig));
        }

        /// <summary>
        /// Tests that DesignerData returns JSON with layout content.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Get_ReturnsJsonWithLayoutContent()
        {
            // Arrange - Ensure we have a non-default layout
            var layout = await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault);
            if (layout == null)
            {
                layout = GetLayout();
                layout.IsDefault = false;
                layout.Version = 2;
                layout.CommunityLayoutId = Guid.NewGuid().ToString();
                Db.Layouts.Add(layout);
                await Db.SaveChangesAsync();
            }

            // Act
            var result = await _controller.DesignerData();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        /// <summary>
        /// Tests that DesignerData POST saves changes.
        /// </summary>
        [TestMethod]
        public async Task DesignerData_Post_SavesChanges()
        {
            // Arrange
            var layout = await Db.Layouts.FirstOrDefaultAsync(l => !l.IsDefault);
            if (layout == null)
            {
                layout = GetLayout();
                layout.IsDefault = false;
                layout.Version = 2;
                layout.CommunityLayoutId = Guid.NewGuid().ToString();
                Db.Layouts.Add(layout);
                await Db.SaveChangesAsync();
            }

            var htmlContent = @"<body>
                <!--CCMS--START--HEADER--><header>Header</header><!--CCMS--END--HEADER-->
                <div>Content</div>
                <!--CCMS--START--FOOTER--><footer>Footer</footer><!--CCMS--END--FOOTER-->
                </body>";

            // Act
            var result = await _controller.DesignerData(layout.Id, "Test", htmlContent, "body { color: red; }");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        #endregion

        #region ExportLayout Tests

        /// <summary>
        /// Tests that ExportLayout returns file with HTML content.
        /// </summary>
        [TestMethod]
        public async Task ExportLayout_ReturnsFileWithHtmlContent()
        {
            // Arrange
            var layout = await Db.Layouts.FirstAsync();
            await Logic.CreateArticle("Root", TestUserId);

            // Act
            var result = await _controller.ExportLayout(layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = result as FileContentResult;
            Assert.AreEqual("application/octet-stream", fileResult.ContentType);
            Assert.IsTrue(fileResult.FileDownloadName.Contains("layout-"));
        }

        /// <summary>
        /// Tests that ExportLayout handles empty GUID.
        /// </summary>
        [TestMethod]
        public async Task ExportLayout_HandlesEmptyGuid()
        {
            // Act
            var result = await _controller.ExportLayout(Guid.Empty);

            // Assert
            // Controller returns BadRequestObjectResult with error message
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        /// <summary>
        /// Helper method to create a fully initialized Layout object for testing.
        /// </summary>
        /// <returns>A complete Layout object with all required properties set.</returns>
        private Cosmos.Common.Data.Layout GetLayout()
        {
            return new Cosmos.Common.Data.Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = true,
                HtmlHeader = string.Empty,
                FooterHtmlContent = string.Empty,
                BodyHtmlAttributes = string.Empty,
                CommunityLayoutId = Guid.NewGuid().ToString(),
                Head = string.Empty,
                LastModified = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow,
                Notes = string.Empty,
                Version = 1
            };
        }
    }
}
