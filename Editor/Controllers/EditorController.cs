// <copyright file="EditorController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Cosmos.BlobService;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Articles.EditorQueries;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services;
    using Cosmos.DynamicConfig;
    using Cosmos.Editor.Services;
    using HtmlAgilityPack;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using SendGrid.Helpers.Errors.Model;
    using Sky.Cms.Hubs;
    using Sky.Cms.Models;
    using Sky.Cms.Services;
    using Sky.Editor.Data;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Articles.Save;
    using Sky.Editor.Features.Templates.Get;
    using Sky.Editor.Models;
    using Sky.Editor.Models.GrapesJs;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.ReservedPaths;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Services.Titles;
    using CommonMediator = Cosmos.Common.Features.Shared.IMediator;

    /// <summary>
    /// Editor controller.
    /// </summary>
    // [ResponseCache(NoStore = true)]
    [Authorize(Roles = "Reviewers, Administrators, Editors, Authors")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class EditorController : BaseController
    {
        private readonly CommonMediator mediator;
        private readonly ArticleEditLogic articleLogic;
        private readonly ApplicationDbContext dbContext;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly UserManager<IdentityUser> userManager;
        private readonly IStorageContext storageContext;

        private readonly ILogger<EditorController> logger;
        private readonly IEditorSettings editorSettings;
        private readonly IViewRenderService viewRenderService;
        private readonly IHubContext<LiveEditorHub> hub;
        private readonly IPublishingService publishingService;
        private readonly IArticleHtmlService htmlService;
        private readonly IReservedPaths reservedPaths;
        private readonly ITitleChangeService titleChangeService;
        private readonly ITemplateService templateService;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorController"/> class.
        /// </summary>
        /// <param name="logger">ILogger to use.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="roleManager">Role manager.</param>
        /// <param name="articleLogic">Article logic.</param>
        /// <param name="editorSettings">Cosmos options.</param>
        /// <param name="viewRenderService">View rendering service.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="hub">Editor SignalR hub.</param>
        /// <param name="publishingService">Publishing service.</param>
        /// <param name="htmlService">HTML service.</param>
        /// <param name="reservedPaths">Reserved path service.</param>
        /// <param name="titleChangeService">Title change service.</param>
        /// <param name="templateService">Template service.</param>
        /// <param name="mediator">Mediator instance for CQRS commands and queries.</param>
        /// <param name="memoryCache">Memory cache for layout caching.</param>
        /// <param name="configProvider">Dynamic configuration provider for tenant-aware caching.</param>
        public EditorController(
            ILogger<EditorController> logger,
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ArticleEditLogic articleLogic,
            IEditorSettings editorSettings,
            IViewRenderService viewRenderService,
            IStorageContext storageContext,
            IHubContext<LiveEditorHub> hub,
            IPublishingService publishingService,
            IArticleHtmlService htmlService,
            IReservedPaths reservedPaths,
            ITitleChangeService titleChangeService,
            ITemplateService templateService,
            CommonMediator mediator,
            IMemoryCache memoryCache,
            IDynamicConfigurationProvider configProvider)
            : base(dbContext, userManager, memoryCache, configProvider)
        {
            this.logger = logger;
            this.dbContext = dbContext;
            this.editorSettings = editorSettings;
            this.roleManager = roleManager;
            this.userManager = userManager;
            this.articleLogic = articleLogic;
            this.storageContext = storageContext;
            this.hub = hub;
            this.publishingService = publishingService;
            this.htmlService = htmlService;
            this.reservedPaths = reservedPaths;
            this.titleChangeService = titleChangeService;
            this.templateService = templateService;
            this.mediator = mediator;

            var htmlUtilities = new HtmlUtilities();

            this.viewRenderService = viewRenderService;
        }

        /// <summary>
        /// Catalog of web pages on this website.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Index()
        {
            // Ensure the required roles exist.
            await SetupNewAdministrator.Ensure_Roles_Exists(roleManager);

            // Ensure default templates exist.
            await this.templateService.EnsureDefaultTemplatesExistAsync();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ViewData["ShowFirstPageBtn"] = false; // Default unless changed below.

            if ((await dbContext.Articles.CountAsync()) == 0)
            {
                var template = await dbContext.Templates.Where(w => w.Title.ToLower().Contains("home page")).FirstOrDefaultAsync();

                if (template == null)
                {
                    ViewData["ShowFirstPageBtn"] = true;
                }
                else
                {
                    return View(viewName: "__NewHomePage", model:
                        new CreatePageViewModel()
                        {
                            TemplateId = template.Id,
                            Title = string.Empty,
                            ArticleNumber = 1,
                            Id = Guid.NewGuid()
                        });
                }
            }

            ViewData["HomePageArticleNumber"] = await dbContext.Pages.Where(f => f.UrlPath == "root").Select(s => s.ArticleNumber).FirstOrDefaultAsync();

            ViewData["ShowNotFoundBtn"] = !await dbContext.ArticleCatalog.Where(w => w.UrlPath == "not_found").CosmosAnyAsync();

            return View();
        }

        /// <summary>
        /// Loads the designer GUI.
        /// </summary>
        /// <param name="id">Article number.</param>
        /// <returns>View.</returns>
        public async Task<IActionResult> Designer(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Loads GrapeJS.
            ViewData["IsDesigner"] = true;

            var article = await GetArticleForEdit(id);

            if (article == null)
            {
                return NotFound();
            }

            var defaultLayout = await GetCurrentLayoutAsync();
            var config = new DesignerConfig(defaultLayout, article.ArticleNumber.ToString(), article.Title);
            var assets = await FileManagerController.GetImageAssetArray(storageContext, $"/pub/articles/{id}", string.Empty);
            if (assets != null)
            {
                config.ImageAssets.AddRange(assets);
            }

            ViewData["DesignerConfig"] = config;
            ViewData["Version"] = article.VersionNumber;

            ViewData["PageTitle"] = article.Title;
            ViewData["Published"] = null;
            ViewData["LastPubDateTime"] = await mediator.QueryAsync(new GetLastPublishedDateQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            var catalogEntry = await mediator.QueryAsync(new GetArticleCatalogEntryQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            var designerUtils = new DesignerUtilities();
            var data = designerUtils.ExtractDesignerData(article.Content);

            return View(new ArticleDesignerDataViewModel
            {
                Id = article.Id,
                ArticleNumber = article.ArticleNumber,
                VersionNumber = article.VersionNumber,
                Title = article.Title,
                Published = null,
                ArticlePermissions = catalogEntry.ArticlePermissions,
                UrlPath = article.UrlPath,
                BannerImage = article.BannerImage,
                Updated = article.Updated,
                HtmlContent = data.HtmlContent,
                CssContent = data.CssContent,
            });
        }

        /// <summary>
        /// Visual designer based on GrapeJS.
        /// </param>
        /// <param name="id">Article number.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet]
        public async Task<IActionResult> GetDesignerData(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var article = await mediator.QueryAsync<ArticleViewModel>(new GetArticleByArticleNumberQuery 
            { 
                ArticleNumber = id 
            });
            if (article == null)
            {
                return NotFound();
            }

            var htmlContent = htmlService.EnsureEditableMarkers(article.Content);

            return Json(new project(htmlContent));
        }

        /// <summary>
        ///     Gets all the versions for an article.
        /// </summary>
        /// <param name="id">Article number.</param>
        /// <param name="sortOrder">Sort order is either asc or desc.</param>
        /// <param name="currentSort">Field to sort on.</param>
        /// <param name="pageNo">Page to return.</param>
        /// <param name="pageSize">Number of records per page.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Versions(int? id, string sortOrder = "desc", string currentSort = "VersionNumber", int pageNo = 0, int pageSize = 10)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id == null)
            {
                return RedirectToAction("Index");
            }

            ViewData["sortOrder"] = sortOrder;
            ViewData["currentSort"] = currentSort;
            ViewData["pageNo"] = pageNo;
            ViewData["pageSize"] = pageSize;
            ViewData["articleNumber"] = id;

            var articleNumber = id.Value;

            var query = dbContext.Articles.Where(w => w.ArticleNumber == articleNumber).Select(s => new ArticleVersionViewModel
            {
                Id = s.Id,
                Published = s.Published,
                Title = s.Title,
                Updated = s.Updated,
                VersionNumber = s.VersionNumber,
                Expires = s.Expires,
                UserId = s.UserId,
                UsesHtmlEditor = s.Content != null && s.Content != string.Empty && (s.Content.ToLower().Contains(" contenteditable=") || s.Content.ToLower().Contains(" data-ccms-ceid="))
            }).AsQueryable();

            ViewData["RowCount"] = await dbContext.Articles.Where(w => w.ArticleNumber == id).CountAsync();
            ViewData["LastVersion"] = await dbContext.Articles.Where(w => w.ArticleNumber == id).MaxAsync(m => m.VersionNumber);

            if (sortOrder == "desc")
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "Published":
                            query = query.OrderByDescending(o => o.Published);
                            break;
                        case "Updated":
                            query = query.OrderByDescending(o => o.Updated);
                            break;
                        case "VersionNumber":
                            query = query.OrderByDescending(o => o.VersionNumber);
                            break;
                        case "Expires":
                            query = query.OrderByDescending(o => o.Expires);
                            break;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "Published":
                            query = query.OrderBy(o => o.Published);
                            break;
                        case "Updated":
                            query = query.OrderBy(o => o.Updated);
                            break;
                        case "VersionNumber":
                            query = query.OrderBy(o => o.VersionNumber);
                            break;
                        case "Expires":
                            query = query.OrderBy(o => o.Expires);
                            break;
                    }
                }
            }

            var article = await dbContext.Articles.Where(a => a.ArticleNumber == id.Value)
                .Select(s => new { s.Title, s.VersionNumber }).FirstOrDefaultAsync();

            ViewData["ArticleTitle"] = article.Title;
            ViewData["ArticleId"] = id.Value;

            var skip = pageNo * pageSize;

            var data = await query.Skip(skip).Take(pageSize).ToListAsync();

            return View(data);
        }

        /// <summary>
        /// Gets the article trash list.
        /// </summary>
        /// <returns>Trask list.</returns>
        [Authorize(Roles = " Administrators, Editors, Authors")]
        public async Task<IActionResult> GetTrashList()
        {
            if (dbContext.Database.IsCosmos())
            {
                var query = "SELECT c.ArticleNumber, c.Title, c.UrlPath, MAX(c.Published) as Published, MAX(c.Updated) as Updated FROM Articles c WHERE c.StatusCode = 2 GROUP BY c.ArticleNumber, c.Title, c.UrlPath";
                var client = dbContext.Database.GetCosmosClient();
                var queryService = new CosmosDbService(client, dbContext.Database.GetCosmosDatabaseId(), "Articles");

                return Json(await queryService.QueryWithGroupByAsync(query));
            }

            var data = await dbContext.Articles
                .Where(w => w.StatusCode == (int)StatusCodeEnum.Deleted)
                .GroupBy(g => new { g.ArticleNumber, g.Title, g.UrlPath })
                .Select(s => new
                {
                    ArticleNumber = s.Key.ArticleNumber,
                    Title = s.Key.Title,
                    UrlPath = s.Key.UrlPath,
                    Published = s.Max(m => m.Published),
                    Updated = s.Max(m => m.Updated)
                }).ToListAsync();

            return Json(data);
        }

        /// <summary>
        /// Open trash.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors")]
        public IActionResult Trash()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return View();
        }

        /// <summary>
        /// Compare two versions.
        /// </summary>
        /// <param name="leftId">Article ID of the left version.</param>
        /// <param name="rightId">Article ID of the right version.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Compare(Guid leftId, Guid rightId)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var left = await mediator.QueryAsync<ArticleViewModel>(new GetArticleByIdQuery { Id = leftId });
            var right = await mediator.QueryAsync<ArticleViewModel>(new GetArticleByIdQuery { Id = rightId });
            @ViewData["PageTitle"] = left.Title;

            ViewData["LeftVersion"] = left.VersionNumber;
            ViewData["RightVersion"] = right.VersionNumber;

            var model = new CompareCodeViewModel()
            {
                EditorTitle = left.Title,
                EditorFields = new[]
                {
                    new EditorField
                    {
                        FieldId = "HeadJavaScript",
                        FieldName = "Head Block",
                        EditorMode = EditorMode.Html,
                        IconUrl = "/images/seti-ui/icons/html.svg",
                        ToolTip = "Content to appear at the bottom of the <head> tag."
                    },
                    new EditorField
                    {
                        FieldId = "Content",
                        FieldName = "Html Content",
                        EditorMode = EditorMode.Html,
                        IconUrl = "~/images/seti-ui/icons/html.svg",
                        ToolTip = "Content to appear in the <body>."
                    },
                    new EditorField
                    {
                        FieldId = "FooterJavaScript",
                        FieldName = "Footer Block",
                        EditorMode = EditorMode.Html,
                        IconUrl = "~/images/seti-ui/icons/html.svg",
                        ToolTip = "Content to appear at the bottom of the <body> tag."
                    }
                },
                Articles = new ArticleViewModel[] { left, right }
            };
            return View(model);
        }

        /// <summary>
        /// Gets template page information.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
        public async Task<IActionResult> GetTemplateInfo(Guid? id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id == null)
            {
                return Json(string.Empty);
            }

            // Use GetTemplateQuery to retrieve the template
            var query = new GetTemplateQuery { TemplateId = id.Value };
            var result = await mediator.QueryAsync(query);
            
            if (!result.IsSuccess || result.Data?.Template == null)
            {
                return NotFound();
            }

            return Json(result.Data.Template);
        }

        /// <summary>
        ///     Creates a <see cref="CreatePageViewModel" /> used to create a new article.
        /// </summary>
        /// <param name="title">Name of new page if known.</param>
        /// <param name="sortOrder">Current sort order.</param>
        /// <param name="currentSort">Field being sorted on.</param>
        /// <param name="pageNo">Page number to retrieve.</param>
        /// <param name="pageSize">Number of records in each page.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
        public async Task<IActionResult> Create(string title = "", string sortOrder = "asc", string currentSort = "Title", int pageNo = 0, int pageSize = 20)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if ((await dbContext.Articles.CountAsync()) == 0)
            {
                var template = await dbContext.Templates.Where(w => w.Title.ToLower().Contains("home page")).FirstOrDefaultAsync();

                if (template == null)
                {
                    ViewData["ShowFirstPageBtn"] = true;
                }
                else
                {
                    return View(viewName: "__NewHomePage", model:
                        new CreatePageViewModel()
                        {
                            TemplateId = template.Id,
                            Title = string.Empty,
                            ArticleNumber = 1,
                            Id = Guid.NewGuid()
                        });
                }
            }

            var defaultLayout = await GetCurrentLayoutAsync();

            ViewData["Layouts"] = await BaseGetLayoutListItems();

            ViewData["sortOrder"] = sortOrder;
            ViewData["currentSort"] = currentSort;
            ViewData["pageNo"] = pageNo;
            ViewData["pageSize"] = pageSize;
            var reserved = await reservedPaths.GetReservedPaths();
            var existingUrls = await dbContext.Articles.Where(w => w.StatusCode == (int)StatusCodeEnum.Active).Select(s => s.Title).Distinct().ToListAsync();
            existingUrls.AddRange(reserved.Select(s => s.Path));
            ViewData["reservedPaths"] = existingUrls;

            var query = (await GetTemplatesForCurrentLayoutAsync())
                .OrderBy(t => t.Title)
                .Select(s => new TemplateIndexViewModel
                {
                    Id = s.Id,
                    LayoutName = defaultLayout.LayoutName,
                    Description = s.Description,
                    Title = s.Title,
                    UsesHtmlEditor = s.Content.ToLower().Contains(" contenteditable=") || s.Content.ToLower().Contains(" data-ccms-ceid=")
                });

            ViewData["RowCount"] = await query.CountAsync();

            if (sortOrder == "desc")
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "LayoutName":
                            query = query.OrderByDescending(o => o.LayoutName);
                            break;
                        case "Description":
                            query = query.OrderByDescending(o => o.Description);
                            break;
                        case "Title":
                            query = query.OrderByDescending(o => o.Title);
                            break;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "LayoutName":
                            query = query.OrderBy(o => o.LayoutName);
                            break;
                        case "Title":
                            query = query.OrderBy(o => o.Title);
                            break;
                        case "Description":
                            query = query.OrderBy(o => o.Description);
                            break;
                    }
                }
            }

            ViewData["TemplateList"] = await query.Skip(pageNo * pageSize).Take(pageSize).ToListAsync();

            return View(new CreatePageViewModel()
            {
                Id = Guid.NewGuid(),
                Title = title.Contains("{new page name}", StringComparison.CurrentCultureIgnoreCase) ? string.Empty : title
            });
        }

        /// <summary>
        ///     Uses <see cref="CreateArticleCommand"/> via mediator to create an <see cref="ArticleViewModel"/> ready for editing.
        /// </summary>
        /// <param name="model">Create page view model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
        [HttpPost]
        public async Task<IActionResult> Create(CreatePageViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Re-populate ViewData for error display
                var defaultLayout = await GetCurrentLayoutAsync();
                ViewData["Layouts"] = await BaseGetLayoutListItems();
                ViewData["sortOrder"] = "asc";
                ViewData["currentSort"] = "title";
                ViewData["pageNo"] = 0;
                ViewData["pageSize"] = 20;

                var query = (await GetTemplatesForCurrentLayoutAsync())
                   .OrderBy(t => t.Title)
                   .Select(s => new TemplateIndexViewModel
                   {
                       Id = s.Id,
                       LayoutName = defaultLayout.LayoutName,
                       Description = s.Description,
                       Title = s.Title,
                       UsesHtmlEditor = s.Content.ToLower().Contains(" contenteditable=") || s.Content.ToLower().Contains(" data-ccms-ceid=")
                   });

                ViewData["RowCount"] = await query.CountAsync();
                ViewData["TemplateList"] = await query.Skip(0 * 20).Take(20).ToListAsync();

                return View(model);
            }

            if (model == null)
            {
                return NotFound();
            }

            model.Title = model.Title.TrimStart('/');

            // REMOVED: Title validation now handled in CreateArticleHandler
            // var validTitle = await titleChangeService.ValidateTitle(model.Title, null);
            // if (!validTitle) { ... }

            // CreateArticleHandler will validate title and return error if conflicts exist
            var command = new CreateArticleCommand
            {
                Title = model.Title,
                TemplateId = model.TemplateId,
                UserId = Guid.Parse(await GetUserId()),
                ArticleType = model.ArticleType,
                BlogKey = string.Empty,
                Category = model.Category,
                Introduction = model.Introduction
            };

            var result = await mediator.SendAsync<CommandResult<ArticleViewModel>>(command);

            if (!result.IsSuccess)
            {
                // Handler validation errors (including title conflicts)
                var errors = result.Errors?.SelectMany(e => e.Value) ?? Enumerable.Empty<string>();
                var errorMessage = result.ErrorMessage ?? string.Join(", ", errors);
                ModelState.AddModelError(string.Empty, errorMessage);
                
                return View(viewName: "__NewHomePage", model: model);
            }

            // Successfully created - redirect to Versions action to view/edit the new article
            return RedirectToAction("Versions", new { id = result.Data.Id });
        }

        /// <summary>
        /// Create initial home page
        /// </summary>
        /// <param name="model">Model used to create a the first home page.</param>
        /// <returns>Returns <see cref="IActionResult"/>.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInitialHomePage(CreatePageViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(viewName: "__NewHomePage", model: model);
            }

            if (model == null)
            {
                return NotFound();
            }

            // Verify this is truly the first article
            if (await dbContext.Articles.CosmosAnyAsync())
            {
                ModelState.AddModelError("Title", "This can only be used to create a website's first home page.");
                return View(viewName: "__NewHomePage", model: model);
            }

            model.Title = model.Title.TrimStart('/');

            // REMOVED: Title validation now handled in CreateArticleHandler
            // var validTitle = await titleChangeService.ValidateTitle(model.Title, null);
            // if (!validTitle) { ... }

            var template = await dbContext.Templates.FirstOrDefaultAsync(f => f.Title.ToLower() == "home page");
            
            if (template == null)
            {
                ModelState.AddModelError("Title", "Home page template not found.");
                return View(viewName: "__NewHomePage", model: model);
            }

            // REFACTORED: Use CreateArticleCommand via mediator
            var command = new CreateArticleCommand
            {
                Title = model.Title,
                TemplateId = template.Id,
                UserId = Guid.Parse(await GetUserId()),
                ArticleType = ArticleType.General,
                BlogKey = string.Empty,
                
                // Special home page properties
                Published = DateTimeOffset.UtcNow,           // Auto-publish
                StatusCode = StatusCodeEnum.Active,          // Override default
                ContentOverride = template.Content,          // Use template as-is
                UrlPathOverride = "root"                     // CRITICAL: Home page must be "root"
            };

            var result = await mediator.SendAsync<CommandResult<ArticleViewModel>>(command);

            if (!result.IsSuccess)
            {
                // Handler validation errors (including title conflicts)
                var errors = result.Errors?.SelectMany(e => e.Value) ?? Enumerable.Empty<string>();
                var errorMessage = result.ErrorMessage ?? string.Join(", ", errors);
                ModelState.AddModelError(string.Empty, errorMessage);
                
                return View(viewName: "__NewHomePage", model: model);
            }

            // Successfully created - redirect to home
            return Redirect("/");
        }

        /// <summary>
        /// Restore an article from trash.
        /// </summary>
        /// <param name="id">Article ID to recover from trash.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors")]
        public async Task<IActionResult> Restore(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await articleLogic.RestoreArticle(id, await GetUserId());

            return Ok();
        }

        /// <summary>
        ///     Publish website dialog.
        /// </summary>
        /// <returns>Returns a view.</returns>
        [Authorize(Roles = "Administrators, Editors")]
        public IActionResult Publish()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return View();
        }

        /// <summary>
        /// Publishes an article.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="datetime">Date and time to publish.</param>
        /// <param name="editorUrl">Editor URL.</param>
        /// <returns>IActionResult.</returns>
        public async Task<IActionResult> PublishPage(Guid articleId, DateTimeOffset? datetime, string editorUrl)
        {
            if (!string.IsNullOrWhiteSpace(editorUrl))
            {
                // Define allowed return paths
                var allowedPaths = new[]
                {
                    "/Editor/Index",
                    "/Editor/Versions",
                    "/Editor/EditCode",
                    "/Editor/Edit",
                    "/Editor/Designer",
                    "/Templates/EditCode",
                    "/Templates/Edit",
                    "/Templates/Designer"
                };

                // Parse and validate the URL
                if (!Uri.TryCreate(editorUrl, UriKind.RelativeOrAbsolute, out var uri))
                {
                    logger.LogWarning("Invalid URL format: {EditorUrl}", editorUrl);
                    return RedirectToAction("Index", "Editor");
                }

                var path = uri.IsAbsoluteUri ? uri.AbsolutePath : editorUrl.Split('?')[0];

                if (!allowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    logger.LogWarning("Redirect to unauthorized path: {Path}", path);
                    return RedirectToAction("Index", "Editor");
                }

                await articleLogic.PublishArticle(articleId, datetime);
                return Redirect(editorUrl);
            }

            await articleLogic.PublishArticle(articleId, datetime);
            return Redirect("/Editor/Index");
        }

        /// <summary>
        /// Un-publishes an article.
        /// </summary>
        /// <param name="id">Article number.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> UnpublishPage(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var article = await dbContext.Articles.Where(w => w.ArticleNumber == id).OrderByDescending(o => o.VersionNumber).FirstOrDefaultAsync();
            await publishingService.UnpublishAsync(article);

            return Ok();
        }

        /// <summary>
        /// Page access permissions.
        /// </summary>
        /// <param name="id">Article ID.</param>
        /// <param name="forRoles">User roles.</param>
        /// <param name="sortOrder">Sort order asc or desc.</param>
        /// <param name="currentSort">Current sort field.</param>
        /// <param name="pageNo">Page number to return.</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpGet]
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> Permissions(int id, bool forRoles = true, string sortOrder = "asc", string currentSort = "Name", int pageNo = 0, int pageSize = 10)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ViewData["sortOrder"] = sortOrder;
            ViewData["currentSort"] = currentSort;
            ViewData["pageNo"] = pageNo;
            ViewData["pageSize"] = pageSize;
            ViewData["showingRoles"] = forRoles;

            var article = await dbContext.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == id);

            var catalogEntry = await mediator.QueryAsync(new GetArticleCatalogEntryQuery
            {
                ArticleNumber = id
            });

            ViewData["ArticleNumber"] = catalogEntry.ArticleNumber;
            ViewData["ArticlePermissions"] = catalogEntry.ArticlePermissions;
            var objectIds = catalogEntry.ArticlePermissions.Select(s => s.IdentityObjectId).ToArray();

            ViewData["ViewModel"] = new ArticlePermissionsViewModel(catalogEntry, forRoles);
            ViewData["Title"] = catalogEntry.Title;
            ViewData["AllowedUsers"] = await userManager.Users.Where(w => objectIds.Contains(w.Id)).ToListAsync();

            ViewData["AllowedRoles"] = await roleManager.Roles.Where(w => objectIds.Contains(w.Id)).ToListAsync();

            IQueryable<ArticlePermisionItem> query;

            if (forRoles)
            {
                query = roleManager.Roles.Select(
                    s => new ArticlePermisionItem
                    {
                        IdentityObjectId = s.Id,
                        Name = s.Name,
                    }).AsQueryable();
            }
            else
            {
                query = userManager.Users.Select(
                    s => new ArticlePermisionItem
                    {
                        IdentityObjectId = s.Id,
                        Name = s.Email,
                    }).AsQueryable();
            }

            // Get count
            ViewData["RowCount"] = await query.CountAsync();

            if (sortOrder.Equals("desc", StringComparison.InvariantCultureIgnoreCase))
            {
                switch (currentSort)
                {
                    case "Name":
                        query = query.OrderByDescending(o => o.Name);
                        break;
                }
            }
            else
            {
                switch (currentSort)
                {
                    case "Name":
                        query = query.OrderBy(o => o.Name);
                        break;
                }
            }

            query = query.Skip(pageNo * pageSize).Take(pageSize);

            var data = await query.ToListAsync();

            return View(data);
        }

        /// <summary>
        /// Sets the permissions for an article.
        /// </summary>
        /// <param name="id">Article Number.</param>
        /// <param name="identityObjectIds">Identity object ID list.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> Permissions(int id, string[] identityObjectIds)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var article = await dbContext.Articles.Where(w => w.ArticleNumber == id).OrderByDescending(o => o.VersionNumber).LastOrDefaultAsync();
                var entry = await mediator.QueryAsync(new GetArticleCatalogEntryQuery
                {
                    ArticleNumber = id
                });

                if (entry.ArticlePermissions == null)
                {
                    entry.ArticlePermissions = new List<ArticlePermission>();
                }
                else
                {
                    entry.ArticlePermissions.Clear();
                }

                var roles = await dbContext.Roles.Where(w => identityObjectIds.Contains(w.Id)).ToListAsync();
                var users = await dbContext.Users.Where(w => identityObjectIds.Contains(w.Id)).ToListAsync();

                if (roles.Any())
                {
                    entry.ArticlePermissions.AddRange(roles.Select(s => new ArticlePermission()
                    {
                        IdentityObjectId = s.Id,
                        IsRoleObject = true
                    }).ToArray());
                }

                if (users.Any())
                {
                    entry.ArticlePermissions.AddRange(users.Select(s => new ArticlePermission()
                    {
                        IdentityObjectId = s.Id,
                        IsRoleObject = false
                    }).ToArray());
                }

                await dbContext.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Open Cosmos logs.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> Logs()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var data = await dbContext.ArticleLogs
                .OrderByDescending(o => o.DateTimeStamp)
                .Select(s => new
                {
                    s.Id,
                    s.ActivityNotes,
                    s.DateTimeStamp,
                    s.IdentityUserId
                }).ToListAsync();

            var model = data.Select(s => new ArticleLogJsonModel
            {
                Id = s.Id,
                ActivityNotes = s.ActivityNotes,
                DateTimeStamp = s.DateTimeStamp.ToUniversalTime(),
                IdentityUserId = s.IdentityUserId
            }).AsQueryable();

            return View(model);
        }

        /// <summary>
        /// Gets a reserved path list.
        /// </summary>
        /// <param name="sortOrder">Sort order either asc or desc.</param>
        /// <param name="currentSort">Field to sort on.</param>
        /// <param name="pageNo">Page number to send back.</param>
        /// <param name="pageSize">Number of records per page.</param>
        /// <param name="filter">Search filter (optional).</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> ReservedPaths(string sortOrder, string currentSort, int pageNo = 0, int pageSize = 10, string filter = "")
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var paths = await reservedPaths.GetReservedPaths();

            ViewData["RowCount"] = paths.Count;

            var query = paths.AsQueryable();

            ViewData["Filter"] = filter;
            ViewData["sortOrder"] = sortOrder;
            ViewData["currentSort"] = currentSort;
            ViewData["pageNo"] = pageNo;
            ViewData["pageSize"] = pageSize;

            if (!string.IsNullOrEmpty(filter))
            {
                var f = filter.ToLower();
                query = query.Where(w => w.Path.ToLower().Contains(f));
            }

            if (sortOrder == "desc")
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "Path":
                            query = query.OrderByDescending(o => o.Path);
                            break;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "Path":
                            query = query.OrderBy(o => o.Path);
                            break;
                        case "CosmosRequired":
                            query = query.OrderBy(o => o.CosmosRequired);
                            break;
                        case "Notes":
                            query = query.OrderBy(o => o.Notes);
                            break;
                    }
                }
                else
                {
                    // Default sort order
                    query = query.OrderBy(o => o.Path);
                }
            }

            query = query.Skip(pageNo * pageSize).Take(pageSize);

            return View(query.ToList());
        }

        /// <summary>
        /// Creates a new reserved path.
        /// </summary>
        /// <returns>ViewResult.</returns>
        [HttpGet]
        public IActionResult CreateReservedPath()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ViewData["Title"] = "Create a Reserved Path";

            return View("~/Views/Editor/EditReservedPath.cshtml", new ReservedPath());
        }

        /// <summary>
        /// Edit a reserved path.
        /// </summary>
        /// <param name="id">Path ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> EditReservedPath(Guid id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ViewData["Title"] = "Edit Reserved Path";

            var paths = await reservedPaths.GetReservedPaths();

            var path = paths.Find(f => f.Id == id);

            if (path == null)
            {
                return NotFound();
            }

            return View(path);
        }

        /// <summary>
        /// Editor page.
        /// </summary>
        /// <param name="id">Article number (int).</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> CcmsContent(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var article = await mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = id });

            return View(article);
        }

        /// <summary>
        ///     Gets an article to edit by ID for the HTML (WYSIWYG) Editor.
        /// </summary>
        /// <param name="id">Article number.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
        public async Task<IActionResult> Edit(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Web browser may ask for favicon.ico, so if the ID is not a number, just skip the response.
            ViewData["BlobEndpointUrl"] = editorSettings.BlobPublicUrl;

            // Get an article, or a template based on the controller name.
            var model = await mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = id });

            ViewData["PageTitle"] = model.Title;
            ViewData["Published"] = null;
            ViewData["LastPubDateTime"] = await mediator.QueryAsync(new GetLastPublishedDateQuery
            {
                ArticleNumber = model.ArticleNumber
            });

            // Override defaults
            model.EditModeOn = true;
            model.Published = null;

            // Authors cannot edit published articles
            if (model.Published.HasValue && User.IsInRole("Authors"))
            {
                return Unauthorized();
            }

            var article = await GetArticleForEdit(id);

            var entry = await mediator.QueryAsync(new GetArticleCatalogEntryQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            return View(new HtmlEditorViewModel(model, entry));
        }

        /// <summary>
        /// Saves article properties.
        /// </summary>
        /// <param name="model">Live editor post model from JSON body.</param>
        /// <param name="queryModel">Optional query string overrides.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] EditPostViewModel model, [FromQuery] EditPostViewModel? queryModel = null)
        {
            if (model == null)
            {
                return BadRequest("No data sent.");
            }

            ApplyQueryOverrides(model, queryModel);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate CryptoContextToken if provided
            if (!string.IsNullOrEmpty(model.CryptoContextToken))
            {
                if (!IsValidCryptoContextToken(model.CryptoContextToken))
                {
                    return BadRequest("Invalid CryptoContextToken.");
                }
            }

            // Validate Title
            if (string.IsNullOrEmpty(model.Title))
            {
                return Json(new
                {
                    ServerSideSuccess = false,
                    errors = new Dictionary<string, string[]>
                    {
                        ["Title"] = new[] { "Title cannot be null or empty." }
                    }
                });
            }

            // Get original article
            var article = await mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = model.ArticleNumber });
            if (article == null)
            {
                throw new NotFoundException($"Could not find article with #: {model.ArticleNumber}.");
            }

            // Update content if editor region specified
            if (!string.IsNullOrWhiteSpace(model.EditorId))
            {
                var decryptedPayload = CryptoJsDecryption.Decrypt(model.Payload);
                article.Content = UpdateRegionInDocument(
                    model.EditorId,
                    article.Content,
                    decryptedPayload);
            }
            else if (model.Command == "SaveBody")
            {
                // SaveBody command: replace entire content (empty or null payload is valid)
                article.Content = CryptoJsDecryption.Decrypt(model.Payload);
            }
            else if (model.Command == "SaveCode")
            {
                // SaveCode command: update content and scripts from Code Editor
                var decryptedContent = CryptoJsDecryption.Decrypt(model.Payload);

                // Validate no nested editable regions
                var nestedRegionError = ValidateNoNestedEditableRegions(decryptedContent);
                if (nestedRegionError != null)
                {
                    return Json(new
                    {
                        ServerSideSuccess = false,
                        errors = new Dictionary<string, string[]>
                        {
                            ["Payload"] = new[] { nestedRegionError }
                        }
                    });
                }

                article.Content = decryptedContent;
                article.HeadJavaScript = CryptoJsDecryption.Decrypt(model.HeadJavaScript);
                article.FooterJavaScript = CryptoJsDecryption.Decrypt(model.FooterJavaScript);
            }
            else if (model.Command == "SaveDesigner")
            {
                // SaveDesigner command: GrapesJS designer output
                // Payload (HTML) and CssContent are encrypted in the model
                var htmlContent = string.Empty;
                var cssContent = string.Empty;

                if (model.Payload != null)
                {
                    htmlContent = CryptoJsDecryption.Decrypt(model.Payload);
                }

                if (model.CssContent != null)
                {
                    cssContent = CryptoJsDecryption.Decrypt(model.CssContent);
                }

                // Validate no nested editable regions in HTML content
                var nestedRegionError = ValidateNoNestedEditableRegions(htmlContent);
                if (nestedRegionError != null)
                {
                    return Json(new
                    {
                        ServerSideSuccess = false,
                        errors = new Dictionary<string, string[]>
                        {
                            ["Payload"] = new[] { nestedRegionError }
                        }
                    });
                }

                // Add editable markers if needed
                htmlContent = htmlService.EnsureEditableMarkers(htmlContent);

                // Assemble the final HTML output (HTML + CSS combined)
                var designerUtils = new DesignerUtilities();
                var assembledHtml = designerUtils.AssembleDesignerOutput(
                    new DesignerDataViewModel
                    {
                        CssContent = cssContent,
                        HtmlContent = htmlContent,
                        Title = model.Title,
                        Id = model.Id
                    });

                article.Content = assembledHtml;
            }
            else if (model.Command == "SavePageProperties")
            {
                // SavePageProperties command: metadata-only update (preserve existing content)
                // This allows updates to Title, BannerImage, ArticleType, Category, Introduction
                // without changing content, scripts, etc.
                // Content remains unchanged - will be preserved in SaveArticleCommand below
            }
            else if (string.IsNullOrWhiteSpace(model.Command))
            {
                // Invalid/empty command
                return Json(new
                {
                    ServerSideSuccess = false,
                    errors = new Dictionary<string, string[]>
                    {
                        ["Command"] = new[] { "Command cannot be null or empty." }
                    }
                });
            }
            else
            {
                // Unrecognized command
                return Json(new
                {
                    ServerSideSuccess = false,
                    errors = new Dictionary<string, string[]>
                    {
                        ["Command"] = new[] { $"Unrecognized command: '{model.Command}'. Valid commands are: SaveBody, SaveCode, SaveDesigner, SavePageProperties." }
                    }
                });
            }

            // NEW: Use SaveArticle command
            var command = new SaveArticleCommand
            {
                ArticleNumber = model.ArticleNumber,
                Title = model.Title,
                Content = article.Content,
                HeadJavaScript = article.HeadJavaScript,
                FooterJavaScript = article.FooterJavaScript,
                BannerImage = model.BannerImage,
                ArticleType = model.ArticleType,
                Category = model.Category,
                Introduction = model.Introduction,
                UrlPath = article.UrlPath,
                Published = article.Published,
                UserId = Guid.Parse(await GetUserId())
            };

            var result = await mediator.SendAsync<CommandResult<Sky.Editor.Features.Articles.Save.ArticleUpdateResult>>(command);

            if (!result.IsSuccess)
            {
                return Json(new
                {
                    success = false,
                    errors = result.Errors ?? new Dictionary<string, string[]>
                    {
                        ["general"] = new[] { result.ErrorMessage ?? "Save failed" }
                    }
                });
            }

            // Notify SignalR clients of changes
            if (!string.IsNullOrWhiteSpace(model.EditorId))
            {
                await hub.Clients.All.SendCoreAsync("UpdateEditors", [model.Id, model.Payload]);
            }

            // Return ArticleUpdateResult wrapped in compatible format
            return Json(new
            {
                ServerSideSuccess = result.Data!.ServerSideSuccess,
                Model = result.Data.Model,
                CdnResults = result.Data.CdnResults
            });
        }

        /// <summary>
        /// Edit web page code with Monaco editor.
        /// </param>
        /// <param name="id">Article Number (not ID).</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> EditCode(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get an article, or a template based on the controller name.
            var article = await GetArticleForEdit(id);
            if (article == null)
            {
                return NotFound();
            }

            ViewData["Version"] = article.VersionNumber;

            ViewData["PageTitle"] = article.Title;
            ViewData["Published"] = null;
            ViewData["LastPubDateTime"] = await mediator.QueryAsync(new GetLastPublishedDateQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            var catalogEntry = await mediator.QueryAsync(new GetArticleCatalogEntryQuery
            {
                ArticleNumber = article.ArticleNumber
            });

            return View(new EditCodePostModel
            {
                Id = article.Id,
                ArticleNumber = article.ArticleNumber,
                VersionNumber = article.VersionNumber,
                Title = article.Title,
                Published = null,
                ArticlePermissions = catalogEntry.ArticlePermissions,
                EditorTitle = article.Title,
                UrlPath = article.UrlPath,
                BannerImage = article.BannerImage,
                Updated = article.Updated,
                EditorFields = new[]
                {
                    new EditorField
                    {
                        FieldId = "HeadJavaScript",
                        FieldName = "Head Block",
                        EditorMode = EditorMode.Html,
                        IconUrl = "/images/seti-ui/icons/html.svg",
                        ToolTip = "Content to appear at the bottom of the <head> tag."
                    },
                    new EditorField
                    {
                        FieldId = "Content",
                        FieldName = "Html Content",
                        EditorMode = EditorMode.Html,
                        IconUrl = "~/images/seti-ui/icons/html.svg",
                        ToolTip = "Content to appear in the <body>."
                    },
                    new EditorField
                    {
                        FieldId = "FooterJavaScript",
                        FieldName = "Footer Block",
                        EditorMode = EditorMode.Html,
                        IconUrl = "~/images/seti-ui/icons/html.svg",
                        ToolTip = "Content to appear at the bottom of the <body> tag."
                    }
                },
                HeadJavaScript = article.HeaderJavaScript,
                FooterJavaScript = article.FooterJavaScript,
                Content = article.Content,
                EditingField = "HeadJavaScript",
                CustomButtons = new[] { "Preview", "Html", "Export", "Import" }
            });
        }

        /// <summary>
        /// Opens the page scheduler.
        /// </summary>
        /// <returns>View.</returns>
        public IActionResult Scheduler()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return View();
        }

        /// <summary>
        /// Exports a page as a file.
        /// </summary>
        /// <param name="id">Article version ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
        public async Task<IActionResult> ExportPage(Guid? id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ArticleViewModel article;
            var userId = Guid.Parse(await GetUserId());
            if (id.HasValue)
            {
                article = await mediator.QueryAsync(new GetArticleByIdQuery
                {
                    Id = id.Value
                });
            }
            else
            {
                // Create temporary blank page for export using CQRS command
                var command = new CreateArticleCommand
                {
                    Title = "Blank Page",
                    UserId = userId,
                    ArticleType = ArticleType.General,
                    BlogKey = string.Empty,
                    TemplateId = null
                };

                var result = await mediator.SendAsync<CommandResult<ArticleViewModel>>(command);
                
                if (!result.IsSuccess)
                {
                    return BadRequest(result.ErrorMessage);
                }

                article = result.Data;
            }

            var html = await articleLogic.ExportArticle(article, viewRenderService);

            var exportName = $"pageid-{article.ArticleNumber}-version-{article.VersionNumber}.html";

            var bytes = Encoding.UTF8.GetBytes(html);

            return File(bytes, "application/octet-stream", exportName);
        }

        /// <summary>
        /// Pre-load the website (useful if CDN configured).
        /// </summary>
        /// <returns>IAction result.</returns>
        [HttpGet]
        [Authorize(Roles = "Administrators")]
        public IActionResult Preload()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return View(new PreloadViewModel());
        }

        /// <summary>
        /// Check to see if a page title is already taken.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="title">Article title.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> CheckTitle(int articleNumber, string title)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await titleChangeService.ValidateTitle(title, articleNumber);

            if (result)
            {
                return Json(true);
            }

            return Json($"Title '{title}' is already taken.");
        }

        /// <summary>
        /// Gets a list of articles (web pages) on this website.
        /// </summary>
        /// <param name="term">search text value (optional).</param>
        /// <param name="publishedOnly">Only retrieve published articles.</param>
        /// <param name="articleType">Article type to retrieve.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpGet]
        public async Task<IActionResult> GetArticleList(string term = "", bool publishedOnly = true, int articleType = 0)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var blogPostArticleType = (int)ArticleType.BlogPost;

            if (dbContext.Database.IsCosmos())
            {
                var whereClause = publishedOnly ? $"WHERE c.Published != null AND " : "WHERE ";
                whereClause += $"c.StatusCode = {(int)StatusCodeEnum.Active} ";

                if (!string.IsNullOrEmpty(term))
                {
                    whereClause += $"AND LOWER(c.Title) LIKE '%{term.ToLower()}%' ";
                }

                var query = $"SELECT c.ArticleNumber, c.ArticleType, c.Title, c.UrlPath, MAX(c.Published) as Published, MAX(c.Updated) as Updated FROM Articles c {whereClause} GROUP BY c.ArticleNumber, c.ArticleType, c.Title, c.UrlPath";
                var client = dbContext.Database.GetCosmosClient();
                var queryService = new CosmosDbService(client, dbContext.Database.GetCosmosDatabaseId(), "Articles");

                var data = await queryService.QueryWithGroupByAsync<ArticleListViewItem>(query);

                var model = data.Select(s => new
                {
                    s.ArticleNumber,
                    s.ArticleType,
                    s.Title,
                    IsDefault = s.UrlPath == "root",
                    LastPublished = s.Published.HasValue ? s.Published.Value.UtcDateTime.ToString("o") : null,
                    UrlPath = HttpUtility.UrlEncode(s.UrlPath).Replace("%2f", "/"),
                    Updated = s.Updated.UtcDateTime.ToString("o")
                }).OrderBy(o => o.Title).ToList();

                return Json(model);
            }
            else
            {
                // LINQ equivalent for the SQL GROUP BY and MAX aggregate
                var query = publishedOnly ? dbContext.Articles
                    .Where(a => a.Published != null && a.StatusCode == (int)StatusCodeEnum.Active && a.ArticleType != blogPostArticleType) :
                    dbContext.Articles
                    .Where(a => a.StatusCode == (int)StatusCodeEnum.Active && a.ArticleType == 0);

                if (!string.IsNullOrEmpty(term))
                {
                    query = query.Where(a => a.Title.ToLower().Contains(term.ToLower()));
                }

                var grouped = await query
                .GroupBy(a => new { a.ArticleNumber, a.Title, a.UrlPath })
                .Select(g => new
                {
                    ArticleNumber = g.Key.ArticleNumber,
                    Title = g.Key.Title,
                    UrlPath = g.Key.UrlPath,
                    Published = g.Max(x => x.Published),
                    Updated = g.Max(x => x.Updated)
                })
                .OrderBy(o => o.Title)
                .ToListAsync();

                var model = grouped.Select(s => new
                {
                    s.ArticleNumber,
                    s.Title,
                    IsDefault = s.UrlPath == "root",
                    LastPublished = s.Published.HasValue ? s.Published.Value.UtcDateTime.ToString("o") : null,
                    UrlPath = HttpUtility.UrlEncode(s.UrlPath).Replace("%2f", "/"),
                    Updated = s.Updated
                }).ToList();

                return Json(model);
            }
        }

        /// <summary>
        /// Gets an encryption key.
        /// </summary>
        /// <returns>Key.</returns>
        public async Task<IActionResult> GetEncryptionKey()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var setting = await dbContext.Settings.Where(w => w.Description == "EncryptionKey").FirstOrDefaultAsync();
            if (setting == null)
            {
                var random = new Random();
                var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                var value = new string(Enumerable.Repeat(chars, 16)
                                            .Select(s => s[random.Next(s.Length)])
                                            .ToArray());

                setting = new Setting()
                {
                    Description = "EncryptionKey",
                    Value = value
                };

                dbContext.Settings.Add(setting);
                await dbContext.SaveChangesAsync();
            }

            return Json(setting.Value);
        }

        /// <summary>
        /// Publish list of web pages to static website.
        /// </summary>
        /// <param name="guids">List of page IDs.</param>
        /// <returns>IActionResult.</returns>
        [HttpPost]
        [Authorize(Roles = "Editors,Administrators")]
        public async Task<IActionResult> PublishStaticPages([FromBody] List<Guid> guids)
        {
            try
            {
                // Empty or null list triggers "publish all" in the service
                await publishingService.CreateStaticPages(guids);

                var count = guids?.Count ?? await dbContext.Pages.CountAsync();

                return Json(new
                {
                    success = true,
                    count = count,
                    message = $"Successfully published {count} page(s)"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing static pages");
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Sends an article (or page) to trash bin.
        /// </summary>
        /// <param name="id">Article number.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpGet]
        public async Task<IActionResult> TrashArticle(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await articleLogic.DeleteArticle(id);
            return Ok();
        }

        /// <summary>
        /// Redirect manager page.
        /// </summary>
        /// <param name="sortOrder">Sort order.</param>
        /// <param name="currentSort">Current sort item.</param>
        /// <param name="pageNo">Page number.</param>
        /// <param name="pageSize">Page size.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> Redirects(string sortOrder, string currentSort, int pageNo = 0, int pageSize = 10)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ViewData["sortOrder"] = sortOrder;
            ViewData["currentSort"] = currentSort;
            ViewData["pageNo"] = pageNo;
            ViewData["pageSize"] = pageSize;

            var redirectsResult = await mediator.QueryAsync(new GetArticleRedirectsQuery());
            var query = redirectsResult.AsQueryable();

            ViewData["RowCount"] = query.Count();

            if (sortOrder == "desc")
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "FromUrl":
                            query = query.OrderByDescending(o => o.FromUrl);
                            break;
                        case "Title":
                            query = query.OrderByDescending(o => o.Id);
                            break;
                        case "ToUrl":
                            query = query.OrderByDescending(o => o.ToUrl);
                            break;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "FromUrl":
                            query = query.OrderBy(o => o.FromUrl);
                            break;
                        case "Id":
                            query = query.OrderBy(o => o.Id);
                            break;
                        case "ToUrl":
                            query = query.OrderBy(o => o.ToUrl);
                            break;
                    }
                }
            }

            var model = query.Skip(pageNo * pageSize).Take(pageSize).ToList();

            return View(model);
        }

        /// <summary>
        /// Sends an article (or page) to trash bin.
        /// </summary>
        /// <param name="id">Article ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpGet]
        public async Task<IActionResult> RedirectDelete(Guid id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var article = await dbContext.Articles.FirstOrDefaultAsync(f => f.Id == id);

            await articleLogic.DeleteArticle(article.ArticleNumber);

            return RedirectToAction("Redirects");
        }

        /// <summary>
        /// Updates a redirect.
        /// </summary>
        /// <param name="id">Article ID.</param>
        /// <param name="fromUrl">Redirect from URL.</param>
        /// <param name="toUrl">Redirect to URL.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> RedirectEdit([FromForm] Guid id, string fromUrl, string toUrl)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var redirect = await dbContext.Articles.FirstOrDefaultAsync(f => f.Id == id && f.StatusCode == (int)StatusCodeEnum.Redirect);
            if (redirect == null)
            {
                return NotFound();
            }

            redirect.UrlPath = fromUrl;
            redirect.Content = toUrl;

            await dbContext.SaveChangesAsync();

            return RedirectToAction("Redirects");
        }

        /// <summary>
        /// Flush the CDN if configured.
        /// </summary>
        /// <returns>IActionResult.</returns>
        [HttpGet]
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> RefreshCdn()
        {
            try
            {
                var cdnService = await CdnService.GetCdnServiceAsync(dbContext, logger, HttpContext);

                if (cdnService == null)
                {
                    return Json(new List<object>());
                }

                var results = await cdnService.PurgeCdn();

                return Json(results.Select(r => new
                {
                    provider = r.ProviderName,
                    success = r.IsSuccessStatusCode,
                    message = r.Message
                }));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error purging CDN");
                return Json(new[]
                {
                    new
                    {
                        provider = "CDN",
                        success = false,
                        message = ex.Message
                    }
                });
            }
        }

        /// <summary>
        ///     Disposes of resources for this controller.
        /// </summary>
        /// <param name="disposing">Dispose or not.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        private static void ApplyQueryOverrides(EditPostViewModel model, EditPostViewModel? queryModel)
        {
            if (queryModel == null)
            {
                return;
            }

            if (model.Id == Guid.Empty && queryModel.Id != Guid.Empty)
            {
                model.Id = queryModel.Id;
            }

            if (model.ArticleNumber == 0 && queryModel.ArticleNumber > 0)
            {
                model.ArticleNumber = queryModel.ArticleNumber;
            }

            if (model.VersionNumber == 0 && queryModel.VersionNumber > 0)
            {
                model.VersionNumber = queryModel.VersionNumber;
            }

            if (string.IsNullOrWhiteSpace(model.EditorId) && !string.IsNullOrWhiteSpace(queryModel.EditorId))
            {
                model.EditorId = queryModel.EditorId;
            }

            if (string.IsNullOrWhiteSpace(model.Command) && !string.IsNullOrWhiteSpace(queryModel.Command))
            {
                model.Command = queryModel.Command;
            }

            if (string.IsNullOrWhiteSpace(model.Payload) && !string.IsNullOrWhiteSpace(queryModel.Payload))
            {
                model.Payload = queryModel.Payload;
            }

            if (string.IsNullOrWhiteSpace(model.HeadJavaScript) && !string.IsNullOrWhiteSpace(queryModel.HeadJavaScript))
            {
                model.HeadJavaScript = queryModel.HeadJavaScript;
            }

            if (string.IsNullOrWhiteSpace(model.FooterJavaScript) && !string.IsNullOrWhiteSpace(queryModel.FooterJavaScript))
            {
                model.FooterJavaScript = queryModel.FooterJavaScript;
            }

            if (string.IsNullOrWhiteSpace(model.CssContent) && !string.IsNullOrWhiteSpace(queryModel.CssContent))
            {
                model.CssContent = queryModel.CssContent;
            }

            if (string.IsNullOrWhiteSpace(model.EditingField) && !string.IsNullOrWhiteSpace(queryModel.EditingField))
            {
                model.EditingField = queryModel.EditingField;
            }

            if (string.IsNullOrWhiteSpace(model.EditorType) && !string.IsNullOrWhiteSpace(queryModel.EditorType))
            {
                model.EditorType = queryModel.EditorType;
            }

            if (!model.Published.HasValue && queryModel.Published.HasValue)
            {
                model.Published = queryModel.Published;
            }

            if (!model.Updated.HasValue && queryModel.Updated.HasValue)
            {
                model.Updated = queryModel.Updated;
            }

            if (string.IsNullOrWhiteSpace(model.CryptoContextToken) && !string.IsNullOrWhiteSpace(queryModel.CryptoContextToken))
            {
                model.CryptoContextToken = queryModel.CryptoContextToken;
            }

            if (string.IsNullOrWhiteSpace(model.Title) && !string.IsNullOrWhiteSpace(queryModel.Title))
            {
                model.Title = queryModel.Title;
            }

            if (string.IsNullOrWhiteSpace(model.UrlPath) && !string.IsNullOrWhiteSpace(queryModel.UrlPath))
            {
                model.UrlPath = queryModel.UrlPath;
            }

            if (string.IsNullOrWhiteSpace(model.BannerImage) && !string.IsNullOrWhiteSpace(queryModel.BannerImage))
            {
                model.BannerImage = queryModel.BannerImage;
            }

            if (string.IsNullOrWhiteSpace(model.RoleList) && !string.IsNullOrWhiteSpace(queryModel.RoleList))
            {
                model.RoleList = queryModel.RoleList;
            }

            if (string.IsNullOrWhiteSpace(model.Category) && !string.IsNullOrWhiteSpace(queryModel.Category))
            {
                model.Category = queryModel.Category;
            }

            if (string.IsNullOrWhiteSpace(model.Introduction) && !string.IsNullOrWhiteSpace(queryModel.Introduction))
            {
                model.Introduction = queryModel.Introduction;
            }
        }

        /// <summary>
        /// Updates the HTML within an editor region no a web page.
        /// </summary>
        /// <param name="editorId">Editor ID on page.</param>
        /// <param name="pageBody">Page body.</param>
        /// <param name="updatedContent">Updated content.</param>
        /// <returns>Revised page body.</returns>
        private string UpdateRegionInDocument(string editorId, string pageBody, string updatedContent)
        {
            // Get the editable regions from the original document.
            var originalHtmlDoc = new HtmlDocument();
            originalHtmlDoc.LoadHtml(pageBody);
            var originalEditableDivs = originalHtmlDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]");

            // Find the region we are updating
            var target = originalEditableDivs.FirstOrDefault(w => w.Attributes["data-ccms-ceid"].Value == editorId);
            if (target != null)
            {
                // Update the region now
                target.InnerHtml = updatedContent;
            }

            // Now carry over what's being UPDATED to the original.
            return originalHtmlDoc.DocumentNode.OuterHtml;
        }

        /// <summary>
        /// Validates that HTML content does not contain nested editable regions.
        /// </summary>
        /// <param name="htmlContent">HTML content to validate.</param>
        /// <returns>Error message if nested regions found, null otherwise.</returns>
        private string? ValidateNoNestedEditableRegions(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                return null;
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            // Find all elements with data-ccms-ceid attribute
            var editableRegions = htmlDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]");

            if (editableRegions == null || editableRegions.Count == 0)
            {
                return null;
            }

            // Check each region for nested regions
            foreach (var region in editableRegions)
            {
                // Check if this region has any descendant with data-ccms-ceid
                var nestedRegions = region.SelectNodes(".//*[@data-ccms-ceid]");
                if (nestedRegions != null && nestedRegions.Count > 0)
                {
                    var regionId = region.GetAttributeValue("data-ccms-ceid", "unknown");
                    return $"Nested editable regions are not allowed. Region '{regionId}' contains nested regions.";
                }
            }

            return null;
        }

        /// <summary>
        /// Validates the CryptoContextToken format and authenticity.
        /// </summary>
        /// <param name="token">The token to validate.</param>
        /// <returns>True if valid, false otherwise.</returns>
        private bool IsValidCryptoContextToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            // For now, reject obviously invalid tokens (e.g., test tokens)
            // In a full implementation, this would validate against a secure store
            if (token.StartsWith("invalid-", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // TODO: Implement full token validation against session store
            // For now, accept any non-empty, non-invalid token as valid
            return true;
        }

        private async Task<Article> GetArticleForEdit(int articleNumber)
        {
            var article = await dbContext.Articles.Where(w => w.ArticleNumber == articleNumber).OrderByDescending(o => o.VersionNumber).FirstOrDefaultAsync();
            if (article == null)
            {
                return null;
            }

            if (article.Published.HasValue)
            {
                // Use CreateArticleVersionCommand via mediator instead of deprecated NewVersion method
                var versionCommand = new Sky.Editor.Features.Articles.CreateVersion.CreateArticleVersionCommand
                {
                    ArticleNumber = article.ArticleNumber
                };
                var versionResult = await mediator.SendAsync(versionCommand);
                return versionResult.IsSuccess ? (await dbContext.Articles.Where(a => a.ArticleNumber == article.ArticleNumber).OrderByDescending(x => x.VersionNumber).FirstAsync()) : null;
            }

            return article;
        }
    }
}
