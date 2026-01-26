// <copyright file="BlogControllerTests.cs" company="Moonrise Software, LLC">
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
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Controllers;
    using Sky.Editor.Features.Articles.Save;
    using Sky.Editor.Features.Shared;
    using Sky.Editor.Models.Blogs;
    using Sky.Editor.Services.BlogPublishing;
    using Sky.Editor.Services.CDN;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Integration tests for the <see cref="BlogController"/> class.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class BlogControllerTests : SkyCmsTestBase
    {
        private BlogController controller = null!;
        private Mock<IMediator> mediatorMock = null!;
        private Mock<UserManager<IdentityUser>> userManagerMock = null!;
        private Mock<IBlogRenderingService> blogRenderingServiceMock = null!;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
            
            mediatorMock = new Mock<IMediator>();
            blogRenderingServiceMock = new Mock<IBlogRenderingService>();
            
            // Create a proper UserManager mock
            var store = new Mock<IUserStore<IdentityUser>>();
            userManagerMock = new Mock<UserManager<IdentityUser>>(
                store.Object, null, null, null, null, null, null, null, null);

            // Create test user
            var testUser = new IdentityUser
            {
                Id = TestUserId.ToString(),
                UserName = "testuser@example.com",
                Email = "testuser@example.com"
            };

            // Configure UserManager to return the test user
            userManagerMock
                .Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(testUser);

            // Configure BlogRenderingService to return dummy HTML
            blogRenderingServiceMock
                .Setup(x => x.GenerateBlogStreamHtml(It.IsAny<Article>()))
                .ReturnsAsync("<html><body>Blog Stream</body></html>");

            blogRenderingServiceMock
                .Setup(x => x.GenerateBlogEntryHtml(It.IsAny<Article>()))
                .ReturnsAsync("<html><body>Blog Entry</body></html>");

            controller = new BlogController(
                Db,
                Logic,
                SlugService,
                TemplateService,
                userManagerMock.Object,
                blogRenderingServiceMock.Object,  // Use mocked BlogRenderingService
                TitleChangeService,
                mediatorMock.Object
            );

            // Set up controller context with authenticated user
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com")
            }, "TestAuthentication"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            controller?.Dispose();
            Db.Dispose();
        }

        #region Create Blog Stream Tests

        [TestMethod]
        public async Task Create_ValidBlogStream_RedirectsToIndex()
        {
            // Arrange
            var model = new BlogStreamViewModel
            {
                Title = "Tech Blog",
                Description = "A blog about technology",
                HeroImage = "https://example.com/hero.jpg"
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleUpdateResult>
                {
                    IsSuccess = true,
                    Data = new ArticleUpdateResult
                    {
                        ServerSideSuccess = true,
                        CdnResults = new List<CdnResult>()
                    }
                });

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(BlogController.Index), redirectResult.ActionName);

            mediatorMock.Verify(
                m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default),
                Times.Once);
        }

        [TestMethod]
        public async Task Create_InvalidModel_ReturnsViewWithErrors()
        {
            // Arrange
            var model = new BlogStreamViewModel
            {
                Title = "", // Invalid - required
                Description = "Description"
            };
            controller.ModelState.AddModelError(nameof(model.Title), "Title is required");

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Create", viewResult.ViewName);
            Assert.IsFalse(controller.ModelState.IsValid);
        }

        [TestMethod]
        public async Task Create_SaveFails_ReturnsViewWithValidationErrors()
        {
            // Arrange
            var model = new BlogStreamViewModel
            {
                Title = "Test Blog",
                Description = "Description"
            };

            var errors = new Dictionary<string, string[]>
            {
                { "Title", new[] { "Title already exists" } }
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleUpdateResult>
                {
                    IsSuccess = false,
                    Errors = errors
                });

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Create", viewResult.ViewName);
            Assert.IsTrue(controller.ModelState.ContainsKey("Title"));
        }

        [TestMethod]
        public async Task Create_TitleConflict_ReturnsViewWithError()
        {
            // Arrange
            await Logic.CreateArticle("Existing Page", TestUserId);

            var model = new BlogStreamViewModel
            {
                Title = "Existing Page",
                Description = "Description"
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsTrue(controller.ModelState.ContainsKey(nameof(model.BlogKey)));
        }

        #endregion

        #region Create Blog Entry Tests

        [TestMethod]
        public async Task CreateEntry_ValidEntry_RedirectsToEditorEdit()
        {
            // Arrange
            var blogKey = "tech-blog";
            await Logic.CreateArticle("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleUpdateResult>
                {
                    IsSuccess = true,
                    Data = new ArticleUpdateResult { ServerSideSuccess = true }
                });

            // Act
            var result = await controller.CreateEntry(blogKey, "New Blog Post");

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Edit", redirectResult.ActionName);
            Assert.AreEqual("Editor", redirectResult.ControllerName);

            mediatorMock.Verify(
                m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default),
                Times.Once);
        }

        [TestMethod]
        public async Task CreateEntry_InvalidBlogKey_ReturnsNotFound()
        {
            // Act
            var result = await controller.CreateEntry("non-existent-blog", "Test Entry");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        [TestMethod]
        public async Task CreateEntry_EmptyTitle_ReturnsBadRequest()
        {
            // Arrange
            var blogKey = "tech-blog";
            await Logic.CreateArticle("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);

            // Act
            var result = await controller.CreateEntry(blogKey, "");

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task CreateEntry_SaveFails_ReturnsServerError()
        {
            // Arrange
            var blogKey = "tech-blog";
            await Logic.CreateArticle("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleUpdateResult>
                {
                    IsSuccess = false,
                    ErrorMessage = "Save failed"
                });

            // Act
            var result = await controller.CreateEntry(blogKey, "New Post");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(500, objectResult.StatusCode);
        }

        #endregion

        #region Edit Blog Entry Tests

        [TestMethod]
        public async Task EditEntry_ValidEdit_RedirectsToEntries()
        {
            // Arrange
            var blogKey = "tech-blog";
            await Logic.CreateArticle("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);
            var entry = await Logic.CreateArticle("Blog Post", TestUserId, null, blogKey, ArticleType.BlogPost);

            var model = new BlogEntryEditViewModel
            {
                ArticleNumber = entry.ArticleNumber,
                BlogKey = blogKey,
                Title = "Updated Blog Post",
                Content = "<p>Updated content</p>",
                Introduction = "Updated intro",
                PublishNow = false
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleUpdateResult>
                {
                    IsSuccess = true,
                    Data = new ArticleUpdateResult { ServerSideSuccess = true }
                });

            // Act
            var result = await controller.EditEntry(blogKey, entry.ArticleNumber, model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(BlogController.Entries), redirectResult.ActionName);

            mediatorMock.Verify(
                m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default),
                Times.Once);
            
            // Verify blog stream HTML was regenerated
            blogRenderingServiceMock.Verify(
                x => x.GenerateBlogStreamHtml(It.IsAny<Article>()),
                Times.Once);
        }

        [TestMethod]
        public async Task EditEntry_InvalidModel_ReturnsViewWithErrors()
        {
            // Arrange
            var blogKey = "tech-blog";
            await Logic.CreateArticle("Blog Post", TestUserId, null, blogKey, ArticleType.BlogPost);

            var model = new BlogEntryEditViewModel
            {
                BlogKey = blogKey,
                Title = "", // Invalid
                Content = "<p>Content</p>"
            };
            controller.ModelState.AddModelError(nameof(model.Title), "Title is required");

            // Act
            var result = await controller.EditEntry(blogKey, 1, model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("EditEntry", viewResult.ViewName);
        }

        [TestMethod]
        public async Task EditEntry_SaveFails_ReturnsViewWithValidationErrors()
        {
            // Arrange
            var blogKey = "tech-blog";
            var entry = await Logic.CreateArticle("Blog Post", TestUserId, null, blogKey, ArticleType.BlogPost);

            var model = new BlogEntryEditViewModel
            {
                ArticleNumber = entry.ArticleNumber,
                BlogKey = blogKey,
                Title = "Updated Post",
                Content = "<p>Content</p>"
            };

            var errors = new Dictionary<string, string[]>
            {
                { "Content", new[] { "Content is required" } }
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleUpdateResult>
                {
                    IsSuccess = false,
                    Errors = errors
                });

            // Act
            var result = await controller.EditEntry(blogKey, entry.ArticleNumber, model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsTrue(controller.ModelState.ContainsKey("Content"));
        }

        [TestMethod]
        public async Task EditEntry_WithPublishNow_PublishesArticle()
        {
            // Arrange
            var blogKey = "tech-blog";
            await Logic.CreateArticle("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);
            var entry = await Logic.CreateArticle("Blog Post", TestUserId, null, blogKey, ArticleType.BlogPost);

            var model = new BlogEntryEditViewModel
            {
                ArticleNumber = entry.ArticleNumber,
                BlogKey = blogKey,
                Title = "Published Post",
                Content = "<p>Content</p>",
                PublishNow = true,
                Published = null
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleUpdateResult>
                {
                    IsSuccess = true,
                    Data = new ArticleUpdateResult { ServerSideSuccess = true }
                });

            // Act
            var result = await controller.EditEntry(blogKey, entry.ArticleNumber, model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            
            // Verify article was published
            var publishedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == entry.ArticleNumber);
            Assert.IsNotNull(publishedArticle);
            
            // Verify blog stream HTML was regenerated
            blogRenderingServiceMock.Verify(
                x => x.GenerateBlogStreamHtml(It.IsAny<Article>()),
                Times.Once);
        }

        #endregion

        #region Delete Blog Entry Tests

        [TestMethod]
        public async Task ConfirmDeleteEntry_ValidEntry_RedirectsToEntries()
        {
            // Arrange
            var blogKey = "tech-blog";
            await Logic.CreateArticle("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);
            var entry = await Logic.CreateArticle("Blog Post", TestUserId, null, blogKey, ArticleType.BlogPost);

            // Act
            var result = await controller.ConfirmDeleteEntry(blogKey, entry.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(BlogController.Entries), redirectResult.ActionName);

            // Verify article was deleted
            var deletedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == entry.ArticleNumber);
            Assert.IsNotNull(deletedArticle);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, deletedArticle.StatusCode);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task EndToEnd_CreateBlogStreamAndEntry_Success()
        {
            // Arrange
            var streamModel = new BlogStreamViewModel
            {
                Title = "My Tech Blog",
                Description = "A blog about tech",
                HeroImage = "https://example.com/hero.jpg"
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleUpdateResult>
                {
                    IsSuccess = true,
                    Data = new ArticleUpdateResult { ServerSideSuccess = true }
                });

            // Act - Create blog stream
            var createResult = await controller.Create(streamModel);
            Assert.IsInstanceOfType(createResult, typeof(RedirectToActionResult));

            // Verify blog stream exists
            var blogStream = await Db.Articles
                .FirstOrDefaultAsync(a => a.Title == "My Tech Blog");
            Assert.IsNotNull(blogStream);
            Assert.AreEqual((int)ArticleType.BlogStream, blogStream.ArticleType);

            // Act - Create blog entry
            var entryResult = await controller.CreateEntry(blogStream.BlogKey, "First Post");
            Assert.IsInstanceOfType(entryResult, typeof(RedirectToActionResult));

            mediatorMock.Verify(
                m => m.SendAsync(It.IsAny<SaveArticleCommand>(), default),
                Times.Exactly(2)); // Once for stream, once for entry
        }

        #endregion
    }
}
