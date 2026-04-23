// <copyright file="BlogControllerBlogPostTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Blogs
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
    using Sky.Editor.Features.Blogs.UpdatePost;
    using Sky.Editor.Models.Blogs;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for BlogController blog post CRUD operations using the new dedicated command handlers.
    /// </summary>
    [TestClass]
    public class BlogControllerBlogPostTests : SkyCmsTestBase
    {
        private BlogController controller;
        private Article blogStream;

        /// <summary>
        /// Initialize test context before each test.
        /// </summary>
        [TestInitialize]
        public new async Task Setup()
        {
            InitializeTestContext(seedLayout: true);

            // Add a user to the in-memory database so UserManager can find it
            var testUser = new IdentityUser { Id = TestUserId.ToString(), UserName = "testuser" };
            Db.Users.Add(testUser);
            await Db.SaveChangesAsync();

            // Ensure templates exist
            await TemplateService.EnsureDefaultTemplatesExistAsync();

            // Create a test blog stream
            blogStream = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Blog Stream",
                BlogKey = "test-blog",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream Content</div>",
                UrlPath = "test-blog",
                Published = DateTimeOffset.UtcNow
            };
            Db.Articles.Add(blogStream);

            // Add ArticleNumber record so CreateArticleHandler can generate the next article number
            Db.ArticleNumbers.Add(new ArticleNumber { LastNumber = 1 });

            await Db.SaveChangesAsync();

            // Initialize the controller
            controller = new BlogController(
                Db,
                SlugService,
                TemplateService,
                UserManager,
                BlogStreamRenderingService,
                Mediator,
                LayoutCacheService,
                DynamicConfigurationProvider);

            // Set up controller context with authenticated user
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString())
            }, "test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal,
                RequestServices = Services
            };
            httpContext.Request.Host = new HostString("example.com");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Mock IUrlHelper to support RedirectToAction
            var mockUrlHelper = new Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>();
            mockUrlHelper.Setup(x => x.ActionContext).Returns(new Microsoft.AspNetCore.Mvc.ActionContext
            {
                HttpContext = httpContext,
                RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
            });
            mockUrlHelper.Setup(x => x.Action(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>()))
                .Returns("/mock/url");
            controller.Url = mockUrlHelper.Object;

            // Set up TempData
            var tempDataProvider = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>();
            var tempDataDictionary = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, tempDataProvider.Object);
            controller.TempData = tempDataDictionary;
        }

        /// <summary>
        /// Cleanup after each test.
        /// </summary>
        [TestCleanup]
        public async Task Cleanup()
        {
            await DisposeAsync();
        }

        #region Create Blog Stream Tests

        /// <summary>
        /// Tests that Create creates a blog stream article in the database.
        /// </summary>
        [TestMethod]
        public async Task Create_BlogStream_SucceedsWithValidData()
        {
            // Arrange
            var model = new BlogStreamViewModel
            {
                Title = "New Tech Blog",
                Description = "A blog about technology",
                HeroImage = "/images/hero.jpg"
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(BlogController.Index), redirect.ActionName);

            var createdStream = await Db.Articles
                .Where(a => a.Title == model.Title)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();

            Assert.IsNotNull(createdStream, "Blog stream article should be created in the database");
            Assert.AreEqual((int)ArticleType.BlogStream, createdStream.ArticleType, "Created article should be a blog stream");
        }

        #endregion

        #region CreatePost Tests

        /// <summary>
        /// Tests that CreatePost creates a blog post with the new CreateBlogPostCommand.
        /// </summary>
        [TestMethod]
        public async Task CreatePost_SucceedsWithValidData()
        {
            // Arrange
            var title = "New Blog Post";

            // Act
            var result = await controller.CreatePost(blogStream.BlogKey, title);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = (RedirectToActionResult)result;
            Assert.AreEqual("Edit", redirect.ActionName);
            Assert.AreEqual("Editor", redirect.ControllerName);

            // Verify the post was created in the database
            var articleNumber = (int)redirect.RouteValues["id"];
            var createdPost = await Db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNotNull(createdPost);
            Assert.AreEqual(title, createdPost.Title);
            Assert.AreEqual(blogStream.BlogKey, createdPost.BlogKey);
            Assert.AreEqual((int)ArticleType.BlogPost, createdPost.ArticleType);
            Assert.AreEqual($"{blogStream.BlogKey}/new-blog-post", createdPost.UrlPath);
        }

        /// <summary>
        /// Tests that CreatePost returns NotFound when blog does not exist.
        /// </summary>
        [TestMethod]
        public async Task CreatePost_ReturnsNotFound_WhenBlogDoesNotExist()
        {
            // Arrange
            var invalidBlogKey = "non-existent-blog";
            var title = "New Post";

            // Act
            var result = await controller.CreatePost(invalidBlogKey, title);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        /// <summary>
        /// Tests that CreatePost returns BadRequest when title is empty.
        /// </summary>
        [TestMethod]
        public async Task CreatePost_ReturnsBadRequest_WhenTitleIsEmpty()
        {
            // Arrange
            var emptyTitle = string.Empty;

            // Act
            var result = await controller.CreatePost(blogStream.BlogKey, emptyTitle);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that CreatePost normalizes the title into a slug for the UrlPath.
        /// </summary>
        [TestMethod]
        public async Task CreatePost_NormalizesTitle_IntoSlug()
        {
            // Arrange
            var title = "My Awesome Blog Post!";
            var expectedSlug = "my-awesome-blog-post";

            // Act
            var result = await controller.CreatePost(blogStream.BlogKey, title);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = (RedirectToActionResult)result;

            var articleNumber = (int)redirect.RouteValues["id"];
            var createdPost = await Db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsTrue(createdPost.UrlPath.Contains(expectedSlug));
        }

        #endregion

        #region ConfirmDeletePost Tests

        /// <summary>
        /// Tests that ConfirmDeletePost successfully deletes a blog post with the new DeleteBlogPostCommand.
        /// </summary>
        [TestMethod]
        public async Task ConfirmDeletePost_SuccessfullyDeletesPost()
        {
            // Arrange - Create a blog post first
            var createResult = await controller.CreatePost(blogStream.BlogKey, "Post to Delete");
            var redirect = (RedirectToActionResult)createResult;
            var articleNumber = (int)redirect.RouteValues["id"];

            var beforeDelete = await Db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNotNull(beforeDelete);

            // Act
            var result = await controller.ConfirmDeletePost(blogStream.BlogKey, articleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(BlogController.Posts), redirectResult.ActionName);

            // Verify the post is marked as deleted
            var deletedPost = await Db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNotNull(deletedPost);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, deletedPost.StatusCode);
        }

        /// <summary>
        /// Tests that ConfirmDeletePost marks all versions of a post as deleted.
        /// </summary>
        [TestMethod]
        public async Task ConfirmDeletePost_DeletesAllVersions_OfPost()
        {
            // Arrange - Create a post and edit it to create multiple versions
            var createResult = await controller.CreatePost(blogStream.BlogKey, "Multi-Version Post");
            var redirect = (RedirectToActionResult)createResult;
            var articleNumber = (int)redirect.RouteValues["id"];

            var originalPost = await Db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();

            // Create a second version by updating via command
            var updateCommand = new UpdateBlogPostCommand
            {
                ArticleNumber = articleNumber,
                Title = "Updated",
                Content = "<p>Updated</p>",
                UserId = TestUserId
            };
            await Mediator.SendAsync(updateCommand);

            // Act
            var deleteResult = await controller.ConfirmDeletePost(blogStream.BlogKey, articleNumber);

            // Assert
            Assert.IsInstanceOfType(deleteResult, typeof(RedirectToActionResult));

            // Verify both versions are marked deleted
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .ToListAsync();

            Assert.IsTrue(allVersions.All(v => v.StatusCode == (int)StatusCodeEnum.Deleted),
                "All versions should be marked as deleted");
        }

        /// <summary>
        /// Tests that ConfirmDeletePost returns appropriate error message when post not found.
        /// </summary>
        [TestMethod]
        public async Task ConfirmDeletePost_HandlesNotFound_Gracefully()
        {
            // Arrange
            var nonExistentArticleNumber = 99999;

            // Act
            var result = await controller.ConfirmDeletePost(blogStream.BlogKey, nonExistentArticleNumber);

            // Assert
            // When DeleteBlogPostCommand fails, it redirects with error message
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(BlogController.Posts), redirectResult.ActionName);
        }

        /// <summary>
        /// Tests that ConfirmDeletePost verifies BlogKey ownership.
        /// </summary>
        [TestMethod]
        public async Task ConfirmDeletePost_VerifiesBlogKeyOwnership()
        {
            // Arrange - Create a post in test-blog
            var createResult = await controller.CreatePost(blogStream.BlogKey, "Ownership Test");
            var redirect = (RedirectToActionResult)createResult;
            var articleNumber = (int)redirect.RouteValues["id"];

            // Try to delete from a different blog key
            var wrongBlogKey = "different-blog";

            // Act
            var result = await controller.ConfirmDeletePost(wrongBlogKey, articleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            // The post should still exist with Active status (deletion should have failed)
            var post = await Db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.AreEqual((int)StatusCodeEnum.Active, post.StatusCode);
        }

        #endregion

    }
}
