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
    using Sky.Editor.Features.Shared;
    using Sky.Editor.Models;
    using Sky.Editor.Models.GrapesJs;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.ReservedPaths;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Services.Titles;

    /// <summary>
    /// Editor controller.
    /// </summary>
    // [ResponseCache(NoStore = true)]
    [Authorize(Roles = "Reviewers, Administrators, Editors, Authors")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class EditorController : BaseController
    {
        private readonly IMediator mediator;
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
        /// <param name="mediator">Mediator instance.</param>
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
            IMediator mediator,
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
            ViewData["LastPubDateTime"] = await articleLogic.GetLastPublishedDate(id);

            var catalogEntry = await articleLogic.GetCatalogEntry(article);

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
        /// Save designer data.
        /// </summary>
        /// <param name="model">Designer post model.</param>
        /// <returns>IActionResult.</returns>
        [HttpPost]
        public async Task<IActionResult> Designer(ArticleDesignerDataViewModel model)
        {
            if (model == null)
            {
                return Json(new { success = false, message = "No data sent." });
            }

            model.HtmlContent = CryptoJsDecryption.Decrypt(model.HtmlContent);
            model.CssContent = CryptoJsDecryption.Decrypt(model.CssContent);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!NestedEditableRegionValidation.Validate(model.HtmlContent))
            {
                return BadRequest("Cannot have nested editable regions.");
            }

            model.HtmlContent = htmlService.EnsureEditableMarkers(model.HtmlContent);

            var article = await articleLogic.GetArticleByArticleNumber(model.ArticleNumber, null);

            if (article == null)
            {
                return NotFound();
            }

            var designerUtils = new DesignerUtilities();
            var html = designerUtils.AssembleDesignerOutput(
                new DesignerDataViewModel()
                {
                    CssContent = model.CssContent,
                    HtmlContent = model.HtmlContent,
                    Title = model.Title,
                    Id = model.Id
                });

            try
            {
                // NEW: Use SaveArticle command
                var command = new SaveArticleCommand
                {
                    ArticleNumber = article.ArticleNumber,
                    Title = model.Title,
                    Content = html,
                    HeadJavaScript = article.HeadJavaScript,
                    FooterJavaScript = article.FooterJavaScript,
                    BannerImage = article.BannerImage,
                    UrlPath = article.UrlPath,
                    ArticleType = (ArticleType)article.ArticleType,
                    Category = article.Category,
                    Introduction = article.Introduction,
                    Published = article.Published,
                    UserId = Guid.Parse(await GetUserId())
                };

                var result = await mediator.SendAsync(command);

                if (!result.IsSuccess)
                {
                    var errorMessage = result.ErrorMessage ??
                        string.Join(", ", result.Errors?.SelectMany(e => e.Value) ?? Array.Empty<string>());
                    return Json(new DesignerResult { success = false, message = errorMessage });
                }

                return Json(new DesignerResult { success = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving designer content for article {ArticleNumber}", model.ArticleNumber);
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Visual designer based on GrapeJS.
        /// </summary>
        /// <param name="id">Article number.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet]
        public async Task<IActionResult> GetDesignerData(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var article = await articleLogic.GetArticleByArticleNumber(id, null);
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
        [Authorize(Roles = "Administrators, Editors, Authors")]
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

            var left = await articleLogic.GetArticleById(leftId, Guid.Parse(await GetUserId()));
            var right = await articleLogic.GetArticleById(rightId, Guid.Parse(await GetUserId()));
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

            var model = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == id.Value);

            return Json(model);
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

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                // Title validation errors will be in result.Errors["Title"]
                var errorMessage = result.ErrorMessage ??
                    string.Join(", ", result.Errors?.SelectMany(e => e.Value) ?? Array.Empty<string>());
                ModelState.AddModelError(string.Empty, errorMessage);

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

            return RedirectToAction("Versions", "Editor", new { Id = result.Data.ArticleNumber });
        }

        /// <summary>
        ///     Creates a new version for an article and redirects to editor.
        /// </summary>
        /// <param name="id">Article ID.</param>
        /// <param name="entityId">Entity Id to use as new version.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors")]
        public async Task<IActionResult> CreateVersion(int id, Guid? entityId = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Grab the latest versions regardless
            var latest = await dbContext.Articles.OrderByDescending(o => o.VersionNumber).FirstOrDefaultAsync(f =>
                    f.ArticleNumber == id);

            // This is the article that we will edit
            Article article;

            // Are we basing this on an existing entity?
            if (entityId == null)
            {
                // Yes we are, target that version now.
                article = latest;
            }
            else
            {
                // We are here because the new version is being based on a
                // specific older version, not the latest version.
                //
                //
                // Create a new version based on a specific version
                article = await dbContext.Articles.FirstOrDefaultAsync(f =>
                    f.Id == entityId.Value);
            }

            var newArticle = new Article()
            {
                Id = Guid.NewGuid(),
                ArticleNumber = article.ArticleNumber,
                BannerImage = article.BannerImage,
                Content = article.Content,
                Expires = article.Expires,
                FooterJavaScript = article.FooterJavaScript,
                HeaderJavaScript = article.HeaderJavaScript,
                Published = article.Published,
                StatusCode = article.StatusCode,
                Title = article.Title,
                Updated = article.Updated,
                UrlPath = article.UrlPath,
                UserId = User.Identity.Name,
                VersionNumber = latest.VersionNumber + 1
            };

            dbContext.Articles.Add(newArticle);

            await dbContext.SaveChangesAsync();

            return RedirectToAction("EditCode", "Editor", new { id = newArticle.ArticleNumber });
        }

        /// <summary>
        /// Create a duplicate page from a specified page.
        /// </summary>
        /// <param name="id">Page ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Clone(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var lastVersion = await dbContext.Articles.Where(a => a.ArticleNumber == id).MaxAsync(m => m.VersionNumber);

            var articleViewModel = await articleLogic.GetArticleByArticleNumber(id, lastVersion);

            ViewData["Original"] = articleViewModel;

            if (articleViewModel == null)
            {
                return NotFound();
            }

            var model = new DuplicateViewModel()
            {
                Id = articleViewModel.Id,
                Published = articleViewModel.Published,
                Title = articleViewModel.Title,
                ArticleId = articleViewModel.ArticleNumber,
                ArticleVersion = articleViewModel.VersionNumber
            };

            return View(model);
        }

        /// <summary>
        /// Creates a duplicate page from a specified page and version.
        /// </summary>
        /// <param name="model">Dublice page view model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators, Editors, Authors")]
        public async Task<IActionResult> Clone(DuplicateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string title = string.Empty;

            if (string.IsNullOrEmpty(model.ParentPageTitle))
            {
                title = model.Title;
            }
            else
            {
                title = $"{model.ParentPageTitle.Trim('/')}/{model.Title.Trim('/')} ";
            }

            if (await dbContext.Articles.Where(a => a.Title.ToLower() == title.ToLower() && a.StatusCode != (int)StatusCodeEnum.Deleted).CosmosAnyAsync())
            {
                if (string.IsNullOrEmpty(model.ParentPageTitle))
                {
                    ModelState.AddModelError("Title", "Page title already taken.");
                }
                else
                {
                    ModelState.AddModelError("Title", "Sub-page title already taken.");
                }
            }

            var userId = Guid.Parse(await GetUserId());

            var articleViewModel = await articleLogic.GetArticleById(model.Id, userId);

            if (ModelState.IsValid)
            {
                articleViewModel.ArticleNumber = 0;
                articleViewModel.Id = Guid.NewGuid();
                articleViewModel.Published = model.Published;
                articleViewModel.Title = title;

                try
                {
                    var clone = await articleLogic.CreateArticle(articleViewModel.Title, userId);
                    clone.StatusCode = articleViewModel.StatusCode;
                    clone.CacheDuration = articleViewModel.CacheDuration;
                    clone.Content = articleViewModel.Content;
                    clone.FooterJavaScript = articleViewModel.FooterJavaScript;
                    clone.HeadJavaScript = articleViewModel.HeadJavaScript;
                    clone.LanguageCode = articleViewModel.LanguageCode;

                    var result = await articleLogic.SaveArticle(clone, userId);

                    // Otherwise, open in the Monaco code editor
                    return RedirectToAction("Versions", "Editor", new { id = result.Model.ArticleNumber });
                }
                catch (Exception e)
                {
                    ModelState.AddModelError(string.Empty, e.Message);
                }
            }

            ViewData["Original"] = articleViewModel;

            return View(model);
        }

        /// <summary>
        ///     Creates a <see cref="CreatePageViewModel" /> used to create a new article.
        /// </summary>
        /// <param name="id">Article ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> NewHome(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var page = await dbContext.Articles.FirstOrDefaultAsync(f => f.ArticleNumber == id);
            return View(new NewHomeViewModel
            {
                Id = page.Id,
                ArticleNumber = page.ArticleNumber,
                Title = page.Title,
                IsNewHomePage = false,
                UrlPath = page.UrlPath
            });
        }

        /// <summary>
        /// Make a web page the new home page.
        /// </summary>
        /// <param name="model">Now home page post model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> NewHome(NewHomeViewModel model)
        {
            if (model == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await userManager.GetUserAsync(User);
            await articleLogic.CreateHomePage(model);

            return RedirectToAction("Index");
        }

        /// <summary>
        /// Creates a new home page.
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

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                // Handler validation errors (including title conflicts)
                var errorMessage = result.ErrorMessage ?? 
                    string.Join(", ", result.Errors?.SelectMany(e => e.Value) ?? Array.Empty<string>());
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
            }

            await articleLogic.PublishArticle(articleId, datetime);

            return Redirect(editorUrl);
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

            var article = await dbContext.Articles.FindAsync(id);

            var catalogEntry = await articleLogic.GetCatalogEntry(article);

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
                var entry = await articleLogic.GetCatalogEntry(article);

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

            return View(await query.ToListAsync());
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

            var article = await articleLogic.GetArticleByArticleNumber(id, null);

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
            var model = await articleLogic.GetArticleByArticleNumber(id, null);

            ViewData["PageTitle"] = model.Title;
            ViewData["Published"] = null;
            ViewData["LastPubDateTime"] = await articleLogic.GetLastPublishedDate(id);

            // Override defaults
            model.EditModeOn = true;
            model.Published = null;

            // Authors cannot edit published articles
            if (model.Published.HasValue && User.IsInRole("Authors"))
            {
                return Unauthorized();
            }

            var article = await GetArticleForEdit(id);

            var entry = await articleLogic.GetCatalogEntry(article);

            return View(new HtmlEditorViewModel(model, entry));
        }

        /// <summary>
        /// Saves article properties.
        /// </summary>
        /// <param name="model">Live editor post model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<IActionResult> Edit(HtmlEditorPostViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (string.IsNullOrEmpty(model.Title))
            {
                throw new ArgumentException("Title cannot be null or empty.");
            }

            if (model == null)
            {
                throw new ArgumentException("SaveEditorContent method, model was null.");
            }

            // Get original article
            var article = await articleLogic.GetArticleByArticleNumber(model.ArticleNumber, null);
            if (article == null)
            {
                throw new NotFoundException($"Could not find article with #: {model.ArticleNumber}.");
            }

            // Update content if editor region specified
            if (!string.IsNullOrWhiteSpace(model.EditorId))
            {
                article.Content = UpdateRegionInDocument(
                    model.EditorId,
                    article.Content,
                    CryptoJsDecryption.Decrypt(model.Data));
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

            var result = await mediator.SendAsync(command);

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
                await hub.Clients.All.SendCoreAsync("UpdateEditors", [model.Id, model.Data]);
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
        /// Updates a single region in an editable document.
        /// </summary>
        /// <param name="model">Editor view model.</param>
        /// <returns>Returns OK on success.</returns>
        public async Task<IActionResult> EditSaveRegion(EditorRegionViewModel model)
        {
            var article = await dbContext.Articles.Where(w => w.ArticleNumber == model.ArticleNumber).OrderBy(o => o.VersionNumber).LastOrDefaultAsync();

            var decryptedData = CryptoJsDecryption.Decrypt(model.Data);

            // Now carry over what's being UPDATED to the original.
            var content = UpdateRegionInDocument(model.EditorId, article.Content, decryptedData);

            if (article.Content != content)
            {
                article.Content = content;
                article.Updated = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync();
                await hub.Clients.All.SendCoreAsync("UpdateEditors", [model.EditorId, model.Data]);
            }

            return Ok();
        }

        /// <summary>
        /// Updates the entire body of a web page.
        /// </summary>
        /// <param name="model">Editor view model.</param>
        /// <returns>Returns OK on success.</returns>
        public async Task<IActionResult> EditSaveBody(EditorRegionViewModel model)
        {
            var article = await dbContext.Articles.Where(w => w.ArticleNumber == model.ArticleNumber).OrderBy(o => o.VersionNumber).LastOrDefaultAsync();

            var decryptedData = CryptoJsDecryption.Decrypt(model.Data);

            if (article.Content != decryptedData)
            {
                article.Content = decryptedData;
                article.Updated = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync();
            }

            return Ok();
        }

        /// <summary>
        /// Edit web page code with Monaco editor.
        /// </summary>
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
            ViewData["LastPubDateTime"] = await articleLogic.GetLastPublishedDate(id);

            var catalogEntry = await articleLogic.GetCatalogEntry(article);

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
        ///     Saves the code and html of the page.
        /// </summary>
        /// <param name="model">Edit code post model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        ///     This method saves page code to the database.  <see cref="EditCodePostModel.Content" /> is validated using method
        ///     <see cref="BaseController.BaseValidateHtml" />.
        ///     HTML formatting errors that could not be automatically fixed are logged with
        ///     <see cref="ControllerBase.ModelState" /> and
        ///     the code is not saved in the database.
        /// </remarks>
        [HttpPost]
        [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
        public async Task<IActionResult> EditCode(EditCodePostModel model)
        {
            model.Content = CryptoJsDecryption.Decrypt(model.Content);
            model.HeadJavaScript = CryptoJsDecryption.Decrypt(model.HeadJavaScript);
            model.FooterJavaScript = CryptoJsDecryption.Decrypt(model.FooterJavaScript);
            var saveError = new StringBuilder();

            // Validate the model as it comes in.
            if (ModelState.IsValid)
            {
                if (model == null)
                {
                    return NotFound();
                }

                // Check for nested editable regions.
                if (!NestedEditableRegionValidation.Validate(model.Content))
                {
                    ModelState.AddModelError("Content", "Cannot have nested editable regions.");
                }

                model.Content = htmlService.EnsureEditableMarkers(model.Content);

                // CHANGED: Use articleLogic to get the full article with all properties
                var article = await articleLogic.GetArticleByArticleNumber(model.ArticleNumber, null);

                if (article == null)
                {
                    return NotFound();
                }

                var jsonModel = new SaveCodeResultJsonModel();

                // If still valid, continue processing.
                if (ModelState.IsValid)
                {
                    try
                    {
                        // NEW: Use SaveArticle command instead of ArticleEditLogic
                        var command = new SaveArticleCommand
                        {
                            ArticleNumber = model.ArticleNumber,
                            Title = model.Title,
                            Content = model.Content,
                            HeadJavaScript = model.HeadJavaScript,
                            FooterJavaScript = model.FooterJavaScript,
                            BannerImage = article.BannerImage,
                            UrlPath = article.UrlPath,
                            ArticleType = (ArticleType)article.ArticleType,
                            Category = article.Category,
                            Introduction = article.Introduction,
                            Published = article.Published,
                            UserId = Guid.Parse(await GetUserId())
                        };

                        var result = await mediator.SendAsync(command);

                        if (!result.IsSuccess)
                        {
                            // Handle validation errors
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
                            else if (result.ErrorMessage != null)
                            {
                                ModelState.AddModelError("Save", result.ErrorMessage);
                            }

                            jsonModel.ErrorCount = ModelState.ErrorCount;
                            jsonModel.IsValid = false;
                            jsonModel.Errors.AddRange(ModelState.Values
                                .Where(w => w.ValidationState == ModelValidationState.Invalid)
                                .ToList());
                            jsonModel.ValidationState = ModelValidationState.Invalid;

                            return Json(jsonModel);
                        }

                        // Success - result.Data.Model contains the updated article
                        logger.LogInformation(
                            "Successfully saved article {ArticleNumber} via mediator",
                            model.ArticleNumber);
                    }
                    catch (Exception e)
                    {
                        ViewData["Version"] = article.VersionNumber;
                        var provider = new EmptyModelMetadataProvider();
                        ModelState.AddModelError("Save", e, provider.GetMetadataForType(typeof(string)));
                        logger.LogError(e, "Error saving article {ArticleNumber}", model.ArticleNumber);
                    }

                    jsonModel.ErrorCount = ModelState.ErrorCount;
                    jsonModel.IsValid = ModelState.IsValid;
                    jsonModel.Errors.AddRange(ModelState.Values
                        .Where(w => w.ValidationState == ModelValidationState.Invalid)
                        .ToList());
                    jsonModel.ValidationState = ModelState.ValidationState;

                    return Json(jsonModel);
                }
            }

            // Error handling (unchanged)
            saveError.AppendLine("Error(s):");
            saveError.AppendLine("<ul>");

            var errors = ModelState.Values.Where(w => w.ValidationState == ModelValidationState.Invalid).ToList();

            foreach (var error in errors)
            {
                foreach (var e in error.Errors)
                {
                    saveError.AppendLine("<li>" + e.ErrorMessage + "</li>");
                }
            }

            saveError.AppendLine("</ul>");

            return StatusCode(StatusCodes.Status500InternalServerError, saveError.ToString());
        }

        /// <summary>
        /// Performs a query to see what pages will have changes.
        /// </summary>
        /// <param name="model">Post view model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<IActionResult> SearchAndReplaceQuery(SearchAndReplaceViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.ArticleNumber.HasValue)
            {
                var articleCount = await dbContext.Articles.Where(c => c.ArticleNumber == model.ArticleNumber && c.Content.Contains(model.FindValue)).CountAsync();

                ViewData["SearchAndReplacePrequery"] = $"{articleCount} versions will be modified.";
            }
            else
            {
                var articleCount = await dbContext.Articles.Where(c => c.Published != null && c.Content.Contains(model.FindValue)).CountAsync();

                ViewData["SearchAndReplacePrequery"] = $"{articleCount} published articles will be modified.";
            }

            return View(model);
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
                article = await articleLogic.GetArticleById(id.Value, userId);
            }
            else
            {
                // Get the user's ID for logging.
                article = await articleLogic.CreateArticle("Blank Page", userId);
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
        /// Gets a list of articles (web pages).
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
        /// Gets a list of published pages.
        /// </summary>
        /// <returns>List of published pages.</returns>
        [HttpGet]
        public async Task<IActionResult> GetPublishedPageList()
        {
            var activeCode = (int)StatusCodeEnum.Active;
            var redirectCode = (int)StatusCodeEnum.Redirect;
            var pages = await dbContext.Pages.Where(w => w.Published.HasValue && (w.StatusCode == activeCode || w.StatusCode == redirectCode)).Select(s =>
            new
            {
                s.Id,
                s.ArticleNumber
            }).ToListAsync();

            return Json(pages);
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
        /// Publishes a table of contents and new site map file.
        /// </summary>
        /// <param name="path">TOC root path.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet]
        [Authorize(Roles = "Editors,Administrators")]
        public async Task<IActionResult> PublishTOC(string path = "/")
        {
            await publishingService.WriteTocAsync(path);
            return Ok();
        }

        /// <summary>
        /// Gets a list of articles (pages) on this website.
        /// </summary>
        /// <param name="text">Search text.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>Returns published and non-published links.</remarks>
        public async Task<IActionResult> List_Articles(string text)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IQueryable<Article> query = dbContext.Articles
            .OrderBy(o => o.Title)
            .Where(w => w.StatusCode == (int)StatusCodeEnum.Active || w.StatusCode == (int)StatusCodeEnum.Inactive);

            if (!string.IsNullOrEmpty(text))
            {
                query = query.Where(x => x.Title.ToLower().Contains(text.ToLower()));
            }

            var model = await query.Select(s => new
            {
                s.Title,
                s.UrlPath
            }).Distinct().Take(10).ToListAsync();

            return Json(model);
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
        ///     Gets a role list, and allows for filtering.
        /// </summary>
        /// <param name="text">Filter string.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpGet]
        public async Task<IActionResult> Get_RoleList(string text)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var query = dbContext.Roles.Select(s => new RoleItemViewModel
            {
                Id = s.Id,
                RoleName = s.Name,
                RoleNormalizedName = s.NormalizedName
            });

            if (!string.IsNullOrEmpty(text))
            {
                query = query.Where(w => w.RoleName.StartsWith(text));
            }

            return Json(await query.OrderBy(r => r.RoleName).ToListAsync());
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

            var query = articleLogic.GetArticleRedirects();

            ViewData["RowCount"] = await query.CountAsync();

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

            var model = await query.Skip(pageNo * pageSize).Take(pageSize).ToListAsync();

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
        /// Updates the time stamps for all published pages.
        /// </summary>
        /// <returns>IActionResult.</returns>
        [HttpGet]
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> UpdateTimeStamps()
        {
            var pages = await dbContext.Pages.ToListAsync();
            var c = 0;
            foreach (var page in pages)
            {
                c++;
                page.Updated = DateTime.UtcNow;

                if (c >= 20)
                {
                    await dbContext.SaveChangesAsync();
                    c = 0;
                }
            }

            await dbContext.SaveChangesAsync();

            return Json("Ok");
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
                var cdnService = CdnService.GetCdnService(dbContext, logger, HttpContext);

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

        private async Task<Article> GetArticleForEdit(int articleNumber)
        {
            var article = await dbContext.Articles.Where(w => w.ArticleNumber == articleNumber).OrderByDescending(o => o.VersionNumber).FirstOrDefaultAsync();
            if (article == null)
            {
                return null;
            }

            if (article.Published.HasValue)
            {
                return await this.articleLogic.NewVersion(article);
            }

            return article;
        }

    }
}
