// <copyright file="ITemplateService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Templates
{
    using Cosmos.Common.Data;
    using Sky.Editor.Services.Templates.Models;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Service for managing page templates and template operations.
    /// </summary>
    public interface ITemplateService
    {
        // ============================================================
        // EXISTING METHODS (Keep these as-is)
        // ============================================================

        /// <summary>
        /// Ensures default templates exist in the database.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EnsureDefaultTemplatesExistAsync();

        /// <summary>
        /// Gets all available templates.
        /// </summary>
        /// <returns>List of page templates.</returns>
        Task<List<PageTemplate>> GetAllTemplatesAsync();

        /// <summary>
        /// Gets templates filtered by category.
        /// </summary>
        /// <param name="category">Category name to filter by.</param>
        /// <returns>List of templates in the specified category.</returns>
        Task<List<PageTemplate>> GetTemplatesByCategoryAsync(string category);

        /// <summary>
        /// Gets a specific template by its key.
        /// </summary>
        /// <param name="key">Template key identifier.</param>
        /// <returns>The requested page template.</returns>
        Task<PageTemplate> GetTemplateByKeyAsync(string key);

        /// <summary>
        /// Gets the HTML content of a template.
        /// </summary>
        /// <param name="key">Template key identifier.</param>
        /// <returns>Template HTML content.</returns>
        Task<string> GetTemplateContentAsync(string key);

        /// <summary>
        /// Searches templates by search term.
        /// </summary>
        /// <param name="searchTerm">Search term to match against template name, description, or tags.</param>
        /// <returns>List of matching templates.</returns>
        Task<List<PageTemplate>> SearchTemplatesAsync(string searchTerm);

        /// <summary>
        /// Gets all design versions for a template.
        /// </summary>
        /// <param name="key">Template key identifier.</param>
        /// <returns>List of page design versions.</returns>
        Task<List<PageDesignVersion>> GetTemplateDesignVersionsAsync(string key);

        /// <summary>
        /// Gets the latest version of a template for editing.
        /// </summary>
        /// <param name="key">Template key identifier.</param>
        /// <returns>Editable page design version.</returns>
        Task<PageDesignVersion> GetVersionForEdit(string key);

        /// <summary>
        /// Gets a specific template version by ID.
        /// </summary>
        /// <param name="id">Version ID.</param>
        /// <returns>The requested page design version.</returns>
        Task<PageDesignVersion> GetVersion(string id);

        /// <summary>
        /// Saves a template design version.
        /// </summary>
        /// <param name="model">Template version to save.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Save(PageDesignVersion model);

        /// <summary>
        /// Publishes a template design version.
        /// </summary>
        /// <param name="model">Template version to publish.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Publish(PageDesignVersion model);

        // ============================================================
        // NEW METHODS FOR TEMPLATE APPLICATION
        // ============================================================

        /// <summary>
        /// Previews the impact of applying a template to all articles using it.
        /// </summary>
        /// <param name="templateId">Template ID to preview.</param>
        /// <returns>Preview data showing all articles that would be affected.</returns>
        /// <remarks>
        /// <para>This method analyzes which articles use the specified template and determines:</para>
        /// <list type="bullet">
        ///   <item>How many articles would be affected</item>
        ///   <item>Which editable regions can be successfully merged</item>
        ///   <item>Warnings for any merge conflicts (missing regions, etc.)</item>
        ///   <item>Whether each article has a published version that would be preserved</item>
        /// </list>
        /// <para>Use this before calling <see cref="ApplyTemplateToArticlesAsync"/> to show users what will change.</para>
        /// </remarks>
        Task<TemplateApplicationPreview> PreviewTemplateApplicationAsync(Guid templateId);

        /// <summary>
        /// Applies template changes to a single article, creating a new draft version.
        /// </summary>
        /// <param name="articleNumber">Article number to update.</param>
        /// <param name="templateId">Template ID to apply.</param>
        /// <returns>Result containing new version details and success status.</returns>
        /// <remarks>
        /// <para><b>Behavior:</b></para>
        /// <list type="bullet">
        ///   <item>Gets the latest version of the article</item>
        ///   <item>Merges editable content from current version into new template structure</item>
        ///   <item>Creates a NEW version (increments version number)</item>
        ///   <item>Marks new version as DRAFT (Published = null)</item>
        ///   <item>Preserves ALL existing versions (including published versions)</item>
        /// </list>
        /// <para><b>Example:</b></para>
        /// <code>
        /// Article currently at version 3 (published)
        /// → ApplyTemplateToArticleAsync called
        /// → Creates version 4 (DRAFT with new template)
        /// → Version 3 remains published
        /// → Version 1, 2, 3 are preserved
        /// </code>
        /// </remarks>
        Task<TemplateApplicationResult> ApplyTemplateToArticleAsync(int articleNumber, Guid templateId);

        /// <summary>
        /// Applies template changes to multiple articles, creating draft versions for each.
        /// </summary>
        /// <param name="templateId">Template ID to apply.</param>
        /// <param name="articleNumbers">Specific article numbers to update. If null, applies to ALL articles using this template.</param>
        /// <returns>Batch result with success/failure details per article.</returns>
        /// <remarks>
        /// <para><b>Use Cases:</b></para>
        /// <list type="bullet">
        ///   <item><b>Bulk update:</b> Pass <c>null</c> for articleNumbers to update all articles using the template</item>
        ///   <item><b>Selective update:</b> Pass specific article numbers to update only chosen articles</item>
        /// </list>
        /// <para><b>Error Handling:</b></para>
        /// <list type="bullet">
        ///   <item>Failures on individual articles do NOT stop the batch operation</item>
        ///   <item>Result contains success/failure counts and detailed errors per article</item>
        ///   <item>All successful updates create draft versions that require manual review before publishing</item>
        /// </list>
        /// <para><b>Example:</b></para>
        /// <code>
        /// // Apply to all articles using template
        /// var result = await ApplyTemplateToArticlesAsync(templateId, null);
        /// // result.SuccessCount = 45, result.FailureCount = 2
        /// 
        /// // Apply to specific articles
        /// var result = await ApplyTemplateToArticlesAsync(templateId, new List&lt;int&gt; { 101, 102, 103 });
        /// </code>
        /// </remarks>
        Task<TemplateBatchApplicationResult> ApplyTemplateToArticlesAsync(Guid templateId, List<int>? articleNumbers = null);

        /// <summary>
        /// Publishes draft versions created by template application for selected articles.
        /// </summary>
        /// <param name="templateId">Template ID whose draft versions should be published.</param>
        /// <param name="articleNumbers">Specific articles to publish. If null, publishes ALL draft versions for this template.</param>
        /// <returns>Batch result with publish status per article.</returns>
        /// <remarks>
        /// <para><b>Workflow:</b></para>
        /// <list type="number">
        ///   <item>User calls <see cref="PreviewTemplateApplicationAsync"/> to review affected articles</item>
        ///   <item>User calls <see cref="ApplyTemplateToArticlesAsync"/> to create draft versions</item>
        ///   <item>User reviews individual draft articles in the UI</item>
        ///   <item>User calls <see cref="PublishTemplateChangesAsync"/> to publish reviewed drafts</item>
        /// </list>
        /// <para><b>Safety:</b></para>
        /// <list type="bullet">
        ///   <item>Only publishes draft versions created by template application</item>
        ///   <item>Does NOT modify or delete previous published versions</item>
        ///   <item>Each article's version history is preserved</item>
        /// </list>
        /// <para><b>Example:</b></para>
        /// <code>
        /// // Publish all drafts for this template
        /// var result = await PublishTemplateChangesAsync(templateId, null);
        /// 
        /// // Publish only selected articles after review
        /// var reviewed = new List&lt;int&gt; { 101, 105, 107 }; // User approved these
        /// var result = await PublishTemplateChangesAsync(templateId, reviewed);
        /// </code>
        /// </remarks>
        Task<TemplateBatchPublishResult> PublishTemplateChangesAsync(Guid templateId, List<int>? articleNumbers = null);
    }
}
