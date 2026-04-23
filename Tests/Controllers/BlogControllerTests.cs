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
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services.BlogPublishing;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Controllers;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Blogs.CreatePost;
    using Sky.Editor.Features.Blogs.DeleteBlog;
    using Sky.Editor.Features.Blogs.DeleteStream;
    using Sky.Editor.Features.Blogs.GetBlog;
    using Sky.Editor.Features.Blogs.GetStream;
    using Sky.Editor.Features.Blogs.UpdateBlog;
    using Sky.Editor.Features.Blogs.UpdateStream;
    using Sky.Editor.Models.Blogs;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Integration tests for the <see cref="BlogController"/> class.
    /// </summary>
    [TestClass]
    public class BlogControllerTests : SkyCmsTestBase
    {
        private BlogController controller = null!;
        private Mock<IMediator> mediatorMock = null!;
        private Mock<UserManager<IdentityUser>> userManagerMock = null!;
        private Mock<IBlogStreamRenderingService> blogRenderingServiceMock = null!;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);

            mediatorMock = new Mock<IMediator>();
            blogRenderingServiceMock = new Mock<IBlogStreamRenderingService>();

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

            // Configure BlogStreamRenderingService to return dummy HTML
            blogRenderingServiceMock
                .Setup(x => x.GenerateBlogStreamWrapperAsync(It.IsAny<Article>(), It.IsAny<string>()))
                .ReturnsAsync("<html><body>Blog Stream</body></html>");

            blogRenderingServiceMock
                .Setup(x => x.GenerateBlogPostSnippetAsync(It.IsAny<Article>()))
                .ReturnsAsync("<html><body>Blog Entry</body></html>");

            controller = new BlogController(
                Db,
                SlugService,
                TemplateService,
                userManagerMock.Object,
                blogRenderingServiceMock.Object,  // Use mocked BlogRenderingService
                mediatorMock.Object,
                LayoutCacheService,
                DynamicConfigurationProvider
            );

            // Set up controller context with authenticated user
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com")
            }, "TestAuthentication"));

            var httpContext = new DefaultHttpContext { User = user };

            // Set up TempData
            var tempDataProvider = new Mock<ITempDataProvider>();
            var tempDataDictionary = new TempDataDictionary(httpContext, tempDataProvider.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            controller.TempData = tempDataDictionary;
        }

        [TestCleanup]
        public void Cleanup()
        {
            controller?.Dispose();
            Db.Dispose();
        }

        #region Create Blog Stream Tests

        /// <summary>
        /// Tests that Create_ValidBlogStream_RedirectsToIndex.
        /// </summary>
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
                .Setup(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleViewModel>
                {
                    IsSuccess = true,
                    Data = new ArticleViewModel()
                });

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(BlogController.Posts), redirectResult.ActionName);

            mediatorMock.Verify(
                m => m.SendAsync(It.IsAny<CreateArticleCommand>(), default),
                Times.Once);
        }

        /// <summary>
        /// Tests that Create_InvalidModel_ReturnsViewWithErrors.
        /// </summary>
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

        /// <summary>
        /// Tests that Create_SaveFails_ReturnsViewWithValidationErrors.
        /// </summary>
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
                .Setup(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleViewModel>
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

        /// <summary>
        /// Tests that Create_TitleConflict_ReturnsViewWithError.
        /// </summary>
        [TestMethod]
        public async Task Create_TitleConflict_ReturnsViewWithError()
        {
            // Arrange
            await CreateArticleAsync("Existing Page", TestUserId);

            var model = new BlogStreamViewModel
            {
                Title = "Existing Page",
                Description = "Description"
            };

            var errors = new Dictionary<string, string[]>
            {
                { nameof(model.BlogKey), new[] { "A blog with this key already exists" } }
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<CreateArticleCommand>(), default))
                .ReturnsAsync(new CommandResult<ArticleViewModel>
                {
                    IsSuccess = false,
                    Errors = errors
                });

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsTrue(controller.ModelState.ContainsKey(nameof(model.BlogKey)));
        }

        #endregion

        #region Create Blog Entry Tests

        /// <summary>
        /// Tests that CreateEntry_ValidEntry_RedirectsToEditorEdit.
        /// </summary>
        [TestMethod]
        public async Task CreateEntry_ValidEntry_RedirectsToEditorEdit()
        {
            // Arrange
            var blogKey = "tech-blog";
            await CreateArticleAsync("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<CreateBlogPostCommand>(), default))
                .ReturnsAsync(new CommandResult<CreateBlogPostCommandResult>
                {
                    IsSuccess = true,
                    Data = new CreateBlogPostCommandResult { ArticleNumber = 123 }
                });

            // Act
            var result = await controller.CreateEntry(blogKey, "New Blog Post");

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Edit", redirectResult.ActionName);
            Assert.AreEqual("Editor", redirectResult.ControllerName);

            mediatorMock.Verify(
                m => m.SendAsync(It.IsAny<CreateBlogPostCommand>(), default),
                Times.Once);
        }

        /// <summary>
        /// Tests that CreateEntry_InvalidBlogKey_ReturnsNotFound.
        /// </summary>
        [TestMethod]
        public async Task CreateEntry_InvalidBlogKey_ReturnsNotFound()
        {
            // Act
            var result = await controller.CreateEntry("non-existent-blog", "Test Entry");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        /// <summary>
        /// Tests that CreateEntry_EmptyTitle_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task CreateEntry_EmptyTitle_ReturnsBadRequest()
        {
            // Arrange
            var blogKey = "tech-blog";
            await CreateArticleAsync("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);

            // Act
            var result = await controller.CreateEntry(blogKey, "");

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that CreateEntry_SaveFails_ReturnsServerError.
        /// </summary>
        [TestMethod]
        public async Task CreateEntry_SaveFails_ReturnsServerError()
        {
            // Arrange
            var blogKey = "tech-blog";
            await CreateArticleAsync("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<CreateBlogPostCommand>(), default))
                .ReturnsAsync(new CommandResult<CreateBlogPostCommandResult>
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

        #region Delete Blog Entry Tests

        /// <summary>
        /// Tests that ConfirmDeleteEntry_ValidEntry_RedirectsToEntries.
        /// </summary>
        [TestMethod]
        public async Task ConfirmDeleteEntry_ValidEntry_RedirectsToEntries()
        {
            // Arrange
            var blogKey = "tech-blog";
            await CreateArticleAsync("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);
            var entry = await CreateArticleAsync("Blog Post", TestUserId, null, blogKey, ArticleType.BlogPost);

            // Configure mediator to handle DeleteArticleCommand
            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<Sky.Editor.Features.Articles.Delete.DeleteArticleCommand>(), default))
                .ReturnsAsync((Sky.Editor.Features.Articles.Delete.DeleteArticleCommand cmd, System.Threading.CancellationToken ct) =>
                {
                    // Simulate the delete operation by marking the article as deleted
                    var articles = Db.Articles.Where(a => a.ArticleNumber == cmd.ArticleNumber).ToList();
                    foreach (var article in articles)
                    {
                        article.StatusCode = (int)StatusCodeEnum.Deleted;
                    }
                    Db.SaveChanges();
                    return new CommandResult<Unit> { IsSuccess = true, Data = Unit.Value };
                });

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

        #region Additional CRUD Tests

        /// <summary>
        /// Tests that Index_ReturnsView.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsView()
        {
            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Index", viewResult.ViewName);
        }

        /// <summary>
        /// Tests that Create_Get_ReturnsCreateView.
        /// </summary>
        [TestMethod]
        public void Create_Get_ReturnsCreateView()
        {
            // Act
            var result = controller.Create();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Create", viewResult.ViewName);
            Assert.IsInstanceOfType(viewResult.Model, typeof(BlogViewModel));
        }

        /// <summary>
        /// Tests that Delete_Get_ReturnsDeleteConfirmationView.
        /// </summary>
        [TestMethod]
        public async Task Delete_Get_ReturnsDeleteConfirmationView()
        {
            // Arrange
            var blog = await CreateArticleAsync("Blog to Delete", TestUserId, null, "blog-delete", ArticleType.BlogStream);
            var blogEntity = await Db.Articles.FirstAsync(a => a.ArticleNumber == blog.ArticleNumber);

            mediatorMock
                .Setup(m => m.QueryAsync(It.IsAny<GetBlogQuery>(), default))
                .ReturnsAsync(new CommandResult<GetBlogQueryResult>
                {
                    IsSuccess = true,
                    Data = new GetBlogQueryResult
                    {
                        Article = blogEntity,
                        Title = "Blog to Delete",
                        BlogKey = "blog-delete"
                    }
                });

            // Act
            var result = await controller.Delete(blogEntity.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Delete", viewResult.ViewName);
            Assert.IsInstanceOfType(viewResult.Model, typeof(BlogViewModel));

            var model = (BlogViewModel)viewResult.Model;
            Assert.AreEqual(blogEntity.Id, model.Id);
            Assert.AreEqual("Blog to Delete", model.Title);
        }

        /// <summary>
        /// Tests that Delete_Get_ReturnsNotFound_WhenBlogNotExists.
        /// </summary>
        [TestMethod]
        public async Task Delete_Get_ReturnsNotFound_WhenBlogNotExists()
        {
            // Arrange
            mediatorMock
                .Setup(m => m.QueryAsync(It.IsAny<GetBlogQuery>(), default))
                .ReturnsAsync(new CommandResult<GetBlogQueryResult>
                {
                    IsSuccess = false
                });

            // Act
            var result = await controller.Delete(Guid.NewGuid());

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Edit Blog Stream Tests

        /// <summary>
        /// Tests that Edit_Get_ReturnsEditView_WhenBlogExists.
        /// </summary>
        [TestMethod]
        public async Task Edit_Get_ReturnsEditView_WhenBlogExists()
        {
            // Arrange
            var blog = await CreateArticleAsync("Edit Test Blog", TestUserId, null, "edit-test", ArticleType.BlogStream);
            var blogEntity = await Db.Articles.FirstAsync(a => a.ArticleNumber == blog.ArticleNumber);
            blogEntity.Introduction = "Test description";
            blogEntity.BannerImage = "/images/hero.jpg";
            await Db.SaveChangesAsync();

            mediatorMock
                .Setup(m => m.QueryAsync(It.IsAny<GetBlogQuery>(), default))
                .ReturnsAsync(new CommandResult<GetBlogQueryResult>
                {
                    IsSuccess = true,
                    Data = new GetBlogQueryResult
                    {
                        Article = blogEntity,
                        Title = "Edit Test Blog",
                        Description = "Test description",
                        HeroImage = "/images/hero.jpg",
                        UrlPath = "edit-test",
                        Published = blogEntity.Published
                    }
                });

            // Act
            var result = await controller.Edit(blogEntity.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Edit", viewResult.ViewName);
            Assert.IsInstanceOfType(viewResult.Model, typeof(BlogViewModel));

            var model = (BlogViewModel)viewResult.Model;
            Assert.AreEqual("Edit Test Blog", model.Title);
            Assert.AreEqual("Test description", model.Description);
            Assert.AreEqual("/images/hero.jpg", model.HeroImage);
        }

        /// <summary>
        /// Tests that Edit_Get_ReturnsNotFound_WhenBlogNotExists.
        /// </summary>
        [TestMethod]
        public async Task Edit_Get_ReturnsNotFound_WhenBlogNotExists()
        {
            // Arrange
            mediatorMock
                .Setup(m => m.QueryAsync(It.IsAny<GetBlogQuery>(), default))
                .ReturnsAsync(new CommandResult<GetBlogQueryResult>
                {
                    IsSuccess = false
                });

            // Act
            var result = await controller.Edit(Guid.NewGuid());

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that Edit_Post_UpdatesBlogStream_WhenValid.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_UpdatesBlogStream_WhenValid()
        {
            // Arrange
            var blog = await CreateArticleAsync("Original Blog", TestUserId, null, "original", ArticleType.BlogStream);
            var blogEntity = await Db.Articles.FirstAsync(a => a.ArticleNumber == blog.ArticleNumber);

            var model = new BlogStreamViewModel
            {
                Id = blogEntity.Id,
                Title = "Updated Blog Title",
                Description = "Updated description",
                HeroImage = "/images/updated-hero.jpg",
                Published = DateTimeOffset.UtcNow
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<UpdateBlogCommand>(), default))
                .ReturnsAsync(new CommandResult<Article>
                {
                    IsSuccess = true,
                    Data = blogEntity
                });

            // Act
            var result = await controller.Edit(blogEntity.Id, model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Index", redirectResult.ActionName);
        }

        /// <summary>
        /// Tests that Edit_Post_ReturnsBadRequest_WhenIdMismatch.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_ReturnsBadRequest_WhenIdMismatch()
        {
            // Arrange
            var blog = await CreateArticleAsync("Test Blog", TestUserId, null, "test", ArticleType.BlogStream);
            var blogEntity = await Db.Articles.FirstAsync(a => a.ArticleNumber == blog.ArticleNumber);

            var model = new BlogStreamViewModel
            {
                Id = Guid.NewGuid(), // Different ID
                Title = "Updated Title"
            };

            // Act
            var result = await controller.Edit(blogEntity.Id, model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestResult));
        }

        /// <summary>
        /// Tests that Edit_Post_ValidatesTitleConflict.
        /// </summary>
        [TestMethod]
        public async Task Edit_Post_ValidatesTitleConflict()
        {
            // Arrange
            var blog1 = await CreateArticleAsync("Existing Blog", TestUserId, null, "existing", ArticleType.BlogStream);
            var blog2 = await CreateArticleAsync("Test Blog", TestUserId, null, "test", ArticleType.BlogStream);
            var blog2Entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == blog2.ArticleNumber);

            var model = new BlogStreamViewModel
            {
                Id = blog2Entity.Id,
                Title = "Existing Blog", // Conflicts with blog1
                Description = "Test"
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<UpdateBlogCommand>(), default))
                .ReturnsAsync(new CommandResult<Article>
                {
                    IsSuccess = false,
                    ErrorMessage = "Title conflict",
                    Errors = new Dictionary<string, string[]>
                    {
                        { "BlogKey", new[] { "A blog stream with this title already exists." } }
                    }
                });

            // Act
            var result = await controller.Edit(blog2Entity.Id, model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsFalse(controller.ModelState.IsValid);
            Assert.IsTrue(controller.ModelState.ContainsKey("BlogKey"));
        }

        #endregion

        #region Blog Entry Listing Tests

        /// <summary>
        /// Tests that Posts_ReturnsViewWithBlogPosts.
        /// </summary>
        [TestMethod]
        public async Task Posts_ReturnsViewWithBlogPosts()
        {
            // Arrange
            var blogKey = "tech-blog";
            var blog = await CreateArticleAsync("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);
            var entry1 = await CreateArticleAsync("Entry 1", TestUserId, null, blogKey, ArticleType.BlogPost);
            var entry2 = await CreateArticleAsync("Entry 2", TestUserId, null, blogKey, ArticleType.BlogPost);

            // Publish the blog entries to create catalog entries
            var article1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == entry1.ArticleNumber);
            var article2 = await Db.Articles.FirstAsync(a => a.ArticleNumber == entry2.ArticleNumber);
            await Logic.PublishArticle(article1.Id, DateTimeOffset.UtcNow);
            await Logic.PublishArticle(article2.Id, DateTimeOffset.UtcNow);

            // Update catalog entries to link them to the blog
            var catalog1 = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == entry1.ArticleNumber);
            var catalog2 = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == entry2.ArticleNumber);
            catalog1.BlogKey = blogKey;
            catalog2.BlogKey = blogKey;
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Posts(blogKey);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("Entries", viewResult.ViewName);

            var model = viewResult.Model as BlogPostsListViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(blogKey, model.BlogKey);
            Assert.AreEqual(2, model.Entries.Count);
        }

        /// <summary>
        /// Tests that Posts_ReturnsBadRequest_WhenBlogKeyNull.
        /// </summary>
        [TestMethod]
        public async Task Posts_ReturnsBadRequest_WhenBlogKeyNull()
        {
            // Act
            var result = await controller.Posts(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestResult));
        }

        /// <summary>
        /// Tests that Posts_ReturnsNotFound_WhenBlogNotExists.
        /// </summary>
        [TestMethod]
        public async Task Posts_ReturnsNotFound_WhenBlogNotExists()
        {
            // Act
            var result = await controller.Posts("nonexistent-blog");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region JSON API Tests

        /// <summary>
        /// Tests that GetBlogs_ReturnsJsonListOfBlogs.
        /// </summary>
        [TestMethod]
        public async Task GetBlogs_ReturnsJsonListOfBlogs()
        {
            // Arrange
            var blog1 = await CreateArticleAsync("Blog A", TestUserId, null, "blog-a", ArticleType.BlogStream);
            var blog2 = await CreateArticleAsync("Blog B", TestUserId, null, "blog-b", ArticleType.BlogStream);

            // Act
            var result = await controller.GetBlogs();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var data = jsonResult.Value as List<BlogViewModel>;

            Assert.IsNotNull(data);
            Assert.IsTrue(data.Count >= 2, $"Expected at least 2 blogs, found {data.Count}");
            Assert.IsTrue(data.Any(b => b.Title == "Blog A"));
            Assert.IsTrue(data.Any(b => b.Title == "Blog B"));
        }

        /// <summary>
        /// Tests that GetPosts_ReturnsJsonListOfPosts.
        /// </summary>
        [TestMethod]
        public async Task GetPosts_ReturnsJsonListOfPosts()
        {
            // Arrange
            var blogKey = "tech-blog";
            var blog = await CreateArticleAsync("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);
            var entry1 = await CreateArticleAsync("Entry 1", TestUserId, null, blogKey, ArticleType.BlogPost);
            var entry2 = await CreateArticleAsync("Entry 2", TestUserId, null, blogKey, ArticleType.BlogPost);

            // Act
            var result = await controller.GetPosts(blogKey);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        /// <summary>
        /// Tests that GetPosts_ReturnsBadRequest_WhenBlogKeyNull.
        /// </summary>
        [TestMethod]
        public async Task GetPosts_ReturnsBadRequest_WhenBlogKeyNull()
        {
            // Act
            var result = await controller.GetPosts(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestResult));
        }

        /// <summary>
        /// Tests that GetPosts_ReturnsNotFound_WhenBlogNotExists.
        /// </summary>
        [TestMethod]
        public async Task GetPosts_ReturnsNotFound_WhenBlogNotExists()
        {
            // Act
            var result = await controller.GetPosts("nonexistent");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Delete Post Tests

        /// <summary>
        /// Tests that DeletePost_Get_ReturnsDeleteConfirmationView.
        /// </summary>
        [TestMethod]
        public async Task DeletePost_Get_ReturnsDeleteConfirmationView()
        {
            // Arrange
            var blogKey = "tech-blog";
            await CreateArticleAsync("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);
            var entry = await CreateArticleAsync("Entry to Delete", TestUserId, null, blogKey, ArticleType.BlogPost);

            // Create catalog entry manually since CreateArticle doesn't create it for unpublished articles
            var catalog = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == entry.ArticleNumber);
            if (catalog == null)
            {
                catalog = new CatalogEntry
                {
                    ArticleNumber = entry.ArticleNumber,
                    BlogKey = blogKey,
                    Title = entry.Title,
                    UrlPath = entry.UrlPath,
                    Published = entry.Published,
                    Updated = entry.Updated
                };
                Db.ArticleCatalog.Add(catalog);
            }
            else
            {
                catalog.BlogKey = blogKey;
            }
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.DeletePost(blogKey, entry.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("DeleteEntry", viewResult.ViewName);

            var model = viewResult.Model as BlogPostListItem;
            Assert.IsNotNull(model);
            Assert.AreEqual(entry.ArticleNumber, model.ArticleNumber);
        }

        /// <summary>
        /// Tests that DeletePost_Get_ReturnsNotFound_WhenPostNotExists.
        /// </summary>
        [TestMethod]
        public async Task DeletePost_Get_ReturnsNotFound_WhenPostNotExists()
        {
            // Act
            var result = await controller.DeletePost("tech-blog", 99999);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that DeletePost_Get_ReturnsNotFound_WhenBlogKeyMismatch.
        /// </summary>
        [TestMethod]
        public async Task DeletePost_Get_ReturnsNotFound_WhenBlogKeyMismatch()
        {
            // Arrange
            var entry = await CreateArticleAsync("Entry", TestUserId, null, "blog-a", ArticleType.BlogPost);
            var catalog = await Db.ArticleCatalog.FirstAsync(c => c.ArticleNumber == entry.ArticleNumber);
            catalog.BlogKey = "blog-a";
            await Db.SaveChangesAsync();

            // Act - Try to delete with wrong blog key
            var result = await controller.DeletePost("blog-b", entry.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Preview Tests

        /// <summary>
        /// Tests that PreviewStream_ReturnsPreviewView_WhenBlogExists.
        /// </summary>
        [TestMethod]
        public async Task PreviewStream_ReturnsPreviewView_WhenBlogExists()
        {
            // Arrange
            var blogKey = "preview-blog";
            var blog = await CreateArticleAsync("Preview Blog", TestUserId, null, blogKey, ArticleType.BlogStream);

            // Act
            var result = await controller.PreviewStream(blogKey);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsTrue(viewResult.ViewName.EndsWith("/Views/Home/Preview.cshtml"));
        }

        /// <summary>
        /// Tests that PreviewStream_ReturnsNotFound_WhenBlogNotExists.
        /// </summary>
        [TestMethod]
        public async Task PreviewStream_ReturnsNotFound_WhenBlogNotExists()
        {
            // Act
            var result = await controller.PreviewStream("nonexistent-blog");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Delete Blog Stream Tests

        /// <summary>
        /// Tests that ConfirmDelete_DeletesBlogAndAllPosts.
        /// </summary>
        [TestMethod]
        public async Task ConfirmDelete_DeletesBlogAndAllPosts()
        {
            // Arrange
            // Create a home page first to avoid the blog being marked as the home page
            await CreateArticleAsync("Home Page", TestUserId, null, "", ArticleType.General);

            var blogKey = "delete-test";
            var blog = await CreateArticleAsync("Blog to Delete", TestUserId, null, blogKey, ArticleType.BlogStream);
            var entry1 = await CreateArticleAsync("Entry 1", TestUserId, null, blogKey, ArticleType.BlogPost);
            var entry2 = await CreateArticleAsync("Entry 2", TestUserId, null, blogKey, ArticleType.BlogPost);

            var blogEntity = await Db.Articles.FirstAsync(a => a.ArticleNumber == blog.ArticleNumber);

            // Configure mediator to return successful result for DeleteBlogStreamCommand
            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<DeleteBlogStreamCommand>()))
                .ReturnsAsync(CommandResult<bool>.Success(true));

            // Act
            var result = await controller.ConfirmDelete(blogEntity.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(BlogController.Index), redirectResult.ActionName);

            // Verify success message is set
            Assert.IsTrue(controller.TempData.ContainsKey("Success"));
            Assert.AreEqual("Blog and all posts deleted successfully", controller.TempData["Success"]);

            // Verify mediator was called with correct command
            mediatorMock.Verify(
                m => m.SendAsync(It.Is<DeleteBlogStreamCommand>(cmd =>
                    cmd.Id == blogEntity.Id &&
                    cmd.UserId == TestUserId)),
                Times.Once);
        }

        /// <summary>
        /// Tests that ConfirmDelete_ReturnsNotFound_WhenBlogNotExists.
        /// </summary>
        [TestMethod]
        public async Task ConfirmDelete_ReturnsNotFound_WhenBlogNotExists()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Configure mediator to return failure result indicating blog not found
            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<DeleteBlogStreamCommand>()))
                .ReturnsAsync(CommandResult<bool>.Failure("Blog not found"));

            // Act
            var result = await controller.ConfirmDelete(nonExistentId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        #endregion

        #region Create Entry Error Handling Tests

        /// <summary>
        /// Tests that CreateEntry_ReturnsNotFound_WhenBlogNotExists.
        /// </summary>
        [TestMethod]
        public async Task CreateEntry_ReturnsNotFound_WhenBlogNotExists()
        {
            // Act
            var result = await controller.CreateEntry("nonexistent-blog", "Test Entry");

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
        }

        /// <summary>
        /// Tests that CreateEntry_ReturnsBadRequest_WhenTitleEmpty.
        /// </summary>
        [TestMethod]
        public async Task CreateEntry_ReturnsBadRequest_WhenTitleEmpty()
        {
            // Arrange
            var blogKey = "tech-blog";
            await CreateArticleAsync("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);

            // Act
            var result = await controller.CreateEntry(blogKey, "");

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that CreateEntry_ReturnsError_WhenMediatorFails.
        /// </summary>
        [TestMethod]
        public async Task CreateEntry_ReturnsError_WhenMediatorFails()
        {
            // Arrange
            var blogKey = "tech-blog";
            await CreateArticleAsync("Tech Blog", TestUserId, null, blogKey, ArticleType.BlogStream);

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<CreateBlogPostCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult<CreateBlogPostCommandResult>
                {
                    IsSuccess = false,
                    ErrorMessage = "Failed to create entry"
                });

            // Act
            var result = await controller.CreateEntry(blogKey, "New Entry");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(500, objectResult.StatusCode);
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Tests that EndToEnd_CreateBlogStreamAndEntry_Success.
        /// </summary>
        [TestMethod]
        public async Task EndToEnd_CreateBlogStreamAndEntry_Success()
        {
            // Arrange - Create blog stream using ArticleLogic directly (like other tests)
            var blogStream = await CreateArticleAsync(
                "My Tech Blog",
                TestUserId,
                null,
                "my-tech-blog",
                ArticleType.BlogStream);

            // Update introduction via database
            var blogStreamEntity = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == blogStream.ArticleNumber);
            Assert.IsNotNull(blogStreamEntity);
            blogStreamEntity.Introduction = "A blog about tech";
            await Db.SaveChangesAsync();

            // Verify blog stream exists and has correct properties
            Assert.AreEqual("My Tech Blog", blogStreamEntity.Title);
            Assert.AreEqual((int)ArticleType.BlogStream, blogStreamEntity.ArticleType);
            Assert.AreEqual("my-tech-blog", blogStreamEntity.BlogKey);

            // Mock CreateBlogPostCommand for blog entry creation
            var mockEntryResult = new CreateBlogPostCommandResult
            {
                ArticleNumber = 99,
                BlogKey = blogStreamEntity.BlogKey,
                UrlPath = $"{blogStreamEntity.BlogKey}/first-post"
            };

            mediatorMock
                .Setup(m => m.SendAsync(It.IsAny<CreateBlogPostCommand>(), default))
                .ReturnsAsync(new CommandResult<CreateBlogPostCommandResult>
                {
                    IsSuccess = true,
                    Data = mockEntryResult
                });

            // Act - Create blog entry via controller
            var entryResult = await controller.CreateEntry(blogStreamEntity.BlogKey, "First Post");

            // Assert
            Assert.IsInstanceOfType(entryResult, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)entryResult;
            Assert.AreEqual("Edit", redirectResult.ActionName);
            Assert.AreEqual("Editor", redirectResult.ControllerName);
            Assert.AreEqual(mockEntryResult.ArticleNumber, redirectResult.RouteValues["id"]);

            // Verify mediator was called with correct command
            mediatorMock.Verify(
                m => m.SendAsync(It.Is<CreateBlogPostCommand>(cmd =>
                    cmd.Title == "First Post" &&
                    cmd.BlogKey == blogStreamEntity.BlogKey),
                default),
                Times.Once);
        }

        #endregion
    }
}

