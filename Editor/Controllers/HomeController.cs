// <copyright file="HomeController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Controllers
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Articles.EditorQueries;
    using Cosmos.Common.Models;
    using HtmlAgilityPack;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Net.Http.Headers;
    using Sky.Cms.Models;
    using Sky.Cms.Services;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Layouts;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Home page controller.
    /// </summary>
    [Authorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]

    public class HomeController : Controller
    {
        private readonly Cosmos.Common.Features.Shared.IMediator articleQueries;
        private readonly EditorSettings options;
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<IdentityUser> userManager;
        private readonly IArticleHtmlService articleHtmlService;
        private readonly ILayoutTemplateService layoutTemplateService;
        private readonly IViewRenderService viewRenderService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeController"/> class.
        /// </summary>
        /// <param name="logger">ILogger to use.</param>
        /// <param name="options">Cosmos configuration.</param>
        /// <param name="dbContext"><see cref="ApplicationDbContext">Database context</see>.</param>
        /// <param name="articleQueries"><see cref="Cosmos.Common.Features.Shared.IMediator">Article queries.</see>.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="signInManager">Sign in manager service.</param>
        /// <param name="emailSender">Email service.</param>
        /// <param name="configuration">Website configuration.</param>
        /// <param name="services">Services provider.</param>
        /// <param name="articleHtmlService">Article HTML service.</param>
        /// <param name="layoutTemplateService">Layout template service.</param>
        /// <param name="viewRenderService">View rendering service.</param>
        public HomeController(
            ILogger<HomeController> logger,
            IEditorSettings options,
            ApplicationDbContext dbContext,
            Cosmos.Common.Features.Shared.IMediator articleQueries,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IEmailSender emailSender,
            IConfiguration configuration,
            IServiceProvider services,
            IArticleHtmlService articleHtmlService,
            ILayoutTemplateService layoutTemplateService = null,
            IViewRenderService viewRenderService = null)
        {
            this.options = (EditorSettings)options;
            this.articleQueries = articleQueries;
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.articleHtmlService = articleHtmlService;
            this.layoutTemplateService = layoutTemplateService
                ?? services?.GetService(typeof(ILayoutTemplateService)) as ILayoutTemplateService
                ?? throw new InvalidOperationException("Layout template service is not available");
            this.viewRenderService = viewRenderService
                ?? services?.GetService(typeof(IViewRenderService)) as IViewRenderService
                ?? throw new InvalidOperationException("View rendering service is not available");
        }

        /// <summary>
        /// Get edit list.
        /// </summary>
        /// <param name="target">Path to page.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> EditList(string target)
        {
            target = NormalizeTargetPath(target);

            var article = await articleQueries.QueryAsync(new GetArticleByUrlQuery
            {
                UrlPath = target
            });

            if (article == null)
            {
                return NotFound($"No article found for URL: {target}");
            }

            var data = await dbContext.Articles.OrderByDescending(o => o.VersionNumber)
                .Where(a => a.ArticleNumber == article.ArticleNumber).Select(s => new ArticleEditMenuItem
                {
                    Id = s.Id,
                    ArticleNumber = s.ArticleNumber,
                    Published = s.Published,
                    VersionNumber = s.VersionNumber,
                    UsesHtmlEditor = articleHtmlService.HasEditableRegions(s.Content)
                }).OrderByDescending(o => o.VersionNumber).Take(1).ToListAsync();

            return Json(data);
        }

        /// <summary>
        /// Gets the index page.
        /// </summary>
        /// <param name="lang">Language code.</param>
        /// <param name="mode">json or nothing.</param>
        /// <param name="itemId">Article, Template or Layout ID when previewing.</param>
        /// <param name="previewType">Type of object we are previewing.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Index(string lang = "", string mode = "", Guid? itemId = null, string previewType = "")
        {
            // Note: Setup check is handled by middleware (TenantSetupMiddleware for multi-tenant,
            // or Program.cs middleware for single-tenant) before this action is reached.
            // Ensure user is authenticated (middleware may bypass during setup)
            if (User.Identity?.IsAuthenticated == false)
            {
                Response.Cookies.Delete("CosmosAuthCookie");
                return Redirect("~/Identity/Account/Login");
            }

            // Make sure the user's claims identity has an account here.
            var user = await userManager.GetUserAsync(User);

            if (user == null)
            {
                Response.Cookies.Delete("CosmosAuthCookie");
                return Redirect("~/Identity/Account/Logout");
            }

            if (options.AllowSetup && (await dbContext.Users.CountAsync()) == 1 && !User.IsInRole("Administrators"))
            {
                await userManager.AddToRoleAsync(user, "Administrators");
            }

            // If yes, do NOT include headers that allow caching. 
            Response.Headers[HeaderNames.CacheControl] = "no-store";

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ArticleViewModel article;

            // This is NOT a preview, so we need to load the article by URL. If it doesn't exist, we need to load the not found page.
            if (string.IsNullOrEmpty(previewType))
            {
                ViewData["LoadEditList"] = true;
                ViewData["IsPreview"] = false;

                var path = HttpContext.Request.Path.Value?.TrimStart('/') ?? string.Empty;
                article = await articleQueries.QueryAsync(new GetArticleByUrlQuery
                {
                    UrlPath = path
                });

                if (article == null)
                {
                    // See if a page is un-published, but does exist, let us edit it.
                    article = await articleQueries.QueryAsync(new GetArticleByUrlQuery
                    {
                        UrlPath = HttpContext.Request.Path
                    });

                    // Create your own not found page for a graceful page for users.
                    article = await articleQueries.QueryAsync(new GetArticleByUrlQuery
                    {
                        UrlPath = "/not_found"
                    });

                    HttpContext.Response.StatusCode = 404;

                    if (article == null)
                    {
                        return NotFound();
                    }
                }

                await SetRenderedView(article);
                return View("Wrapper");
            }

            // This is a preview, so we need to load the object by ID. If it doesn't exist, we need to load the not found page.
            ViewData["IsPreview"] = true;
            ViewData["LoadEditList"] = false;

            if (previewType == "editor")
            {
                // This is an article preview
                await SetRenderedView(await articleQueries.QueryAsync(new GetArticleByIdQuery
                {
                    Id = itemId.Value
                }));
            }
            else if (previewType == "layouts")
            {
                await SetRenderedView(await GetLayoutPreview(itemId));
            }
            else if (previewType == "templates")
            {
                await SetRenderedView(await GetTemplatePreview(itemId));
            }
            else
            {
                return BadRequest($"Invalid preview type: {previewType}");
            }

            ViewData["CurrentPath"] = HttpContext.Request.Path.Value?.TrimStart('/') ?? string.Empty;

            return View("Wrapper");
        }

        /// <summary>
        /// Gets the error page.
        /// </summary>
        /// <returns>Returns an <see cref="IActionResult"/>.</returns>
        public IActionResult Error()
        {
            ViewData["EditModeOn"] = false;
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// Gets the application validation for Microsoft.
        /// </summary>
        /// <returns>Returns an <see cref="FileContentResult"/> if successful.</returns>
        [AllowAnonymous]
        public IActionResult GetMicrosoftIdentityAssociation()
        {
            var model = new MicrosoftValidationObject();
            model.associatedApplications.Add(new AssociatedApplication() { applicationId = options.MicrosoftAppId });

            var data = Newtonsoft.Json.JsonConvert.SerializeObject(model);

            return File(Encoding.UTF8.GetBytes(data), "application/json", fileDownloadName: "microsoft-identity-association.json");
        }

        /// <summary>
        /// Returns if a user has not been granted access yet.
        /// </summary>
        /// <returns>Returns an <see cref="IActionResult"/>.</returns>
        [Authorize]
        public IActionResult AccessPending()
        {
            var model = new ArticleViewModel
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 0,
                UrlPath = null,
                VersionNumber = 0,
                Published = null,
                Title = "Access Pending",
                Content = null,
                Updated = default,
                HeadJavaScript = null,
                FooterJavaScript = null,
                Layout = null,
                ReadWriteMode = false,
                PreviewMode = false,
                EditModeOn = false
            };
            return View(model);
        }

        private async Task<ArticleViewModel> GetLayoutPreview(Guid? itemId)
        {
            if (!itemId.HasValue)
            {
                throw new ArgumentNullException(nameof(itemId), "Layout ID cannot be null for preview");
            }

            var entity = await dbContext.Layouts.FirstOrDefaultAsync(f => f.Id == itemId);

            if (entity == null)
            {
                throw new InvalidOperationException($"Layout with ID {itemId} not found");
            }

            var previews = await layoutTemplateService.GetAllTemplatesAsync();
            var defaultPreview = previews?.FirstOrDefault();

            if (defaultPreview == null)
            {
                throw new InvalidOperationException("No default preview template available");
            }

            return CreatePreviewArticleModel(entity.Id, defaultPreview.Name, defaultPreview.Content, new LayoutViewModel(entity));
        }

        private async Task<ArticleViewModel> GetTemplatePreview(Guid? itemId)
        {
            if (!itemId.HasValue)
            {
                throw new ArgumentNullException(nameof(itemId), "Template ID cannot be null for preview");
            }

            var entity = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == itemId);

            if (entity == null)
            {
                throw new InvalidOperationException($"Template with ID {itemId} not found");
            }

            // Prepare preview content: ensure markers, then populate editable regions with Lorem Ipsum.
            var markedHtml = articleHtmlService.EnsureEditableMarkers(entity.Content);

            var doc = new HtmlDocument();
            doc.LoadHtml(markedHtml);

            var legacyEditableNodes = doc.DocumentNode.SelectNodes("//*[@contenteditable]") ?? new HtmlNodeCollection(null);
            bool templateUpdated = false;

            foreach (var node in legacyEditableNodes)
            {
                if (node.Attributes["contenteditable"] != null)
                {
                    node.Attributes.Remove("contenteditable");
                    templateUpdated = true;
                }

                var existingCeid = node.GetAttributeValue("data-ccms-ceid", string.Empty);
                if (string.IsNullOrWhiteSpace(existingCeid))
                {
                    node.SetAttributeValue("data-ccms-ceid", Guid.NewGuid().ToString());
                    templateUpdated = true;
                }
            }

            var convertedHtml = doc.DocumentNode.OuterHtml;

            if (templateUpdated)
            {
                entity.Content = convertedHtml;
                await dbContext.SaveChangesAsync();
            }

            var previewDoc = new HtmlDocument();
            previewDoc.LoadHtml(convertedHtml);

            var editableNodes = previewDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]") ?? new HtmlNodeCollection(null);

            int titleIndex = 0;
            int textIndex = 0;

            foreach (var node in editableNodes)
            {
                // Handle image widgets with placeholder
                var editorConfig = node.GetAttributeValue("data-editor-config", string.Empty).ToLowerInvariant();
                if (editorConfig == "image-widget")
                {
                    node.InnerHtml = "<div style=\"background-color: #e0e0e0; display: flex; align-items: center; justify-content: center; min-height: 200px; color: #666; font-size: 16px; font-family: Arial, sans-serif;\">Image goes here</div>";
                    continue;
                }

                bool isTitle = editorConfig == "title" || editorConfig == "heading";
                if (!isTitle)
                {
                    var tagName = node.Name?.ToLowerInvariant();
                    isTitle = tagName == "h1" || tagName == "h2" || tagName == "h3" || tagName == "h4" || tagName == "h5" || tagName == "h6";
                }

                if (isTitle)
                {
                    var text = LoremIpsum.Titles[titleIndex % LoremIpsum.Titles.Length];
                    node.InnerHtml = WebUtility.HtmlEncode(text);
                    titleIndex++;
                }
                else
                {
                    var text = LoremIpsum.Texts[textIndex % LoremIpsum.Texts.Length];
                    node.InnerHtml = $"<p>{WebUtility.HtmlEncode(text)}</p>";
                    textIndex++;
                }
            }

            var previewHtml = previewDoc.DocumentNode.OuterHtml;
            var defaultLayout = await LayoutHelper.GetCurrentDefaultLayoutAsync(dbContext);

            return CreatePreviewArticleModel(entity.Id, entity.Title, previewHtml, new LayoutViewModel(defaultLayout));
        }

        private async Task SetRenderedView(ArticleViewModel model)
        {
            var renderedView = await viewRenderService.RenderToStringAsync("~/Views/Home/Index.cshtml", model);
            ViewData["RenderedView"] = renderedView;
        }

        private static string NormalizeTargetPath(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return string.Empty;
            }

            return WebUtility.UrlDecode(target).Trim().TrimStart('/').TrimEnd('/');
        }

        private static ArticleViewModel CreatePreviewArticleModel(Guid id, string title, string content, LayoutViewModel layout)
        {
            return new ArticleViewModel
            {
                ArticleNumber = 1,
                LanguageCode = string.Empty,
                LanguageName = string.Empty,
                CacheDuration = 10,
                Content = content,
                StatusCode = StatusCodeEnum.Active,
                Id = id,
                Published = DateTimeOffset.UtcNow,
                Title = title,
                UrlPath = Guid.NewGuid().ToString(),
                Updated = DateTimeOffset.UtcNow,
                VersionNumber = 1,
                HeadJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                Layout = layout
            };
        }
    }
}
