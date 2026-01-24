// <copyright file="BlogController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Articles.Save;
    using Sky.Editor.Features.Shared;
    using Sky.Editor.Models.Blogs;
    using Sky.Editor.Services.BlogPublishing;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Services.Titles;

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
        private readonly ArticleEditLogic articleLogic;
        private readonly ISlugService slugService;
        private readonly ITemplateService templateService;
        private readonly IBlogRenderingService blogRenderingService;
        private readonly ITitleChangeService titleChangeService;
        private readonly IMediator mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlogController"/> class.
        /// </summary>
        /// <param name="db">Application database context.</param>
        /// <param name="articleLogic">Article editing / publishing logic service.</param>
        /// <param name="slugService">Slug normalization and uniqueness helper.</param>
        /// <param name="templateService">Template management service.</param>
        /// <param name="userManager">User management service.</param>
        /// <param name="blogRenderingService">Blog rendering service.</param>
        /// <param name="titleChangeService">Title change service.</param>
        /// <param name="mediator">Mediator for dispatching commands.</param>
        /// <param name="memoryCache">Memory cache for layout caching.</param>
        /// <param name="configProvider">Dynamic configuration provider for tenant-aware caching.</param>
        public BlogController(
            ApplicationDbContext db,
            ArticleEditLogic articleLogic,
            ISlugService slugService,
            ITemplateService templateService,
            UserManager<IdentityUser> userManager,
            IBlogRenderingService blogRenderingService,
            ITitleChangeService titleChangeService,
            IMediator mediator,
            IMemoryCache memoryCache,
            IDynamicConfigurationProvider configProvider)
            : base(db, userManager, memoryCache, configProvider)
        {
            this.db = db;
            this.articleLogic = articleLogic;
            this.slugService = slugService;
            this.templateService = templateService;
            this.blogRenderingService = blogRenderingService;
            this.titleChangeService = titleChangeService;
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

            if (!await titleChangeService.ValidateTitle(model.Title, null))
            {
                ModelState.AddModelError(nameof(model.BlogKey), "Blog key conflicts with existing page on this website.");
                return View("Create", model);
            }

            // Create blog stream article.
            model.BlogKey = slugService.Normalize(model.Title);
            var defautLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(db);

            var blogEntryTemplage = await db.Templates.FirstOrDefaultAsync(t =>t.LayoutId == defautLayout.Id && t.PageType == "blog-stream");

            if (blogEntryTemplage == null)
            {
                throw new InvalidOperationException("Blog entry template not found.");
            }

            var article = await articleLogic.CreateArticle(model.Title, Guid.Parse(await GetUserId()), blogEntryTemplage.Id, model.BlogKey, ArticleType.BlogStream);

            // Make the image URL relative path if it's absolute.
            if (string.IsNullOrWhiteSpace(model.HeroImage) == false && Uri.IsWellFormedUriString(model.HeroImage, UriKind.Absolute))
            {
                var uri = new Uri(model.HeroImage);

                // If the URI has a host different from the current request, ignore it.
                if (uri.Host.Equals(Request.Host.Host, StringComparison.OrdinalIgnoreCase))
                {
                    model.HeroImage = uri.PathAndQuery;
                }
            }

            article.BannerImage = model.HeroImage ?? string.Empty;
            article.Introduction = model.Description;
            article.Content = blogEntryTemplage.Content; // Blog stream articles have no body content.
            article.Published = model.Published;

            // MIGRATED: Use SaveArticleHandler via mediator
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Content = article.Content,
                BannerImage = article.BannerImage,
                Introduction = article.Introduction,
                Published = article.Published,
                ArticleType = ArticleType.BlogStream,
                UserId = Guid.Parse(await GetUserId())
            };

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                foreach (var error in result.Errors)
                {
                    foreach (var message in error.Value)
                    {
                        ModelState.AddModelError(error.Key, message);
                    }
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
        public async Task<IActionResult> Edit(Guid id)
        {
            var article = await articleLogic.GetArticleById(id, Cms.Controllers.EnumControllerName.Edit, Guid.Parse(await GetUserId()));
            if (article == null)
            {
                return NotFound();
            }

            return View("Edit", new BlogStreamViewModel
            {
                Id = article.Id,
                BlogKey = article.UrlPath,
                Title = article.Title,
                Description = article.Introduction,
                HeroImage = article.BannerImage,
                Published = article.Published
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

            var article = await db.Articles.FirstOrDefaultAsync(f => f.Id == id);

            if (article.Title.Equals(model.Title, StringComparison.CurrentCultureIgnoreCase) == false && !await titleChangeService.ValidateTitle(model.Title, null))
            {
                ModelState.AddModelError(nameof(model.BlogKey), "Blog key conflicts with existing page on this website.");
                return View(model);
            }

            // Save old title in case of change.
            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;

            // Update changes.
            article.Title = model.Title;
            article.UrlPath = slugService.Normalize(model.Title);
            article.Introduction = model.Description;
            article.BannerImage = model.HeroImage;
            article.Published = model.Published;
            article.Content = await blogRenderingService.GenerateBlogStreamHtml(article);
            await db.SaveChangesAsync();

            // Handle title change.
            if (oldTitle != article.Title)
            {
                await titleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            }

            if (article.Published.HasValue)
            {
                await articleLogic.PublishArticle(article.Id, article.Published.Value);
            }

            return View(model);
        }

        /// <summary>
        /// Displays confirmation page for blog deletion.
        /// </summary>
        /// <param name="id">Blog identifier.</param>
        /// <returns>Delete confirmation view or 404.</returns>
        [HttpGet("{id:guid}/delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var article = await db.Articles.FirstOrDefaultAsync(b => b.Id == id);
            if (article == null)
            {
                return NotFound();
            }

            return View("Delete", new BlogStreamViewModel
            {
                Id = article.Id,
                BlogKey = article.BlogKey,
                Title = article.Title
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
            var article = await db.Articles.FirstOrDefaultAsync(b => b.Id == id);
            if (article == null)
            {
                return NotFound();
            }

            var blogKey = article.BlogKey;
            var entries = await db.Articles
                .Where(c => c.BlogKey == blogKey).Select(c => c.ArticleNumber).Distinct()
                .ToListAsync();

            foreach (var entryNumber in entries)
            {
                // Delete each article associated with this blog.
                await articleLogic.DeleteArticle(entryNumber);
            }

            await articleLogic.DeleteArticle(article.ArticleNumber);

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

            var blog = await db.Articles.FirstOrDefaultAsync(b => b.BlogKey == blogKey);
            if (blog == null)
            {
                return NotFound();
            }

            var entries = await db.ArticleCatalog
                .Where(c => c.BlogKey == blogKey)
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
                .ToListAsync();

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
        /// Displays create entry form for a given blog.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="title">Title of the blog entry.</param>
        /// <returns>Create entry view or 404 if blog not found.</returns>
        [HttpGet("{blogKey}/entries/create/{title}")]
        public async Task<IActionResult> CreateEntry(string blogKey, string title)
        {
            var blogStreamType = (int)ArticleType.BlogStream;
            var blog = await db.Articles.FirstOrDefaultAsync(b => b.BlogKey == blogKey && b.ArticleType == blogStreamType);
            var defautLayout = await Cosmos.Common.Data.Logic.LayoutHelper.GetCurrentDefaultLayoutAsync(db);

            if (blog == null)
            {
                return NotFound("Blog not found.");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest("Title is required.");
            }

            var blogEntryTemplage = await db.Templates.FirstOrDefaultAsync(t => t.LayoutId == defautLayout.Id && t.PageType == "blog-post");

            if (blogEntryTemplage == null)
            {
                throw new InvalidOperationException("Blog entry template not found.");
            }

            var userId = Guid.Parse(await GetUserId());

            var article = await articleLogic.CreateArticle(title, userId, blogEntryTemplage.Id, blogKey);

            article.ArticleType = ArticleType.BlogPost;
            article.Content = blogEntryTemplage.Content;
            article.Published = null;

            // MIGRATED: Use SaveArticleHandler via mediator
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Content = article.Content,
                ArticleType = ArticleType.BlogPost,
                Published = null,
                UserId = userId
            };

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                // Log error and return error view
                return StatusCode(500, "Failed to create blog entry");
            }

            return RedirectToAction("Edit", "Editor", new { id = article.ArticleNumber });
        }

        /// <summary>
        /// Displays edit form for an existing blog entry (latest version).
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="articleNumber">Logical article number.</param>
        /// <returns>Edit entry view or 404.</returns>
        [HttpGet("{blogKey}/entries/{articleNumber:int}/edit")]
        public async Task<IActionResult> EditEntry(string blogKey, int articleNumber)
        {
            var article = await db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();

            if (article == null || article.BlogKey != blogKey)
            {
                return NotFound();
            }

            var vm = new BlogEntryEditViewModel
            {
                BlogKey = blogKey,
                ArticleNumber = article.ArticleNumber,
                Id = article.Id,
                Title = article.Title,
                Introduction = article.Introduction,
                Content = article.Content,
                BannerImage = article.BannerImage,
                PublishNow = article.Published != null
            };
            return View("EditEntry", vm);
        }

        /// <summary>
        /// Processes edits to a blog entry. May trigger publish if requested.
        /// </summary>
        /// <param name="blogKey">Blog key.</param>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="model">Edited entry model.</param>
        /// <returns>Redirect to entries list on success; same view with errors otherwise.</returns>
        [HttpPost("{blogKey}/entries/{articleNumber:int}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEntry(string blogKey, int articleNumber, BlogEntryEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("EditEntry", model);
            }

            var userId = Guid.Parse(await GetUserId());
            var blogStreamType = (int)ArticleType.BlogStream;

            var articleVm = await articleLogic.GetArticleByArticleNumber(articleNumber, null);

            // MIGRATED: Use SaveArticleHandler via mediator
            var command = new SaveArticleCommand
            {
                ArticleNumber = articleVm.ArticleNumber,
                Title = model.Title,
                Content = model.Content,
                Introduction = model.Introduction,
                BannerImage = model.BannerImage,
                Published = model.Published,
                ArticleType = ArticleType.BlogPost,
                UserId = userId
            };

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                foreach (var error in result.Errors)
                {
                    foreach (var message in error.Value)
                    {
                        ModelState.AddModelError(error.Key, message);
                    }
                }
                return View("EditEntry", model);
            }

            if (model.PublishNow)
            {
                if (model.Published == null)
                {
                    await articleLogic.PublishArticle(articleVm.Id, DateTimeOffset.UtcNow);
                }
                else
                {
                    await articleLogic.PublishArticle(articleVm.Id, model.Published.Value);
                }
            }

            // Render the blog stream article
            var blogStreamArticle = await db.Articles.FirstOrDefaultAsync(a => a.BlogKey == blogKey && a.ArticleType == blogStreamType);
            blogStreamArticle.Content = await blogRenderingService.GenerateBlogStreamHtml(blogStreamArticle);

            return RedirectToAction(nameof(Entries), new { blogKey });
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
            var catalog = await db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == articleNumber);
            if (catalog == null || catalog.BlogKey != blogKey)
            {
                return NotFound();
            }

            var vm = new BlogEntryListItem
            {
                BlogKey = catalog.BlogKey,
                ArticleNumber = catalog.ArticleNumber,
                Title = catalog.Title,
                Published = catalog.Published,
                Updated = catalog.Updated,
                UrlPath = catalog.UrlPath,
                Introduction = catalog.Introduction,
                BannerImage = catalog.BannerImage
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
            await articleLogic.DeleteArticle(articleNumber);

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
            article.Content = await blogRenderingService.GenerateBlogStreamHtml(article);
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
                .Where(a => a.UrlPath == blogKey && a.StatusCode != deletedEnum && a.ArticleType == streamType)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();

            return entity;
        }

    }
}
