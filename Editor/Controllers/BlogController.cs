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
    using Sky.Editor.Features.Blogs.DeleteStream;
    using Sky.Editor.Features.Blogs.GetStream;
    using Sky.Editor.Features.Blogs.UpdateStream;
    using Sky.Editor.Models.Blogs;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Templates;

    /// <summary>
    /// Editor-facing controller for managing blog streams (multi-blog support) and their entries (blog posts).
    /// </summary>
    /// <remarks>
    /// Responsibilities:
    /// <list type="bullet">
    ///   <item>Create, list, edit, and delete blog streams (<c>Blog</c> records).</item>
    ///   <item>Enforce uniqueness and validation of <c>BlogKey</c> values (route-safe identifiers).</item>
    ///   <item>Maintain a single default blog stream (used as reassignment target).</item>
    ///   <item>Create, edit, publish (immediate), and delete blog post entries via vertical slice architecture.</item>
    ///   <item>Provide JSON listing endpoint for client-side selection widgets.</item>
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
        /// Lists all blog streams ordered by sort order then key.
        /// </summary>
        /// <returns>Index view containing a list of <see cref="BlogStreamViewModel"/>.</returns>
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // Ensure that the Blog stream template exists.
            await templateService.EnsureDefaultTemplatesExistAsync();
            return View("Index");
        }

        /// <summary>
        /// Displays the create blog stream form.
        /// </summary>
        /// <returns>Create view with default model.</returns>
        [HttpGet("create")]
        public IActionResult Create() =>
            View("Create", new BlogStreamViewModel());

        /// <summary>
        /// Handles blog stream creation.
        /// </summary>
        /// <param name="model">Submitted blog stream view model.</param>
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
        /// Displays edit form for a specified blog stream.
        /// </summary>
        /// <param name="id">Blog identifier (GUID).</param>
        /// <returns>Edit view or 404 if not found.</returns>
        [HttpGet("{id:guid}/edit")]
        public new async Task<IActionResult> Edit(Guid id)
        {
            var query = new GetBlogStreamQuery
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

            return View("Edit", new BlogStreamViewModel
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
        /// Processes blog stream edits.
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

            var command = new UpdateBlogStreamCommand
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
            var query = new GetBlogStreamQuery { Id = id };
            var result = await mediator.QueryAsync(query);

            if (!result.IsSuccess || result.Data == null)
            {
                return NotFound();
            }

            var streamData = result.Data;

            return View("Delete", new BlogStreamViewModel
            {
                Id = streamData.Article.Id,
                BlogKey = streamData.BlogKey,
                Title = streamData.Title
            });
        }

        /// <summary>
        /// Performs deletion of a blog stream.
        /// </summary>
        /// <param name="id">Blog identifier.</param>
        /// <returns>Redirect to <see cref="Index"/> or view with errors.</returns>
        [HttpPost("{id:guid}/confirmdelete")]
        public async Task<IActionResult> ConfirmDelete(Guid id)
        {
            var command = new DeleteBlogStreamCommand
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

            TempData["Success"] = "Blog stream and all entries deleted successfully";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Lists entries (articles) for a specific blog stream.
        /// </summary>
        /// <param name="blogKey">Unique blog key.</param>
        /// <returns>Entries view with listing model or 400/404 on invalid key.</returns>
        [HttpGet("{blogKey}/entries")]
        public async Task<IActionResult> Entries(string blogKey)
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

            // Cosmos DB does not support cross-entity joins. Query Articles directly
            // (same pattern as GetEntries), group by ArticleNumber, and take the
            // latest version of each blog post.
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
                .Select(c => new BlogEntryListItem
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

            var vm = new BlogEntriesListViewModel
            {
                BlogKey = blog.BlogKey,
                BlogTitle = blog.Title,
                BlogDescription = blog.Introduction,
                HeroImage = blog.BannerImage,
                BlogUrlPath = blog.UrlPath,
                Entries = entries.OrderByDescending(c => c.Published ?? c.Updated).ToList()
            };
            return View("Entries", vm);
        }

        /// <summary>
        /// Creates a new blog entry for a given blog.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="title">Title of the blog entry.</param>
        /// <returns>Redirect to editor on success, or 404/400/500 on failure.</returns>
        [HttpPost("{blogKey}/entries/create")]
        public async Task<IActionResult> CreateEntry(string blogKey, [FromForm] string title)
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

            var defaultLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(db);

            var userId = Guid.Parse(await GetUserId());

            // Use dedicated CreateBlogPostCommand handler
            var command = new CreateBlogPostCommand
            {
                Title = title,
                Content = string.Empty, // Start with empty content; user will edit and publish
                BlogKey = blogKey,
                TemplateId = Guid.Empty,
                UserId = userId,
                Published = null // Explicitly unpublished until user publishes
            };

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                var errorMessage = result.ErrorMessage ?? "Failed to create blog post.";
                return StatusCode(500, $"Failed to create blog entry: {errorMessage}");
            }

            return RedirectToAction("Edit", "Editor", new { id = result.Data.ArticleNumber });
        }

        /// <summary>
        /// Displays delete confirmation for a blog entry.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Delete entry view or 404.</returns>
        [HttpGet("{blogKey}/entries/{articleNumber:int}/delete")]
        public async Task<IActionResult> DeleteEntry(string blogKey, int articleNumber)
        {
            // Verify the article belongs to this blog
            var article = await db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .FirstOrDefaultAsync();
            if (article == null || article.BlogKey != blogKey)
            {
                return NotFound();
            }

            var vm = new BlogEntryListItem
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
        /// Deletes a blog entry (article) via logic layer.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Redirect to entries listing.</returns>
        [HttpPost("{blogKey}/entries/{articleNumber:int}/confirmdeleteentry")]
        public async Task<IActionResult> ConfirmDeleteEntry(string blogKey, int articleNumber)
        {
            try
            {
                // Verify the article belongs to this blog
                var article = await db.Articles
                    .Where(a => a.ArticleNumber == articleNumber)
                    .FirstOrDefaultAsync();

                if (article == null)
                {
                    TempData["Error"] = "Article not found.";
                    return RedirectToAction(nameof(Entries), new { blogKey });
                }

                if (article.BlogKey != blogKey)
                {
                    TempData["Error"] = "Article does not belong to this blog.";
                    return RedirectToAction(nameof(Entries), new { blogKey });
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

                TempData["Success"] = "Blog entry deleted successfully.";
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Article not found.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting article: {ex.Message}";
            }

            return RedirectToAction(nameof(Entries), new { blogKey });
        }

        /// <summary>
        /// Anonymous preview page (simplified listing) for a specific blog stream, returning recent posts.
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
        /// Returns JSON list of all blog streams (for client-side UI).
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
                .Select(b => new BlogStreamViewModel
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
        /// Lists entries (articles) for a specific blog stream.
        /// </summary>
        /// <param name="blogKey">Unique blog key.</param>
        /// <returns>Entries view with listing model or 400/404 on invalid key.</returns>
        [HttpGet("{blogKey}/getentries")]
        public async Task<IActionResult> GetEntries(string blogKey)
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

            // BlogEntryListItem
            // Get the entries that match the blog key with the exception of the blog stream article itself.
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
                .Select(c => new BlogEntryListItem
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
