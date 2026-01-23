// <copyright file="LayoutsController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Cosmos.BlobService;
    using Cosmos.Cms.Data.Logic;
    using Cosmos.Common.Data;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services;
    using Cosmos.DynamicConfig;
    using HtmlAgilityPack;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Sky.Cms.Models;
    using Sky.Cms.Services;
    using Sky.Editor.Data;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Models;
    using Sky.Editor.Models.GrapesJs;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Layouts;

    /// <summary>
    /// Layouts controller.
    /// </summary>
    [Authorize(Roles = "Administrators, Editors")]
    public class LayoutsController : BaseController
    {
        // Constants for magic strings
        private const string DefaultLayoutName = "Default Layout";
        private const string NewLayoutPrefix = "New Layout";
        private const string DefaultLayoutNotes = "Default layout created. Please customize using code editor.";
        private const string NewLayoutNotes = "New layout created. Please customize using code editor.";
        private const string HtmlRemovalDiv = "<div style=\"display:none;\"></div>";

        // HTML comment markers
        private const string HeaderStartMarker = "<!--CCMS--START--HEADER-->";
        private const string HeaderEndMarker = "<!--CCMS--END--HEADER-->";
        private const string FooterStartMarker = "<!--CCMS--START--FOOTER-->";
        private const string FooterEndMarker = "<!--CCMS--END--FOOTER-->";

        // Sort field names
        private const string SortFieldLayoutName = "LayoutName";
        private const string SortFieldName = "Name";
        private const string SortFieldArticleNumber = "ArticleNumber";
        private const string SortFieldDescription = "Description";

        // Sort orders
        private const string SortOrderAsc = "asc";
        private const string SortOrderDesc = "desc";

        private readonly ArticleEditLogic articleLogic;
        private readonly ApplicationDbContext dbContext;
        private readonly Uri blobPublicAbsoluteUrl;
        private readonly IViewRenderService viewRenderService;
        private readonly IStorageContext storageContext;
        private readonly IArticleHtmlService htmlService;
        private readonly ILogger<LayoutsController> logger;
        private readonly ILayoutImportService layoutImportService;

        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutsController"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="articleLogic"><see cref="ArticleEditLogic">Article edit logic</see>.</param>
        /// <param name="options"><see cref="IEditorSettings">Editor configuration</see> options.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="viewRenderService">View rendering service.</param>
        /// <param name="editorSettings">Editor settings.</param>
        /// <param name="htmlService">Html service.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="layoutImportService">Layout import service.</param>
        /// <param name="memoryCache">Memory cache for layout caching.</param>
        /// <param name="configProvider">Dynamic configuration provider for tenant-aware caching.</param>
        public LayoutsController(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            ArticleEditLogic articleLogic,
            IEditorSettings options,
            IStorageContext storageContext,
            IViewRenderService viewRenderService,
            IEditorSettings editorSettings,
            IArticleHtmlService htmlService,
            ILogger<LayoutsController> logger,
            ILayoutImportService layoutImportService,
            IMemoryCache memoryCache,
            IDynamicConfigurationProvider configProvider)
            : base(dbContext, userManager, memoryCache, configProvider)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.articleLogic = articleLogic ?? throw new ArgumentNullException(nameof(articleLogic));
            this.storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));
            this.htmlService = htmlService ?? throw new ArgumentNullException(nameof(htmlService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.viewRenderService = viewRenderService ?? throw new ArgumentNullException(nameof(viewRenderService));
            this.layoutImportService = layoutImportService ?? throw new ArgumentNullException(nameof(layoutImportService));

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            blobPublicAbsoluteUrl = options.GetBlobAbsoluteUrl();
        }

        /// <summary>
        /// Imports community templates.
        /// </summary>
        /// <param name="htmlService">HTML service.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="communityPages">Community pages.</param>
        /// <param name="layoutId">Layout ID.</param>
        /// <param name="communityLayoutId">Community layout ID.</param>
        /// <param name="layoutNumber">Layout number to assign to templates.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task ImportCommunityTemplates(
            IArticleHtmlService htmlService,
            ApplicationDbContext dbContext,
            IEnumerable<Template> communityPages,
            Guid layoutId,
            string communityLayoutId,
            int layoutNumber)
        {
            foreach (var page in communityPages)
            {
                var template = new Template
                {
                    CommunityLayoutId = page.CommunityLayoutId,
                    Content = htmlService.EnsureEditableMarkers(page.Content),
                    Description = page.Description,
                    LayoutId = layoutId,
                    LayoutNumber = layoutNumber,
                    Title = page.Title,
                    PageType = page.PageType,
                    Id = Guid.NewGuid()
                };
                dbContext.Templates.Add(template);
            }

            await dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Gets a list of layouts with version initialization if needed.
        /// </summary>
        /// <returns>A list of layouts.</returns>
        public async Task<IActionResult> GetLayouts()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var layouts = await dbContext.Layouts.ToListAsync();

                // Check if version initialization is needed - use transaction for data consistency
                if (layouts.Any(l => !l.Version.HasValue || l.Version == 0))
                {
                    await InitializeLayoutVersions(layouts);
                }

                return Json(layouts.Select(s => new LayoutIndexViewModel
                {
                    Id = s.Id,
                    IsDefault = s.IsDefault,
                    LayoutName = s.LayoutName,
                    Notes = s.Notes,
                    Version = s.Version ?? 0,
                    LastModified = s.LastModified ?? DateTimeOffset.UtcNow,
                    Published = s.Published
                }).OrderByDescending(o => o.Version).ToList());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving layouts");
                return StatusCode(500, "An error occurred while retrieving layouts");
            }
        }

        /// <summary>
        /// Gets a list of layouts.
        /// </summary>
        /// <param name="sortOrder">Sort order either asc or desc (default is asc).</param>
        /// <param name="currentSort">Field to sort on.</param>
        /// <param name="pageNo">Page number to return.</param>
        /// <param name="pageSize">Number of records in each page.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Index(string sortOrder = SortOrderAsc, string currentSort = SortFieldLayoutName, int pageNo = 0, int pageSize = 10)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate pagination parameters
            if (pageNo < 0)
            {
                logger.LogWarning("Invalid pageNo {PageNo} provided, defaulting to 0", pageNo);
                pageNo = 0;
            }

            if (pageSize <= 0 || pageSize > 100)
            {
                logger.LogWarning("Invalid pageSize {PageSize} provided, defaulting to 10", pageSize);
                pageSize = 10;
            }

            try
            {
                ViewData["ShowCreateFirstLayout"] = !await dbContext.Layouts.CosmosAnyAsync();
                ViewData["ShowFirstPageBtn"] = !await dbContext.Articles.CosmosAnyAsync();
                ViewData["sortOrder"] = sortOrder;
                ViewData["currentSort"] = currentSort;
                ViewData["pageNo"] = pageNo;
                ViewData["pageSize"] = pageSize;

                var query = dbContext.Layouts.AsQueryable();

                ViewData["RowCount"] = await query.CountAsync();

                query = ApplySorting(query, sortOrder, currentSort);

                var model = query.Select(s => new LayoutIndexViewModel
                {
                    Id = s.Id,
                    IsDefault = s.IsDefault,
                    LayoutName = s.LayoutName,
                    Notes = s.Notes
                });

                return View(await model.Skip(pageNo * pageSize).Take(pageSize).ToListAsync());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading layouts index");
                return StatusCode(500, "An error occurred while loading layouts");
            }
        }

        /// <summary>
        /// Page returns a list of community layouts.
        /// </summary>
        /// <param name="sortOrder">Sort order either asc or desc (default is asc).</param>
        /// <param name="currentSort">Field to sort on.</param>
        /// <param name="pageNo">Page number to return.</param>
        /// <param name="pageSize">Number of records in each page.</param>
        /// <returns>Returns an <see cref="IActionResult"/>.</returns>
        public async Task<IActionResult> CommunityLayouts(string sortOrder = SortOrderAsc, string currentSort = SortFieldName, int pageNo = 0, int pageSize = 10)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate pagination parameters
            if (pageNo < 0)
            {
                logger.LogWarning("Invalid pageNo {PageNo} provided, defaulting to 0", pageNo);
                pageNo = 0;
            }

            if (pageSize <= 0 || pageSize > 100)
            {
                logger.LogWarning("Invalid pageSize {PageSize} provided, defaulting to 10", pageSize);
                pageSize = 10;
            }

            try
            {
                ViewData["sortOrder"] = sortOrder;
                ViewData["currentSort"] = currentSort;
                ViewData["pageNo"] = pageNo;
                ViewData["pageSize"] = pageSize;

                var catalog = await layoutImportService.GetCommunityCatalogAsync();
                var query = catalog.LayoutCatalog.AsQueryable();

                ViewData["RowCount"] = query.Count();

                query = ApplyCommunitySorting(query, sortOrder, currentSort);

                return View(query.Skip(pageNo * pageSize).Take(pageSize).ToList());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading community layouts");
                return StatusCode(500, "An error occurred while loading community layouts");
            }
        }

        /// <summary>
        /// Create a new layout.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Create()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var layoutCount = await dbContext.Layouts.CountAsync();
                
                // Get next available LayoutNumber
                var maxLayoutNumber = await dbContext.Layouts
                    .Where(l => l.LayoutNumber > 0)
                    .MaxAsync(l => (int?)l.LayoutNumber) ?? 0;
                
                var layout = new Cosmos.Common.Data.Layout
                {
                    IsDefault = false,
                    LayoutName = $"{NewLayoutPrefix} {layoutCount}",
                    Notes = NewLayoutNotes,
                    LayoutNumber = maxLayoutNumber + 1,
                    Version = 1
                };

                dbContext.Layouts.Add(layout);
                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Created new layout {LayoutId} with name '{LayoutName}', LayoutNumber={LayoutNumber}",
                    layout.Id, layout.LayoutName, layout.LayoutNumber);

                return RedirectToAction("EditCode", new { layout.Id });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating new layout");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the layout");
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Deletes a layout that is not the default layout.
        /// </summary>
        /// <param name="id">ID of the layout to delete.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id == Guid.Empty)
            {
                return BadRequest("Invalid layout ID");
            }

            try
            {
                var entity = await dbContext.Layouts.FindAsync(id);

                if (entity == null)
                {
                    return NotFound($"Layout with ID {id} not found");
                }

                if (entity.IsDefault)
                {
                    return BadRequest("Cannot delete the default layout.");
                }

                var pages = await dbContext.Templates.Where(t => t.LayoutId == id).ToListAsync();
                dbContext.Templates.RemoveRange(pages);
                dbContext.Layouts.Remove(entity);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Deleted layout {LayoutId} '{LayoutName}' and {TemplateCount} associated templates",
                    id, entity.LayoutName, pages.Count);

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting layout {LayoutId}", id);
                return StatusCode(500, "An error occurred while deleting the layout");
            }
        }

        /// <summary>
        /// Loads the designer GUI editing the latest version.
        /// </summary>
        /// <returns>View.</returns>
        public async Task<IActionResult> Designer()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                ViewData["IsDesigner"] = true;

                var layout = await GetLayoutForEdit();
                var config = new DesignerConfig(layout, layout.Id.ToString(), layout.LayoutName);

                var assets = await FileManagerController.GetImageAssetArray(storageContext, "/pub", "/pub/articles");
                if (assets != null)
                {
                    config.ImageAssets.AddRange(assets);
                }

                return View(config);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading designer");
                return StatusCode(500, "An error occurred while loading the designer");
            }
        }

        /// <summary>
        /// Gets data to edit.
        /// </summary>
        /// <returns>IActionResult.</returns>
        [HttpGet]
        public async Task<IActionResult> DesignerData()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var layout = await GetLayoutForEdit();
                var htmlContent = BuildDesignerHtml(layout);

                return Json(new project(htmlContent));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving designer data");
                return StatusCode(500, "An error occurred while retrieving designer data");
            }
        }

        /// <summary>
        /// Save designer data.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <param name="title">Template title.</param>
        /// <param name="htmlContent">HTML content.</param>
        /// <param name="cssContent">CSS content.</param>
        /// <returns>IActionResult.</returns>
        [HttpPost]
        public async Task<IActionResult> DesignerData(Guid id, string title, string htmlContent, string cssContent)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid layout ID");
            }

            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                return BadRequest("HTML content is required");
            }

            try
            {
                var layout = await dbContext.Layouts.FirstOrDefaultAsync(f => f.Id == id);

                if (layout == null)
                {
                    return NotFound($"Layout with ID {id} not found");
                }

                htmlContent = DecryptContent(htmlContent);
                cssContent = DecryptContent(cssContent);

                if (!NestedEditableRegionValidation.Validate(htmlContent))
                {
                    return BadRequest("Cannot have nested editable regions.");
                }

                var header = GetTextBetween(htmlContent, HeaderStartMarker, HeaderEndMarker);
                var footer = GetTextBetween(htmlContent, FooterStartMarker, FooterEndMarker);

                var designerUtils = new DesignerUtilities();
                footer = designerUtils.AssembleDesignerOutput(new DesignerDataViewModel
                {
                    HtmlContent = footer,
                    CssContent = cssContent,
                    Title = title
                });

                layout.HtmlHeader = header;
                layout.FooterHtmlContent = footer;
                layout.LastModified = DateTimeOffset.UtcNow;

                await dbContext.SaveChangesAsync();

                logger.LogInformation("Saved designer data for layout {LayoutId}", id);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving designer data for layout {LayoutId}", id);
                return Json(new { success = false, error = "An error occurred while saving" });
            }
        }

        /// <summary>
        /// Edit code for a layout.
        /// </summary>
        /// <param name="id">Optional ID of the layout to view (not edit).</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> EditCode(Guid? id = null)
        {
            if (id.HasValue && id.Value == Guid.Empty)
            {
                return BadRequest("Invalid layout ID");
            }

            try
            {
                var layout = id.HasValue
                    ? await dbContext.Layouts.FirstOrDefaultAsync(f => f.Id == id.Value)
                    : await GetLayoutForEdit();

                if (layout == null)
                {
                    return NotFound(id.HasValue ? $"Layout with ID {id} not found" : "No layout found");
                }

                ViewData["PageTitle"] = layout.LayoutName;
                ViewData["ReadOnly"] = id.HasValue;

                var model = BuildLayoutCodeViewModel(layout);

                return View(model);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading layout code editor for ID {LayoutId}", id);
                return StatusCode(500, "An error occurred while loading the code editor");
            }
        }

        /// <summary>
        ///     Saves the code and html of the layout.
        /// </summary>
        /// <param name="model">Post <see cref="LayoutCodeViewModel">model</see>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<IActionResult> EditCode(LayoutCodeViewModel model)
        {
            if (model == null)
            {
                return BadRequest("Model cannot be null");
            }

            if (model.Id == Guid.Empty)
            {
                return BadRequest("Invalid layout ID");
            }

            try
            {
                DecryptModelProperties(model);

                if (!ModelState.IsValid)
                {
                    ViewData["PageTitle"] = model.EditorTitle;
                    return View(model);
                }

                model.BodyHtmlAttributes = StripBOM(model.BodyHtmlAttributes);

                var layout = await dbContext.Layouts.FindAsync(model.Id);

                if (layout == null)
                {
                    return NotFound($"Layout with ID {model.Id} not found");
                }

                UpdateLayoutFromModel(layout, model);

                await dbContext.SaveChangesAsync();

                logger.LogInformation("Saved code changes for layout {LayoutId}", model.Id);

                return Json(BuildSaveResultModel());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving layout code for ID {LayoutId}", model.Id);
                return StatusCode(500, "An error occurred while saving the layout");
            }
        }

        /// <summary>
        /// Gets a layout to edit it's notes.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> EditNotes()
        {
            try
            {
                var layout = await GetLayoutForEdit();

                return View(new LayoutIndexViewModel
                {
                    Id = layout.Id,
                    IsDefault = layout.IsDefault,
                    LayoutName = layout.LayoutName,
                    Notes = layout.Notes
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading edit notes view");
                return StatusCode(500, "An error occurred while loading the edit notes page");
            }
        }

        /// <summary>
        /// Edit layout notes.
        /// </summary>
        /// <param name="model">Layout post <see cref="LayoutIndexViewModel">model</see>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<IActionResult> EditNotes(LayoutIndexViewModel model)
        {
            if (model == null)
            {
                return BadRequest("Model cannot be null");
            }

            if (model.Id == Guid.Empty)
            {
                return BadRequest("Invalid layout ID");
            }

            try
            {
                model.Notes = DecryptContent(model.Notes);

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var layout = await dbContext.Layouts.FindAsync(model.Id);

                if (layout == null)
                {
                    return NotFound($"Layout with ID {model.Id} not found");
                }

                layout.LayoutName = model.LayoutName;

                var parsedNotes = ParseAndValidateNotes(model.Notes);
                if (!string.IsNullOrEmpty(parsedNotes.ErrorMessage))
                {
                    ModelState.AddModelError("Notes", parsedNotes.ErrorMessage);
                    return View(model);
                }

                layout.Notes = parsedNotes.CleanedText;
                layout.LastModified = DateTimeOffset.UtcNow;

                await dbContext.SaveChangesAsync();

                logger.LogInformation("Updated notes for layout {LayoutId}", model.Id);

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving layout notes for ID {LayoutId}", model?.Id);
                ModelState.AddModelError(string.Empty, "An error occurred while saving notes");
                return View(model);
            }
        }

        /// <summary>
        /// Preview how a layout will look in edit mode.
        /// </summary>
        /// <param name="id">ID of the layout.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> EditPreview(int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (id <= 0)
            {
                return BadRequest("Invalid layout ID");
            }

            try
            {
                var layout = await dbContext.Layouts.FindAsync(id);

                if (layout == null)
                {
                    return NotFound($"Layout with ID {id} not found");
                }

                var model = await articleLogic.GetArticleByUrl(string.Empty);
                model.Layout = new LayoutViewModel(layout);
                model.EditModeOn = true;
                model.ReadWriteMode = true;
                model.PreviewMode = true;

                return View("~/Views/Home/Index.cshtml", model);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading edit preview for layout {LayoutId}", id);
                return StatusCode(500, "An error occurred while loading the edit preview");
            }
        }

        /// <summary>
        /// Exports a layout with a blank page.
        /// </summary>
        /// <param name="id">ID of the layout.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
        public async Task<IActionResult> ExportLayout(Guid? id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (!id.HasValue || id.Value == Guid.Empty)
            {
                return BadRequest("Invalid layout ID");
            }

            try
            {
                var article = await articleLogic.GetArticleByUrl(string.Empty);
                var layout = await dbContext.Layouts.FirstOrDefaultAsync(f => f.Id == id);

                if (layout == null)
                {
                    return NotFound($"Layout with ID {id} not found");
                }

                article.Layout = new LayoutViewModel(layout);

                var htmlUtilities = new HtmlUtilities();

                article.Layout.Head = htmlUtilities.RelativeToAbsoluteUrls(article.Layout.Head, blobPublicAbsoluteUrl, false);
                article.Layout.HtmlHeader = htmlUtilities.RelativeToAbsoluteUrls(article.Layout.HtmlHeader, blobPublicAbsoluteUrl, true);
                article.Layout.FooterHtmlContent = htmlUtilities.RelativeToAbsoluteUrls(article.Layout.FooterHtmlContent, blobPublicAbsoluteUrl, true);
                article.HeadJavaScript = htmlUtilities.RelativeToAbsoluteUrls(article.HeadJavaScript, blobPublicAbsoluteUrl, false);
                article.Content = htmlUtilities.RelativeToAbsoluteUrls(article.Content, blobPublicAbsoluteUrl, false);
                article.FooterJavaScript = htmlUtilities.RelativeToAbsoluteUrls(article.HeadJavaScript, blobPublicAbsoluteUrl, false);

                var html = await viewRenderService.RenderToStringAsync("~/Views/Layouts/ExportLayout.cshtml", article);
                var bytes = Encoding.UTF8.GetBytes(html);

                logger.LogInformation("Exported layout {LayoutId}", id);

                return File(bytes, "application/octet-stream", $"layout-{article.Layout.Id}.html");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error exporting layout {LayoutId}", id);
                return StatusCode(500, "An error occurred while exporting the layout");
            }
        }

        /// <summary>
        ///     Publishes a layout as the default layout.
        /// </summary>
        /// <param name="id">Layout ID.</param>
        /// <returns>Success or failure.</returns>
        public async Task<IActionResult> Publish(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Invalid layout ID");
            }

            try
            {
                var layout = await dbContext.Layouts.FirstOrDefaultAsync(f => f.Id == id);

                if (layout == null)
                {
                    return NotFound($"Layout with ID {id} not found");
                }

                if (layout.IsDefault)
                {
                    return Ok();
                }

                layout.IsDefault = true;
                layout.Published = DateTimeOffset.UtcNow;

                var others = await dbContext.Layouts.Where(w => w.Id != id && w.IsDefault == true).ToListAsync();
                foreach (var item in others)
                {
                    item.IsDefault = false;
                    item.Published = null;
                }

                await dbContext.SaveChangesAsync();

                logger.LogInformation("Published layout {LayoutId} as default", id);

                return RedirectToAction("Publish", "Editor");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing layout {LayoutId}", id);
                return StatusCode(500, "An error occurred while publishing the layout");
            }
        }

        /// <summary>
        /// Gets a community layout.
        /// </summary>
        /// <param name="id">Layout ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Import(string id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("Layout ID is required");
            }

            try
            {
                if (await dbContext.Layouts.Where(c => c.CommunityLayoutId == id).CosmosAnyAsync())
                {
                    return BadRequest("Design already loaded.");
                }

                var layout = await layoutImportService.GetCommunityLayoutAsync(id, false);
                var communityPages = await layoutImportService.GetCommunityTemplatePagesAsync(id);

                // Get next available LayoutNumber
                var maxLayoutNumber = await dbContext.Layouts
                    .Where(l => l.LayoutNumber > 0)
                    .MaxAsync(l => (int?)l.LayoutNumber) ?? 0;

                layout.LayoutNumber = maxLayoutNumber + 1;

                if ((await dbContext.Layouts.FirstOrDefaultAsync(a => a.IsDefault)) == null)
                {
                    layout.Version = 1;
                    layout.IsDefault = true;
                }
                else
                {
                    layout.Version = (await dbContext.Layouts.CountAsync()) + 1;
                    layout.IsDefault = false;
                }

                dbContext.Layouts.Add(layout);
                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Imported community layout {CommunityLayoutId} as layout {LayoutId} with LayoutNumber={LayoutNumber}",
                    id, layout.Id, layout.LayoutNumber);

                if (communityPages != null && communityPages.Any())
                {
                    await ImportCommunityTemplates(htmlService, dbContext, communityPages, layout.Id, id, layout.LayoutNumber);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error importing community layout {CommunityLayoutId}", id);
                ModelState.AddModelError("Id", ex.Message);
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        ///  Promotes a layout to a new version.
        /// </summary>
        /// <param name="id">ID of the layout to promote.</param>
        /// <returns>New version number.</returns>
        public async Task<IActionResult> Promote(Guid id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (id == Guid.Empty)
            {
                return BadRequest("Invalid layout ID");
            }

            try
            {
                var layout = await dbContext.Layouts.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);

                if (layout == null)
                {
                    return NotFound($"Layout with ID {id} not found");
                }

                var newLayout = await NewVersion(layout);

                logger.LogInformation("Promoted layout {OldLayoutId} to new version {NewLayoutId} with version number {Version}",
                    id, newLayout.Id, newLayout.Version);

                return Json(newLayout.Version);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error promoting layout {LayoutId}", id);
                return StatusCode(500, "An error occurred while promoting the layout");
            }
        }

        /// <summary>
        /// Initializes layout versions within a transaction.
        /// </summary>
        /// <param name="layouts">List of layouts to initialize.</param>
        private async Task InitializeLayoutVersions(List<Layout> layouts)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                // Reload within transaction to prevent race conditions
                layouts = await dbContext.Layouts.ToListAsync();

                // Double-check condition after acquiring transaction lock
                if (layouts.Any(l => !l.Version.HasValue || l.Version == 0))
                {
                    var pub = layouts.FirstOrDefault(f => f.IsDefault);

                    if (pub != null)
                    {
                        pub.Published = DateTimeOffset.UtcNow;
                        pub.LastModified = DateTimeOffset.UtcNow;
                        pub.Version = layouts.Count;

                        var count = 1;
                        foreach (var layout in layouts.Where(w => w.Id != pub.Id))
                        {
                            layout.Published = null;
                            layout.LastModified = DateTimeOffset.UtcNow;
                            layout.Version = count;
                            count++;
                        }

                        await dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        logger.LogInformation("Initialized layout versions for {Count} layouts", layouts.Count);
                    }
                    else
                    {
                        logger.LogWarning("No default layout found during version initialization");
                        await transaction.RollbackAsync();
                    }
                }
                else
                {
                    await transaction.RollbackAsync();
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "Error initializing layout versions");
                throw;
            }
        }

        /// <summary>
        /// Applies sorting to a layout query.
        /// </summary>
        /// <param name="query">Query to sort.</param>
        /// <param name="sortOrder">Sort order (asc/desc).</param>
        /// <param name="currentSort">Field to sort by.</param>
        /// <returns>Sorted query.</returns>
        private static IQueryable<Layout> ApplySorting(IQueryable<Layout> query, string sortOrder, string currentSort)
        {
            if (string.IsNullOrWhiteSpace(currentSort))
            {
                return query;
            }

            var isDescending = string.Equals(sortOrder, SortOrderDesc, StringComparison.OrdinalIgnoreCase);

            return currentSort switch
            {
                SortFieldLayoutName => isDescending
                    ? query.OrderByDescending(o => o.LayoutName)
                    : query.OrderBy(o => o.LayoutName),
                _ => query
            };
        }

        /// <summary>
        /// Applies sorting to a community layout query.
        /// </summary>
        private static IQueryable<LayoutCatalogItem> ApplyCommunitySorting(
            IQueryable<LayoutCatalogItem> query,
            string sortOrder,
            string currentSort)
        {
            if (string.IsNullOrWhiteSpace(currentSort))
            {
                return query;
            }

            var isDescending = string.Equals(sortOrder, SortOrderDesc, StringComparison.OrdinalIgnoreCase);

            return currentSort switch
            {
                SortFieldArticleNumber => isDescending
                    ? query.OrderByDescending(o => o.License)
                    : query.OrderBy(o => o.License),
                SortFieldName => isDescending
                    ? query.OrderByDescending(o => o.Name)
                    : query.OrderBy(o => o.Name),
                SortFieldDescription => isDescending
                    ? query.OrderByDescending(o => o.Description)
                    : query.OrderBy(o => o.Description),
                _ => query
            };
        }

        /// <summary>
        /// Builds HTML content for the designer.
        /// </summary>
        private static string BuildDesignerHtml(Layout layout)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<body>");
            builder.AppendLine(HeaderStartMarker);

            if (string.IsNullOrEmpty(layout.HtmlHeader))
            {
                builder.AppendLine("<header style='height:50px;display:flex;justify-content:center;align-items:center;'>HEADER GOES HERE</header>");
            }
            else
            {
                builder.AppendLine(layout.HtmlHeader);
            }

            builder.AppendLine(HeaderEndMarker);
            builder.AppendLine("<div data-gjs-type='grapesjs-not-editable' draggable='false' style='height:50vh;display:flex;justify-content:center;align-items:center;'>");
            builder.AppendLine("<div style='text-align:center'>PAGE CONTENT GOES IN THIS BLOCK<br/>Cannot edit with layout designer.</div>");
            builder.AppendLine("</div>");
            builder.AppendLine(FooterStartMarker);

            if (string.IsNullOrEmpty(layout.FooterHtmlContent))
            {
                builder.AppendLine("<footer style='height:50px;display:flex;justify-content:center;align-items:center;'>FOOTER GOES HERE</footer>");
            }
            else
            {
                builder.AppendLine(layout.FooterHtmlContent);
            }

            builder.AppendLine(FooterEndMarker);
            builder.AppendLine("</body>");

            return builder.ToString();
        }

        /// <summary>
        /// Builds a layout code view model from a layout entity.
        /// </summary>
        private static LayoutCodeViewModel BuildLayoutCodeViewModel(Layout layout)
        {
            return new LayoutCodeViewModel
            {
                Id = layout.Id,
                EditorTitle = layout.LayoutName,
                EditorFields = new List<EditorField>
                {
                    new ()
                    {
                        FieldId = "Head",
                        FieldName = "Head",
                        EditorMode = EditorMode.Html,
                        ToolTip = "Layout content to appear in the HEAD of every page."
                    },
                    new ()
                    {
                        FieldId = "HtmlHeader",
                        FieldName = "Header Content",
                        EditorMode = EditorMode.Html,
                        ToolTip = "Layout body header content to appear on every page."
                    },
                    new ()
                    {
                        FieldId = "FooterHtmlContent",
                        FieldName = "Footer Content",
                        EditorMode = EditorMode.Html,
                        ToolTip = "Layout footer content to appear at the bottom of the body on every page."
                    }
                },
                CustomButtons = new List<string> { "Preview", "Layouts" },
                Head = layout.Head,
                HtmlHeader = layout.HtmlHeader,
                BodyHtmlAttributes = layout.BodyHtmlAttributes,
                FooterHtmlContent = layout.FooterHtmlContent,
                EditingField = "Head"
            };
        }

        /// <summary>
        /// Decrypts model properties.
        /// </summary>
        private void DecryptModelProperties(LayoutCodeViewModel model)
        {
            model.Head = DecryptContent(StripBOM(model.Head));
            model.HtmlHeader = DecryptContent(StripBOM(model.HtmlHeader));
            model.FooterHtmlContent = DecryptContent(StripBOM(model.FooterHtmlContent));
        }

        /// <summary>
        /// Strips Byte Order Marks.
        /// </summary>
        /// <param name="data">HTML data.</param>
        /// <returns>Un-BOMed html.</returns>
        private string StripBOM(string data)
        {
            // See: https://danielwertheim.se/utf-8-bom-adventures-in-c/
            if (string.IsNullOrEmpty(data) || string.IsNullOrWhiteSpace(data))
            {
                return data;
            }

            // Get rid of Zero Length strings
            var rows = data.Split("\r\n");
            var builder = new StringBuilder();
            foreach (var row in rows)
            {
                if (!row.Trim().Equals(string.Empty))
                {
                    builder.AppendLine(row);
                }
            }

            data = builder.ToString();

            // Search for and eliminate BOM
            var filtered = new string(data.ToArray().Where(c => c != '\uFEFF' && c != '\u00a0').ToArray());

            using var memStream = new MemoryStream();
            using var writer = new StreamWriter(memStream, new UTF8Encoding(false));
            writer.Write(filtered);
            writer.Flush();

            var clean = Encoding.UTF8.GetString(memStream.ToArray());

            return clean;
        }

        /// <summary>
        /// Decrypts content safely.
        /// </summary>
        private static string DecryptContent(string content)
        {
            return string.IsNullOrEmpty(content) ? string.Empty : CryptoJsDecryption.Decrypt(content);
        }

        /// <summary>
        /// Updates layout entity from view model.
        /// </summary>
        private void UpdateLayoutFromModel(Layout layout, LayoutCodeViewModel model)
        {
            layout.FooterHtmlContent = BaseValidateHtml("FooterHtmlContent", model.FooterHtmlContent);
            layout.Head = BaseValidateHtml("Head", model.Head);
            layout.HtmlHeader = BaseValidateHtml("HtmlHeader", model.HtmlHeader);
            layout.BodyHtmlAttributes = model.BodyHtmlAttributes;
            layout.LastModified = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Builds save result JSON model.
        /// </summary>
        private SaveCodeResultJsonModel BuildSaveResultModel()
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
        /// Parses and validates HTML notes.
        /// </summary>
        private (string CleanedText, string ErrorMessage) ParseAndValidateNotes(string notes)
        {
            var contentHtmlDocument = new HtmlDocument();
            contentHtmlDocument.LoadHtml(HttpUtility.HtmlDecode(notes));

            if (contentHtmlDocument.ParseErrors.Any())
            {
                var errors = string.Join("; ", contentHtmlDocument.ParseErrors.Select(e => e.Reason));
                return (string.Empty, errors);
            }

            var cleanedText = contentHtmlDocument.ParsedText
                .Replace(HtmlRemovalDiv, string.Empty, StringComparison.Ordinal)
                .Trim();

            return (cleanedText, string.Empty);
        }

        /// <summary>
        /// Gets the layout for editing - creates a new version if the current one is default.
        /// </summary>
        /// <returns>Layout for editing.</returns>
        private async Task<Cosmos.Common.Data.Layout> GetLayoutForEdit()
        {
            var layout = await dbContext.Layouts.OrderByDescending(o => o.Version).FirstOrDefaultAsync();

            if (layout == null)
            {
                layout = new Layout
                {
                    Id = Guid.NewGuid(),
                    IsDefault = true,
                    LayoutName = DefaultLayoutName,
                    Notes = DefaultLayoutNotes,
                    LayoutNumber = 1,
                    Version = 1,
                    LastModified = DateTimeOffset.UtcNow
                };
                dbContext.Layouts.Add(layout);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Created default layout {LayoutId} with LayoutNumber=1", layout.Id);

                return layout;
            }

            if (layout.IsDefault)
            {
                return await NewVersion(layout);
            }

            return layout;
        }

        /// <summary>
        ///  Creates a new layout from an existing layout.
        /// </summary>
        /// <param name="layout">Existing layout.</param>
        /// <returns>New layout with an incremented version number.</returns>
        private async Task<Cosmos.Common.Data.Layout> NewVersion(Cosmos.Common.Data.Layout layout)
        {
            var newLayout = new Layout
            {
                CommunityLayoutId = layout.CommunityLayoutId,
                LayoutName = layout.LayoutName,
                Notes = layout.Notes,
                Head = layout.Head,
                HtmlHeader = layout.HtmlHeader,
                BodyHtmlAttributes = layout.BodyHtmlAttributes,
                FooterHtmlContent = layout.FooterHtmlContent,
                IsDefault = false,
                LayoutNumber = layout.LayoutNumber,
                Version = (await dbContext.Layouts.CountAsync()) + 1,
                LastModified = DateTimeOffset.UtcNow,
                Published = null,
                Id = Guid.NewGuid()
            };

            dbContext.Layouts.Add(newLayout);
            await dbContext.SaveChangesAsync();

            logger.LogInformation(
                "Created new version of layout family LayoutNumber={LayoutNumber}, Version={Version}",
                newLayout.LayoutNumber, newLayout.Version);

            return newLayout;
        }

        /// <summary>
        /// Extracts text between two markers in a string.
        /// </summary>
        /// <param name="input">Input string.</param>
        /// <param name="start">Start marker.</param>
        /// <param name="end">End marker.</param>
        /// <returns>Text between markers or empty string.</returns>
        private static string GetTextBetween(string input, string start, string end)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end))
            {
                return string.Empty;
            }

            int startIndex = input.IndexOf(start, StringComparison.Ordinal);
            if (startIndex == -1)
            {
                return string.Empty;
            }

            startIndex += start.Length;
            int endIndex = input.IndexOf(end, startIndex, StringComparison.Ordinal);
            if (endIndex == -1)
            {
                return string.Empty;
            }

            return input.Substring(startIndex, endIndex - startIndex);
        }
    }
}
