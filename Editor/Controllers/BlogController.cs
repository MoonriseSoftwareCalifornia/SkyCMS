// <copyright file="BlogController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Services.BlogPublishing;
    using Cosmos.Common.Services.Caching;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Articles.Delete;
    using Sky.Editor.Features.Blogs.CreatePost;
    using Sky.Editor.Features.Blogs.DeleteBlog;
    using Sky.Editor.Features.Blogs.GetBlog;
    using Sky.Editor.Features.Blogs.UpdateBlog;
    using Sky.Editor.Models.Blogs;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Templates;

    /// <summary>
    /// Editor-facing controller for managing blogs (multi-blog support) and their posts.
    /// </summary>
    /// <remarks>
    /// Responsibilities:
    /// <list type="bullet">
    ///   <item>Create, list, edit, and delete blogs.</item>
    ///   <item>Enforce uniqueness and validation of <c>BlogKey</c> values (route-safe identifiers).</item>
    ///   <item>Maintain a single default blog used as a reassignment target.</item>
    ///   <item>Create, edit, publish, and delete blog posts via vertical slice architecture.</item>
    ///   <item>Provide JSON listing endpoints for client-side selection widgets.</item>
    ///   <item>Provide an anonymous preview (<see cref="PreviewStream(string)"/>) for a specific blog.</item>
    /// </list>
    /// Security:
    /// All actions require authentication via <see cref="AuthorizeAttribute"/> except the preview endpoint which allows anonymous access.
    /// </remarks>
    [Authorize]
    [Route("editor/blogs")]
    public class BlogController : Cms.Controllers.BaseController
    {
        private readonly ApplicationDbContext db;
        private readonly ISlugService slugService;
        private readonly ITemplateService templateService;
        private readonly IBlogStreamRenderingService blogStreamRenderingService;
        private readonly IMediator mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlogController"/> class.
        /// </summary>
        /// <param name="db">Application database context.</param>
        /// <param name="slugService">Slug normalization and uniqueness helper.</param>
        /// <param name="templateService">Template management service.</param>
        /// <param name="userManager">User management service.</param>
        /// <param name="blogStreamRenderingService">Blog stream rendering service for modern client-side orchestration.</param>
        /// <param name="mediator">Mediator for dispatching commands.</param>
        /// <param name="memoryCache">Memory cache for layout caching.</param>
        /// <param name="configProvider">Dynamic configuration provider for tenant-aware caching.</param>
        public BlogController(
            ApplicationDbContext db,
            ISlugService slugService,
            ITemplateService templateService,
            UserManager<IdentityUser> userManager,
            IBlogStreamRenderingService blogStreamRenderingService,
            IMediator mediator,
            ICacheService<Layout> memoryCache,
            IDynamicConfigurationProvider configProvider)
            : base(db, userManager, mediator, memoryCache, configProvider)
        {
            this.db = db;
            this.slugService = slugService;
            this.templateService = templateService;
            this.blogStreamRenderingService = blogStreamRenderingService;
            this.mediator = mediator;
        }

        /// <summary>
        /// Lists all blogs ordered by sort order then key.
        /// </summary>
        /// <returns>Index view containing a list of <see cref="BlogViewModel"/>.</returns>
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // Ensure that the blog template exists.
            await templateService.EnsureDefaultTemplatesExistAsync();
            return View("Index");
        }

        /// <summary>
        /// Displays the create blog form.
        /// </summary>
        /// <returns>Create view with default model.</returns>
        [HttpGet("create")]
        public IActionResult Create() =>
            View("Create", new BlogViewModel());

        /// <summary>
        /// Handles blog creation.
        /// </summary>
        /// <param name="model">Submitted blog view model.</param>
        /// <returns>Redirect to <see cref="Index"/> on success; same view with validation errors otherwise.</returns>
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogStreamViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Create", model);
            }

            // REMOVED: Title validation now handled in CreateArticleHandler
            // if (!await titleChangeService.ValidateTitle(model.Title, null)) { ... }

            // Normalize blog key from title
            model.BlogKey = slugService.Normalize(model.Title);

            var defaultLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(db);

            var blogStreamTemplate = await db.Templates.FirstOrDefaultAsync(t => t.LayoutId == defaultLayout.Id && t.PageType == "blog-stream");

            if (blogStreamTemplate == null)
            {
                throw new InvalidOperationException("Blog stream template not found.");
            }

            // Normalize image URL to relative path if needed (controller-level concern)
            var heroImage = model.HeroImage;
            if (!string.IsNullOrWhiteSpace(heroImage) && Uri.IsWellFormedUriString(heroImage, UriKind.Absolute))
            {
                var uri = new Uri(heroImage);

                // Only convert if it's from the current host
                if (uri.Host.Equals(Request.Host.Host, StringComparison.OrdinalIgnoreCase))
                {
                    heroImage = uri.PathAndQuery;
                }
            }

            // CreateArticleHandler will validate title and return error if conflicts exist
            var command = new CreateArticleCommand
            {
                Title = model.Title,
                TemplateId = blogStreamTemplate.Id,
                UserId = Guid.Parse(await GetUserId()),
                ArticleType = ArticleType.BlogStream,
                BlogKey = model.BlogKey,
                BannerImage = heroImage,
                Introduction = model.Description,
                ContentOverride = blogStreamTemplate.Content, // Blog streams use template content as-is
                Published = DateTimeOffset.UtcNow // Publish immediately upon creation
            };

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                // Title validation errors will be in result.Errors["Title"]
                if (result.Errors != null)
                {
                    foreach (var error in result.Errors)
                    {
                        foreach (var message in error.Value)
                        {
                            ModelState.AddModelError(error.Key, message);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    ModelState.AddModelError(string.Empty, result.ErrorMessage);
                }

                return View("Create", model);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Displays edit form for a specified blog.
        /// </summary>
        /// <param name="id">Blog identifier (GUID).</param>
        /// <returns>Edit view or 404 if not found.</returns>
        [HttpGet("{id:guid}/edit")]
        public new async Task<IActionResult> Edit(Guid id)
        {
            var query = new GetBlogQuery
            {
                Id = id,
                UserId = Guid.Parse(await GetUserId())
            };

            var result = await mediator.QueryAsync(query);

            if (!result.IsSuccess || result.Data == null)
            {
                return NotFound();
            }

            var streamData = result.Data;

            return View("Edit", new BlogViewModel
            {
                Id = streamData.Article.Id,
                BlogKey = streamData.UrlPath,
                Title = streamData.Title,
                Description = streamData.Description,
                HeroImage = streamData.HeroImage,
                Published = streamData.Published
            });
        }

        /// <summary>
        /// Processes blog edits.
        /// </summary>
        /// <param name="id">Route blog identifier.</param>
        /// <param name="model">Edited blog view model.</param>
        /// <returns>Redirect to <see cref="Index"/> on success; edit view with errors otherwise.</returns>
        [HttpPost("{id:guid}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, BlogStreamViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View("Edit", model);
            }

            var command = new UpdateBlogCommand
            {
                Id = id,
                Title = model.Title,
                Description = model.Description,
                HeroImage = model.HeroImage,
                Published = model.Published,
                UserId = Guid.Parse(await GetUserId())
            };

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                // Title validation errors will be in result.Errors
                foreach (var error in result.Errors)
                {
                    foreach (var message in error.Value)
                    {
                        ModelState.AddModelError(error.Key, message);
                    }
                }

                return View("Edit", model);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Displays confirmation page for blog deletion.
        /// </summary>
        /// <param name="id">Blog identifier.</param>
        /// <returns>Delete confirmation view or 404.</returns>
        [HttpGet("{id:guid}/delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var query = new GetBlogQuery { Id = id };
            var result = await mediator.QueryAsync(query);

            if (!result.IsSuccess || result.Data == null)
            {
                return NotFound();
            }

            var streamData = result.Data;

            return View("Delete", new BlogViewModel
            {
                Id = streamData.Article.Id,
                BlogKey = streamData.BlogKey,
                Title = streamData.Title
            });
        }

        /// <summary>
        /// Performs deletion of a blog.
        /// </summary>
        /// <param name="id">Blog identifier.</param>
        /// <returns>Redirect to <see cref="Index"/> or view with errors.</returns>
        [HttpPost("{id:guid}/confirmdelete")]
        public async Task<IActionResult> ConfirmDelete(Guid id)
        {
            var command = new DeleteBlogCommand
            {
                Id = id,
                UserId = Guid.Parse(await GetUserId())
            };

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                // Check if the error indicates "not found"
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound();
                }

                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = "Blog and all posts deleted successfully";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Lists posts for a specific blog.
        /// </summary>
        /// <param name="blogKey">Unique blog key.</param>
        /// <returns>Posts view with listing model or 400/404 on invalid key.</returns>
        [HttpGet("{blogKey}/posts")]
        public Task<IActionResult> Posts(string blogKey) => ListPosts(blogKey);

        /// <summary>
        /// Legacy alias for listing posts for a specific blog.
        /// </summary>
        /// <param name="blogKey">Unique blog key.</param>
        /// <returns>Posts view with listing model or 400/404 on invalid key.</returns>
        [HttpGet("{blogKey}/entries")]
        public Task<IActionResult> Entries(string blogKey) => ListPosts(blogKey);

        private async Task<IActionResult> ListPosts(string blogKey)
        {
            if (string.IsNullOrWhiteSpace(blogKey))
            {
                return BadRequest();
            }

            var blog = await GetLatestStreamArticleAsync(blogKey);
            if (blog == null)
            {
                return NotFound();
            }

            var deletedEnum = (int)StatusCodeEnum.Deleted;
            var blogStreamArticleNumber = blog.ArticleNumber;
            var rawEntries = await db.Articles
                .Where(c => c.BlogKey == blogKey
                    && c.ArticleNumber != blogStreamArticleNumber
                    && c.StatusCode != deletedEnum)
                .Select(c => new
                {
                    c.BlogKey,
                    c.ArticleNumber,
                    c.Title,
                    c.Published,
                    c.Updated,
                    c.UrlPath,
                    c.Introduction,
                    c.BannerImage,
                    c.VersionNumber
                })
                .ToListAsync();

            var entries = rawEntries
                .GroupBy(e => e.ArticleNumber)
                .Select(g => g.OrderByDescending(e => e.VersionNumber).First())
                .Select(c => new BlogPostListItem
                {
                    BlogKey = c.BlogKey,
                    ArticleNumber = c.ArticleNumber,
                    Title = c.Title,
                    Published = c.Published,
                    Updated = c.Updated,
                    UrlPath = c.UrlPath,
                    Introduction = c.Introduction,
                    BannerImage = c.BannerImage
                })
                .ToList();

            var vm = new BlogPostsListViewModel
            {
                BlogKey = blog.BlogKey,
                BlogTitle = blog.Title,
                BlogDescription = blog.Introduction,
                HeroImage = blog.BannerImage,
                BlogUrlPath = blog.UrlPath,
                Entries = entries.Cast<BlogEntryListItem>().OrderByDescending(c => c.Published ?? c.Updated).ToList()
            };
            return View("Entries", vm);
        }

        /// <summary>
        /// Creates a new blog post for a given blog.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="title">Title of the blog post.</param>
        /// <returns>Redirect to editor on success, or 404/400/500 on failure.</returns>
        [HttpPost("{blogKey}/posts/create")]
        public Task<IActionResult> CreatePost(string blogKey, [FromForm] string title) => CreatePostCore(blogKey, title);

        /// <summary>
        /// Legacy alias for creating a new blog post for a given blog.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="title">Title of the blog post.</param>
        /// <returns>Redirect to editor on success, or 404/400/500 on failure.</returns>
        [HttpPost("{blogKey}/entries/create")]
        public Task<IActionResult> CreateEntry(string blogKey, [FromForm] string title) => CreatePostCore(blogKey, title);

        private async Task<IActionResult> CreatePostCore(string blogKey, string title)
        {
            var blogStreamType = (int)ArticleType.BlogStream;
            var blog = await db.Articles.FirstOrDefaultAsync(b => b.BlogKey == blogKey && b.ArticleType == blogStreamType);

            if (blog == null)
            {
                return NotFound("Blog not found.");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest("Title is required.");
            }

            var userId = Guid.Parse(await GetUserId());

            var command = new CreateBlogPostCommand
            {
                Title = title,
                Content = string.Empty,
                BlogKey = blogKey,
                TemplateId = Guid.Empty,
                UserId = userId,
                Published = null
            };

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                var errorMessage = result.ErrorMessage ?? "Failed to create blog post.";
                return StatusCode(500, $"Failed to create blog post: {errorMessage}");
            }

            return RedirectToAction("VisualEditor", "Editor", new { id = result.Data.ArticleNumber });
        }

        /// <summary>
        /// Displays delete confirmation for a blog post.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Delete post view or 404.</returns>
        [HttpGet("{blogKey}/posts/{articleNumber:int}/delete")]
        public Task<IActionResult> DeletePost(string blogKey, int articleNumber) => DeletePostCore(blogKey, articleNumber);

        /// <summary>
        /// Legacy alias for displaying delete confirmation for a blog post.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Delete post view or 404.</returns>
        [HttpGet("{blogKey}/entries/{articleNumber:int}/delete")]
        public Task<IActionResult> DeleteEntry(string blogKey, int articleNumber) => DeletePostCore(blogKey, articleNumber);

        private async Task<IActionResult> DeletePostCore(string blogKey, int articleNumber)
        {
            var article = await db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .FirstOrDefaultAsync();
            if (article == null || article.BlogKey != blogKey)
            {
                return NotFound();
            }

            var vm = new BlogPostListItem
            {
                BlogKey = article.BlogKey,
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Published = article.Published,
                Updated = article.Updated,
                UrlPath = article.UrlPath,
                Introduction = article.Introduction,
                BannerImage = article.BannerImage
            };
            return View("DeleteEntry", vm);
        }

        /// <summary>
        /// Deletes a blog post via logic layer.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Redirect to post listing.</returns>
        [HttpPost("{blogKey}/posts/{articleNumber:int}/confirmdelete")]
        public Task<IActionResult> ConfirmDeletePost(string blogKey, int articleNumber) => ConfirmDeletePostCore(blogKey, articleNumber);

        /// <summary>
        /// Legacy alias for deleting a blog post via logic layer.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Redirect to post listing.</returns>
        [HttpPost("{blogKey}/entries/{articleNumber:int}/confirmdeleteentry")]
        public Task<IActionResult> ConfirmDeleteEntry(string blogKey, int articleNumber) => ConfirmDeletePostCore(blogKey, articleNumber);

        private async Task<IActionResult> ConfirmDeletePostCore(string blogKey, int articleNumber)
        {
            try
            {
                var article = await db.Articles
                    .Where(a => a.ArticleNumber == articleNumber)
                    .FirstOrDefaultAsync();

                if (article == null)
                {
                    TempData["Error"] = "Article not found.";
                    return RedirectToAction(nameof(Posts), new { blogKey });
                }

                if (article.BlogKey != blogKey)
                {
                    TempData["Error"] = "Article does not belong to this blog.";
                    return RedirectToAction(nameof(Posts), new { blogKey });
                }

                var deleteArticleCommand = new DeleteArticleCommand()
                {
                    ArticleNumber = articleNumber
                };

                var result = await mediator.SendAsync(deleteArticleCommand);

                if (!result.IsSuccess)
                {
                    throw new Exception(result.ErrorMessage);
                }

                TempData["Success"] = "Blog post deleted successfully.";
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Article not found.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting article: {ex.Message}";
            }

            return RedirectToAction(nameof(Posts), new { blogKey });
        }

        /// <summary>
        /// Anonymous preview page for a specific blog, returning recent posts.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <returns>Preview view with recent posts; 404 if blog not found.</returns>
        [HttpGet("{blogKey}/preview")]
        [AllowAnonymous]
        public async Task<IActionResult> PreviewStream(string blogKey)
        {
            var article = await GetLatestStreamArticleAsync(blogKey);
            if (article == null)
            {
                return NotFound();
            }

            // update content just to be sure.
            article.Content = await blogStreamRenderingService.GenerateBlogStreamWrapperAsync(article, blogKey);
            await db.SaveChangesAsync();

            ViewData["articleId"] = article.Id;

            return View("~/Views/Home/Preview.cshtml");
        }

        /// <summary>
        /// Returns JSON list of all blogs (for client-side UI).
        /// </summary>
        /// <returns>JSON array of <see cref="BlogStreamViewModel"/>.</returns>
        [HttpGet("GetBlogs")]
        public async Task<IActionResult> GetBlogs()
        {
            var deletedEnum = (int)StatusCodeEnum.Deleted;
            var articleType = (int)ArticleType.BlogStream;
            var blogs = await db.Articles
                .Where(b => b.ArticleType == articleType && b.StatusCode != deletedEnum)
                .ToListAsync();

            // Get the latest version of each blog stream.
            // This linq expression is done outside of the database query to avoid complex SQL generation.
            var data = blogs.GroupBy(a => a.ArticleNumber)
                .Select(g => g.OrderBy(a => a.VersionNumber).LastOrDefault())
                .Select(b => new BlogViewModel
                {
                    Id = b.Id,
                    BlogKey = b.BlogKey,
                    Title = b.Title,
                    Description = b.Introduction,
                    HeroImage = b.BannerImage,
                    UrlPath = b.UrlPath
                })
                .ToList();

            return Json(data.OrderBy(b => b.Title).ToList());
        }

        /// <summary>
        /// Returns JSON list of posts for a specific blog.
        /// </summary>
        /// <param name="blogKey">Unique blog key.</param>
        /// <returns>JSON list of posts or 400/404 on invalid key.</returns>
        [HttpGet("{blogKey}/getposts")]
        public Task<IActionResult> GetPosts(string blogKey) => GetPostsCore(blogKey);

        /// <summary>
        /// Legacy alias for returning JSON list of posts for a specific blog.
        /// </summary>
        /// <param name="blogKey">Unique blog key.</param>
        /// <returns>JSON list of posts or 400/404 on invalid key.</returns>
        [HttpGet("{blogKey}/getentries")]
        public Task<IActionResult> GetEntries(string blogKey) => GetPostsCore(blogKey);

        private async Task<IActionResult> GetPostsCore(string blogKey)
        {
            if (string.IsNullOrWhiteSpace(blogKey))
            {
                return BadRequest();
            }

            var blog = await GetLatestStreamArticleAsync(blogKey);
            if (blog == null)
            {
                return NotFound();
            }

            var deletedEnum = (int)StatusCodeEnum.Deleted;
            var blogStreamArticleNumber = blog.ArticleNumber;
            var entries = await db.Articles
                .Where(c => c.BlogKey == blogKey && c.ArticleNumber != blogStreamArticleNumber && c.StatusCode != deletedEnum)
                .Select(c => new
                {
                    c.BlogKey,
                    c.ArticleNumber,
                    c.Title,
                    c.Published,
                    c.Updated,
                    c.UrlPath,
                    c.Introduction,
                    c.BannerImage,
                    c.VersionNumber
                })
                .ToListAsync();

            var model = entries
                .GroupBy(e => e.ArticleNumber)
                .Select(g => g.OrderByDescending(e => e.VersionNumber).First())
                .Select(c => new BlogPostListItem
                {
                    BlogKey = c.BlogKey,
                    ArticleNumber = c.ArticleNumber,
                    Title = c.Title,
                    Published = c.Published,
                    Updated = c.Updated,
                    UrlPath = c.UrlPath,
                    Introduction = c.Introduction,
                    BannerImage = c.BannerImage
                })
                .Cast<BlogEntryListItem>()
                .ToList();

            return Json(model.OrderByDescending(c => c.Published ?? c.Updated).ToList());
        }

        private async Task<Article> GetLatestStreamArticleAsync(string blogKey)
        {
            var deletedEnum = (int)StatusCodeEnum.Deleted;
            var streamType = (int)ArticleType.BlogStream;

            var entity = await db.Articles
                .Where(a => a.BlogKey == blogKey && a.StatusCode != deletedEnum && a.ArticleType == streamType)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();

            return entity;
        }
    }
}
