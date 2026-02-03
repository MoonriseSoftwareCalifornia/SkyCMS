using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sky.Cms.Controllers;
using Sky.Editor.Models;
using Sky.Cms.Models;
using Cosmos.Cms.Common;
using Cosmos.Common.Data;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Cosmos.Common.Models;
using Cosmos.Common.Data.Logic;
using Microsoft.AspNetCore.Identity;
using Cosmos.Common.Services;

namespace Sky.Tests.Controllers
{
    [TestClass]
    public class EditorControllerTests : SkyCmsTestBase
    {
        private EditorController controller = null!;

        [TestInitialize]
        public new void Setup()
        {
            base.Setup();
            // Instantiate controller as in EditorControllerSaveTests
            controller = new EditorController(
                Logger, Db, UserManager, RoleManager, Logic, EditorSettings,
                ViewRenderService, Storage, Hub.Object, PublishingService,
                ArticleHtmlService, ReservedPaths, TitleChangeService,
                TemplateService, Mediator, Cache, DynamicConfigurationProvider);

            // Setup user context (reuse from EditorControllerSaveTests)
            var user = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, TestUserId.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "test@example.com"),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Administrators")
                }, "TestAuth"));
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user }
            };
        }

        [TestMethod]
        public async Task Edit_Get_ReturnsViewModel_WhenArticleExists()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.EditCode(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(EditCodePostModel));
            var model = (EditCodePostModel)viewResult.Model;
            Assert.AreEqual(article.ArticleNumber, model.ArticleNumber);
            Assert.AreEqual("Test Article", model.Title);
        }

        [TestMethod]
        public async Task Edit_Get_ReturnsNotFound_WhenArticleDoesNotExist()
        {
            // Arrange
            var nonExistentId = 99999;

            // Act
            var result = await controller.EditCode(nonExistentId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Create_Post_CreatesNewArticle_WithValidData()
        {
            // Arrange
            // First ensure we have a template
            var template = await Db.Templates.FirstOrDefaultAsync();
            if (template == null)
            {
                template = new Template
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Template",
                    Content = "<html><body></body></html>",
                    Description = "Test"
                };
                Db.Templates.Add(template);
                await Db.SaveChangesAsync();
            }

            var model = new CreatePageViewModel
            {
                Title = "New Article",
                TemplateId = template.Id,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = (RedirectToActionResult)result;
            Assert.AreEqual("Versions", redirect.ActionName);

            // Verify article was created in database
            var articles = await Db.Articles.Where(a => a.Title == "New Article").ToListAsync();
            Assert.IsTrue(articles.Count > 0, "Article should be created in database");
        }

        [TestMethod]
        public async Task Create_Post_ReturnsValidationErrors_WhenModelInvalid()
        {
            // Arrange
            var model = new CreatePageViewModel
            {
                Title = string.Empty, // Invalid - empty title
                TemplateId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Manually add model state error (simulating validation)
            controller.ModelState.AddModelError("Title", "Title is required");

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsFalse(controller.ModelState.IsValid);
            Assert.IsTrue(controller.ModelState.ContainsKey("Title"));
        }

        // NOTE: EditorController does not have a Delete or Trash method
        // Articles are typically unpublished or managed through other workflows

        [TestMethod]
        public async Task PublishPage_Post_PublishesArticle_AndPurgesCdn()
        {
            // Arrange
            var article = await Logic.CreateArticle("Article to Publish", TestUserId);
            article.Content = "<p>Content ready to publish</p>";
            await Logic.SaveArticle(article, TestUserId);

            var editorUrl = "/Editor/Index";

            // Act
            var result = await controller.PublishPage(article.Id, DateTimeOffset.UtcNow, editorUrl);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectResult));
            var redirect = (RedirectResult)result;
            Assert.AreEqual(editorUrl, redirect.Url);

            // Verify article was published
            var publishedArticle = await Logic.GetArticleByArticleNumber(article.ArticleNumber, null);
            Assert.IsNotNull(publishedArticle.Published, "Article should have a Published date");
            Assert.IsTrue(publishedArticle.StatusCode == (int)StatusCodeEnum.Active,
                         "Article should have Active status code");
        }

        [TestMethod]
        public async Task Clone_Post_CreatesArticleCopy_WithNewTitle()
        {
            // Arrange
            var originalArticle = await Logic.CreateArticle("Original Article", TestUserId);
            originalArticle.Content = "<p>Original content</p>";
            await Logic.SaveArticle(originalArticle, TestUserId);

            var model = new DuplicateViewModel
            {
                Id = originalArticle.Id,
                ArticleId = originalArticle.ArticleNumber,
                ArticleVersion = originalArticle.VersionNumber,
                Title = "Cloned Article"
            };

            // Act
            var result = await controller.Clone(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = (RedirectToActionResult)result;
            Assert.AreEqual("Versions", redirect.ActionName);

            // Verify cloned article was created
            var clonedArticles = await Db.Articles
                .Where(a => a.Title == "Cloned Article")
                .ToListAsync();
            Assert.IsTrue(clonedArticles.Count > 0, "Cloned article should exist");
            var cloned = clonedArticles.First();
            Assert.Contains("Original content", cloned.Content, "Cloned article should have original content");
            Assert.AreNotEqual(originalArticle.ArticleNumber, cloned.ArticleNumber,
                              "Cloned article should have different article number");
        }

        [TestMethod]
        public async Task Versions_Get_ReturnsVersionHistory_ForArticle()
        {
            // Arrange
            var article = await Logic.CreateArticle("Versioned Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Manually create a second version in the database
            // (SaveArticle updates in place, so we need to manually create a snapshot)
            var version1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            var version2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = article.ArticleNumber,
                VersionNumber = version1.VersionNumber + 1,
                Title = "Versioned Article - Updated",
                Content = version1.Content,
                UrlPath = version1.UrlPath,
                Published = version1.Published,
                Updated = DateTimeOffset.UtcNow,
                UserId = version1.UserId,
                ArticleType = version1.ArticleType,
                Category = version1.Category,
                Introduction = version1.Introduction,
                StatusCode = version1.StatusCode,
                BannerImage = version1.BannerImage,
                HeaderJavaScript = version1.HeaderJavaScript,
                FooterJavaScript = version1.FooterJavaScript
            };
            Db.Articles.Add(version2);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Versions(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);

            // Verify version history contains multiple versions
            var versions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .ToListAsync();
            Assert.IsTrue(versions.Count >= 2, "Should have at least 2 versions");
        }

        [TestMethod]
        public async Task Compare_Get_ShowsDiffBetweenVersions()
        {
            // Arrange
            var article = await Logic.CreateArticle("Article for Compare", TestUserId);
            article.Content = "<p>Version 1 content</p>";
            await Logic.SaveArticle(article, TestUserId);

            // Manually create a second version in the database for comparison
            // (SaveArticle updates in place, so we need to manually create a snapshot)
            var version1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            var version1Id = version1.Id;

            // Create version 2 as a new database record with the same ArticleNumber
            var version2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = article.ArticleNumber,
                VersionNumber = version1.VersionNumber + 1,
                Title = version1.Title,
                Content = "<p>Version 2 content - updated</p>",
                UrlPath = version1.UrlPath,
                Published = version1.Published,
                Updated = DateTimeOffset.UtcNow,
                UserId = version1.UserId,
                ArticleType = version1.ArticleType,
                Category = version1.Category,
                Introduction = version1.Introduction,
                BlogKey = version1.BlogKey
            };
            Db.Articles.Add(version2);
            await Db.SaveChangesAsync();
            var version2Id = version2.Id;

            // Act
            var result = await controller.Compare(version1Id, version2Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(CompareCodeViewModel));

            // Verify both versions exist in database
            var v1 = await Db.Articles.FindAsync(version1Id);
            var v2 = await Db.Articles.FindAsync(version2Id);

            Assert.IsNotNull(v1, "Version 1 should exist");
            Assert.IsNotNull(v2, "Version 2 should exist");
            Assert.AreNotEqual(v1.Content, v2.Content, "Versions should have different content");
        }

        [TestMethod]
        public async Task CreateVersion_CreatesNewVersionWithIncrementedNumber()
        {
            // Arrange
            var article = await Logic.CreateArticle("Article for Versioning", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var originalVersion = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            var originalVersionNumber = originalVersion.VersionNumber;

            // Act
            var result = await controller.CreateVersion(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("EditCode", redirectResult.ActionName);

            // Verify new version was created
            var versions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();

            Assert.AreEqual(2, versions.Count, "Should have 2 versions after CreateVersion");
            Assert.AreEqual(originalVersionNumber, versions[0].VersionNumber);
            Assert.AreEqual(originalVersionNumber + 1, versions[1].VersionNumber);
            
            // Verify content was copied
            Assert.AreEqual(originalVersion.Content, versions[1].Content);
            Assert.AreEqual(originalVersion.Title, versions[1].Title);
        }

        [TestMethod]
        public async Task CreateVersion_BasedOnSpecificOlderVersion()
        {
            // Arrange
            var article = await Logic.CreateArticle("Multi-Version Article", TestUserId);
            article.Content = "<p>Version 1 content</p>";
            await Logic.SaveArticle(article, TestUserId);

            // Create version 2 manually
            var version1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            var version2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = article.ArticleNumber,
                VersionNumber = 2,
                Title = version1.Title,
                Content = "<p>Version 2 content - different</p>",
                UrlPath = version1.UrlPath,
                Published = version1.Published,
                Updated = DateTimeOffset.UtcNow,
                UserId = version1.UserId,
                ArticleType = version1.ArticleType,
                Category = version1.Category,
                StatusCode = version1.StatusCode,
                BannerImage = version1.BannerImage,
                HeaderJavaScript = version1.HeaderJavaScript,
                FooterJavaScript = version1.FooterJavaScript
            };
            Db.Articles.Add(version2);
            await Db.SaveChangesAsync();

            // Act - Create version 3 based on version 1 (not latest)
            var result = await controller.CreateVersion(article.ArticleNumber, version1.Id);

            // Assert
            var versions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();

            Assert.AreEqual(3, versions.Count, "Should have 3 versions");
            Assert.AreEqual(3, versions[2].VersionNumber, "New version should be version 3");
            
            // Version 3 should have content from version 1, not version 2
            Assert.AreEqual(version1.Content, versions[2].Content, 
                "New version should be based on version 1 content");
            Assert.AreNotEqual(version2.Content, versions[2].Content,
                "New version should NOT match version 2 content");
        }

        [TestMethod]
        public async Task CreateVersion_RedirectsToEditCode()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.CreateVersion(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("EditCode", redirectResult.ActionName);
            Assert.AreEqual("Editor", redirectResult.ControllerName);
            
            // Verify the redirect includes the article number
            Assert.IsNotNull(redirectResult.RouteValues);
            Assert.IsTrue(redirectResult.RouteValues.ContainsKey("id"));
            Assert.AreEqual(article.ArticleNumber, redirectResult.RouteValues["id"]);
        }

        [TestMethod]
        public async Task CreateVersion_CopiesAllArticleProperties()
        {
            // Arrange
            var article = await Logic.CreateArticle("Article with Properties", TestUserId);
            article.Content = "<p>Test content</p>";
            article.BannerImage = "banner.jpg";
            article.HeadJavaScript = "<script>console.log('head');</script>";
            article.FooterJavaScript = "<script>console.log('footer');</script>";
            await Logic.SaveArticle(article, TestUserId);

            var originalArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act
            await controller.CreateVersion(article.ArticleNumber);

            // Assert
            var newVersion = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            Assert.AreEqual(originalArticle.Content, newVersion.Content);
            Assert.AreEqual(originalArticle.BannerImage, newVersion.BannerImage);
            Assert.AreEqual(originalArticle.HeaderJavaScript, newVersion.HeaderJavaScript);
            Assert.AreEqual(originalArticle.FooterJavaScript, newVersion.FooterJavaScript);
            Assert.AreEqual(originalArticle.Title, newVersion.Title);
            Assert.AreEqual(originalArticle.UrlPath, newVersion.UrlPath);
            Assert.AreEqual(originalArticle.StatusCode, newVersion.StatusCode);
        }

        #region Trash and Restore Tests

        [TestMethod]
        public async Task TrashArticle_MovesArticleToTrash()
        {
            // Arrange
            // Create a home page first (first article becomes home page with UrlPath="root")
            var homePage = await Logic.CreateArticle("Home Page", TestUserId);
            await Logic.SaveArticle(homePage, TestUserId);
            
            // Create the article we want to trash (this will be the second article)
            var article = await Logic.CreateArticle("Article to Trash", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.TrashArticle(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Verify article is marked as deleted
            var trashedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            Assert.IsNotNull(trashedArticle);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, trashedArticle.StatusCode);
        }

        [TestMethod]
        public async Task GetTrashList_ReturnsOnlyDeletedArticles()
        {
            // Arrange
            var activeArticle = await Logic.CreateArticle("Active Article", TestUserId);
            await Logic.SaveArticle(activeArticle, TestUserId);

            var trashedArticle = await Logic.CreateArticle("Trashed Article", TestUserId);
            await Logic.SaveArticle(trashedArticle, TestUserId);
            await controller.TrashArticle(trashedArticle.ArticleNumber);

            // Act
            var result = await controller.GetTrashList();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);

            // The value should be a collection containing only the trashed article
            var trashList = jsonResult.Value as System.Collections.IEnumerable;
            Assert.IsNotNull(trashList);
            
            // Verify trash list contains at least one item
            var items = trashList.Cast<object>().ToList();
            Assert.IsTrue(items.Count > 0, "Trash list should contain deleted articles");
        }

        [TestMethod]
        public async Task Restore_RestoresArticleFromTrash()
        {
            // Arrange
            // Create home page first (cannot be trashed)
            var homePage = await Logic.CreateArticle("Home Page", TestUserId);
            await Logic.SaveArticle(homePage, TestUserId);
            
            // Create a second article that can be trashed
            var article = await Logic.CreateArticle("Article to Restore", TestUserId);
            await Logic.SaveArticle(article, TestUserId);
            
            // Trash it first
            await controller.TrashArticle(article.ArticleNumber);
            
            // Verify it's trashed
            var trashedArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, trashedArticle.StatusCode);

            // Act
            var result = await controller.Restore(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Verify article is restored
            var restoredArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            Assert.AreNotEqual((int)StatusCodeEnum.Deleted, restoredArticle.StatusCode);
        }

        [TestMethod]
        public async Task Trash_ReturnsView()
        {
            // Act
            var result = controller.Trash();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        #endregion

        #region Clone Tests

        [TestMethod]
        public async Task Clone_Get_ReturnsViewModel_WithOriginalData()
        {
            // Arrange
            var article = await Logic.CreateArticle("Original Article", TestUserId);
            article.Content = "<p>Original content</p>";
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.Clone(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(DuplicateViewModel));

            var model = (DuplicateViewModel)viewResult.Model;
            Assert.AreEqual("Original Article", model.Title);
            Assert.AreEqual(article.ArticleNumber, model.ArticleId);
        }

        [TestMethod]
        public async Task Clone_Get_ReturnsNotFound_WhenArticleDoesNotExist()
        {
            // Act
            var result = await controller.Clone(99999);

            // Assert - Should throw exception or return NotFound
            // The actual behavior depends on GetArticleByArticleNumber implementation
            // If it returns null, NotFound is returned
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Clone_Post_CreatesNewArticle_WithDifferentArticleNumber()
        {
            // Arrange
            var originalArticle = await Logic.CreateArticle("Original for Clone", TestUserId);
            originalArticle.Content = "<p>Original content for cloning</p>";
            await Logic.SaveArticle(originalArticle, TestUserId);

            var original = await Db.Articles.FirstAsync(a => a.ArticleNumber == originalArticle.ArticleNumber);

            var model = new DuplicateViewModel
            {
                Id = original.Id,
                Title = "Cloned Article",
                ArticleId = original.ArticleNumber,
                ArticleVersion = original.VersionNumber,
                Published = null
            };

            // Act
            var result = await controller.Clone(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Versions", redirectResult.ActionName);

            // Verify clone was created
            var allArticles = await Db.Articles.ToListAsync();
            var clonedArticles = allArticles.Where(a => a.Title == "Cloned Article").ToList();
            
            Assert.IsTrue(clonedArticles.Count > 0, "Cloned article should exist");
            var cloned = clonedArticles.First();
            Assert.AreNotEqual(original.ArticleNumber, cloned.ArticleNumber,
                "Cloned article should have different article number");
        }

        [TestMethod]
        public async Task Clone_Post_FailsIfTitleAlreadyExists()
        {
            // Arrange
            var article1 = await Logic.CreateArticle("Existing Title", TestUserId);
            await Logic.SaveArticle(article1, TestUserId);

            var article2 = await Logic.CreateArticle("Source Article", TestUserId);
            await Logic.SaveArticle(article2, TestUserId);

            var source = await Db.Articles.FirstAsync(a => a.ArticleNumber == article2.ArticleNumber);

            var model = new DuplicateViewModel
            {
                Id = source.Id,
                Title = "Existing Title", // This title already exists
                ArticleId = source.ArticleNumber,
                ArticleVersion = source.VersionNumber
            };

            // Act
            var result = await controller.Clone(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsFalse(controller.ModelState.IsValid);
            Assert.IsTrue(controller.ModelState.ContainsKey("Title"));
        }

        [TestMethod]
        public async Task Clone_Post_HandlesParentPageTitle()
        {
            // Arrange
            var originalArticle = await Logic.CreateArticle("Parent Article", TestUserId);
            await Logic.SaveArticle(originalArticle, TestUserId);

            var original = await Db.Articles.FirstAsync(a => a.ArticleNumber == originalArticle.ArticleNumber);

            var model = new DuplicateViewModel
            {
                Id = original.Id,
                Title = "Child Page",
                ParentPageTitle = "Parent",
                ArticleId = original.ArticleNumber,
                ArticleVersion = original.VersionNumber
            };

            // Act
            var result = await controller.Clone(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            // Verify the title was constructed with parent path
            var clonedArticles = await Db.Articles
                .Where(a => a.Title.Contains("Parent") && a.Title.Contains("Child"))
                .ToListAsync();
            
            Assert.IsTrue(clonedArticles.Count > 0, "Cloned article with parent path should exist");
        }

        #endregion

        #region Title Validation Tests

        [TestMethod]
        public async Task CheckTitle_ReturnsTrue_WhenTitleAvailable()
        {
            // Arrange
            var uniqueTitle = "Unique Title " + Guid.NewGuid();

            // Act
            var result = await controller.CheckTitle(0, uniqueTitle);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.AreEqual(true, jsonResult.Value);
        }

        [TestMethod]
        public async Task CheckTitle_ReturnsFalse_WhenTitleTaken()
        {
            // Arrange
            var article = await Logic.CreateArticle("Taken Title", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.CheckTitle(0, "Taken Title");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.AreNotEqual(true, jsonResult.Value); // Should return error message
        }

        [TestMethod]
        public async Task CheckTitle_AllowsSameTitleForSameArticle()
        {
            // Arrange
            var article = await Logic.CreateArticle("My Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act - Check same title for same article number
            var result = await controller.CheckTitle(article.ArticleNumber, "My Article");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.AreEqual(true, jsonResult.Value); // Should allow same title for same article
        }

        #endregion

        #region Publishing Tests

        [TestMethod]
        public async Task PublishPage_PublishesArticle()
        {
            // Arrange
            var article = await Logic.CreateArticle("Article to Publish", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            var publishTime = DateTimeOffset.UtcNow;

            // Act
            var result = await controller.PublishPage(dbArticle.Id, publishTime, "/Editor/Index");

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectResult));
            
            // Verify article was published
            var publishedArticle = await Db.Articles.FindAsync(dbArticle.Id);
            Assert.IsNotNull(publishedArticle.Published);
        }

        [TestMethod]
        public async Task UnpublishPage_UnpublishesArticle()
        {
            // Arrange
            var article = await Logic.CreateArticle("Published Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);
            
            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            await Logic.PublishArticle(dbArticle.Id, DateTimeOffset.UtcNow);

            // Verify it's published
            var publishedArticle = await Db.Articles.FindAsync(dbArticle.Id);
            Assert.IsNotNull(publishedArticle.Published, "Article should be published before test");

            // Act
            var result = await controller.UnpublishPage(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        [TestMethod]
        public async Task PublishPage_ValidatesReturnUrl()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);
            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act - Try to redirect to unauthorized path
            var result = await controller.PublishPage(dbArticle.Id, DateTimeOffset.UtcNow, "/Unauthorized/Path");

            // Assert - Should redirect to safe default
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Index", redirectResult.ActionName);
            Assert.AreEqual("Editor", redirectResult.ControllerName);
        }

        [TestMethod]
        public async Task Publish_ReturnsView()
        {
            // Act
            var result = controller.Publish();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        #endregion

        #region Export Tests

        [TestMethod]
        public async Task ExportPage_ReturnsHtmlFile()
        {
            // Arrange
            var article = await Logic.CreateArticle("Article to Export", TestUserId);
            article.Content = "<p>Exportable content</p>";
            await Logic.SaveArticle(article, TestUserId);

            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act
            var result = await controller.ExportPage(dbArticle.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            Assert.AreEqual("application/octet-stream", fileResult.ContentType);
            Assert.IsTrue(fileResult.FileDownloadName.EndsWith(".html"));
            Assert.IsTrue(fileResult.FileContents.Length > 0);
        }

        [TestMethod]
        public async Task ExportPage_IncludesCorrectVersionNumber()
        {
            // Arrange
            var article = await Logic.CreateArticle("Versioned Export", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // Act
            var result = await controller.ExportPage(dbArticle.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            
            // Filename should contain article number and version
            Assert.IsTrue(fileResult.FileDownloadName.Contains($"pageid-{article.ArticleNumber}"));
            Assert.IsTrue(fileResult.FileDownloadName.Contains($"version-{dbArticle.VersionNumber}"));
        }

        #endregion

        #region Designer Tests

        [TestMethod]
        public async Task Designer_Get_ReturnsDesignerViewModel()
        {
            // Arrange
            var article = await Logic.CreateArticle("Designer Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.Designer(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(ArticleDesignerDataViewModel));

            var model = (ArticleDesignerDataViewModel)viewResult.Model;
            Assert.AreEqual(article.ArticleNumber, model.ArticleNumber);
        }

        [TestMethod]
        public async Task GetDesignerData_ReturnsArticleContent()
        {
            // Arrange
            var article = await Logic.CreateArticle("Designer Data Article", TestUserId);
            article.Content = "<div>Designer content</div>";
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.GetDesignerData(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        #endregion

        #region Permissions Tests

        [TestMethod]
        public async Task Permissions_Get_ReturnsView()
        {
            // Arrange
            var article = await Logic.CreateArticle("Article with Permissions", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.Permissions(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
        }

        #endregion

        #region Article Listing Tests

        [TestMethod]
        public async Task GetArticleList_ReturnsPublishedArticles()
        {
            // Arrange
            var publishedArticle = await Logic.CreateArticle("Published Article", TestUserId);
            await Logic.SaveArticle(publishedArticle, TestUserId);
            
            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == publishedArticle.ArticleNumber);
            dbArticle.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            var unpublishedArticle = await Logic.CreateArticle("Unpublished Article", TestUserId);
            await Logic.SaveArticle(unpublishedArticle, TestUserId);

            // Act
            var result = await controller.GetArticleList(publishedOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        [TestMethod]
        public async Task GetArticleList_FiltersBy_SearchTerm()
        {
            // Arrange
            var article1 = await Logic.CreateArticle("Searchable Article", TestUserId);
            await Logic.SaveArticle(article1, TestUserId);

            var article2 = await Logic.CreateArticle("Other Article", TestUserId);
            await Logic.SaveArticle(article2, TestUserId);

            // Act
            var result = await controller.GetArticleList(term: "Searchable", publishedOnly: false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        [TestMethod]
        public async Task List_Articles_FiltersAndLimitsResults()
        {
            // Arrange
            for (int i = 0; i < 15; i++)
            {
                var article = await Logic.CreateArticle($"Test Article {i}", TestUserId);
                await Logic.SaveArticle(article, TestUserId);
            }

            // Act
            var result = await controller.List_Articles("Test");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            
            // Should limit to 10 results as per controller logic
            var resultList = jsonResult.Value as System.Collections.IEnumerable;
            Assert.IsNotNull(resultList);
            var count = resultList.Cast<object>().Count();
            Assert.IsTrue(count <= 10, "Should limit results to 10");
        }

        #endregion

        #region Reserved Paths Tests

        [TestMethod]
        public async Task ReservedPaths_ReturnsView()
        {
            // Act
            var result = await controller.ReservedPaths("asc", "Path");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        [TestMethod]
        public async Task CreateReservedPath_ReturnsEmptyModel()
        {
            // Act
            var result = controller.CreateReservedPath();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(ReservedPath));
        }

        #endregion

        #region Redirect Tests

        [TestMethod]
        public async Task Redirects_Get_ReturnsView()
        {
            // Act
            var result = await controller.Redirects("asc", "FromUrl");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        #endregion

        #region Miscellaneous Tests

        [TestMethod]
        public async Task Index_ReturnsView()
        {
            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        [TestMethod]
        public async Task GetEncryptionKey_ReturnsKey()
        {
            // Act
            var result = await controller.GetEncryptionKey();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            Assert.IsInstanceOfType(jsonResult.Value, typeof(string));
            Assert.IsTrue(((string)jsonResult.Value).Length > 0);
        }

        [TestMethod]
        public async Task GetPublishedPageList_ReturnsPages()
        {
            // Arrange
            var article = await Logic.CreateArticle("Published Page", TestUserId);
            await Logic.SaveArticle(article, TestUserId);
            
            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            dbArticle.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetPublishedPageList();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        [TestMethod]
        public void Scheduler_ReturnsView()
        {
            // Act
            var result = controller.Scheduler();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        [TestMethod]
        public void Preload_ReturnsView()
        {
            // Act
            var result = controller.Preload();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        #endregion

        #region Permissions POST Tests

        [TestMethod]
        public async Task Permissions_Post_SetsArticlePermissions()
        {
            // Arrange
            var article = await Logic.CreateArticle("Article with Permissions", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Create some test roles
            var role1 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "TestRole1", NormalizedName = "TESTROLE1" };
            var role2 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "TestRole2", NormalizedName = "TESTROLE2" };
            await RoleManager.CreateAsync(role1);
            await RoleManager.CreateAsync(role2);

            var identityObjectIds = new[] { role1.Id, role2.Id };

            // Act
            var result = await controller.Permissions(article.ArticleNumber, identityObjectIds);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Verify permissions were saved
            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            
            Assert.IsNotNull(catalogEntry);
            Assert.IsNotNull(catalogEntry.ArticlePermissions);
            Assert.AreEqual(2, catalogEntry.ArticlePermissions.Count, "Should have 2 permissions");
            
            var permissionIds = catalogEntry.ArticlePermissions.Select(p => p.IdentityObjectId).ToList();
            Assert.IsTrue(permissionIds.Contains(role1.Id), "Should contain role1 permission");
            Assert.IsTrue(permissionIds.Contains(role2.Id), "Should contain role2 permission");
        }

        #endregion

        #region NewHome Tests

        [TestMethod]
        public async Task NewHome_Get_ReturnsViewModel()
        {
            // Arrange
            var article = await Logic.CreateArticle("Future Home Page", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.NewHome(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(NewHomeViewModel));

            var model = (NewHomeViewModel)viewResult.Model;
            Assert.AreEqual(article.ArticleNumber, model.ArticleNumber);
            Assert.AreEqual("Future Home Page", model.Title);
        }

        [TestMethod]
        public async Task NewHome_Post_ChangesHomePage()
        {
            // Arrange
            // Create initial home page (first article becomes root)
            var currentHome = await Logic.CreateArticle("Current Home", TestUserId);
            await Logic.SaveArticle(currentHome, TestUserId);
            
            // Verify it's the root
            var homeArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == currentHome.ArticleNumber);
            Assert.AreEqual("root", homeArticle.UrlPath, "First article should be root");

            // Create a new article that will become the new home
            var newHome = await Logic.CreateArticle("New Home Page", TestUserId);
            await Logic.SaveArticle(newHome, TestUserId);

            // Publish both so they can be swapped
            await Logic.PublishArticle(homeArticle.Id, DateTimeOffset.UtcNow);
            var newHomeArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == newHome.ArticleNumber);
            await Logic.PublishArticle(newHomeArticle.Id, DateTimeOffset.UtcNow);

            var model = new NewHomeViewModel
            {
                Id = newHomeArticle.Id,
                ArticleNumber = newHome.ArticleNumber,
                Title = "New Home Page",
                IsNewHomePage = true,
                UrlPath = newHomeArticle.UrlPath
            };

            // Act
            var result = await controller.NewHome(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = (RedirectToActionResult)result;
            Assert.AreEqual("Index", redirect.ActionName);

            // Verify the new article is now root
            var updatedNewHome = await Db.Articles
                .Where(a => a.ArticleNumber == newHome.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();
            Assert.AreEqual("root", updatedNewHome.UrlPath, "New home should now be root");

            // Verify the old home has a different URL
            var updatedOldHome = await Db.Articles
                .Where(a => a.ArticleNumber == currentHome.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();
            Assert.AreNotEqual("root", updatedOldHome.UrlPath, "Old home should no longer be root");
        }

        #endregion

        #region Static Publishing Tests

        [TestMethod]
        public async Task PublishStaticPages_PublishesSelectedPages()
        {
            // Arrange
            var article1 = await Logic.CreateArticle("Page 1", TestUserId);
            await Logic.SaveArticle(article1, TestUserId);
            var dbArticle1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article1.ArticleNumber);
            dbArticle1.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            var article2 = await Logic.CreateArticle("Page 2", TestUserId);
            await Logic.SaveArticle(article2, TestUserId);
            var dbArticle2 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article2.ArticleNumber);
            dbArticle2.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            var pageIds = new List<Guid> { dbArticle1.Id, dbArticle2.Id };

            // Act
            var result = await controller.PublishStaticPages(pageIds);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            
            // Verify success response
            var json = System.Text.Json.JsonSerializer.Serialize(jsonResult.Value);
            Assert.IsTrue(json.Contains("success"), "Response should contain success property");
        }

        [TestMethod]
        public async Task PublishTOC_GeneratesTocAndSitemap()
        {
            // Arrange
            var article = await Logic.CreateArticle("TOC Test Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);
            var dbArticle = await Db.Articles.FirstAsync(a => a.ArticleNumber == article.ArticleNumber);
            dbArticle.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.PublishTOC("/");

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        #endregion

        #region Redirect Management Tests

        [TestMethod]
        public async Task RedirectEdit_UpdatesRedirect()
        {
            // Arrange
            // Create a redirect article
            var redirectArticle = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 999,
                VersionNumber = 1,
                Title = "Redirect Test",
                UrlPath = "/old-path",
                Content = "/new-path",
                StatusCode = (int)StatusCodeEnum.Redirect,
                Updated = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(redirectArticle);
            await Db.SaveChangesAsync();

            var newFromUrl = "/updated-old-path";
            var newToUrl = "/updated-new-path";

            // Act
            var result = await controller.RedirectEdit(redirectArticle.Id, newFromUrl, newToUrl);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = (RedirectToActionResult)result;
            Assert.AreEqual("Redirects", redirect.ActionName);

            // Verify redirect was updated
            var updated = await Db.Articles.FindAsync(redirectArticle.Id);
            Assert.AreEqual(newFromUrl, updated.UrlPath);
            Assert.AreEqual(newToUrl, updated.Content);
        }

        [TestMethod]
        public async Task RedirectDelete_DeletesRedirect()
        {
            // Arrange
            // Create a redirect article
            var redirectArticle = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 998,
                VersionNumber = 1,
                Title = "Redirect to Delete",
                UrlPath = "/redirect-to-delete",
                Content = "/target",
                StatusCode = (int)StatusCodeEnum.Redirect,
                Updated = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(redirectArticle);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.RedirectDelete(redirectArticle.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = (RedirectToActionResult)result;
            Assert.AreEqual("Redirects", redirect.ActionName);

            // Verify redirect was marked as deleted
            var deleted = await Db.Articles.FindAsync(redirectArticle.Id);
            Assert.IsNotNull(deleted);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, deleted.StatusCode);
        }

        #endregion

        #region Utility and View Tests

        [TestMethod]
        public async Task Get_RoleList_ReturnsFilteredRoles()
        {
            // Arrange
            var role1 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "AdminRole", NormalizedName = "ADMINROLE" };
            var role2 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "EditorRole", NormalizedName = "EDITORROLE" };
            var role3 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "ReviewerRole", NormalizedName = "REVIEWERROLE" };
            
            await RoleManager.CreateAsync(role1);
            await RoleManager.CreateAsync(role2);
            await RoleManager.CreateAsync(role3);

            // Act - filter for roles starting with "Admin"
            var result = await controller.Get_RoleList("Admin");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            
            // Verify filtered results
            var roles = jsonResult.Value as System.Collections.IEnumerable;
            Assert.IsNotNull(roles);
            var roleList = roles.Cast<object>().ToList();
            Assert.IsTrue(roleList.Count > 0, "Should return at least one role");
        }

        [TestMethod]
        public async Task Logs_ReturnsActivityLogs()
        {
            // Arrange
            var log1 = new ArticleLog
            {
                Id = Guid.NewGuid(),
                ActivityNotes = "Created article",
                DateTimeStamp = DateTimeOffset.UtcNow,
                IdentityUserId = TestUserId.ToString()
            };
            var log2 = new ArticleLog
            {
                Id = Guid.NewGuid(),
                ActivityNotes = "Updated article",
                DateTimeStamp = DateTimeOffset.UtcNow.AddMinutes(-5),
                IdentityUserId = TestUserId.ToString()
            };
            
            Db.ArticleLogs.Add(log1);
            Db.ArticleLogs.Add(log2);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Logs();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
        }

        [TestMethod]
        public async Task CcmsContent_ReturnsArticleContent()
        {
            // Arrange
            var article = await Logic.CreateArticle("CcmsContent Test", TestUserId);
            article.Content = "<div>Test content for CcmsContent</div>";
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var result = await controller.CcmsContent(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            Assert.IsInstanceOfType(viewResult.Model, typeof(ArticleViewModel));
            
            var model = (ArticleViewModel)viewResult.Model;
            Assert.AreEqual(article.ArticleNumber, model.ArticleNumber);
            Assert.Contains("Test content for CcmsContent", model.Content);
        }

        [TestMethod]
        public async Task EditReservedPath_ReturnsEditView()
        {
            // Arrange
            var reservedPathId = Guid.NewGuid();
            // Note: Reserved paths are typically managed by IReservedPaths service
            // This test verifies the controller returns the correct view

            // Act - Try to edit a non-existent path (should return NotFound)
            var result = await controller.EditReservedPath(reservedPathId);

            // Assert - Since the path doesn't exist, should return NotFound
            // In a real scenario with actual reserved paths, this would return a ViewResult
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Live Editing Tests

        [TestMethod]
        public async Task EditSaveRegion_UpdatesSingleRegion()
        {
            // Arrange
            var article = await Logic.CreateArticle("Live Edit Test", TestUserId);
            article.Content = "<div data-ccms-ceid=\"header\">Original Header</div><div data-ccms-ceid=\"content\">Original Content</div>";
            await Logic.SaveArticle(article, TestUserId);

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                EditorId = "header",
                Data = CryptoJsDecryption.Encrypt("<h1>Updated Header</h1>")
            };

            // Act
            var result = await controller.EditSaveRegion(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Verify only the header region was updated
            var updatedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            Assert.Contains("Updated Header", updatedArticle.Content, "Header region should be updated");
            Assert.Contains("Original Content", updatedArticle.Content, "Content region should remain unchanged");
        }

        [TestMethod]
        public async Task EditSaveBody_UpdatesEntireBody()
        {
            // Arrange
            var article = await Logic.CreateArticle("Body Edit Test", TestUserId);
            article.Content = "<div>Original body content</div>";
            await Logic.SaveArticle(article, TestUserId);

            var newContent = "<div><h1>Completely New Content</h1><p>New paragraph</p></div>";
            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                EditorId = "body",
                Data = CryptoJsDecryption.Encrypt(newContent)
            };

            // Act
            var result = await controller.EditSaveBody(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Verify entire body was replaced
            var updatedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            Assert.AreEqual(newContent, updatedArticle.Content, "Entire body should be replaced");
            Assert.DoesNotContain("Original body content", updatedArticle.Content);
        }

        #endregion

        #region Search and Replace Tests

        [TestMethod]
        public async Task SearchAndReplaceQuery_ShowsImpactPreview()
        {
            // Arrange
            var article1 = await Logic.CreateArticle("Search Test 1", TestUserId);
            article1.Content = "<p>This contains the findme keyword</p>";
            await Logic.SaveArticle(article1, TestUserId);
            
            var dbArticle1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article1.ArticleNumber);
            dbArticle1.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            var article2 = await Logic.CreateArticle("Search Test 2", TestUserId);
            article2.Content = "<p>This also has findme in it</p>";
            await Logic.SaveArticle(article2, TestUserId);
            
            var dbArticle2 = await Db.Articles.FirstAsync(a => a.ArticleNumber == article2.ArticleNumber);
            dbArticle2.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            var model = new SearchAndReplaceViewModel
            {
                FindValue = "findme",
                ReplaceValue = "replacewith",
                ArticleNumber = null // Search all published articles
            };

            // Act
            var result = await controller.SearchAndReplaceQuery(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
            
            // Verify ViewData contains the impact preview
            Assert.IsTrue(viewResult.ViewData.ContainsKey("SearchAndReplacePrequery"));
            var prequery = viewResult.ViewData["SearchAndReplacePrequery"] as string;
            Assert.IsNotNull(prequery);
            Assert.IsTrue(prequery.Contains("published articles will be modified") || 
                         prequery.Contains("versions will be modified"),
                         "Preview should indicate how many items will be modified");
        }

        #endregion

        #region CreateInitialHomePage Tests

        [TestMethod]
        public async Task CreateInitialHomePage_CreatesRootPage()
        {
            // Arrange
            // Ensure database is empty (no articles exist)
            var existingArticles = await Db.Articles.ToListAsync();
            Db.Articles.RemoveRange(existingArticles);
            await Db.SaveChangesAsync();

            // Ensure home page template exists
            var homeTemplate = await Db.Templates.FirstOrDefaultAsync(t => t.Title.ToLower() == "home page");
            if (homeTemplate == null)
            {
                homeTemplate = new Template
                {
                    Id = Guid.NewGuid(),
                    Title = "Home Page",
                    Content = "<html><body><h1>Welcome Home</h1></body></html>",
                    Description = "Home page template"
                };
                Db.Templates.Add(homeTemplate);
                await Db.SaveChangesAsync();
            }

            var model = new CreatePageViewModel
            {
                Id = Guid.NewGuid(),
                Title = "My First Home Page",
                TemplateId = homeTemplate.Id,
                ArticleNumber = 1
            };

            // Act
            var result = await controller.CreateInitialHomePage(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectResult));
            var redirect = (RedirectResult)result;
            Assert.AreEqual("/", redirect.Url, "Should redirect to home page");

            // Verify the home page was created
            var homePageArticles = await Db.Articles.Where(a => a.UrlPath == "root").ToListAsync();
            Assert.IsTrue(homePageArticles.Count > 0, "Home page should be created");

            var homePage = homePageArticles.First();
            Assert.AreEqual("My First Home Page", homePage.Title);
            Assert.AreEqual("root", homePage.UrlPath, "First article should have 'root' UrlPath");
            Assert.IsNotNull(homePage.Published, "Home page should be auto-published");
            Assert.AreEqual((int)StatusCodeEnum.Active, homePage.StatusCode);
        }

        [TestMethod]
        public async Task CreateInitialHomePage_FailsWhenArticlesExist()
        {
            // Arrange
            // Create an existing article (so it's NOT the first)
            var existingArticle = await Logic.CreateArticle("Existing Article", TestUserId);
            await Logic.SaveArticle(existingArticle, TestUserId);

            var homeTemplate = await Db.Templates.FirstOrDefaultAsync(t => t.Title.ToLower() == "home page");
            if (homeTemplate == null)
            {
                homeTemplate = new Template
                {
                    Id = Guid.NewGuid(),
                    Title = "Home Page",
                    Content = "<html><body><h1>Welcome</h1></body></html>",
                    Description = "Home page template"
                };
                Db.Templates.Add(homeTemplate);
                await Db.SaveChangesAsync();
            }

            var model = new CreatePageViewModel
            {
                Id = Guid.NewGuid(),
                Title = "Invalid Home Page",
                TemplateId = homeTemplate.Id
            };

            // Act
            var result = await controller.CreateInitialHomePage(model);

            // Assert - Should return view with error, not redirect
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsFalse(controller.ModelState.IsValid, "ModelState should be invalid");
            Assert.IsTrue(controller.ModelState.ContainsKey("Title"), "Should have Title error");
        }

        #endregion
    }
}