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
    using Cosmos.Common.Services.Caching;
    using Cosmos.DynamicConfig;
    using Cosmos.Editor.Services;
    using HtmlAgilityPack;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using SendGrid.Helpers.Errors.Model;
    using Sky.Cms.Hubs;
    using Sky.Cms.Models;
    using Sky.Cms.Services;
    using Sky.Editor.Data;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Articles.Delete;
    using Sky.Editor.Features.Articles.Save;
    using Sky.Editor.Features.Articles.Trash;
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
        /// <param name="logger">Logger.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="roleManager">Role manager.</param>
        /// <param name="articleLogic">Article logic.</param>
        /// <param name="editorSettings">Editor settings.</param>
        /// <param name="viewRenderService">View renderer.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="hub">Live editor hub.</param>
        /// <param name="publishingService">Publishing service.</param>
        /// <param name="htmlService">HTML service.</param>
        /// <param name="reservedPaths">Reserved paths.</param>
        /// <param name="titleChangeService">Title change service.</param>
        /// <param name="templateService">Template service.</param>
        /// <param name="mediator">Mediator.</param>
        /// <param name="memoryCache">Memory cache.</param>
        /// <param name="configProvider">Dynamic configuration provider.</param>
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
            ICacheService<Layout> memoryCache,
            IDynamicConfigurationProvider configProvider)
            : base(dbContext, userManager, mediator, memoryCache, configProvider)
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

            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var initialHomePageResult = await TryGetInitialHomePageResultAsync();
            if (initialHomePageResult != null)
            {
                return initialHomePageResult;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            ViewData["IsDesigner"] = true;

            var context = await GetEditableArticleContextAsync(id);
            if (context == null)
            {
                return NotFound();
            }

            var (article, catalogEntry) = context.Value;

            var defaultLayout = await GetCurrentLayoutAsync();
            var config = new DesignerConfig(defaultLayout, article.ArticleNumber.ToString(), article.Title);
            var assets = await FileManagerController.GetImageAssetArray(storageContext, $"/pub/articles/{id}", string.Empty);
            if (assets != null)
            {
                config.ImageAssets.AddRange(assets);
            }

            ViewData["DesignerConfig"] = config;
            await PopulateEditorViewDataAsync(article.ArticleNumber, article.Title, article.Content, article.VersionNumber);

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
        /// Gets designer for GrapeJS.
        /// </summary>
        /// <param name="id">Article number.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet]
        public async Task<IActionResult> GetDesignerData(int id)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var article = await GetArticleViewModelAsync(id);

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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            if (id == null)
            {
                return RedirectToAction("Index");
            }

            PopulateSortPagingViewData(sortOrder, currentSort, pageNo, pageSize);
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

            var deletedStatusCode = (int)StatusCodeEnum.Deleted;
            var data = await dbContext.Articles
                .Where(w => w.StatusCode == deletedStatusCode)
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var left = await mediator.QueryAsync<ArticleViewModel>(new GetArticleByIdQuery { Id = leftId });
            var right = await mediator.QueryAsync<ArticleViewModel>(new GetArticleByIdQuery { Id = rightId });
            @ViewData["PageTitle"] = left.Title;

            ViewData["LeftVersion"] = left.VersionNumber;
            ViewData["RightVersion"] = right.VersionNumber;

            var model = new CompareCodeViewModel()
            {
                EditorTitle = left.Title,
                EditorFields = GetDefaultCodeEditorFields(),
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            if (id == null)
            {
                return Json(string.Empty);
            }

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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var initialHomePageResult = await TryGetInitialHomePageResultAsync();
            if (initialHomePageResult != null)
            {
                return initialHomePageResult;
            }

            await PopulateCreatePageViewDataAsync(sortOrder, currentSort, pageNo, pageSize);

            return View(new CreatePageViewModel()
            {
                Id = Guid.NewGuid(),
                Title = title.Contains("{new page name}", StringComparison.CurrentCultureIgnoreCase) ? string.Empty : title
            });
        }

        /// <summary>
        ///     Uses <see cref="CreateArticleCommand"/> via mediator to create a <see cref="ArticleViewModel"/> general article ready for editing.
        /// </summary>
        /// <param name="model">Create page view model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
        [HttpPost]
        public async Task<IActionResult> Create(CreatePageViewModel model)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                await PopulateCreatePageViewDataAsync(pageNo: 0, pageSize: 20, currentSort: "title");
                return View(model);
            }

            if (model == null)
            {
                return NotFound();
            }

            model.Title = model.Title.TrimStart('/');

            var command = new CreateArticleCommand
            {
                Title = model.Title,
                TemplateId = model.TemplateId,
                UserId = Guid.Parse(await GetUserId()),
                ArticleType = ArticleType.General,
                BlogKey = string.Empty,
                Category = model.Category,
                Introduction = model.Introduction
            };

            var result = await mediator.SendAsync<CommandResult<ArticleViewModel>>(command);

            if (!result.IsSuccess)
            {
                AddCommandErrorsToModelState(result);
                return View(viewName: "__NewHomePage", model: model);
            }

            return RedirectToAction("Versions", new { id = result.Data.ArticleNumber });
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
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

            var template = await GetHomePageTemplateAsync(exactTitleMatch: true);

            if (template == null)
            {
                ModelState.AddModelError("Title", "Home page template not found.");
                return View(viewName: "__NewHomePage", model: model);
            }

            var command = new CreateArticleCommand
            {
                Title = model.Title,
                TemplateId = template.Id,
                UserId = Guid.Parse(await GetUserId()),
                ArticleType = ArticleType.General,
                BlogKey = string.Empty,
                Published = DateTimeOffset.UtcNow,
                StatusCode = StatusCodeEnum.Active,
                ContentOverride = template.Content,
                UrlPathOverride = "root"
            };

            var result = await mediator.SendAsync<CommandResult<ArticleViewModel>>(command);

            if (!result.IsSuccess)
            {
                AddCommandErrorsToModelState(result);
                return View(viewName: "__NewHomePage", model: model);
            }

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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            PopulateSortPagingViewData(sortOrder, currentSort, pageNo, pageSize);
            ViewData["showingRoles"] = forRoles;

            var catalogEntry = await GetArticleCatalogEntryAsync(id);
            if (catalogEntry == null)
            {
                return NotFound();
            }

            ViewData["ArticleNumber"] = catalogEntry.ArticleNumber;
            ViewData["ArticlePermissions"] = catalogEntry.ArticlePermissions;
            var objectIds = catalogEntry.ArticlePermissions.Select(s => s.IdentityObjectId).ToArray();

            ViewData["ViewModel"] = new ArticlePermissionsViewModel(catalogEntry, forRoles);
            ViewData["Title"] = catalogEntry.Title;
            ViewData["AllowedUsers"] = await userManager.Users.Where(w => objectIds.Contains(w.Id)).ToListAsync();
            ViewData["AllowedRoles"] = await roleManager.Roles.Where(w => objectIds.Contains(w.Id)).ToListAsync();

            var query = GetPermissionItemsQuery(forRoles);

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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            try
            {
                // Load with tracking to enable change detection
                var entry = await dbContext.ArticleCatalog
                    .Include(c => c.ArticlePermissions)
                    .FirstOrDefaultAsync(a => a.ArticleNumber == id);
                if (entry == null)
                {
                    return NotFound();
                }

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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var paths = await reservedPaths.GetReservedPaths();

            ViewData["RowCount"] = paths.Count;

            var query = paths.AsQueryable();

            ViewData["Filter"] = filter;
            PopulateSortPagingViewData(sortOrder, currentSort, pageNo, pageSize);

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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();

            if (invalidModelState != null)
            {
                return invalidModelState;
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
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            ViewData["BlobEndpointUrl"] = editorSettings.BlobPublicUrl;

            var model = await GetArticleViewModelAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            var isPublished = model.Published.HasValue;

            await PopulateEditorViewDataAsync(model.ArticleNumber, model.Title, model.Content, showHtmlEditorMenuPick: false);

            model.EditModeOn = true;
            model.Published = null;

            if (isPublished && User.IsInRole("Authors"))
            {
                return Unauthorized();
            }

            var context = await GetEditableArticleContextAsync(id);
            if (context == null)
            {
                return NotFound();
            }

            return View(new HtmlEditorViewModel(model, context.Value.CatalogEntry));
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

            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            // Validate CryptoContextToken if provided
            if (!string.IsNullOrEmpty(model.CryptoContextToken))
            {
                if (!IsValidCryptoContextToken(model.CryptoContextToken))
                {
                    return BadRequest("Invalid CryptoContextToken.");
                }
            }

            // Get original article
            var article = await mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = model.ArticleNumber });
            if (article == null)
            {
                throw new NotFoundException($"Could not find article with #: {model.ArticleNumber}.");
            }

            IActionResult? commandResult = model.Command switch
            {
                "SaveRegion" => HandleSaveRegionCommand(article, model),
                "SaveBody" => HandleSaveBodyCommand(article, model),
                "SaveCode" => HandleSaveCodeCommand(article, model),
                "SaveDesigner" => HandleSaveDesignerCommand(article, model),
                "SavePageProperties" => HandleSavePagePropertiesCommand(article, model),
                _ when string.IsNullOrWhiteSpace(model.Command) => Json(new
                {
                    ServerSideSuccess = false,
                    errors = new Dictionary<string, string[]>
                    {
                        ["Command"] = new[] { "Command cannot be null or empty." }
                    }
                }),
                _ => Json(new
                {
                    ServerSideSuccess = false,
                    errors = new Dictionary<string, string[]>
                    {
                        ["Command"] = new[] { $"Unrecognized command: '{model.Command}'. Valid commands are: SaveRegion, SaveBody, SaveCode, SaveDesigner, SavePageProperties." }
                    }
                })
            };

            if (commandResult != null)
            {
                return commandResult;
            }

            // NEW: Use SaveArticle command
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                BannerImage = article.BannerImage,
                ArticleType = article.ArticleType,
                Category = article.Category,
                Introduction = article.Introduction,
                Content = article.Content,
                HeadJavaScript = article.HeadJavaScript,
                FooterJavaScript = article.FooterJavaScript,
                UrlPath = article.UrlPath,
                Published = article.Published,
                UserId = Guid.Parse(await GetUserId())
            };

            var result = await mediator.SendAsync<CommandResult<Sky.Editor.Features.Articles.Save.ArticleUpdateResult>>(command);

            if (!result.IsSuccess)
            {
                return Json(new
                {
                    ServerSideSuccess = false,
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var context = await GetEditableArticleContextAsync(id);
            if (context == null)
            {
                return NotFound();
            }

            var (article, catalogEntry) = context.Value;

            await PopulateEditorViewDataAsync(article.ArticleNumber, article.Title, article.Content, article.VersionNumber);

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
                EditorFields = GetDefaultCodeEditorFields(),
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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

                var query = $"SELECT c.ArticleNumber, c.ArticleType, c.Title, c.UrlPath, " +
                    "MAX(c.Published) as Published, " +
                    "MAX(c.Updated) as Updated, " +
                    "MAX(IIF(IS_DEFINED(c.Content) AND (CONTAINS(LOWER(c.Content), ' contenteditable=') OR CONTAINS(LOWER(c.Content), ' data-ccms-ceid=')), 1, 0)) as EditableRegionCount " +
                    $"FROM Articles c {whereClause} GROUP BY c.ArticleNumber, c.ArticleType, c.Title, c.UrlPath";

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
                    Updated = s.Updated.UtcDateTime.ToString("o"),
                    HtmlEditorEnabled = (s.EditableRegionCount ?? 0) > 0,
                }).OrderBy(o => o.Title).ToList();

                return Json(model);
            }
            else
            {
                var activeStatusCode = (int)StatusCodeEnum.Active;
                var query = publishedOnly ? dbContext.Articles
                    .Where(a => a.Published != null && a.StatusCode == activeStatusCode && a.ArticleType != blogPostArticleType) :
                    dbContext.Articles
                    .Where(a => a.StatusCode == activeStatusCode && a.ArticleType == 0);

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
                    Updated = g.Max(x => x.Updated),
                    EditableRegionCount = g.Count(x =>
                        x.Content != null &&
                        (x.Content.ToLower().Contains(" contenteditable=") ||
                         x.Content.ToLower().Contains(" data-ccms-ceid=")))
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
                    Updated = s.Updated,
                    HtmlEditorEnabled = s.EditableRegionCount > 0,
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var deleteArticleCommand = new DeleteArticleCommand
            {
                ArticleNumber = id
            };

            var result = await mediator.SendAsync<CommandResult<Unit>>(deleteArticleCommand);

            if (!result.IsSuccess)
            {
                return BadRequest(result.ErrorMessage);
            }

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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            PopulateSortPagingViewData(sortOrder, currentSort, pageNo, pageSize);

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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var redirectStatusCode = (int)StatusCodeEnum.Redirect;
            var redirect = await dbContext.Articles.FirstOrDefaultAsync(f => f.Id == id && f.StatusCode == redirectStatusCode);
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

        /// <summary>
        /// Populates editor view data values.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="title">Article title.</param>
        /// <param name="content">Article content.</param>
        /// <param name="versionNumber">Optional version number.</param>
        /// <param name="showHtmlEditorMenuPick">Whether to enable the HTML editor menu pick option.</param>
        private async Task PopulateEditorViewDataAsync(int articleNumber, string title, string content, int? versionNumber = null, bool showHtmlEditorMenuPick = true)
        {
            if (versionNumber.HasValue)
            {
                ViewData["Version"] = versionNumber.Value;
            }

            ViewData["PageTitle"] = title;
            ViewData["Published"] = null;
            ViewData["LastPubDateTime"] = await mediator.QueryAsync(new GetLastPublishedDateQuery
            {
                ArticleNumber = articleNumber
            });

            var htmlContent = htmlService.EnsureEditableMarkers(content);
            ViewData["EnableHtmlEditorMenuPick"] = showHtmlEditorMenuPick && htmlService.HasEditableRegions(htmlContent);
        }

        /// <summary>
        /// Populates create page view data values.
        /// </summary>
        /// <param name="sortOrder">Sort order.</param>
        /// <param name="currentSort">Current sort field.</param>
        /// <param name="pageNo">Page number to retrieve.</param>
        /// <param name="pageSize">Number of records in each page.</param>
        private async Task PopulateCreatePageViewDataAsync(string sortOrder = "asc", string currentSort = "Title", int pageNo = 0, int pageSize = 20)
        {
            var defaultLayout = await GetCurrentLayoutAsync();

            ViewData["Layouts"] = await BaseGetLayoutListItems();
            PopulateSortPagingViewData(sortOrder, currentSort, pageNo, pageSize);

            var reserved = await reservedPaths.GetReservedPaths();
            var activeStatusCode = (int)StatusCodeEnum.Active;
            var existingUrls = await dbContext.Articles.Where(w => w.StatusCode == activeStatusCode).Select(s => s.Title).Distinct().ToListAsync();
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
                    UsesHtmlEditor = s.Content.ToLower().Contains(" contenteditable=") || s.Content.ToLower().Contains(" data-ccms-ceid="/**/)
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
        }

        /// <summary>
        /// Gets the permission item query.
        /// </summary>
        /// <param name="forRoles">Whether to return role items instead of user items.</param>
        /// <returns>A queryable list of permission items.</returns>
        private IQueryable<ArticlePermisionItem> GetPermissionItemsQuery(bool forRoles)
        {
            if (forRoles)
            {
                return roleManager.Roles.Select(
                    s => new ArticlePermisionItem
                    {
                        IdentityObjectId = s.Id,
                        Name = s.Name,
                    }).AsQueryable();
            }

            return userManager.Users.Select(
                s => new ArticlePermisionItem
                {
                    IdentityObjectId = s.Id,
                    Name = s.Email,
                }).AsQueryable();
        }

        /// <summary>
        /// Gets the initial home page result when the site has no articles.
        /// </summary>
        /// <returns>A result for first-time home page creation; otherwise, <see langword="null" />.</returns>
        private async Task<IActionResult?> TryGetInitialHomePageResultAsync()
        {
            ViewData["ShowFirstPageBtn"] = false;

            if (await dbContext.Articles.CountAsync() != 0)
            {
                return null;
            }

            var template = await GetHomePageTemplateAsync();
            if (template == null)
            {
                ViewData["ShowFirstPageBtn"] = true;
                return null;
            }

            return View(viewName: "__NewHomePage", model: CreateInitialHomePageViewModel(template.Id));
        }

        /// <summary>
        /// Gets the home page template.
        /// </summary>
        /// <param name="exactTitleMatch">Whether to require an exact title match.</param>
        /// <returns>The matching home page template, if found; otherwise, <see langword="null" />.</returns>
        private async Task<Template?> GetHomePageTemplateAsync(bool exactTitleMatch = false)
        {
            if (exactTitleMatch)
            {
                return await dbContext.Templates.FirstOrDefaultAsync(f => f.Title.ToLower() == "home page");
            }

            return await dbContext.Templates.FirstOrDefaultAsync(f => f.Title.ToLower().Contains("home page"));
        }

        /// <summary>
        /// Creates the initial home page view model.
        /// </summary>
        /// <param name="templateId">Template ID.</param>
        /// <returns>A populated initial home page view model.</returns>
        private CreatePageViewModel CreateInitialHomePageViewModel(Guid templateId)
        {
            return new CreatePageViewModel
            {
                TemplateId = templateId,
                Title = string.Empty,
                ArticleNumber = 1,
                Id = Guid.NewGuid()
            };
        }

        /// <summary>
        /// Adds command errors to model state.
        /// </summary>
        /// <typeparam name="T">Command result data type.</typeparam>
        /// <param name="result">Command result.</param>
        private void AddCommandErrorsToModelState<T>(CommandResult<T> result)
        {
            var errors = result.Errors?.SelectMany(e => e.Value) ?? Enumerable.Empty<string>();
            var errorMessage = result.ErrorMessage ?? string.Join(", ", errors);
            ModelState.AddModelError(string.Empty, errorMessage);
        }

        /// <summary>
        /// Gets an article view model by article number.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>The article view model, if found; otherwise, <see langword="null" />.</returns>
        private async Task<ArticleViewModel?> GetArticleViewModelAsync(int articleNumber)
        {
            return await mediator.QueryAsync<ArticleViewModel>(new GetArticleByArticleNumberQuery
            {
                ArticleNumber = articleNumber
            });
        }

        /// <summary>
        /// Gets an article catalog entry by article number.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>The catalog entry, if found; otherwise, <see langword="null" />.</returns>
        private async Task<CatalogEntry?> GetArticleCatalogEntryAsync(int articleNumber)
        {
            return await mediator.QueryAsync(new GetArticleCatalogEntryQuery
            {
                ArticleNumber = articleNumber
            });
        }

        /// <summary>
        /// Gets the editable article context.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>The editable article and catalog entry, if found; otherwise, <see langword="null" />.</returns>
        private async Task<(Article Article, CatalogEntry CatalogEntry)?> GetEditableArticleContextAsync(int articleNumber)
        {
            var article = await GetArticleForEdit(articleNumber);
            if (article == null)
            {
                return null;
            }

            var catalogEntry = await GetArticleCatalogEntryAsync(article.ArticleNumber);
            if (catalogEntry == null)
            {
                return null;
            }

            return (article, catalogEntry);
        }

        /// <summary>
        /// Handles the <c>SaveRegion</c> command: updates a single editable region in the page body
        /// or, for blog posts, replaces the entire content.
        /// </summary>
        /// <param name="article">Article to update.</param>
        /// <param name="model">Editor post model.</param>
        /// <returns><see langword="null"/> on success; an <see cref="IActionResult"/> error response otherwise.</returns>
        private IActionResult? HandleSaveRegionCommand(ArticleViewModel article, EditPostViewModel model)
        {
            var decryptedPayload = DecryptContent(model.Payload);

            if (article.ArticleType == ArticleType.BlogPost)
            {
                article.Content = decryptedPayload;
                article.BannerImage = model.BannerImage;
            }
            else
            {
                article.Content = UpdateRegionInDocument(
                    model.EditorId,
                    article.Content,
                    decryptedPayload);
            }

            return null;
        }

        /// <summary>
        /// Handles the <c>SaveBody</c> command: replaces the entire page content.
        /// </summary>
        /// <param name="article">Article to update.</param>
        /// <param name="model">Editor post model.</param>
        /// <returns>Always <see langword="null"/> (no early-exit errors possible).</returns>
        private IActionResult? HandleSaveBodyCommand(ArticleViewModel article, EditPostViewModel model)
        {
            article.Content = DecryptContent(model.Payload);
            return null;
        }

        /// <summary>
        /// Handles the <c>SaveCode</c> command: updates content and script fields from the code editor.
        /// </summary>
        /// <param name="article">Article to update.</param>
        /// <param name="model">Editor post model.</param>
        /// <returns><see langword="null"/> on success; a validation error <see cref="IActionResult"/> if nested regions are detected.</returns>
        private IActionResult? HandleSaveCodeCommand(ArticleViewModel article, EditPostViewModel model)
        {
            var decryptedContent = DecryptContent(model.Payload);

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
            article.HeadJavaScript = DecryptContent(model.HeadJavaScript);
            article.FooterJavaScript = DecryptContent(model.FooterJavaScript);
            article.Title = model.Title;

            return null;
        }

        /// <summary>
        /// Handles the <c>SaveDesigner</c> command: processes GrapesJS designer output and assembles the final HTML.
        /// </summary>
        /// <param name="article">Article to update.</param>
        /// <param name="model">Editor post model.</param>
        /// <returns><see langword="null"/> on success; a validation error <see cref="IActionResult"/> if nested regions are detected.</returns>
        private IActionResult? HandleSaveDesignerCommand(ArticleViewModel article, EditPostViewModel model)
        {
            var htmlContent = model.Payload != null ? DecryptContent(model.Payload) : string.Empty;
            var cssContent = model.CssContent != null ? DecryptContent(model.CssContent) : string.Empty;

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

            htmlContent = htmlService.EnsureEditableMarkers(htmlContent);

            var designerUtils = new DesignerUtilities();
            article.Content = designerUtils.AssembleDesignerOutput(
                new DesignerDataViewModel
                {
                    CssContent = cssContent,
                    HtmlContent = htmlContent,
                    Title = model.Title,
                    Id = model.Id
                });

            article.Title = model.Title;
            article.BannerImage = model.BannerImage;
            article.Category = model.Category;
            article.Introduction = model.Introduction;

            return null;
        }

        /// <summary>
        /// Handles the <c>SavePageProperties</c> command: updates article metadata without touching content or scripts.
        /// </summary>
        /// <param name="article">Article to update.</param>
        /// <param name="model">Editor post model.</param>
        /// <returns><see langword="null"/> on success; a validation error <see cref="IActionResult"/> if the title is empty.</returns>
        private IActionResult? HandleSavePagePropertiesCommand(ArticleViewModel article, EditPostViewModel model)
        {
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

            article.BannerImage = model.BannerImage;
            article.Category = model.Category;
            article.Introduction = model.Introduction;
            article.Title = model.Title;
            article.Published = model.Published;

            return null;
        }

        /// <summary>
        /// Updates the HTML within an editor region on a web page.
        /// </summary>
        /// <param name="editorId">Editor ID on page.</param>
        /// <param name="pageBody">Page body.</param>
        /// <param name="updatedContent">Updated content.</param>
        /// <returns>Revised page body.</returns>
        private string UpdateRegionInDocument(string editorId, string pageBody, string updatedContent)
        {
            var originalHtmlDoc = new HtmlDocument();
            originalHtmlDoc.LoadHtml(pageBody);
            var originalEditableDivs = originalHtmlDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]");

            var target = originalEditableDivs.FirstOrDefault(w => w.Attributes["data-ccms-ceid"].Value == editorId);
            if (target != null)
            {
                target.InnerHtml = updatedContent;
            }

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

            var editableRegions = htmlDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]");

            if (editableRegions == null || editableRegions.Count == 0)
            {
                return null;
            }

            foreach (var region in editableRegions)
            {
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
        /// Gets an article ready for editing.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>The editable article, if found; otherwise, <see langword="null" />.</returns>
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

        /// <summary>
        /// Permanently trashes an article.
        /// </summary>
        /// <param name="id">Article number.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpGet]
        [Authorize(Roles = "Administrators, Editors")]
        public async Task<IActionResult> TrashPermanently(int id)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var result = await mediator.SendAsync<CommandResult<Unit>>(new TrashArticleCommand
            {
                ArticleNumber = id
            });

            if (!result.IsSuccess)
            {
                return BadRequest(result.ErrorMessage ?? "Failed to permanently trash article.");
            }

            return Ok();
        }
    }
}
