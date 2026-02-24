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
    using CommonMediator = Cosmos.Common.Features.Shared.IMediator;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Sky.Cms.Models;
    using Sky.Editor.Data;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Models;
    using Sky.Editor.Models.GrapesJs;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Features.Templates.Save;
    using Sky.Editor.Features.Templates.Create;
    using Sky.Editor.Features.Templates.Delete;
    using Sky.Editor.Features.Templates.Get;
    using Sky.Editor.Features.Templates.UpdateMetadata;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Templates controller.
    /// </summary>
    [Authorize(Roles = "Administrators, Editors")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class TemplatesController : BaseController
    {
        private readonly ArticleEditLogic articleLogic;
        private readonly ApplicationDbContext dbContext;
        private readonly IEditorSettings options;
        private readonly IStorageContext storageContext;
        private readonly IArticleHtmlService htmlService;
        private readonly ITemplateService templateServices;
        private readonly CommonMediator mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplatesController"/> class.
        /// Constructor.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="storageContext">Storage context service.</param>
        /// <param name="articleLogic">Article edit logic.</param>
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
            ArticleEditLogic articleLogic,
            IEditorSettings options,
            IArticleHtmlService htmlService,
            ITemplateService templateServices,
            CommonMediator mediator,
            IMemoryCache memoryCache,
            IDynamicConfigurationProvider configProvider)
            : base(dbContext, userManager, memoryCache, configProvider)
        {
            this.dbContext = dbContext;
            this.articleLogic = articleLogic;
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var defaultLayout = await GetCurrentLayoutAsync();

            ViewData["Layouts"] = await BaseGetLayoutListItems();

            ViewData["sortOrder"] = sortOrder;
            ViewData["currentSort"] = currentSort;
            ViewData["pageNo"] = pageNo;
            ViewData["pageSize"] = pageSize;

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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == id);

            if (template == null)
            {
                return NotFound();
            }

            ViewData["templateId"] = id;
            ViewData["sortOrder"] = sortOrder;
            ViewData["currentSort"] = currentSort;
            ViewData["pageNo"] = pageNo;
            ViewData["pageSize"] = pageSize;
            ViewData["template"] = template;
            ViewData["canApplyChanges"] = template.Content.ToLower().Contains(" data-ccms-ceid=");

            if (!string.IsNullOrEmpty(filter))
            {
                filter = filter.TrimStart('/');
            }

            ViewData["Filter"] = filter;

            ViewData["PublisherUrl"] = options.PublisherUrl;

            ViewData["ShowNotFoundBtn"] = !await dbContext.ArticleCatalog.Where(w => w.UrlPath == "not_found").CosmosAnyAsync();

            if (!string.IsNullOrEmpty(filter))
            {
                filter = filter.TrimStart('/');
            }

            ViewData["Filter"] = filter;

            ViewData["sortOrder"] = sortOrder;
            ViewData["currentSort"] = currentSort;
            ViewData["pageNo"] = pageNo;
            ViewData["pageSize"] = pageSize;

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
                            ModelState.AddModelError("", 
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
                        ModelState.AddModelError("", 
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
                        ModelState.AddModelError("", $"Failed to create template version: {versionResult.ErrorMessage}");
                        return BadRequest(ModelState);
                    }

                    return RedirectToAction("EditCode", "Templates", new { entity.Id });
                }
                catch (Exception ex)
                {
                    // ⚠️ WARNING: Partial data may exist in Cosmos DB
                    ModelState.AddModelError("", $"Error creating template: {ex.Message}");
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
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
        /// Save changes to template title and description.
        /// </summary>
        /// <param name="model">Template edit post model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TemplateEditViewModel model)
        {
            model.Description = CryptoJsDecryption.Decrypt(model.Description);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var command = new UpdateTemplateMetadataCommand
            {
                TemplateId = model.Id,
                Title = model.Title,
                Description = model.Description
            };

            var result = await mediator.SendAsync(command);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to update template.");
                return View(model);
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// Edit template code.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> EditCode(Guid id)
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

            var entity = result.Data.Template;

            var model = new TemplateCodeEditorViewModel
            {
                Id = entity.Id,
                EditorTitle = "Template Editor",
                EditorFields = new List<EditorField>
                {
                    new ()
                    {
                        EditorMode = EditorMode.Html,
                        FieldName = "Html Content",
                        FieldId = "Content",
                        IconUrl = "~/images/seti-ui/icons/html.svg",
                        ToolTip = string.Empty
                    }
                },
                CustomButtons = new List<string>
                {
                    "Preview"
                },
                EditingField = "Content",
                Content = htmlService.EnsureEditableMarkers(entity.Content),
                Version = 0,
                Title = entity.Title
            };
            return View(model);
        }

        /// <summary>
        /// Save edited template code.
        /// </summary>
        /// <param name="model">Template post model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<IActionResult> EditCode(TemplateCodeEditorViewModel model)
        {
            model.Content = CryptoJsDecryption.Decrypt(model.Content);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check for nested editable regions.
            if (!NestedEditableRegionValidation.Validate(model.Content))
            {
                ModelState.AddModelError("Content", "Cannot have nested editable regions.");
                return Json(BuildSaveResultModel());
            }

            try
            {
                // Get the latest version of this template
                var latestVersion = await dbContext.PageDesignVersions
                    .Where(v => v.TemplateId == model.Id)
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefaultAsync();

                if (latestVersion == null)
                {
                    ModelState.AddModelError("Content", "Template version not found.");
                    return Json(BuildSaveResultModel());
                }

                // Use SavePageDesignVersionHandler to save the changes
                // This ensures editable markers are properly added and content is validated
                var saveCommand = new SavePageDesignVersionCommand
                {
                    Id = latestVersion.Id,
                    Title = model.Title,
                    Description = latestVersion.Description,
                    Content = model.Content,
                    PageType = latestVersion.PageType,
                    LayoutId = latestVersion.LayoutId,
                    CommunityLayoutId = latestVersion.CommunityLayoutId
                };

                var result = await mediator.SendAsync(saveCommand);

                if (!result.IsSuccess)
                {
                    ModelState.AddModelError("Content", result.ErrorMessage ?? "Failed to save template.");
                    return Json(BuildSaveResultModel());
                }

                return Json(BuildSaveResultModel());
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Content", $"An error occurred while saving: {ex.Message}");
                return Json(BuildSaveResultModel());
            }
        }

        /// <summary>
        /// Loads the designer GUI.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>View.</returns>
        public async Task<IActionResult> Designer(Guid id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Loads GrapeJS.
            ViewData["IsDesigner"] = true;

            var query = new GetTemplateQuery { TemplateId = id };
            var result = await mediator.QueryAsync(query);
            
            if (!result.IsSuccess || result.Data?.Template == null)
            {
                return NotFound();
            }

            var template = result.Data.Template;

            var defaultLayout = await GetCurrentLayoutAsync();
            var config = new DesignerConfig(defaultLayout, id.ToString(), template.Title);
            var assets = await FileManagerController.GetImageAssetArray(storageContext, "/pub", "/pub/articles");
            if (assets != null)
            {
                config.ImageAssets.AddRange(assets);
            }

            return View(config);
        }

        /// <summary>
        /// Visual designer based on GrapeJS.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>IActionResult.</returns>
        [HttpGet]
        public async Task<IActionResult> DesignerData(Guid id)
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

            var entity = result.Data.Template;

            var htmlContent = htmlService.EnsureEditableMarkers(entity.Content);

            return Json(new project(htmlContent));
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            DesignerDataViewModel model = new DesignerDataViewModel()
            {
                Id = id,
                HtmlContent = CryptoJsDecryption.Decrypt(htmlContent),
                CssContent = CryptoJsDecryption.Decrypt(cssContent),
                Title = title
            };

            // Check for nested editable regions.
            if (!NestedEditableRegionValidation.Validate(model.HtmlContent))
            {
                return BadRequest("Cannot have nested editable regions.");
            }

            try
            {
                var entity = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == model.Id);

                if (entity == null)
                {
                    return NotFound();
                }

                // Get the latest version of this template
                var latestVersion = await dbContext.PageDesignVersions
                    .Where(v => v.TemplateId == model.Id)
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefaultAsync();

                if (latestVersion == null)
                {
                    return BadRequest("Template version not found.");
                }

                // Assemble the designer output
                var designerUtils = new DesignerUtilities();
                var assembledContent = designerUtils.AssembleDesignerOutput(model);

                // Determine the title to use
                var finalTitle = string.IsNullOrEmpty(model.Title)
                    ? (string.IsNullOrEmpty(entity.Title) ? $"Template {await dbContext.Templates.CountAsync()}" : entity.Title)
                    : model.Title;

                // Use SavePageDesignVersionHandler to save the changes
                // This ensures editable markers are properly added and content is validated
                var saveCommand = new SavePageDesignVersionCommand
                {
                    Id = latestVersion.Id,
                    Title = finalTitle,
                    Description = latestVersion.Description,
                    Content = assembledContent,
                    PageType = latestVersion.PageType,
                    LayoutId = latestVersion.LayoutId,
                    CommunityLayoutId = latestVersion.CommunityLayoutId
                };

                var result = await mediator.SendAsync(saveCommand);

                if (!result.IsSuccess)
                {
                    return BadRequest(new { error = result.ErrorMessage ?? "Failed to save template." });
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"An error occurred while saving: {ex.Message}" });
            }
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = await dbContext.Templates.FirstOrDefaultAsync(f => f.Id == templateId);
            if (template == null)
            {
                return NotFound($"Template with ID '{templateId}' was not found.");
            }

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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Apply template to all articles using this template - creates drafts
            var result = await templateServices.ApplyTemplateToArticlesAsync(id, null);

            if (result.AllSucceeded)
            {
                TempData["Success"] = $"Template applied to {result.SuccessCount} articles. Draft versions created. Review and publish individually.";
            }
            else
            {
                TempData["Warning"] = $"Template applied: {result.SuccessCount} succeeded, {result.FailureCount} failed.";
            }

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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
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
        /// Updates all the pages that use this template using the service layer batch operation.
        /// </summary>
        /// <param name="id">Template ID.</param>
        /// <returns>Task with the batch result.</returns>
        [Obsolete("Use TemplateService.ApplyTemplateToArticlesAsync directly instead.")]
        public async Task UpdateAllPages(Guid id)
        {
            // This method is now a wrapper for backward compatibility
            // Use the service layer's batch operation instead of looping
            await templateServices.ApplyTemplateToArticlesAsync(id, null);
        }
    }
}
