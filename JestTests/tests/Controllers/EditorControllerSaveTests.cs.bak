// <copyright file="EditorControllerSaveTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using Sky.Editor.Models;
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for EditorController save operations using SaveArticle feature.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class EditorControllerSaveTests : SkyCmsTestBase
    {
        private EditorController controller = null!;

        [TestInitialize]
        public new void Setup()
        {
            base.Setup();

            // Use the Mediator from the base class (already configured)
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
                Mediator); // Use Mediator from base class

            // Setup user context
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

        #region EditCode Method Tests

        [TestMethod]
        public async Task EditCode_Post_UsesSaveArticleCommand()
        {
            // Arrange - Create an article first
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var model = new EditCodePostModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated via EditCode",
                Content = CryptoJsDecryption.Encrypt("<p>Updated content</p>"),
                HeadJavaScript = CryptoJsDecryption.Encrypt("<script>console.log('head');</script>"),
                FooterJavaScript = CryptoJsDecryption.Encrypt("<script>console.log('footer');</script>"),
                Updated = DateTimeOffset.UtcNow
            };

            // Act
            var result = await controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;

            // Verify article was updated
            var updatedArticle = await Logic.GetArticleByArticleNumber(article.ArticleNumber, null);
            Assert.AreEqual("Updated via EditCode", updatedArticle.Title);
        }

        [TestMethod]
        public async Task EditCode_Post_WithValidationErrors_ReturnsErrors()
        {
            // Arrange - Create an article
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var model = new EditCodePostModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = string.Empty, // Invalid - empty title
                Content = CryptoJsDecryption.Encrypt("<p>Content</p>"),
                HeadJavaScript = CryptoJsDecryption.Encrypt(string.Empty),
                FooterJavaScript = CryptoJsDecryption.Encrypt(string.Empty),
                Updated = DateTimeOffset.UtcNow
            };

            // Act
            var result = await controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;

            // Verify error structure (SaveCodeResultJsonModel)
            dynamic? value = jsonResult.Value;
            Assert.IsNotNull(value);
            Assert.IsFalse(value!.IsValid);
            Assert.IsTrue(value.ErrorCount > 0);
        }

        #endregion

        #region Edit (WYSIWYG) Method Tests

        [TestMethod]
        public async Task Edit_Post_UsesSaveArticleCommand()
        {
            // Arrange
            var article = await Logic.CreateArticle("Original Title", TestUserId);
            article.Content = "<div contenteditable='true'><p>Original content</p></div>";
            await Logic.SaveArticle(article, TestUserId);

            // **INVESTIGATION 1**: Verify content was saved to database
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNotNull(savedArticle?.Content, "Content should be saved in database");
            Assert.Contains("Original content", savedArticle.Content, "Content should contain expected text");
            Console.WriteLine($"Database Article Content: '{savedArticle.Content}'");

            // **INVESTIGATION 2**: Verify GetArticleByArticleNumber retrieves content properly
            var retrievedArticle = await Logic.GetArticleByArticleNumber(article.ArticleNumber, null);
            Assert.IsNotNull(retrievedArticle?.Content, "Retrieved article should have content");
            Assert.Contains("Original content", retrievedArticle.Content, "Retrieved content should match saved content");
            Console.WriteLine($"Retrieved Article Content: '{retrievedArticle.Content}'");

            var model = new HtmlEditorPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated via WYSIWYG",
                BannerImage = "https://example.com/banner.jpg",
                ArticleType = ArticleType.General,
                Category = "Technology",
                Introduction = "Test intro"
                // Note: EditorId and Data are NOT set, so existing content should be preserved
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult), "Controller should return JsonResult");
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value, "JsonResult.Value should not be null");

            // Serialize to JSON to inspect the actual structure
            var json = System.Text.Json.JsonSerializer.Serialize(jsonResult.Value, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            Console.WriteLine($"Controller Response:\n{json}");

            // Use JsonDocument for safer property checking
            var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            // Check which response structure we got
            if (root.TryGetProperty("success", out var successProp))
            {
                // This is the failure response format
                var successValue = successProp.GetBoolean();
                var errors = root.TryGetProperty("errors", out var errorsProp)
                    ? System.Text.Json.JsonSerializer.Serialize(errorsProp)
                    : "No errors property";

                Assert.Fail($"SaveArticle command failed. success={successValue}, errors={errors}");
            }

            // Should have ServerSideSuccess for success response
            Assert.IsTrue(root.TryGetProperty("ServerSideSuccess", out var serverSideSuccessProp),
                "Response should have ServerSideSuccess property");
            Assert.IsTrue(serverSideSuccessProp.GetBoolean(), "ServerSideSuccess should be true");

            Assert.IsTrue(root.TryGetProperty("Model", out var modelProp), "Response should have Model property");

            // Verify article was updated in database
            var updatedArticle = await Logic.GetArticleByArticleNumber(article.ArticleNumber, null);
            Assert.AreEqual("Updated via WYSIWYG", updatedArticle.Title);
            Assert.AreEqual("https://example.com/banner.jpg", updatedArticle.BannerImage);
            Assert.AreEqual(ArticleType.General, updatedArticle.ArticleType);
            Assert.AreEqual("Technology", updatedArticle.Category);
            Assert.AreEqual("Test intro", updatedArticle.Introduction);
            Assert.IsNotNull(updatedArticle.Content);
            Assert.Contains("Original content",
updatedArticle.Content, "Content should be preserved when EditorId is not specified");
        }

        [TestMethod]
        public async Task Edit_Post_WithEditorRegion_UpdatesContentCorrectly()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div data-ccms-ceid=\"region1\">Original Content</div>";
            await Logic.SaveArticle(article, TestUserId);

            var model = new HtmlEditorPostViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                EditorId = "region1",
                Data = CryptoJsDecryption.Encrypt("<p>Updated Region Content</p>"),
                BannerImage = string.Empty,
                ArticleType = ArticleType.General,
                Category = string.Empty,
                Introduction = string.Empty
            };

            // Act
            var result = await controller.Edit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));

            var updatedArticle = await Logic.GetArticleByArticleNumber(article.ArticleNumber, null);
            Assert.Contains("Updated Region Content", updatedArticle.Content);
        }

        #endregion

        #region Designer Method Tests

        [TestMethod]
        public async Task Designer_Post_UsesSaveArticleCommand()
        {
            // Arrange
            var article = await Logic.CreateArticle("Designer Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var model = new ArticleDesignerDataViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated via Designer",
                HtmlContent = CryptoJsDecryption.Encrypt("<div>New HTML</div>"),
                CssContent = CryptoJsDecryption.Encrypt(".test { color: red; }")
            };

            // Act
            var result = await controller.Designer(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            Assert.IsInstanceOfType(jsonResult.Value, typeof(DesignerResult));
            var designerResult = (DesignerResult)jsonResult.Value;
            Assert.IsTrue(designerResult.success);
        }

        [TestMethod]
        public async Task Designer_Post_WithNestedEditableRegions_ReturnsBadRequest()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var model = new ArticleDesignerDataViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Test",
                HtmlContent = CryptoJsDecryption.Encrypt(
                    "<div contenteditable='true'><div contenteditable='true'>Nested</div></div>"),
                CssContent = CryptoJsDecryption.Encrypt(string.Empty)
            };

            // Act
            var result = await controller.Designer(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region Title Change Integration

        [TestMethod]
        public async Task EditCode_Post_WithTitleChange_CreatesRedirect()
        {
            // Arrange
            // **FIX**: Create a root article first, so the next article is NOT the root
            await Logic.CreateArticle("Home Page", TestUserId); // This becomes the root page

            // Now create the article we want to test - this will NOT be root
            var article = await Logic.CreateArticle("Original Title", TestUserId);
            article.Content = "<p>Content</p>";
            await Logic.SaveArticle(article, TestUserId);

            // Publish the article with a past date to enable redirect creation
            var pastDate = DateTimeOffset.UtcNow.AddMinutes(-5);
            await Logic.PublishArticle(article.Id, pastDate);

            // Reload the article after publishing to get the updated Published date
            article = await Logic.GetArticleByArticleNumber(article.ArticleNumber, null);

            // Verify article is published
            Assert.IsNotNull(article.Published, "Article should have a Published date");
            Assert.IsTrue(article.Published <= DateTimeOffset.UtcNow, "Published date should be in the past");

            // **KEY**: Save the original URL before changing the title
            var originalUrlPath = article.UrlPath;
            Console.WriteLine($"Original UrlPath: '{originalUrlPath}'");

            // Verify this is NOT the root page
            Assert.AreNotEqual("root", originalUrlPath, "Test article should not be the root page");

            var model = new EditCodePostModel
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Completely Different New Title", // Make sure this creates a different slug
                Content = CryptoJsDecryption.Encrypt("<p>Content</p>"),
                HeadJavaScript = CryptoJsDecryption.Encrypt(string.Empty),
                FooterJavaScript = CryptoJsDecryption.Encrypt(string.Empty),
                Updated = article.Updated
            };

            // Act
            var result = await controller.EditCode(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));

            // Verify the controller returned success
            var jsonResult = (JsonResult)result;
            var json = System.Text.Json.JsonSerializer.Serialize(jsonResult.Value);
            Console.WriteLine($"Controller Response: {json}");

            // Debug: Check all articles
            var allArticles = await Db.Articles.ToListAsync();
            Console.WriteLine($"Total articles in DB: {allArticles.Count}");
            foreach (var a in allArticles)
            {
                Console.WriteLine($"  Article {a.ArticleNumber}: Title='{a.Title}', UrlPath='{a.UrlPath}', StatusCode={a.StatusCode}, Published={a.Published}");
            }

            // Verify redirect was created
            // Redirects are Article entities with StatusCode = Redirect
            var redirectArticles = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            Console.WriteLine($"Redirect articles found: {redirectArticles.Count}");
            foreach (var r in redirectArticles)
            {
                Console.WriteLine($"  Redirect: From '{r.UrlPath}' to (content): '{r.Content}'");
                Console.WriteLine($"  Redirect HeaderJavaScript: '{r.HeaderJavaScript}'");
            }

            Assert.HasCount(1, redirectArticles, "Expected 1 redirect article to be created");

            var redirect = redirectArticles.First();
            Assert.AreEqual(originalUrlPath, redirect.UrlPath, "Redirect should be from the original URL");

            // Verify the article was updated with new title and new URL
            var updatedArticle = await Logic.GetArticleByArticleNumber(article.ArticleNumber, null);
            Assert.AreEqual("Completely Different New Title", updatedArticle.Title);

            // Verify the URL changed
            Assert.AreNotEqual(originalUrlPath, updatedArticle.UrlPath, "URL path should have changed");

            // **FIX**: The redirect Content field contains HTML, not just the URL
            // We need to verify the Content contains a link to the new URL
            var expectedNewUrl = updatedArticle.UrlPath;
            Assert.Contains(expectedNewUrl,
redirect.Content, $"Redirect content should contain the new URL path '{expectedNewUrl}'");

            // Also verify the HeaderJavaScript contains the redirect logic
            Assert.IsNotNull(redirect.HeaderJavaScript, "Redirect should have HeaderJavaScript");
            Assert.Contains($"window.location.href = '{expectedNewUrl}';",
redirect.HeaderJavaScript, "Redirect JavaScript should set window.location to the new URL");

            // Verify the redirect Content contains expected HTML structure
            Assert.Contains("<h1>Redirecting to",
redirect.Content, "Redirect content should have redirect heading");
            Assert.Contains($"<a href=\"/{expectedNewUrl}\">here</a>",
redirect.Content, "Redirect content should have clickable link to new URL");
        }

        #endregion

        #region Mediator Tests

        [TestMethod]
        public void Mediator_CanResolve_SaveArticleHandler()
        {
            // Verify the handler is properly registered
            var command = new Sky.Editor.Features.Articles.Save.SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "Test",
                Content = "<p>Test</p>",
                ArticleType = ArticleType.General,
                UserId = TestUserId
            };

            // This will throw if handler is not registered
            try
            {
                var handlerType = typeof(Sky.Editor.Features.Shared.ICommandHandler<,>)
                    .MakeGenericType(
                        command.GetType(),
                        typeof(Sky.Editor.Features.Shared.CommandResult<Sky.Editor.Features.Articles.Save.ArticleUpdateResult>));

                var handler = Services.GetService(handlerType);
                Assert.IsNotNull(handler, $"Handler not registered: {handlerType.Name}");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to resolve handler: {ex.Message}");
            }
        }

        #endregion
    }
}
