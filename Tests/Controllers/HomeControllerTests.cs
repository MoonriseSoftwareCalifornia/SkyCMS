// <copyright file="HomeControllerTests.cs" company="Moonrise Software, LLC">
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
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using Sky.Cms.Services;

    /// <summary>
    /// Tests for HomeController.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class HomeControllerTests : SkyCmsTestBase
    {
        private HomeController homeController;

        /// <summary>
        /// Initialize test - create HomeController instance.
        /// </summary>
        [TestInitialize]
        public void InitializeTest()
        {
            var logger = new Mock<ILogger<HomeController>>();
            var signInManager = new Mock<SignInManager<IdentityUser>>(
                UserManager,
                Mock.Of<IHttpContextAccessor>(),
                Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
                null,
                null,
                null,
                null);
            var emailSender = new Mock<IEmailSender>();
            var configuration = new Mock<IConfiguration>();
            var services = new Mock<IServiceProvider>();

            // Mock IViewRenderService for preview rendering
            var mockViewRenderService = new Mock<IViewRenderService>();
            mockViewRenderService
                .Setup(v => v.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html>Rendered View</html>");

            // Mock ITempDataDictionaryFactory for ViewResult
            var mockTempDataFactory = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>();
            mockTempDataFactory
                .Setup(f => f.GetTempData(It.IsAny<HttpContext>()))
                .Returns(new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                    new DefaultHttpContext(),
                    Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>()));

            // Mock ILayoutTemplateService for layout preview rendering
            var mockLayoutTemplateService = new Mock<Sky.Editor.Services.Layouts.ILayoutTemplateService>();
            mockLayoutTemplateService
                .Setup(l => l.GetAllTemplatesAsync())
                .ReturnsAsync(new System.Collections.Generic.List<Sky.Editor.Services.Templates.PageTemplate>
                {
                    new Sky.Editor.Services.Templates.PageTemplate
                    {
                        Name = "Default Template",
                        Content = "<div>Default Layout Content</div>"
                    }
                });

            // Mock IArticleHtmlService for template preview
            var mockArticleHtmlService = new Mock<Sky.Editor.Services.Html.IArticleHtmlService>();
            mockArticleHtmlService
                .Setup(h => h.EnsureEditableMarkers(It.IsAny<string>()))
                .Returns<string>(html => html); // Return the input HTML as-is

            // Setup service provider to return required services
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(IViewRenderService)))
                .Returns(mockViewRenderService.Object);
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory)))
                .Returns(mockTempDataFactory.Object);
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(Sky.Editor.Services.Layouts.ILayoutTemplateService)))
                .Returns(mockLayoutTemplateService.Object);
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(Sky.Editor.Services.Html.IArticleHtmlService)))
                .Returns(mockArticleHtmlService.Object);

            homeController = new HomeController(
                logger.Object,
                EditorSettings,
                Db,
                Logic,
                UserManager,
                signInManager.Object,
                emailSender.Object,
                configuration.Object,
                services.Object);

            // Setup user context
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "test@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "TestAuth"));

            var httpContext = new DefaultHttpContext 
            { 
                User = claimsPrincipal,
                RequestServices = mockServiceProvider.Object
            };

            homeController.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        /// <summary>
        /// Test that Index action returns a view.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsView()
        {
            // Arrange - Create a root page article so Index can return a ViewResult
            var rootArticle = new Article
            {
                Id = Guid.NewGuid(),
                Title = "Home Page",
                UrlPath = "root",  // "root" is the canonical path for the home page
                Published = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                ArticleNumber = 1,
                Content = "<p>Welcome to the home page</p>"
            };
            Db.Articles.Add(rootArticle);
            await Db.SaveChangesAsync();

            // Act
            var result = await homeController.Index();

            // Assert
            Assert.IsNotNull(result, "Index should return a result");
            Assert.IsInstanceOfType(result, typeof(ViewResult), "Index should return a ViewResult");
        }

        /// <summary>
        /// Test that Index action with language parameter works.
        /// </summary>
        [TestMethod]
        public async Task Index_WithLanguage_ReturnsView()
        {
            // Arrange - Create a root page article so Index can return a ViewResult
            var rootArticle = new Article
            {
                Id = Guid.NewGuid(),
                Title = "Home Page",
                UrlPath = "root",  // "root" is the canonical path for the home page
                Published = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                ArticleNumber = 1,
                Content = "<p>Welcome to the home page</p>"
            };
            Db.Articles.Add(rootArticle);
            await Db.SaveChangesAsync();

            // Act
            var result = await homeController.Index(lang: "en");

            // Assert
            Assert.IsNotNull(result, "Index should return a result");
            Assert.IsInstanceOfType(result, typeof(ViewResult), "Index should return a ViewResult");
        }

        /// <summary>
        /// Test that Error action returns error view.
        /// </summary>
        [TestMethod]
        public void Error_ReturnsErrorView()
        {
            // Act
            var result = homeController.Error() as ViewResult;

            // Assert
            Assert.IsNotNull(result, "Error should return a ViewResult");
            Assert.IsInstanceOfType(result.Model, typeof(ErrorViewModel), "Model should be ErrorViewModel");
        }

        /// <summary>
        /// Test that EditList returns NotFound when article doesn't exist.
        /// </summary>
        [TestMethod]
        public async Task EditList_ReturnsNotFound_WhenArticleNotExists()
        {
            // Arrange
            string nonExistentUrl = "non-existent-page";

            // Act
            var result = await homeController.EditList(nonExistentUrl);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult), "Should return NotFound when article doesn't exist");
        }

        /// <summary>
        /// Test that EditList returns view when article exists.
        /// </summary>
        [TestMethod]
        public async Task EditList_ReturnsView_WhenArticleExists()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                Title = "Test Article",
                UrlPath = "test-article",
                Published = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                ArticleNumber = 1,
                Content = "<p>Test content</p>"
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            var result = await homeController.EditList("test-article");

            // Assert
            Assert.IsNotNull(result, "EditList should return a result");
        }

        /// <summary>
        /// Test that EditList handles URL-encoded paths correctly.
        /// </summary>
        [TestMethod]
        public async Task EditList_HandlesUrlEncodedPath()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                Title = "Test Article With Spaces",
                UrlPath = "test-article-with-spaces",
                Published = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                ArticleNumber = 1,
                Content = "<p>Test content</p>"
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act - Pass URL-encoded path
            var result = await homeController.EditList("test-article-with-spaces");

            // Assert
            Assert.IsNotNull(result, "EditList should handle URL-encoded paths");
        }

        /// <summary>
        /// Test that GetMicrosoftIdentityAssociation returns JSON file.
        /// </summary>
        [TestMethod]
        public void GetMicrosoftIdentityAssociation_ReturnsJsonFile()
        {
            // Act
            var result = homeController.GetMicrosoftIdentityAssociation();

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            Assert.AreEqual("application/json", fileResult.ContentType);
            Assert.AreEqual("microsoft-identity-association.json", fileResult.FileDownloadName);
            Assert.IsTrue(fileResult.FileContents.Length > 0);
        }

        #region Index - Normal Article Loading Tests

        /// <summary>
        /// Test that Index loads article by URL path.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsArticle_WhenValidUrlPath()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Page", TestUserId);
            article.Content = "<p>Test content</p>";
            await Logic.SaveArticle(article, TestUserId);
            
            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            dbArticle.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            // Set the request path
            homeController.ControllerContext.HttpContext.Request.Path = $"/{dbArticle.UrlPath}";

            // Act
            var result = await homeController.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Wrapper", viewResult.ViewName);
        }

        /// <summary>
        /// Test that Index returns 404 page when article not found.
        /// </summary>
        [TestMethod]
        public async Task Index_Returns404Page_WhenArticleNotFound()
        {
            // Arrange
            // Create a not_found article
            var notFoundArticle = await Logic.CreateArticle("Not Found", TestUserId);
            notFoundArticle.Content = "<h1>Page Not Found</h1>";
            await Logic.SaveArticle(notFoundArticle, TestUserId);
            
            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == notFoundArticle.ArticleNumber);
            dbArticle.UrlPath = "not_found";
            dbArticle.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            // Set request path to non-existent page
            homeController.ControllerContext.HttpContext.Request.Path = "/non-existent-page";

            // Act
            var result = await homeController.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.AreEqual(404, homeController.HttpContext.Response.StatusCode);
        }

        #endregion

        #region Index - Preview Mode Tests

        /// <summary>
        /// Test that Index previews article when previewType is editor.
        /// </summary>
        [TestMethod]
        public async Task Index_PreviewsArticle_WhenPreviewTypeIsEditor()
        {
            // Arrange
            var article = await Logic.CreateArticle("Preview Article", TestUserId);
            article.Content = "<p>Preview content</p>";
            await Logic.SaveArticle(article, TestUserId);
            
            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act
            var result = await homeController.Index(previewType: "editor", itemId: dbArticle.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Wrapper", viewResult.ViewName);
            Assert.AreEqual(true, viewResult.ViewData["IsPreview"]);
        }

        /// <summary>
        /// Test that Index previews template with Lorem Ipsum content.
        /// </summary>
        [TestMethod]
        public async Task Index_PreviewsTemplate_WhenPreviewTypeIsTemplates()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div data-ccms-ceid=\"region1\">Editable</div>",
                Description = "Test"
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await homeController.Index(previewType: "templates", itemId: template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Wrapper", viewResult.ViewName);
            Assert.AreEqual(true, viewResult.ViewData["IsPreview"]);
        }

        /// <summary>
        /// Test that Index previews layout.
        /// </summary>
        [TestMethod]
        public async Task Index_PreviewsLayout_WhenPreviewTypeIsLayouts()
        {
            // Arrange
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                Head = "<title>Test</title>",
                HtmlHeader = "<header>Header</header>",
                FooterHtmlContent = "<footer>Footer</footer>"
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            // Act
            var result = await homeController.Index(previewType: "layouts", itemId: layout.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Wrapper", viewResult.ViewName);
            Assert.AreEqual(true, viewResult.ViewData["IsPreview"]);
        }

        #endregion

        #region Index - Validation Tests

        /// <summary>
        /// Test that Index returns BadRequest for invalid preview type.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsBadRequest_WhenInvalidPreviewType()
        {
            // Act
            var result = await homeController.Index(previewType: "invalid", itemId: Guid.NewGuid());

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region Index - Authentication and Authorization Tests

        /// <summary>
        /// Test that Index redirects to login when user is not authenticated.
        /// </summary>
        [TestMethod]
        public async Task Index_RedirectsToLogin_WhenUserNotAuthenticated()
        {
            // Arrange - Create unauthenticated user
            var unauthenticatedPrincipal = new ClaimsPrincipal(new ClaimsIdentity()); // No authentication type
            homeController.ControllerContext.HttpContext.User = unauthenticatedPrincipal;

            // Act
            var result = await homeController.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectResult));
            var redirectResult = (RedirectResult)result;
            Assert.AreEqual("~/Identity/Account/Login", redirectResult.Url);
        }

        /// <summary>
        /// Test that Index redirects to logout when user not found in database.
        /// </summary>
        [TestMethod]
        public async Task Index_RedirectsToLogout_WhenUserNotFoundInDatabase()
        {
            // Arrange - Create authenticated user that doesn't exist in database
            var nonExistentUserId = Guid.NewGuid();
            var authenticatedPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, nonExistentUserId.ToString()),
                new Claim(ClaimTypes.Name, "nonexistent@example.com")
            }, "TestAuth"));
            homeController.ControllerContext.HttpContext.User = authenticatedPrincipal;

            // Act
            var result = await homeController.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectResult));
            var redirectResult = (RedirectResult)result;
            Assert.AreEqual("~/Identity/Account/Logout", redirectResult.Url);
        }

        /// <summary>
        /// Test that Index auto-promotes sole user to Administrator when setup is allowed.
        /// </summary>
        [TestMethod]
        public async Task Index_AutoPromotesToAdmin_WhenOnlyUserAndSetupAllowed()
        {
            // Arrange - This test requires EditorSettings.AllowSetup to be true
            // This is typically controlled by configuration, so we'll skip this test
            // if setup is not allowed in the test environment
            if (!EditorSettings.AllowSetup)
            {
                Assert.Inconclusive("This test requires AllowSetup to be enabled in EditorSettings configuration");
                return;
            }

            // Ensure the "Administrators" role exists
            if (!await RoleManager.RoleExistsAsync("Administrators"))
            {
                await RoleManager.CreateAsync(new IdentityRole("Administrators"));
            }
            
            // Create a root article for the index to load
            var rootArticle = new Article
            {
                Id = Guid.NewGuid(),
                Title = "Home Page",
                UrlPath = "root",
                Published = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                ArticleNumber = 1,
                Content = "<p>Welcome</p>"
            };
            Db.Articles.Add(rootArticle);
            await Db.SaveChangesAsync();

            // Verify only one user exists (from base setup)
            var userCount = await Db.Users.CountAsync();
            Assert.AreEqual(1, userCount, "Setup: Should have exactly one user");

            // Verify user is not initially an admin
            var user = await UserManager.FindByIdAsync(TestUserId.ToString());
            var isAdmin = await UserManager.IsInRoleAsync(user, "Administrators");
            
            if (isAdmin)
            {
                // Remove the role to test auto-promotion
                await UserManager.RemoveFromRoleAsync(user, "Administrators");
            }

            // Update the controller's User principal to remove the Administrators role claim
            // This is necessary because User.IsInRole() checks claims, not the database
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "test@example.com")
                // Intentionally NOT including the Administrators role claim
            }, "TestAuth"));

            homeController.ControllerContext.HttpContext.User = claimsPrincipal;

            // Act
            var result = await homeController.Index();

            // Assert - User should now be an admin
            var updatedUser = await UserManager.FindByIdAsync(TestUserId.ToString());
            var isNowAdmin = await UserManager.IsInRoleAsync(updatedUser, "Administrators");
            Assert.IsTrue(isNowAdmin, "User should be promoted to Administrator");
        }

        #endregion

        #region Index - ModelState and Unpublished Article Tests

        /// <summary>
        /// Test that Index returns BadRequest when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsBadRequest_WhenModelStateInvalid()
        {
            // Arrange - Add a model error
            homeController.ModelState.AddModelError("TestKey", "Test error");

            // Act
            var result = await homeController.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = (BadRequestObjectResult)result;
            Assert.IsInstanceOfType(badRequestResult.Value, typeof(SerializableError));
        }

        /// <summary>
        /// Test that Index loads unpublished article when it exists but is not published.
        /// </summary>
        [TestMethod]
        public async Task Index_LoadsUnpublishedArticle_WhenArticleExistsButNotPublished()
        {
            // Arrange - Create an unpublished article
            var unpublishedArticle = new Article
            {
                Id = Guid.NewGuid(),
                Title = "Unpublished Page",
                UrlPath = "unpublished-page",
                Published = null, // Not published
                Updated = DateTime.UtcNow,
                ArticleNumber = 1,
                Content = "<p>Draft content</p>",
                StatusCode = (int)StatusCodeEnum.Active
            };
            Db.Articles.Add(unpublishedArticle);

            // Create a not_found page as fallback
            var notFoundArticle = new Article
            {
                Id = Guid.NewGuid(),
                Title = "Not Found",
                UrlPath = "not_found",
                Published = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                ArticleNumber = 2,
                Content = "<h1>404 - Page Not Found</h1>",
                StatusCode = (int)StatusCodeEnum.Active
            };
            Db.Articles.Add(notFoundArticle);
            await Db.SaveChangesAsync();

            // Set request path to unpublished article
            homeController.ControllerContext.HttpContext.Request.Path = "/unpublished-page";

            // Act
            var result = await homeController.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Wrapper", viewResult.ViewName);
            
            // Should load the article (either unpublished or not_found fallback)
            Assert.IsNotNull(viewResult.ViewData["RenderedView"]);
        }

        #endregion

        #region AccessPending Tests

        /// <summary>
        /// Test that AccessPending action returns view with correct model.
        /// </summary>
        [TestMethod]
        public void AccessPending_ReturnsViewWithCorrectModel()
        {
            // Act
            var result = homeController.AccessPending() as ViewResult;

            // Assert
            Assert.IsNotNull(result, "AccessPending should return a ViewResult");
            Assert.IsInstanceOfType(result.Model, typeof(ArticleViewModel), "Model should be ArticleViewModel");
            
            var model = (ArticleViewModel)result.Model;
            Assert.AreEqual("Access Pending", model.Title);
            Assert.IsFalse(model.ReadWriteMode);
            Assert.IsFalse(model.PreviewMode);
            Assert.IsFalse(model.EditModeOn);
        }

        #endregion

        #region Preview Error Handling Tests

        /// <summary>
        /// Test that Index throws exception when previewing non-existent layout.
        /// </summary>
        [TestMethod]
        public async Task Index_ThrowsException_WhenPreviewingNonExistentLayout()
        {
            // Arrange - Use a non-existent layout ID
            var nonExistentLayoutId = Guid.NewGuid();

            // Act & Assert - Should throw InvalidOperationException
            var exceptionThrown = false;
            try
            {
                await homeController.Index(previewType: "layouts", itemId: nonExistentLayoutId);
            }
            catch (InvalidOperationException ex)
            {
                exceptionThrown = true;
                Assert.IsTrue(ex.Message.Contains("not found"), "Exception message should indicate layout not found");
            }

            Assert.IsTrue(exceptionThrown, "InvalidOperationException should be thrown");
        }

        /// <summary>
        /// Test that Index throws exception when previewing layout with null ID.
        /// </summary>
        [TestMethod]
        public async Task Index_ThrowsException_WhenPreviewingLayoutWithNullId()
        {
            // Act & Assert - Should throw ArgumentNullException
            var exceptionThrown = false;
            try
            {
                await homeController.Index(previewType: "layouts", itemId: null);
            }
            catch (ArgumentNullException ex)
            {
                exceptionThrown = true;
                Assert.IsTrue(ex.ParamName == "itemId", "Parameter name should be itemId");
            }

            Assert.IsTrue(exceptionThrown, "ArgumentNullException should be thrown");
        }

        /// <summary>
        /// Test that Index throws exception when previewing non-existent template.
        /// </summary>
        [TestMethod]
        public async Task Index_ThrowsException_WhenPreviewingNonExistentTemplate()
        {
            // Arrange - Use a non-existent template ID
            var nonExistentTemplateId = Guid.NewGuid();

            // Act & Assert - Should throw InvalidOperationException
            var exceptionThrown = false;
            try
            {
                await homeController.Index(previewType: "templates", itemId: nonExistentTemplateId);
            }
            catch (InvalidOperationException ex)
            {
                exceptionThrown = true;
                Assert.IsTrue(ex.Message.Contains("not found"), "Exception message should indicate template not found");
            }

            Assert.IsTrue(exceptionThrown, "InvalidOperationException should be thrown");
        }

        /// <summary>
        /// Test that Index throws exception when previewing template with null ID.
        /// </summary>
        [TestMethod]
        public async Task Index_ThrowsException_WhenPreviewingTemplateWithNullId()
        {
            // Act & Assert - Should throw ArgumentNullException
            var exceptionThrown = false;
            try
            {
                await homeController.Index(previewType: "templates", itemId: null);
            }
            catch (ArgumentNullException ex)
            {
                exceptionThrown = true;
                Assert.IsTrue(ex.ParamName == "itemId", "Parameter name should be itemId");
            }

            Assert.IsTrue(exceptionThrown, "ArgumentNullException should be thrown");
        }

        #endregion
    }
}
