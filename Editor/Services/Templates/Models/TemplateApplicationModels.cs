// <copyright file="TemplateApplicationModels.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Templates.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Preview of template application impact showing all affected articles and potential merge issues.
    /// </summary>
    /// <remarks>
    /// Use this model to show users what will happen before actually applying template changes.
    /// Returned by <see cref="ITemplateService.PreviewTemplateApplicationAsync"/>.
    /// </remarks>
    public class TemplateApplicationPreview
    {
        /// <summary>
        /// Gets or sets the template ID being previewed.
        /// </summary>
        public Guid TemplateId { get; set; }

        /// <summary>
        /// Gets or sets the template name.
        /// </summary>
        public string TemplateName { get; set; }

        /// <summary>
        /// Gets or sets the total number of articles that would be affected by this template application.
        /// </summary>
        public int TotalAffectedArticles { get; set; }

        /// <summary>
        /// Gets or sets the list of individual articles with preview details.
        /// </summary>
        public List<ArticlePreviewItem> Articles { get; set; } = new List<ArticlePreviewItem>();

        /// <summary>
        /// Gets a value indicating whether all articles can be safely merged without warnings.
        /// </summary>
        public bool AllArticlesSafe => Articles.TrueForAll(a => a.CanMerge && string.IsNullOrEmpty(a.MergeWarning));

        /// <summary>
        /// Gets the count of articles that have merge warnings.
        /// </summary>
        public int WarningCount => Articles.Count(a => !string.IsNullOrEmpty(a.MergeWarning));
    }

    /// <summary>
    /// Preview details for a single article showing merge compatibility and warnings.
    /// </summary>
    public class ArticlePreviewItem
    {
        /// <summary>
        /// Gets or sets the article number (unique identifier across versions).
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the article title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the article URL path.
        /// </summary>
        public string UrlPath { get; set; }

        /// <summary>
        /// Gets or sets the current version number of the article.
        /// </summary>
        public int CurrentVersionNumber { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the article has a published version.
        /// </summary>
        /// <remarks>
        /// If true, the published version will be preserved when template is applied.
        /// If false, this is a draft-only article.
        /// </remarks>
        public bool HasPublishedVersion { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the article was last published.
        /// </summary>
        public DateTimeOffset? LastPublished { get; set; }

        /// <summary>
        /// Gets or sets the number of editable regions (data-ccms-ceid markers) found in the current article version.
        /// </summary>
        public int EditableRegionsCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the template can be successfully merged with this article.
        /// </summary>
        /// <remarks>
        /// Set to false if:
        /// - Template has fewer editable regions than the article (content would be lost)
        /// - Critical editable region IDs don't match
        /// - Article content is corrupted or unparseable
        /// </remarks>
        public bool CanMerge { get; set; }

        /// <summary>
        /// Gets or sets a warning message if there are potential merge issues.
        /// </summary>
        /// <remarks>
        /// Example warnings:
        /// - "Template is missing 2 editable regions present in the article"
        /// - "Article has 3 regions that won't be preserved in the new template"
        /// - "Region IDs don't match - manual review recommended"
        /// </remarks>
        public string MergeWarning { get; set; }
    }

    /// <summary>
    /// Result of applying a template to a single article.
    /// </summary>
    public class TemplateApplicationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the template was successfully applied.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the article number that was updated.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the new version number created by the template application.
        /// </summary>
        /// <remarks>
        /// If the article was previously at version 3, this will be 4.
        /// This new version is created as a DRAFT (not published).
        /// </remarks>
        public int NewVersionNumber { get; set; }

        /// <summary>
        /// Gets or sets the unique ID of the new article version created.
        /// </summary>
        public Guid NewVersionId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the new version is a draft (not published).
        /// </summary>
        /// <remarks>
        /// Will always be true for template application - user must explicitly publish after review.
        /// </remarks>
        public bool IsDraft { get; set; }

        /// <summary>
        /// Gets or sets the error message if the operation failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets a list of non-fatal warnings generated during the merge.
        /// </summary>
        /// <remarks>
        /// Examples:
        /// - "Editable region 'sidebar' from original content could not be preserved"
        /// - "Template has new regions that were not in the original"
        /// </remarks>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Batch result for applying a template to multiple articles.
    /// </summary>
    public class TemplateBatchApplicationResult
    {
        /// <summary>
        /// Gets or sets the number of articles successfully updated.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the number of articles that failed to update.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the detailed results for each article processed.
        /// </summary>
        public List<TemplateApplicationResult> Results { get; set; } = new List<TemplateApplicationResult>();

        /// <summary>
        /// Gets or sets the total time taken to process the batch operation.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets a value indicating whether all articles were successfully processed.
        /// </summary>
        public bool AllSucceeded => FailureCount == 0;

        /// <summary>
        /// Gets the total number of articles processed (success + failure).
        /// </summary>
        public int TotalProcessed => SuccessCount + FailureCount;
    }

    /// <summary>
    /// Result of publishing draft versions created by template application.
    /// </summary>
    public class TemplateBatchPublishResult
    {
        /// <summary>
        /// Gets or sets the number of articles successfully published.
        /// </summary>
        public int PublishedCount { get; set; }

        /// <summary>
        /// Gets or sets the number of articles that failed to publish.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the number of articles skipped (no draft version to publish).
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// Gets or sets detailed results per article.
        /// </summary>
        public List<ArticlePublishResult> Results { get; set; } = new List<ArticlePublishResult>();

        /// <summary>
        /// Gets or sets the total time taken to publish all articles.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets a value indicating whether all articles were successfully published.
        /// </summary>
        public bool AllSucceeded => FailureCount == 0;

        /// <summary>
        /// Gets the total number of articles processed.
        /// </summary>
        public int TotalProcessed => PublishedCount + FailureCount + SkippedCount;
    }

    /// <summary>
    /// Result of publishing a single article.
    /// </summary>
    public class ArticlePublishResult
    {
        /// <summary>
        /// Gets or sets the article number.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the article title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the article was successfully published.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the article was skipped (no draft to publish).
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>
        /// Gets or sets the version number that was published.
        /// </summary>
        public int? PublishedVersionNumber { get; set; }

        /// <summary>
        /// Gets or sets the error message if publishing failed.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}