// <copyright file="TemplatesController.cs" company="Moonrise Software, LLC">
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
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services;
    using Cosmos.Common.Services.Caching;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Sky.Cms.Models;
    using Sky.Editor.Data;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Templates.Create;
    using Sky.Editor.Features.Templates.Delete;
    using Sky.Editor.Features.Templates.Get;
    using Sky.Editor.Features.Templates.GetEditable;
    using Sky.Editor.Features.Templates.Publishing;
    using Sky.Editor.Features.Templates.Save;
    using Sky.Editor.Features.Templates.UpdateMetadata;
    using Sky.Editor.Models;
    using Sky.Editor.Models.GrapesJs;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Templates;

    /// <summary>
    /// Templates controller.
    /// </summary>
    [Authorize(Roles = "Administrators, Editors")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class TemplatesController : BaseController
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IEditorSettings options;
        private readonly IStorageContext storageContext;
        private readonly IArticleHtmlService htmlService;
        private readonly ITemplateService templateServices;
        private readonly IMediator mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplatesController"/> class.
        /// Constructor.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="storageContext">Storage context service.</param>
        /// <param name="options">Cosmos Options.</param>
        /// <param name="htmlService">HTML service.</param>
        /// <param name="templateServices">Template services.</param>
        /// <param name="mediator">Mediator instance.</param>
        /// <param name="memoryCache">Memory cache for layout caching.</param>
        /// <param name="configProvider">Dynamic configuration provider for tenant-aware caching.</param>
        public TemplatesController(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            IStorageContext storageContext,
            IEditorSettings options,
            IArticleHtmlService htmlService,
            ITemplateService templateServices,
            IMediator mediator,
            ICacheService<Layout> memoryCache,
            IDynamicConfigurationProvider configProvider)
            : base(dbContext, userManager, mediator, memoryCache, configProvider)
        {
            this.dbContext = dbContext;
            this.options = options;
            this.storageContext = storageContext;
            this.htmlService = htmlService;
            this.templateServices = templateServices;
            this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        /// <summary>
        /// Index view model.
        /// </summary>
        /// <param name="sortOrder">Sort order.</param>
        /// <param name="currentSort">Current sort field.</param>
        /// <param name="pageNo">Page number.</param>
        /// <param name="pageSize">Page size.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Index(string sortOrder = "asc", string currentSort = "Title", int pageNo = 0, int pageSize = 10)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var defaultLayout = await GetCurrentLayoutAsync();

            ViewData["Layouts"] = await BaseGetLayoutListItems();

            PopulateSortPagingViewData(sortOrder, currentSort, pageNo, pageSize);

            await templateServices.EnsureDefaultTemplatesExistAsync();

            var data = (await GetTemplatesForCurrentLayoutAsync())
                .OrderBy(t => t.Title)
                .Select(s => new TemplateIndexViewModel
                {
                    Id = s.Id,
                    LayoutName = defaultLayout.LayoutName,
                    Description = s.Description,
                    Title = s.Title,
                    UsesHtmlEditor = s.Content.ToLower().Contains(" contenteditable=") || s.Content.ToLower().Contains(" data-ccms-ceid=")
                }).ToList();

            ViewData["RowCount"] = data.Count();

            var query = data.AsQueryable();

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

            return View(query.Skip(pageNo * pageSize).Take(pageSize).ToList());
        }

        /// <summary>
        /// Gets a list of articles or pages that use a particular page template.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <param name="sortOrder">Sort order or direction.</param>
        /// <param name="currentSort">Field being sorted on.</param>
        /// <param name="pageNo">Page number to retrieve.</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <param name="filter">Search filter.</param>
        /// <returns>Returns an <see cref="IActionResult"/>.</returns>
        public async Task<IActionResult> Pages(Guid id, string sortOrder = "asc", string currentSort = "Title", int pageNo = 0, int pageSize = 10, string filter = "")
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var template = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == id);

            if (template == null)
            {
                return NotFound();
            }

            ViewData["templateId"] = id;
            PopulateSortPagingViewData(sortOrder, currentSort, pageNo, pageSize);
            ViewData["template"] = template;
            ViewData["canApplyChanges"] = template.Content.ToLower().Contains(" data-ccms-ceid=");

            if (!string.IsNullOrEmpty(filter))
            {
                filter = filter.TrimStart('/');
            }

            ViewData["Filter"] = filter;
            ViewData["PublisherUrl"] = options.PublisherUrl;
            ViewData["ShowNotFoundBtn"] = !await dbContext.ArticleCatalog.Where(w => w.UrlPath == "not_found").CosmosAnyAsync();

            var pages = await dbContext.ArticleCatalog.Where(w => w.TemplateId == id).Select(s => new
            {
                s.ArticleNumber,
                s.Title,
                s.UrlPath,
                s.Published,
                s.Updated
            }).AsNoTracking().ToListAsync();

            var data = pages.GroupBy(articles => articles.ArticleNumber)
                .Select(g => new
                {
                    ArticleNumber = g.Key,
                    Title = g.OrderByDescending(o => o.Updated).First().Title,
                    UrlPath = g.OrderByDescending(o => o.Updated).First().UrlPath,
                    Published = g.OrderByDescending(o => o.Published).First().Published,
                    Updated = g.OrderByDescending(o => o.Updated).First().Updated
                }).ToList();

            var query = data.AsQueryable();

            ViewData["RowCount"] = query.Count();

            if (!string.IsNullOrEmpty(filter))
            {
                var f = filter.ToLower();
                query = query.Where(w => w.Title.ToLower().Contains(f));
            }

            if (sortOrder == "desc")
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "ArticleNumber":
                            query = query.OrderByDescending(o => o.ArticleNumber);
                            break;
                        case "Title":
                            query = query.OrderByDescending(o => o.Title);
                            break;
                        case "LastPublished":
                            query = query.OrderByDescending(o => o.Published);
                            break;
                        case "UrlPath":
                            query = query.OrderByDescending(o => o.UrlPath);
                            break;
                        case "Updated":
                            query = query.OrderByDescending(o => o.Updated);
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
                        case "ArticleNumber":
                            query = query.OrderBy(o => o.ArticleNumber);
                            break;
                        case "Title":
                            query = query.OrderBy(o => o.Title);
                            break;
                        case "LastPublished":
                            query = query.OrderBy(o => o.Published);
                            break;
                        case "UrlPath":
                            query = query.OrderBy(o => o.UrlPath);
                            break;
                        case "Updated":
                            query = query.OrderBy(o => o.Updated);
                            break;
                    }
                }
                else
                {
                    // Default sort order
                    query = query.OrderBy(o => o.Title);
                }
            }

            var users = await dbContext.Users.Select(s => new { s.Id, s.Email }).ToListAsync();
            var roles = await dbContext.Roles.Select(s => new { s.Id, s.Name }).ToListAsync();

            var d = query.Skip(pageNo * pageSize).Take(pageSize).AsNoTracking().ToList();

            var model = new List<ArticleListItem>();

            foreach (var datum in d)
            {
                var item = new ArticleListItem()
                {
                    ArticleNumber = datum.ArticleNumber,
                    IsDefault = datum.UrlPath.Equals("root", StringComparison.CurrentCultureIgnoreCase),
                    UrlPath = datum.UrlPath,
                    LastPublished = datum.Published,
                    Updated = datum.Updated,
                    Title = datum.Title
                };

                model.Add(item);
            }

            return View(model);
        }

        /// <summary>
        /// Create a template method.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Create()
        {
            var defaultLayout = await GetCurrentLayoutAsync();

            // Create a new template entity with initial content.
            // We use an explicit Guid.NewGuid() here for clarity - this will be the template's primary key.
            var entity = new Template
            {
                Id = Guid.NewGuid(),  // Explicit ID assignment ensures we have it before any DB operations
                Title = "New Template " + await dbContext.Templates.CountAsync(),
                Description = "<p>New template, please add descriptive and helpful information here.</p>",
                Content = "<p>" + LoremIpsum.SubSection1 + "</p>",
                LayoutId = defaultLayout?.Id,
                LayoutNumber = defaultLayout?.LayoutNumber ?? 0,
                CommunityLayoutId = defaultLayout?.CommunityLayoutId
            };

            // NOTE: Cosmos DB Limitation
            // Template and PageDesignVersion use different partition keys (Template.Id vs PageDesignVersion.Id)
            // Cosmos DB does NOT support cross-partition transactions.
            // For relational databases (SQL Server, MySQL, SQLite), this transaction provides full ACID guarantees.
            // For Cosmos DB, atomicity is NOT guaranteed - if version creation fails, Template remains in DB.
            // Consider either:
            // 1. Changing Cosmos partition keys to a common value (e.g., TenantId)
            // 2. Removing transactions and implementing cleanup/idempotency
            // 3. Detecting database type and using conditional logic
            if (!dbContext.Database.IsCosmos())
            {
                // Relational databases: Use transaction
                using (var transaction = await dbContext.Database.BeginTransactionAsync())
                {
                    try
                    {
                        dbContext.Templates.Add(entity);
                        await dbContext.SaveChangesAsync();

                        var createVersionCommand = new CreatePageDesignVersionCommand
                        {
                            TemplateId = entity.Id,  // Now valid because Template was saved above
                            Title = entity.Title,
                            Description = entity.Description,
                            Content = entity.Content,
                            PageType = "template",
                            LayoutId = entity.LayoutId,
                            CommunityLayoutId = entity.CommunityLayoutId
                        };

                        var versionResult = await mediator.SendAsync(createVersionCommand);

                        // Step 3: Validate that version creation succeeded
                        // If the handler returns failure, we rollback the transaction
                        // This ensures we never have a Template without at least one PageDesignVersion
                        if (!versionResult.IsSuccess)
                        {
                            // Rollback both the Template creation and any partial version creation
                            await transaction.RollbackAsync();

                            // Return user-friendly error message
                            ModelState.AddModelError(
                                string.Empty,
                                $"Failed to create template version: {versionResult.ErrorMessage}");
                            return BadRequest(ModelState);
                        }

                        // Step 4: Both operations succeeded, commit the transaction
                        // This makes both the Template and PageDesignVersion permanent in the database
                        await transaction.CommitAsync();

                        // Success: Redirect to EditCode page to allow user to edit the template
                        return RedirectToAction("EditCode", "Templates", new { entity.Id });
                    }
                    catch (Exception ex)
                    {
                        // If any unexpected exception occurs, rollback the transaction
                        // This could be a database error, timeout, or any other issue
                        await transaction.RollbackAsync();

                        // Log the error and return to user
                        ModelState.AddModelError(
                            string.Empty,
                            $"Error creating template: {ex.Message}");
                        return BadRequest(ModelState);
                    }
                }
            }
            else
            {
                // Cosmos DB: No cross-partition transaction support
                try
                {
                    dbContext.Templates.Add(entity);
                    await dbContext.SaveChangesAsync();

                    var createVersionCommand = new CreatePageDesignVersionCommand
                    {
                        TemplateId = entity.Id,  // Now valid because Template was saved above
                        Title = entity.Title,
                        Description = entity.Description,
                        Content = entity.Content,
                        PageType = "template",
                        LayoutId = entity.LayoutId,
                        CommunityLayoutId = entity.CommunityLayoutId
                    };

                    var versionResult = await mediator.SendAsync(createVersionCommand);

                    if (!versionResult.IsSuccess)
                    {
                        // ⚠️ WARNING: Template exists but version creation failed
                        // This is a known limitation of Cosmos DB's current partition key design
                        ModelState.AddModelError(string.Empty, $"Failed to create template version: {versionResult.ErrorMessage}");
                        return BadRequest(ModelState);
                    }

                    return RedirectToAction("EditCode", "Templates", new { entity.Id });
                }
                catch (Exception ex)
                {
                    // ⚠️ WARNING: Partial data may exist in Cosmos DB
                    ModelState.AddModelError(string.Empty, $"Error creating template: {ex.Message}");
                    return BadRequest(ModelState);
                }
            }
        }

        /// <summary>
        /// Deletes a template and its associated page design versions.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Delete(Guid id)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            if (id == Guid.Empty)
            {
                return BadRequest("Invalid template ID");
            }

            var userId = Guid.Parse(await GetUserId());
            var command = new DeleteTemplateCommand
            {
                TemplateId = id,
                UserId = userId
            };

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction("Index");
            }

            TempData["Success"] = "Template deleted successfully";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Edit template code.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> EditCode(Guid id)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var editableCommand = new GetEditablePageDesignVersionCommand { TemplateId = id };
            var editableResult = await mediator.SendAsync(editableCommand);

            if (!editableResult.IsSuccess || editableResult.Data?.EditableVersion == null)
            {
                return NotFound();
            }

            var editableVersion = editableResult.Data.EditableVersion;

            var model = new TemplateCodeEditorViewModel
            {
                Id = id,
                EditorTitle = "Template Editor",
                EditorFields = GetTemplateCodeEditorFields(),
                CustomButtons = new List<string>
                {
                    "Preview"
                },
                EditingField = "Content",
                Content = htmlService.EnsureEditableMarkers(editableVersion.Content),
                Version = editableVersion.Version,
                Title = editableVersion.Title
            };
            return View(model);
        }

        /// <summary>
        /// Loads the designer GUI.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>View.</returns>
        public async Task<IActionResult> Designer(Guid id)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            // Loads GrapeJS.
            ViewData["IsDesigner"] = true;

            var editableCommand = new GetEditablePageDesignVersionCommand { TemplateId = id };
            var editableResult = await mediator.SendAsync(editableCommand);

            if (!editableResult.IsSuccess || editableResult.Data?.EditableVersion == null)
            {
                return NotFound();
            }

            var editableVersion = editableResult.Data.EditableVersion;

            var defaultLayout = await GetCurrentLayoutAsync();
            var config = new DesignerConfig(defaultLayout, id.ToString(), editableVersion.Title);

            var assets = await FileManagerController.GetImageAssetArray(storageContext, "/pub", "/pub/articles");

            if (assets != null)
            {
                config.ImageAssets.AddRange(assets);
            }

            var htmlContent = htmlService.EnsureEditableMarkers(editableVersion.Content);
            return View(config);
        }

        /// <summary>
        /// Gets designer for GrapeJS.
        /// </summary>
        /// <param name="id">Article number.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet]
        public async Task<IActionResult> GetDesignerData(Guid id)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            // Loads GrapeJS.
            ViewData["IsDesigner"] = true;

            var editableCommand = new GetEditablePageDesignVersionCommand { TemplateId = id };
            var editableResult = await mediator.SendAsync(editableCommand);

            if (!editableResult.IsSuccess || editableResult.Data?.EditableVersion == null)
            {
                return NotFound();
            }

            var editableVersion = editableResult.Data.EditableVersion;

            var defaultLayout = await GetCurrentLayoutAsync();
            var config = new DesignerConfig(defaultLayout, id.ToString(), editableVersion.Title);
            var assets = await FileManagerController.GetImageAssetArray(storageContext, "/pub", "/pub/articles");
            if (assets != null)
            {
                config.ImageAssets.AddRange(assets);
            }

            var htmlContent = htmlService.EnsureEditableMarkers(editableVersion.Content);

            return Json(new Project(htmlContent));
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
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            if (id == Guid.Empty)
            {
                return BadRequest("Invalid template ID");
            }

            var decryptedHtml = string.IsNullOrEmpty(htmlContent)
                ? string.Empty
                : CryptoJsDecryption.Decrypt(htmlContent);
            var decryptedCss = string.IsNullOrEmpty(cssContent)
                ? string.Empty
                : CryptoJsDecryption.Decrypt(cssContent);

            if (!NestedEditableRegionValidation.Validate(decryptedHtml))
            {
                return BadRequest("Cannot have nested editable regions.");
            }

            var assembledContent = new DesignerUtilities().AssembleDesignerOutput(new DesignerDataViewModel
            {
                Id = id,
                Title = title,
                HtmlContent = decryptedHtml,
                CssContent = decryptedCss
            });

            var editableResult = await mediator.SendAsync(new GetEditablePageDesignVersionCommand { TemplateId = id });
            if (!editableResult.IsSuccess || editableResult.Data?.EditableVersion == null)
            {
                return BadRequest(new { error = editableResult.ErrorMessage ?? "Template version not found." });
            }

            var editableVersion = editableResult.Data.EditableVersion;
            var finalTitle = string.IsNullOrWhiteSpace(title) ? editableVersion.Title : title;

            var saveCommand = new SavePageDesignVersionCommand
            {
                Id = editableVersion.Id,
                Title = finalTitle,
                Description = editableVersion.Description,
                Content = assembledContent,
                PageType = editableVersion.PageType,
                LayoutId = editableVersion.LayoutId,
                CommunityLayoutId = editableVersion.CommunityLayoutId
            };

            var result = await mediator.SendAsync(saveCommand);

            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.ErrorMessage ?? "Failed to save template." });
            }

            return Json(new { success = true });
        }

        /// <summary>
        /// Updates a page using the latest template version.
        /// Creates a new draft version with the updated template applied.
        /// </summary>
        /// <param name="id">Article number.</param>
        /// <param name="templateId">Template ID.</param>
        /// <returns>Returns a redirect to the live editor.</returns>
        public async Task<IActionResult> UpdatePage(int id, Guid templateId)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            // Use GetTemplateQuery to retrieve the template
            var query = new GetTemplateQuery { TemplateId = templateId };
            var queryResult = await mediator.QueryAsync(query);

            if (!queryResult.IsSuccess || queryResult.Data?.Template == null)
            {
                return NotFound($"Template with ID '{templateId}' was not found.");
            }

            var template = queryResult.Data.Template;

            // Apply template using the service layer - creates a new draft version
            var result = await templateServices.ApplyTemplateToArticleAsync(id, templateId);

            if (!result.Success)
            {
                TempData["Error"] = $"Failed to apply template: {result.ErrorMessage}";
                return RedirectToAction("Pages", new { id = templateId });
            }

            // Show warnings if any content regions were lost
            if (result.Warnings.Count > 0)
            {
                TempData["Warning"] = $"Template applied with warnings: {string.Join(", ", result.Warnings)}";
            }
            else
            {
                TempData["Success"] = $"Template applied successfully. Draft version {result.NewVersionNumber} created.";
            }

            // Redirect to editor to review the new DRAFT version
            return RedirectToAction("Edit", "Editor", new { id = id });
        }

        /// <summary>
        /// Preview the impact of applying this template to articles.
        /// Shows which articles will be affected and any merge warnings.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> PreviewImpact(Guid id)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            try
            {
                var preview = await templateServices.PreviewTemplateApplicationAsync(id);

                ViewData["TemplateId"] = id;
                ViewData["TemplateName"] = preview.TemplateName;

                return View(preview);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Preview the impact of applying this template (JSON endpoint for AJAX).
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>JSON preview data.</returns>
        [HttpGet]
        public async Task<IActionResult> PreviewImpactJson(Guid id)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            try
            {
                var preview = await templateServices.PreviewTemplateApplicationAsync(id);
                return Json(preview);
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Applies the template to all pages that use it, creating draft versions.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Publish(Guid id)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            var editableResult = await mediator.SendAsync(new GetEditablePageDesignVersionCommand { TemplateId = id });
            if (!editableResult.IsSuccess || editableResult.Data?.EditableVersion == null)
            {
                TempData["Error"] = editableResult.ErrorMessage ?? "Unable to resolve editable template version.";
                return RedirectToAction("Pages", routeValues: new { id });
            }

            var publishVersionResult = await mediator.SendAsync(new PublishPageDesignVersionCommand
            {
                Id = editableResult.Data.EditableVersion.Id,
                UserId = Guid.Parse(await GetUserId())
            });

            if (!publishVersionResult.IsSuccess)
            {
                TempData["Error"] = publishVersionResult.ErrorMessage ?? "Failed to publish template version.";
                return RedirectToAction("Pages", routeValues: new { id });
            }

            // Template publishing already applies template to all articles via PublishPageDesignVersionCommand
            TempData["Success"] = "Template published successfully.";
            return RedirectToAction("Pages", routeValues: new { id });
        }

        /// <summary>
        /// Publishes draft versions of selected articles.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <param name="articleNumbers">List of article numbers to publish (null = all).</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<IActionResult> PublishDrafts(Guid id, List<int>? articleNumbers = null)
        {
            var invalidModelState = GetInvalidModelStateResult();
            if (invalidModelState != null)
            {
                return invalidModelState;
            }

            try
            {
                var result = await templateServices.PublishTemplateChangesAsync(id, articleNumbers);

                if (result.PublishedCount > 0)
                {
                    var message = articleNumbers == null || articleNumbers.Count == 0
                        ? $"Published {result.PublishedCount} articles."
                        : $"Published {result.PublishedCount} of {articleNumbers.Count} selected articles.";

                    if (result.SkippedCount > 0)
                    {
                        message += $" {result.SkippedCount} skipped (no draft version found).";
                    }

                    TempData["Success"] = message;
                }

                if (result.FailureCount > 0)
                {
                    TempData["Warning"] = $"{result.FailureCount} articles failed to publish. Check logs for details.";
                }

                if (result.PublishedCount == 0 && result.SkippedCount > 0)
                {
                    TempData["Info"] = $"No articles were published. {result.SkippedCount} articles have no draft versions to publish.";
                }

                if (result.PublishedCount == 0 && result.SkippedCount == 0 && result.FailureCount == 0)
                {
                    TempData["Error"] = "No template or articles found to publish.";
                }

                return RedirectToAction("Pages", new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error publishing drafts: {ex.Message}";
                return RedirectToAction("Pages", new { id });
            }
        }

        /// <summary>
        /// Unified save endpoint for template editors.
        /// </summary>
        /// <param name="model">Unified editor post model from JSON body.</param>
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
                return Json(new
                {
                    ServerSideSuccess = false,
                    Errors = BuildModelStateErrors()
                });
            }

            // Validate CryptoContextToken if provided
            if (!string.IsNullOrEmpty(model.CryptoContextToken))
            {
                if (!IsValidCryptoContextToken(model.CryptoContextToken))
                {
                    return Json(new
                    {
                        ServerSideSuccess = false,
                        Errors = new Dictionary<string, string[]>
                        {
                            ["CryptoContextToken"] = new[] { "Invalid CryptoContextToken." }
                        }
                    });
                }
            }

            if (model.Id == Guid.Empty)
            {
                return Json(new
                {
                    ServerSideSuccess = false,
                    Errors = new Dictionary<string, string[]>
                    {
                        ["Id"] = new[] { "Template ID is required." }
                    }
                });
            }

            var query = new GetTemplateQuery { TemplateId = model.Id };
            var queryResult = await mediator.QueryAsync(query);

            if (!queryResult.IsSuccess || queryResult.Data?.Template == null)
            {
                return Json(new
                {
                    ServerSideSuccess = false,
                    Errors = new Dictionary<string, string[]>
                    {
                        ["Id"] = new[] { "Template not found." }
                    }
                });
            }

            var template = queryResult.Data.Template;

            if (string.IsNullOrWhiteSpace(model.Command))
            {
                return Json(new
                {
                    ServerSideSuccess = false,
                    Errors = new Dictionary<string, string[]>
                    {
                        ["Command"] = new[] { "Command cannot be null or empty." }
                    }
                });
            }

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                model.Title = template.Title;
            }

            switch (model.Command)
            {
                case "SavePageProperties":
                    {
                        var description = string.IsNullOrEmpty(model.Payload)
                            ? string.Empty
                            : CryptoJsDecryption.Decrypt(model.Payload);

                        var command = new UpdateTemplateMetadataCommand
                        {
                            TemplateId = model.Id,
                            Title = model.Title,
                            Description = description
                        };

                        var result = await mediator.SendAsync(command);

                        if (!result.IsSuccess)
                        {
                            return Json(new
                            {
                                ServerSideSuccess = false,
                                Errors = result.Errors ?? new Dictionary<string, string[]>
                                {
                                    ["general"] = new[] { result.ErrorMessage ?? "Failed to update template metadata." }
                                }
                            });
                        }

                        return Json(new
                        {
                            ServerSideSuccess = true,
                            Model = new
                            {
                                Title = model.Title
                            }
                        });
                    }

                case "SaveCode":
                    {
                        var content = CryptoJsDecryption.Decrypt(model.Payload);

                        if (!NestedEditableRegionValidation.Validate(content))
                        {
                            return Json(new
                            {
                                ServerSideSuccess = false,
                                Errors = new Dictionary<string, string[]>
                                {
                                    ["Payload"] = new[] { "Cannot have nested editable regions." }
                                }
                            });
                        }

                        return await SaveTemplateVersionAsync(model.Id, model.Title, content);
                    }

                case "SaveDesigner":
                    {
                        var htmlContent = string.IsNullOrEmpty(model.Payload)
                            ? string.Empty
                            : CryptoJsDecryption.Decrypt(model.Payload);
                        var cssContent = string.IsNullOrEmpty(model.CssContent)
                            ? string.Empty
                            : CryptoJsDecryption.Decrypt(model.CssContent);

                        if (!NestedEditableRegionValidation.Validate(htmlContent))
                        {
                            return Json(new
                            {
                                ServerSideSuccess = false,
                                Errors = new Dictionary<string, string[]>
                                {
                                    ["Payload"] = new[] { "Cannot have nested editable regions." }
                                }
                            });
                        }

                        htmlContent = htmlService.EnsureEditableMarkers(htmlContent);

                        var assembledContent = new DesignerUtilities().AssembleDesignerOutput(new DesignerDataViewModel
                        {
                            Id = model.Id,
                            Title = model.Title,
                            HtmlContent = htmlContent,
                            CssContent = cssContent
                        });

                        return await SaveTemplateVersionAsync(model.Id, model.Title, assembledContent);
                    }

                default:
                    return Json(new
                    {
                        ServerSideSuccess = false,
                        Errors = new Dictionary<string, string[]>
                        {
                            ["Command"] = new[] { "Unrecognized command. Valid commands are: SavePageProperties, SaveCode, SaveDesigner." }
                        }
                    });
            }
        }

        private async Task<IActionResult> SaveTemplateVersionAsync(Guid templateId, string title, string content)
        {
            try
            {
                var editableResult = await mediator.SendAsync(new GetEditablePageDesignVersionCommand { TemplateId = templateId });
                if (!editableResult.IsSuccess || editableResult.Data?.EditableVersion == null)
                {
                    return Json(new
                    {
                        ServerSideSuccess = false,
                        Errors = new Dictionary<string, string[]>
                        {
                            ["Payload"] = new[] { editableResult.ErrorMessage ?? "Template version not found." }
                        }
                    });
                }

                var editableVersion = editableResult.Data.EditableVersion;
                var finalTitle = string.IsNullOrWhiteSpace(title) ? editableVersion.Title : title;

                var saveCommand = new SavePageDesignVersionCommand
                {
                    Id = editableVersion.Id,
                    Title = finalTitle,
                    Description = editableVersion.Description,
                    Content = content,
                    PageType = editableVersion.PageType,
                    LayoutId = editableVersion.LayoutId,
                    CommunityLayoutId = editableVersion.CommunityLayoutId
                };

                var result = await mediator.SendAsync(saveCommand);

                if (!result.IsSuccess)
                {
                    return Json(new
                    {
                        ServerSideSuccess = false,
                        Errors = result.Errors ?? new Dictionary<string, string[]>
                        {
                            ["Payload"] = new[] { result.ErrorMessage ?? "Failed to save template." }
                        }
                    });
                }

                return Json(new
                {
                    ServerSideSuccess = true,
                    Model = new
                    {
                        Title = finalTitle
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ServerSideSuccess = false,
                    Errors = new Dictionary<string, string[]>
                    {
                        ["Payload"] = new[] { $"An error occurred while saving: {ex.Message}" }
                    }
                });
            }
        }
    }
}
