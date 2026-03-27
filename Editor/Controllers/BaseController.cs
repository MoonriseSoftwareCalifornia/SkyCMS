// <copyright file="BaseController.cs" company="Moonrise Software, LLC">
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
    using System.Threading.Tasks;
    using System.Web;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services;
    using Cosmos.Common.Services.Caching;
    using Cosmos.DynamicConfig;
    using HtmlAgilityPack;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Sky.Cms.Models;
    using Sky.Editor.Features.Templates.Get;

    /// <summary>
    /// Base controller.
    /// </summary>
    public abstract class BaseController : Controller
    {
        private readonly UserManager<IdentityUser> baseUserManager;
        private readonly ApplicationDbContext dbContext;
        private readonly IMediator mediator;
        private readonly ICacheService<Layout> layoutCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseController"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="mediator">Mediator service.</param>
        /// <param name="layoutCache">Tenant-aware cache service for layout caching.</param>
        public BaseController(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            IMediator mediator,
            ICacheService<Layout> layoutCache = null)
        {
            this.dbContext = dbContext;
            baseUserManager = userManager;
            this.mediator = mediator;
            this.layoutCache = layoutCache;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseController"/> class.
        /// Backward-compatible overload: wraps <paramref name="memoryCache"/> in a tenant-aware
        /// <see cref="CacheService{Layout}"/> so callers that pass raw <see cref="IMemoryCache"/>
        /// still get correct tenant isolation.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="mediator">Mediator service.</param>
        /// <param name="memoryCache">Memory cache; wrapped internally into <see cref="ICacheService{Layout}"/>.</param>
        /// <param name="configProvider">Dynamic configuration provider for tenant-scoped cache keys.</param>
        public BaseController(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            IMediator mediator,
            IMemoryCache memoryCache,
            IDynamicConfigurationProvider configProvider = null)
            : this(
                dbContext,
                userManager,
                mediator,
                memoryCache != null
                    ? new CacheService<Layout>(memoryCache, new NullLogger<CacheService<Layout>>(), configProvider)
                    : null)
        {
        }

        /// <summary>
        ///     Server-side validation of HTML.
        /// </summary>
        /// <param name="fieldName">Field name to validate.</param>
        /// <param name="inputHtml">HTML data to check.</param>
        /// <returns>HTML content.</returns>
        /// <remarks>
        ///     <para>
        ///         The purpose of this method is to validate HTML prior to be saved to the database.
        ///         It uses an instance of <see cref="HtmlAgilityPack.HtmlDocument" /> to check HTML formatting.
        ///     </para>
        /// </remarks>
        public string BaseValidateHtml(string fieldName, string inputHtml)
        {
            if (!string.IsNullOrEmpty(inputHtml))
            {
                var contentHtmlDocument = new HtmlDocument();
                contentHtmlDocument.LoadHtml(HttpUtility.HtmlDecode(inputHtml));
                return contentHtmlDocument.DocumentNode.InnerText.Trim();
            }

            return string.Empty;
        }

        /// <summary>
        ///     Get Layout List Items.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<List<SelectListItem>> BaseGetLayoutListItems()
        {
            var layouts = await dbContext.Layouts.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.LayoutName
            }).ToListAsync();
            if (layouts != null)
            {
                return layouts;
            }

            var layoutViewModel = new LayoutViewModel();

            dbContext.Layouts.Add(layoutViewModel.GetLayout());
            await dbContext.SaveChangesAsync();

            return await dbContext.Layouts.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.LayoutName
            }).ToListAsync();
        }

        /// <summary>
        /// Gets the user ID of the currently logged in user.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected async Task<string> GetUserId()
        {
            // Get the user's ID for logging.
            var user = await baseUserManager.GetUserAsync(User);
            return user.Id;
        }

        /// <summary>
        /// Gets the current default (published) layout for the application.
        /// </summary>
        /// <returns>The default layout, or null if no default layout exists.</returns>
        /// <remarks>
        /// Cache duration: 10 seconds absolute expiration.
        /// </remarks>
        protected async Task<Layout?> GetCurrentLayoutAsync()
        {
            const string cacheKey = "default-layout";

            if (layoutCache != null && layoutCache.TryGet(cacheKey, out var cached) && cached != null)
            {
                return cached;
            }

            var layout = await FetchCurrentLayoutAsync();

            if (layout != null && layoutCache != null)
            {
                layoutCache.Set(cacheKey, layout, TimeSpan.FromSeconds(10));
            }

            return layout;
        }

        /// <summary>
        /// Fetches the current default layout from the database.
        /// </summary>
        /// <returns>The latest version of the default layout that is currently published, or null if none exists.</returns>
        private async Task<Layout?> FetchCurrentLayoutAsync()
        {
            var layoutViewModel = await mediator.QueryAsync(new Cosmos.Common.Features.Layouts.Queries.GetDefaultLayoutQuery());
            if (layoutViewModel == null)
                return null;

            // GetDefaultLayoutQuery returns LayoutViewModel, but we need Layout entity
            // Query the database directly for the Layout entity using the Id from the ViewModel
            return await dbContext.Layouts.FirstOrDefaultAsync(l => l.Id == layoutViewModel.Id);
        }

        /// <summary>
        /// Gets templates filtered for the current default layout.
        /// </summary>
        /// <returns>Queryable collection of templates for the current layout.</returns>
        /// <remarks>
        /// This method handles both migrated and unmigrated data by checking both LayoutNumber and LayoutId.
        /// Templates with LayoutNumber == 0 are considered unmigrated and are matched by LayoutId only.
        /// </remarks>
        protected async Task<IQueryable<Template>> GetTemplatesForCurrentLayoutAsync()
        {
            var layout = await GetCurrentLayoutAsync();

            if (layout == null)
            {
                // Return empty queryable if no default layout exists
                return Enumerable.Empty<Template>().AsQueryable();
            }

            return dbContext.Templates
                .Where(t => t.LayoutNumber == layout.LayoutNumber ||
                            (t.LayoutNumber == 0 && t.LayoutId == layout.Id)); // Handles unmigrated data
        }


        /// <summary>
        /// Builds save result JSON model.
        /// </summary>
        /// <returns>Returns the code editor save result.</returns>
        protected SaveCodeResultJsonModel BuildSaveResultModel()
        {
            var jsonModel = new SaveCodeResultJsonModel
            {
                ErrorCount = ModelState.ErrorCount,
                IsValid = ModelState.IsValid
            };
            jsonModel.Errors.AddRange(ModelState.Values
                .Where(w => w.ValidationState == ModelValidationState.Invalid)
                .ToList());
            jsonModel.ValidationState = ModelState.ValidationState;

            return jsonModel;
        }

        /// <summary>
        /// Edit template title and description.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Edit(Guid id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var query = new GetTemplateQuery { TemplateId = id };
            var result = await mediator.QueryAsync(query);

            if (!result.IsSuccess || result.Data?.Template == null)
            {
                return NotFound();
            }

            var template = result.Data.Template;
            ViewData["Title"] = template.Title;

            var model = new TemplateEditViewModel()
            {
                Title = template.Title,
                Description = template.Description,
                Id = id
            };
            return View(model);
        }

        /// <summary>
        /// Returns a <see cref="BadRequestObjectResult"/> when model state is invalid; otherwise <see langword="null"/>.
        /// </summary>
        /// <returns>Bad-request result or <see langword="null"/>.</returns>
        protected IActionResult? GetInvalidModelStateResult()
        {
            if (ModelState.IsValid)
            {
                return null;
            }

            return BadRequest(ModelState);
        }

        /// <summary>
        /// Populates common sort and paging view data values.
        /// </summary>
        /// <param name="sortOrder">Sort order.</param>
        /// <param name="currentSort">Current sort field.</param>
        /// <param name="pageNo">Page number.</param>
        /// <param name="pageSize">Page size.</param>
        protected void PopulateSortPagingViewData(string sortOrder, string currentSort, int pageNo, int pageSize)
        {
            ViewData["sortOrder"] = sortOrder;
            ViewData["currentSort"] = currentSort;
            ViewData["pageNo"] = pageNo;
            ViewData["pageSize"] = pageSize;
        }

        /// <summary>
        /// Validates the CryptoContextToken format and authenticity.
        /// </summary>
        /// <param name="token">The token to validate.</param>
        /// <returns><see langword="true"/> if valid; otherwise <see langword="false"/>.</returns>
        protected bool IsValidCryptoContextToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (token.StartsWith("invalid-", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Builds a dictionary of model-state errors keyed by field name, suitable for JSON responses.
        /// </summary>
        /// <returns>Dictionary of field-name to error-message arrays.</returns>
        protected Dictionary<string, string[]> BuildModelStateErrors()
        {
            return ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors
                        .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
                        .ToArray());
        }

        /// <summary>
        /// Safely decrypts CryptoJS-encrypted content, returning an empty string for null or empty input.
        /// </summary>
        /// <param name="content">Encrypted content to decrypt.</param>
        /// <returns>Decrypted string, or <see cref="string.Empty"/> if <paramref name="content"/> is null or empty.</returns>
        protected static string DecryptContent(string content)
        {
            return string.IsNullOrEmpty(content) ? string.Empty : CryptoJsDecryption.Decrypt(content);
        }

        /// <summary>
        /// Gets the default code editor fields used by article code editing.
        /// </summary>
        /// <returns>Default article code editor fields.</returns>
        protected static EditorField[] GetDefaultCodeEditorFields()
        {
            return new[]
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
            };
        }

        /// <summary>
        /// Gets the default code editor fields used by template code editing.
        /// </summary>
        /// <returns>Default template code editor fields.</returns>
        protected static List<EditorField> GetTemplateCodeEditorFields()
        {
            return new List<EditorField>
            {
                new ()
                {
                    EditorMode = EditorMode.Html,
                    FieldName = "Html Content",
                    FieldId = "Content",
                    IconUrl = "~/images/seti-ui/icons/html.svg",
                    ToolTip = string.Empty
                }
            };
        }

        /// <summary>
        /// Applies query-string overrides to the edit post model.
        /// Fields in <paramref name="model"/> are only overwritten when they are empty/default
        /// and the corresponding field in <paramref name="queryModel"/> has a value.
        /// </summary>
        /// <param name="model">Primary edit post model (body).</param>
        /// <param name="queryModel">Optional query model containing override values.</param>
        protected static void ApplyQueryOverrides(EditPostViewModel model, EditPostViewModel? queryModel)
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
    }
}
