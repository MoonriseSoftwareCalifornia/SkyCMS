// <copyright file="TemplateService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Templates
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Templates.Models;

    /// <summary>
    /// Implementation of the template service.
    /// </summary>
    public class TemplateService : ITemplateService
    {

        /// <summary>
        /// Tracks which tenants have had templates seeded to avoid redundant DB checks.
        /// Key: Tenant ID (Connection.Id), Value: true when seeded.
        /// </summary>
        private static readonly ConcurrentDictionary<Guid, bool> SeededTenants = new ();

        private readonly IWebHostEnvironment environment;
        private readonly ILogger<TemplateService> logger;
        private readonly ApplicationDbContext dbContext;
        private readonly Cosmos.Common.Features.Shared.IMediator mediator;
        private readonly IDynamicConfigurationProvider dynamicConfigProvider;
        private readonly SemaphoreSlim @lock = new (1, 1);
        private List<PageTemplate> cachedTemplates;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateService"/> class.
        /// </summary>
        /// <param name="environment">The web hosting environment.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="dbContext">The database context.</param>
        /// <param name="mediator">Mediator for CQRS queries.</param>
        /// <param name="dynamicConfigProvider">The dynamic configuration provider for tenant resolution.</param>
        public TemplateService(
            IWebHostEnvironment environment,
            ILogger<TemplateService> logger,
            ApplicationDbContext dbContext,
            Cosmos.Common.Features.Shared.IMediator mediator,
            IDynamicConfigurationProvider dynamicConfigProvider)
        {
            this.environment = environment;
            this.logger = logger;
            this.dbContext = dbContext;
            this.mediator = mediator;
            this.dynamicConfigProvider = dynamicConfigProvider;
        }

        /// <inheritdoc/>
        public async Task EnsureDefaultTemplatesExistAsync()
        {
            // Get current tenant ID from the request context (multi-tenant only)
            Guid? tenantId = null;
            
            if (dynamicConfigProvider != null)
            {
                tenantId = await dynamicConfigProvider.GetCurrentTenantIdAsync();
                
                if (tenantId == null)
                {
                    logger.LogWarning("Cannot ensure templates: Tenant ID not available (HttpContext may be unavailable)");
                    return;
                }
                
                // Check if we've already seeded templates for this tenant (in-memory cache)
                if (SeededTenants.ContainsKey(tenantId.Value))
                {
                    logger.LogDebug("Templates already ensured for tenant {TenantId}, skipping", tenantId.Value);
                    return;
                }

                logger.LogInformation("Ensuring default templates exist for tenant {TenantId}", tenantId.Value);
            }
            else
            {
                // Single-tenant mode: use a fixed sentinel value for caching
                tenantId = Guid.Empty;
                
                if (SeededTenants.ContainsKey(tenantId.Value))
                {
                    logger.LogDebug("Templates already ensured (single-tenant), skipping");
                    return;
                }
                
                logger.LogInformation("Ensuring default templates exist (single-tenant mode)");
            }

            var allTemplates = await GetAllTemplatesAsync();
            var defaultLayoutViewModel = await mediator.QueryAsync(new Cosmos.Common.Features.Layouts.Queries.GetDefaultLayoutQuery());

            // If no default layout exists, we can't create templates
            if (defaultLayoutViewModel == null)
            {
                logger.LogWarning("No default layout found. Cannot ensure default templates exist.");
                return;
            }

            var layoutId = defaultLayoutViewModel.Id;

            // Fetch the full Layout entity from database since we need LayoutNumber and CommunityLayoutId
            var defaultLayout = await dbContext.Layouts.FirstOrDefaultAsync(l => l.Id == layoutId);
            var templatesCreated = 0;
            var templatesUpdated = 0;
            var templatesSkipped = 0;

            foreach (var template in allTemplates)
            {
                // IMPROVED: Check by PageType (unique key) instead of LayoutId + Title
                // This prevents duplicates even if the default layout changes
                var dbTemplate = await dbContext.Templates
                    .FirstOrDefaultAsync(t => t.PageType == template.Key);
                
                if (dbTemplate == null)
                {
                    // Template doesn't exist - create it
                    var html = await LoadTemplateContentAsync(template.FilePath);
                    dbTemplate = new Template
                    {
                        Id = Guid.NewGuid(),
                        Title = template.Name,
                        Description = template.Description,
                        PageType = template.Key,
                        Content = html,
                        LayoutId = layoutId,
                        LayoutNumber = defaultLayout.LayoutNumber,
                        CommunityLayoutId = defaultLayout.CommunityLayoutId
                    };
                    dbContext.Templates.Add(dbTemplate);
                    templatesCreated++;
                    
                    logger.LogInformation("Created template '{PageType}' ({Title})", 
                        template.Key, template.Name);
                }
                else if (dbTemplate.LayoutId != layoutId)
                {
                    // Template exists but uses a different layout - update it to use the current default layout
                    logger.LogInformation(
                        "Template '{PageType}' exists under different layout (old: {OldLayoutId}, new: {NewLayoutId}). Updating to current default", 
                        template.Key, 
                        dbTemplate.LayoutId,
                        layoutId);
                    
                    dbTemplate.LayoutId = layoutId;
                    dbTemplate.LayoutNumber = defaultLayout.LayoutNumber;
                    dbTemplate.CommunityLayoutId = defaultLayout.CommunityLayoutId;
                    dbContext.Templates.Update(dbTemplate);
                    templatesUpdated++;
                }
                else
                {
                    // Template exists and is already using the current default layout
                    templatesSkipped++;
                    logger.LogDebug("Template '{PageType}' already exists with correct layout", 
                        template.Key);
                }
            }

            if (templatesCreated > 0 || templatesUpdated > 0)
            {
                await dbContext.SaveChangesAsync();
                logger.LogInformation(
                    "Templates ensured: {Created} created, {Updated} updated, {Skipped} skipped", 
                    templatesCreated, templatesUpdated, templatesSkipped);
            }
            else
            {
                logger.LogDebug("All templates already exist, no changes made");
            }
            
            // Mark as seeded (in-memory cache to avoid redundant checks on subsequent requests)
            SeededTenants.TryAdd(tenantId.Value, true);
        }

        /// <inheritdoc/>
        public async Task<List<PageTemplate>> GetAllTemplatesAsync()
        {
            if (cachedTemplates != null)
            {
                return cachedTemplates;
            }

            await @lock.WaitAsync();
            try
            {
                if (cachedTemplates != null)
                {
                    return cachedTemplates;
                }

                cachedTemplates = GetStandardTemplates();
                return cachedTemplates;
            }
            finally
            {
                @lock.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<List<PageTemplate>> GetTemplatesByCategoryAsync(string category)
        {
            var allTemplates = await GetAllTemplatesAsync();
            return allTemplates
                .Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <inheritdoc/>
        public async Task<PageTemplate> GetTemplateByKeyAsync(string key)
        {
            var allTemplates = await GetAllTemplatesAsync();
            var template = allTemplates.FirstOrDefault(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (template != null && string.IsNullOrEmpty(template.Content))
            {
                template.Content = await LoadTemplateContentAsync(template.FilePath);
            }

            return template;
        }

        /// <inheritdoc/>
        public async Task<string> GetTemplateContentAsync(string key)
        {
            var template = await GetTemplateByKeyAsync(key);
            return template?.Content;
        }

        /// <inheritdoc/>
        public async Task<List<PageTemplate>> SearchTemplatesAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetAllTemplatesAsync();
            }

            var allTemplates = await GetAllTemplatesAsync();
            var lowerSearch = searchTerm.ToLower();

            return allTemplates
                .Where(t =>
                    t.Name.Contains(lowerSearch, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(lowerSearch, StringComparison.OrdinalIgnoreCase) ||
                    t.Tags.Any(tag => tag.Contains(lowerSearch, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        /// <inheritdoc/>
        public async Task<List<PageDesignVersion>> GetTemplateDesignVersionsAsync(string key)
        {
            var versions = await dbContext.PageDesignVersions
                .Where(v => v.PageType == key)
                .OrderByDescending(v => v.Version)
                .ToListAsync();

            // For backwards compatibility, if no versions are found, create a default version based on the template content.
            if (versions == null || versions.Count == 0)
            {
                var template = await dbContext.Templates.FirstOrDefaultAsync(t => t.PageType == key);
                
                // Add null check for template
                if (template == null)
                {
                    logger.LogWarning("No template found with PageType: {PageType}", key);
                    return new List<PageDesignVersion>(); // Return empty list
                }
                
                var version = new PageDesignVersion
                {
                    Id = Guid.NewGuid(),
                    TemplateId = template.Id,
                    Version = 1,
                    Title = template.Title,
                    Description = template.Description,
                    Content = template.Content ?? string.Empty,
                    PageType = template.PageType,
                    Published = DateTimeOffset.UtcNow,
                    Modified = DateTimeOffset.UtcNow
                };

                dbContext.PageDesignVersions.Add(version);
                await dbContext.SaveChangesAsync();
                return new List<PageDesignVersion> { version };
            }

            return versions;
        }

        /// <inheritdoc/>
        public async Task<PageDesignVersion> GetVersionForEdit(string key)
        {
            var version = dbContext.PageDesignVersions
                .Where(v => v.PageType == key)
                .OrderByDescending(v => v.Version)
                .FirstOrDefault();

            // Add null check for version
            if (version == null)
            {
                logger.LogWarning("No page design version found for PageType: {PageType}", key);
                throw new InvalidOperationException($"No page design version found for PageType: {key}");
            }

            if (version.Published.HasValue)
            {
                var editableVersion = new PageDesignVersion
                {
                    Id = Guid.NewGuid(),
                    TemplateId = version.TemplateId,
                    Version = version.Version + 1,
                    Title = version.Title,
                    Description = version.Description,
                    Content = version.Content,
                    PageType = version.PageType,
                    Published = null, // Not published yet
                    Modified = DateTimeOffset.UtcNow
                };

                dbContext.PageDesignVersions.Add(editableVersion);
                await dbContext.SaveChangesAsync();
                return editableVersion;
            }

            return version;
        }

        /// <inheritdoc/>
        public async Task<PageDesignVersion> GetVersion(string id)
        {
            return await dbContext.PageDesignVersions.FirstOrDefaultAsync(v => v.Id.ToString() == id);
        }

        /// <inheritdoc/>
        public async Task Save(PageDesignVersion model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            try
            {
                var existing = await dbContext.PageDesignVersions.FindAsync(model.Id);

                if (existing == null)
                {
                    // New version - add it
                    dbContext.PageDesignVersions.Add(model);
                }
                else
                {
                    // Update existing version
                    existing.Title = model.Title;
                    existing.Description = model.Description;
                    existing.Content = model.Content;
                    existing.Modified = DateTimeOffset.UtcNow;

                    // Don't modify Published date or Version number
                }

                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Saved page design version {VersionId} (PageType: {PageType}, Version: {Version})",
                    model.Id,
                    model.PageType,
                    model.Version);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving page design version {VersionId}", model.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task Publish(PageDesignVersion model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            try
            {
                // Find the version to publish
                var versionToPublish = await dbContext.PageDesignVersions.FindAsync(model.Id);

                if (versionToPublish == null)
                {
                    throw new InvalidOperationException($"Page design version {model.Id} not found.");
                }

                // Unpublish all other versions of the same PageType
                var otherVersions = await dbContext.PageDesignVersions
                    .Where(v => v.PageType == versionToPublish.PageType && v.Id != versionToPublish.Id)
                    .ToListAsync();

                foreach (var version in otherVersions)
                {
                    version.Published = null;
                }

                // Publish this version
                versionToPublish.Published = DateTimeOffset.UtcNow;
                versionToPublish.Modified = DateTimeOffset.UtcNow;

                // Update the corresponding template if it exists
                var template = await dbContext.Templates.FirstOrDefaultAsync(t => t.Id == versionToPublish.TemplateId);
                if (template != null)
                {
                    template.Content = versionToPublish.Content;
                    template.Title = versionToPublish.Title;
                    template.Description = versionToPublish.Description;
                }

                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Published page design version {VersionId} (PageType: {PageType}, Version: {Version})",
                    versionToPublish.Id,
                    versionToPublish.PageType,
                    versionToPublish.Version);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing page design version {VersionId}", model.Id);
                throw;
            }
        }

        // ============================================================
        // TEMPLATE APPLICATION - HELPER METHODS
        // ============================================================

        /// <summary>
        /// Merges editable content from an article into a template, preserving user content.
        /// </summary>
        /// <param name="templateHtml">New template HTML structure.</param>
        /// <param name="articleHtml">Existing article HTML with user content.</param>
        /// <param name="warnings">List to collect merge warnings.</param>
        /// <returns>Merged HTML content.</returns>
        private string MergeEditableContent(string templateHtml, string articleHtml, List<string> warnings)
        {
            try
            {
                var articleDoc = new HtmlAgilityPack.HtmlDocument();
                var templateDoc = new HtmlAgilityPack.HtmlDocument();

                articleDoc.LoadHtml(articleHtml);
                templateDoc.LoadHtml(templateHtml);

                // Check for HTML parsing errors and add warnings
                if (articleDoc.ParseErrors != null && articleDoc.ParseErrors.Any())
                {
                    warnings.Add("Article HTML contains malformed content that was automatically corrected");
                }

                if (templateDoc.ParseErrors != null && templateDoc.ParseErrors.Any())
                {
                    warnings.Add("Template HTML contains malformed content that was automatically corrected");
                }

                // Find all editable regions in both documents
                var articleEditableRegions = articleDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]") ?? new HtmlAgilityPack.HtmlNodeCollection(null);
                var templateEditableRegions = templateDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]") ?? new HtmlAgilityPack.HtmlNodeCollection(null);

                // Build a dictionary of article regions by ID
                var articleRegionsById = new Dictionary<string, HtmlAgilityPack.HtmlNode>();
                foreach (var region in articleEditableRegions)
                {
                    var id = region.GetAttributeValue("data-ccms-ceid", null);
                    if (!string.IsNullOrEmpty(id))
                    {
                        articleRegionsById[id] = region;
                    }
                }

                // Merge content into template regions
                foreach (var templateRegion in templateEditableRegions)
                {
                    var regionId = templateRegion.GetAttributeValue("data-ccms-ceid", null);
                    if (string.IsNullOrEmpty(regionId)) continue;

                    if (articleRegionsById.TryGetValue(regionId, out var articleRegion))
                    {
                        // Preserve user content from matching region
                        templateRegion.InnerHtml = articleRegion.InnerHtml;
                    }

                    // else: Region is new in template, keep template default content
                }

                // Check for regions in article that are not in template (will be lost)
                var templateRegionIds = new HashSet<string>(
                    templateEditableRegions.Select(r => r.GetAttributeValue("data-ccms-ceid", null))
                        .Where(id => !string.IsNullOrEmpty(id)));

                var lostRegions = articleRegionsById.Keys.Except(templateRegionIds).ToList();
                if (lostRegions.Any())
                {
                    warnings.Add($"The following editable regions will be lost: {string.Join(", ", lostRegions)}");
                }

                // Check for articles with no editable content
                if (articleEditableRegions.Count == 0 && templateEditableRegions.Count > 0)
                {
                    warnings.Add("Article has no editable regions - all content will be replaced with template defaults");
                }

                return templateDoc.DocumentNode.OuterHtml;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error merging editable content");
                throw new InvalidOperationException("Failed to merge template and article content. HTML may be malformed.", ex);
            }
        }

        /// <summary>
        /// Counts editable regions in HTML content.
        /// </summary>
        /// <param name="html">HTML content to analyze.</param>
        /// <returns>Number of editable regions found.</returns>
        private int CountEditableRegions(string html)
        {
            try
            {
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);
                var regions = doc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]");
                return regions?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Detects merge warnings by comparing template and article editable regions.
        /// </summary>
        /// <param name="templateHtml">Template HTML.</param>
        /// <param name="articleHtml">Article HTML.</param>
        /// <returns>Warning message or null if no issues.</returns>
        private string? DetectMergeWarnings(string templateHtml, string articleHtml)
        {
            try
            {
                var articleDoc = new HtmlAgilityPack.HtmlDocument();
                var templateDoc = new HtmlAgilityPack.HtmlDocument();

                articleDoc.LoadHtml(articleHtml);
                templateDoc.LoadHtml(templateHtml);

                var articleRegions = articleDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]");
                var templateRegions = templateDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]");

                var articleRegionIds = new HashSet<string>(
                    (articleRegions ?? Enumerable.Empty<HtmlAgilityPack.HtmlNode>())
                        .Select(r => r.GetAttributeValue("data-ccms-ceid", null))
                        .Where(id => !string.IsNullOrEmpty(id)));

                var templateRegionIds = new HashSet<string>(
                    (templateRegions ?? Enumerable.Empty<HtmlAgilityPack.HtmlNode>())
                        .Select(r => r.GetAttributeValue("data-ccms-ceid", null))
                        .Where(id => !string.IsNullOrEmpty(id)));

                // Check for lost regions
                var lostRegions = articleRegionIds.Except(templateRegionIds).ToList();
                if (lostRegions.Any())
                {
                    return $"Template is missing {lostRegions.Count} editable region(s): {string.Join(", ", lostRegions)}";
                }

                // Check for no editable regions
                if (articleRegionIds.Count == 0 && templateRegionIds.Count > 0)
                {
                    return "Article has no editable regions - content will be lost";
                }

                return null;
            }
            catch
            {
                return "Unable to analyze merge compatibility";
            }
        }

        // ============================================================
        // TEMPLATE APPLICATION - PUBLIC METHODS
        // ============================================================

        /// <inheritdoc/>
        public async Task<TemplateApplicationResult> ApplyTemplateToArticleAsync(int articleNumber, Guid templateId)
        {
            var result = new TemplateApplicationResult
            {
                ArticleNumber = articleNumber,
                Success = false
            };

            try
            {
                // Validate template exists
                var template = await dbContext.Templates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == templateId);

                if (template == null)
                {
                    result.ErrorMessage = $"Template with ID '{templateId}' not found.";
                    return result;
                }

                // Get the latest version of the article
                var latestArticle = await dbContext.Articles
                    .Where(a => a.ArticleNumber == articleNumber)
                    .OrderByDescending(a => a.VersionNumber)
                    .FirstOrDefaultAsync();

                if (latestArticle == null)
                {
                    result.ErrorMessage = $"Article {articleNumber} not found.";
                    return result;
                }

                // Merge template with article content
                var warnings = new List<string>();
                string mergedContent;

                try
                {
                    mergedContent = MergeEditableContent(template.Content, latestArticle.Content, warnings);
                }
                catch (InvalidOperationException ex)
                {
                    result.ErrorMessage = ex.Message;
                    return result;
                }

                // Check if template has any editable regions
                var templateDoc = new HtmlAgilityPack.HtmlDocument();
                templateDoc.LoadHtml(template.Content);
                var templateEditableRegions = templateDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]");
                var hasEditableRegions = templateEditableRegions != null && templateEditableRegions.Count > 0;

                // If template has no editable regions, update the existing article instead of creating a new version
                if (!hasEditableRegions)
                {
                    latestArticle.Content = mergedContent;
                    latestArticle.TemplateId = templateId;
                    latestArticle.Updated = DateTimeOffset.UtcNow;
                    await dbContext.SaveChangesAsync();

                    result.Success = true;
                    result.NewVersionNumber = latestArticle.VersionNumber;
                    result.NewVersionId = latestArticle.Id;
                    result.IsDraft = latestArticle.Published == null;
                    result.Warnings = warnings;

                    logger.LogInformation(
                        "Template {TemplateId} (no editable regions) applied to article {ArticleNumber}. Updated existing version {VersionNumber}",
                        templateId,
                        articleNumber,
                        latestArticle.VersionNumber);

                    return result;
                }

                // Get the highest version number to create the next version
                var maxVersionNumber = await dbContext.Articles
                    .Where(a => a.ArticleNumber == articleNumber)
                    .MaxAsync(a => a.VersionNumber);
                
                var newVersionNumber = maxVersionNumber + 1;

                // Create new article version (DRAFT)
                var newArticle = new Article
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = articleNumber,
                    VersionNumber = newVersionNumber,
                    Content = mergedContent,
                    Title = latestArticle.Title,
                    UrlPath = latestArticle.UrlPath,
                    TemplateId = templateId,
                    Published = null, // DRAFT - not published
                    Updated = DateTimeOffset.UtcNow,
                    HeaderJavaScript = latestArticle.HeaderJavaScript,
                    FooterJavaScript = latestArticle.FooterJavaScript,
                    BannerImage = latestArticle.BannerImage,
                    UserId = latestArticle.UserId,
                    ArticleType = latestArticle.ArticleType,
                    Category = latestArticle.Category,
                    StatusCode = latestArticle.StatusCode,
                    Expires = latestArticle.Expires,
                    Introduction = latestArticle.Introduction,
                    RedirectTarget = latestArticle.RedirectTarget,
                    BlogKey = latestArticle.BlogKey
                };

                dbContext.Articles.Add(newArticle);
                await dbContext.SaveChangesAsync();

                // Build successful result
                result.Success = true;
                result.NewVersionNumber = newVersionNumber;
                result.NewVersionId = newArticle.Id;
                result.IsDraft = true;
                result.Warnings = warnings;

                logger.LogInformation(
                    "Template {TemplateId} applied to article {ArticleNumber}. Created new version {VersionNumber} (DRAFT)",
                    templateId,
                    articleNumber,
                    newVersionNumber);

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying template {TemplateId} to article {ArticleNumber}", templateId, articleNumber);
                result.ErrorMessage = $"Unexpected error: {ex.Message}";
                return result;
            }
        }

        /// <inheritdoc/>
        public async Task<TemplateBatchApplicationResult> ApplyTemplateToArticlesAsync(Guid templateId, List<int>? articleNumbers = null)
        {
            var startTime = DateTimeOffset.UtcNow;
            var result = new TemplateBatchApplicationResult();

            try
            {
                // Validate template exists
                var template = await dbContext.Templates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == templateId);

                if (template == null)
                {
                    logger.LogWarning("Template {TemplateId} not found for batch application", templateId);
                    return result;
                }

                // Get article numbers to process
                List<int> articlesToProcess;
                if (articleNumbers == null || articleNumbers.Count == 0)
                {
                    // Apply to ALL articles using this template
                    articlesToProcess = await dbContext.ArticleCatalog
                        .Where(c => c.TemplateId == templateId)
                        .Select(c => c.ArticleNumber)
                        .Distinct()
                        .ToListAsync();
                }
                else
                {
                    // Apply to specific articles
                    articlesToProcess = articleNumbers;
                }

                logger.LogInformation(
                    "Starting batch template application: {TemplateId} to {Count} articles",
                    templateId,
                    articlesToProcess.Count);

                // Process each article
                foreach (var articleNumber in articlesToProcess)
                {
                    var articleResult = await ApplyTemplateToArticleAsync(articleNumber, templateId);
                    result.Results.Add(articleResult);

                    if (articleResult.Success)
                    {
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailureCount++;
                        logger.LogWarning(
                            "Failed to apply template {TemplateId} to article {ArticleNumber}: {Error}",
                            templateId,
                            articleNumber,
                            articleResult.ErrorMessage);
                    }
                }

                result.Duration = DateTimeOffset.UtcNow - startTime;

                logger.LogInformation(
                    "Batch template application completed: {Success} succeeded, {Failed} failed in {Duration}ms",
                    result.SuccessCount,
                    result.FailureCount,
                    result.Duration.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during batch template application for template {TemplateId}", templateId);
                result.Duration = DateTimeOffset.UtcNow - startTime;
                return result;
            }
        }

        /// <inheritdoc/>
        public async Task<TemplateApplicationPreview> PreviewTemplateApplicationAsync(Guid templateId)
        {
            // Validate template exists
            var template = await dbContext.Templates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == templateId);

            if (template == null)
            {
                throw new InvalidOperationException($"Template with ID '{templateId}' not found.");
            }

            var preview = new TemplateApplicationPreview
            {
                TemplateId = templateId,
                TemplateName = template.Title
            };

            // Find all articles using this template
            var articleNumbers = await dbContext.ArticleCatalog
                .Where(c => c.TemplateId == templateId)
                .Select(c => c.ArticleNumber)
                .Distinct()
                .ToListAsync();

            preview.TotalAffectedArticles = articleNumbers.Count;

            // Build preview for each article
            foreach (var articleNumber in articleNumbers)
            {
                // Get latest version
                var latestArticle = await dbContext.Articles
                    .Where(a => a.ArticleNumber == articleNumber)
                    .OrderByDescending(a => a.VersionNumber)
                    .FirstOrDefaultAsync();

                if (latestArticle == null) continue;

                // Check if article has published version
                var publishedVersion = await dbContext.Articles
                    .Where(a => a.ArticleNumber == articleNumber && a.Published != null)
                    .OrderByDescending(a => a.Published)
                    .FirstOrDefaultAsync();

                var previewItem = new ArticlePreviewItem
                {
                    ArticleNumber = articleNumber,
                    Title = latestArticle.Title,
                    UrlPath = latestArticle.UrlPath,
                    CurrentVersionNumber = latestArticle.VersionNumber,
                    HasPublishedVersion = publishedVersion != null,
                    LastPublished = publishedVersion?.Published,
                    EditableRegionsCount = CountEditableRegions(latestArticle.Content),
                    CanMerge = true
                };

                // Detect merge warnings
                var warning = DetectMergeWarnings(template.Content, latestArticle.Content);
                if (!string.IsNullOrEmpty(warning))
                {
                    previewItem.MergeWarning = warning;
                }

                preview.Articles.Add(previewItem);
            }

            logger.LogInformation(
                "Preview generated for template {TemplateId}: {Count} articles affected",
                templateId,
                preview.TotalAffectedArticles);

            return preview;
        }

        /// <inheritdoc/>
        public async Task<TemplateBatchPublishResult> PublishTemplateChangesAsync(Guid templateId, List<int>? articleNumbers = null)
        {
            var startTime = DateTimeOffset.UtcNow;
            var result = new TemplateBatchPublishResult();

            try
            {
                // Validate template exists
                var template = await dbContext.Templates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == templateId);

                if (template == null)
                {
                    logger.LogWarning("Template {TemplateId} not found for publishing changes", templateId);
                    return result;
                }

                // Get ALL articles using this template
                var allArticles = await dbContext.ArticleCatalog
                    .Where(c => c.TemplateId == templateId)
                    .Select(c => c.ArticleNumber)
                    .Distinct()
                    .ToListAsync();

                // Determine which articles to publish vs skip
                List<int> articlesToPublish;
                if (articleNumbers == null || articleNumbers.Count == 0)
                {
                    // Publish all articles using this template
                    articlesToPublish = allArticles;
                }
                else
                {
                    // Publish only specified articles
                    articlesToPublish = articleNumbers;
                }

                logger.LogInformation(
                    "Publishing template changes: {TemplateId} to {PublishCount} of {TotalCount} articles",
                    templateId,
                    articlesToPublish.Count,
                    allArticles.Count);

                // Process each article
                foreach (var articleNumber in articlesToPublish)
                {
                    try
                    {
                        var publishResult = new ArticlePublishResult
                        {
                            ArticleNumber = articleNumber,
                            Success = false
                        };

                        // Find the latest draft version for this article with the specified template
                        var draftArticle = await dbContext.Articles
                            .Where(a => a.ArticleNumber == articleNumber 
                                && a.TemplateId == templateId 
                                && a.Published == null)
                            .OrderByDescending(a => a.VersionNumber)
                            .FirstOrDefaultAsync();

                        if (draftArticle != null)
                        {
                            // Unpublish other versions
                            var otherVersions = await dbContext.Articles
                                .Where(a => a.ArticleNumber == articleNumber && a.Id != draftArticle.Id)
                                .ToListAsync();

                            foreach (var version in otherVersions)
                            {
                                version.Published = null;
                            }

                            // Publish the draft version
                            draftArticle.Published = DateTimeOffset.UtcNow;
                            await dbContext.SaveChangesAsync();

                            publishResult.Success = true;
                            publishResult.PublishedVersionNumber = draftArticle.VersionNumber;

                            logger.LogInformation(
                                "Published draft article {ArticleNumber}, version {VersionNumber}",
                                articleNumber,
                                draftArticle.VersionNumber);
                        }
                        else
                        {
                            publishResult.ErrorMessage = $"No draft version found for article {articleNumber} with template {templateId}";
                            logger.LogWarning(
                                "No draft found for article {ArticleNumber} with template {TemplateId}",
                                articleNumber,
                                templateId);
                        }

                        result.Results.Add(publishResult);
                    }
                    catch (Exception articleEx)
                    {
                        logger.LogError(articleEx, "Error processing article {ArticleNumber}", articleNumber);
                        result.Results.Add(new ArticlePublishResult
                        {
                            ArticleNumber = articleNumber,
                            Success = false,
                            ErrorMessage = articleEx.Message
                        });
                    }
                }

                // Aggregate results into counts
                result.PublishedCount = result.Results.Count(r => r.Success);
                result.SkippedCount = result.Results.Count(r => !r.Success && 
                    (r.ErrorMessage?.Contains("not in publish list") == true || 
                     r.ErrorMessage?.Contains("No draft version found") == true));
                result.FailureCount = result.Results.Count(r => !r.Success && 
                    r.ErrorMessage?.Contains("not in publish list") != true && 
                    r.ErrorMessage?.Contains("No draft version found") != true);

                result.Duration = DateTimeOffset.UtcNow - startTime;

                logger.LogInformation(
                    "Template publishing completed: {Success} succeeded, {Failed} failed, {Skipped} skipped in {Duration}ms",
                    result.PublishedCount,
                    result.FailureCount,
                    result.SkippedCount,
                    result.Duration.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during template publishing for template {TemplateId}", templateId);
                result.Duration = DateTimeOffset.UtcNow - startTime;
                return result;
            }
        }

        // ============================================================
        // PRIVATE HELPER METHODS
        // ============================================================

        /// <summary>
        /// Loads template content from embedded resource or file system.
        /// </summary>
        /// <param name="filePath">Relative file path to the template.</param>
        /// <returns>Template HTML content or null if not found.</returns>
        private async Task<string> LoadTemplateContentAsync(string filePath)
        {
            try
            {
                // Try embedded resource first
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"{assembly.GetName().Name}.Templates.{filePath.Replace('/', '.').Replace('\\', '.')}";

                await using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    return await reader.ReadToEndAsync();
                }

                // Fallback to file system
                var physicalPath = Path.Combine(environment.ContentRootPath, "Templates", filePath);
                if (File.Exists(physicalPath))
                {
                    return await File.ReadAllTextAsync(physicalPath);
                }

                logger.LogWarning("Template file not found: {FilePath}", filePath);
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading template: {FilePath}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Gets the list of standard page templates available in the system.
        /// </summary>
        /// <returns>List of page template metadata.</returns>
        private List<PageTemplate> GetStandardTemplates()
        {
            return new List<PageTemplate>
            {
                new PageTemplate
                {
                    Key = "blog-stream",
                    Name = "Blog Stream",
                    Description = "Standard blog stream layout with featured image, author info, and comment section.",
                    Category = "Blog",
                    FilePath = "PageTemplates/blog-stream.html",
                    ThumbnailPath = "/images/templates/blog-stream-thumb.png",
                    Tags = new List<string> { "blog", "article", "post", "content" }
                },
                new PageTemplate
                {
                    Key = "blog-post",
                    Name = "Blog Post",
                    Description = "Standard blog post layout with featured image, author info, and comment section.",
                    Category = "Blog",
                    FilePath = "PageTemplates/blog-post.html",
                    ThumbnailPath = "/images/templates/blog-post-thumb.png",
                    Tags = new List<string> { "blog", "article", "post", "content" }
                }
            };
        }
    }
}
