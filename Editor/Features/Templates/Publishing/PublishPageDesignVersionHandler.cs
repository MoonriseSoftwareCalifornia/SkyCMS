// <copyright file="PublishPageDesignVersionHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Publishing
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using HtmlAgilityPack;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Publishing;

    /// <summary>
    /// Handles publishing page design versions and updating all related articles.
    /// </summary>
    public class PublishPageDesignVersionHandler : ICommandHandler<PublishPageDesignVersionCommand, CommandResult<Template>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IPublishingService publishingService;
        private readonly IClock clock;
        private readonly ILogger<PublishPageDesignVersionHandler> logger;
        private readonly IMediator mediator;
        private readonly PublishPageDesignVersionValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishPageDesignVersionHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="publishingService">Publishing service.</param>
        /// <param name="clock">Clock service.</param>
        /// <param name="logger">Logger service.</param>
        /// <param name="mediator">Mediator for sending commands.</param>
        public PublishPageDesignVersionHandler(
            ApplicationDbContext dbContext,
            IPublishingService publishingService,
            IClock clock,
            ILogger<PublishPageDesignVersionHandler> logger,
            IMediator mediator)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.publishingService = publishingService ?? throw new ArgumentNullException(nameof(publishingService));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            validator = new PublishPageDesignVersionValidator();
        }

        /// <summary>
        /// Handles the publish page design version command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with the updated template.</returns>
        public async Task<CommandResult<Template>> HandleAsync(
            PublishPageDesignVersionCommand command,
            CancellationToken cancellationToken = default)
        {
            // Validate
            var validationErrors = validator.Validate(command);
            if (validationErrors.Any())
            {
                return CommandResult<Template>.Failure(validationErrors);
            }

            try
            {
                logger.LogInformation(
                    "Publishing page design version {Id} by user {UserId}",
                    command.Id,
                    command.UserId);

                // Get the page design version
                var pageDesignVersion = await dbContext.PageDesignVersions
                    .FirstOrDefaultAsync(v => v.Id == command.Id, cancellationToken);

                if (pageDesignVersion == null)
                {
                    logger.LogWarning("Page design version {Id} not found", command.Id);
                    return CommandResult<Template>.Failure($"Page design version {command.Id} not found.");
                }

                // Get the template
                var template = await dbContext.Templates
                    .FirstOrDefaultAsync(t => t.Id == pageDesignVersion.TemplateId, cancellationToken);

                if (template == null)
                {
                    logger.LogWarning("Template {TemplateId} not found", pageDesignVersion.TemplateId);
                    return CommandResult<Template>.Failure($"Template {pageDesignVersion.TemplateId} not found.");
                }

                var now = clock.UtcNow;

                // Mark the version as published
                pageDesignVersion.Published = now;
                pageDesignVersion.Modified = now;

                // Update the Template entity with the published content
                template.LayoutId = pageDesignVersion.LayoutId;
                template.CommunityLayoutId = pageDesignVersion.CommunityLayoutId;
                template.Title = pageDesignVersion.Title;
                template.Description = pageDesignVersion.Description;
                template.Content = pageDesignVersion.Content;
                template.PageType = pageDesignVersion.PageType;

                var latestVersionNumber = await dbContext.PageDesignVersions
                    .Where(v => v.TemplateId == pageDesignVersion.TemplateId)
                    .OrderByDescending(v => v.Version)
                    .Select(v => v.Version)
                    .FirstOrDefaultAsync(cancellationToken);

                var nextEditableVersion = new PageDesignVersion
                {
                    Id = Guid.NewGuid(),
                    TemplateId = template.Id,
                    LayoutId = template.LayoutId,
                    CommunityLayoutId = template.CommunityLayoutId,
                    Version = latestVersionNumber + 1,
                    Title = template.Title,
                    Description = template.Description,
                    Content = template.Content,
                    PageType = template.PageType,
                    Published = null,
                    Modified = now
                };

                dbContext.PageDesignVersions.Add(nextEditableVersion);

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Published page design version {Version} for template {TemplateId} and created editable version {NextVersion}",
                    pageDesignVersion.Version,
                    template.Id,
                    nextEditableVersion.Version);

                // Update all articles that use this template
                await UpdateAllArticlesWithTemplate(template.Id, template.Content, command.UserId, cancellationToken);

                return CommandResult<Template>.Success(template);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error publishing page design version {Id}",
                    command.Id);

                return CommandResult<Template>.Failure("An error occurred while publishing the page design version.");
            }
        }

        /// <summary>
        /// Updates all articles that use this template with the new template content.
        /// </summary>
        /// <param name="templateId">Template ID.</param>
        /// <param name="templateContent">New template content.</param>
        /// <param name="userId">User performing the update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task UpdateAllArticlesWithTemplate(
            Guid templateId,
            string templateContent,
            Guid userId,
            CancellationToken cancellationToken)
        {
            // Get all articles that use this template
            var articlesUsingTemplate = await dbContext.ArticleCatalog
                .Where(c => c.TemplateId == templateId)
                .Select(c => c.ArticleNumber)
                .ToListAsync(cancellationToken);

            logger.LogInformation(
                "Updating {Count} articles with new template {TemplateId}",
                articlesUsingTemplate.Count,
                templateId);

            foreach (var articleNumber in articlesUsingTemplate)
            {
                await ApplyTemplateToArticle(articleNumber, templateContent, userId, cancellationToken);
            }
        }

        /// <summary>
        /// Applies the template to a single article, creating a new version and republishing if needed.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="templateContent">Template content to apply.</param>
        /// <param name="userId">User performing the update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task ApplyTemplateToArticle(
            int articleNumber,
            string templateContent,
            Guid userId,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get the latest version of the article
                var currentArticle = await dbContext.Articles
                    .Where(a => a.ArticleNumber == articleNumber)
                    .OrderByDescending(a => a.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken);

                if (currentArticle == null)
                {
                    logger.LogWarning("Article {ArticleNumber} not found", articleNumber);
                    return;
                }

                // Apply template changes while preserving editable content
                var updatedContent = MergeTemplateWithArticleContent(currentArticle.Content, templateContent);

                // Create a new article version by cloning the current one
                var newArticle = new Article
                {
                    Id = Guid.NewGuid(), // New ID for new version
                    ArticleNumber = currentArticle.ArticleNumber,
                    VersionNumber = currentArticle.VersionNumber + 1, // Increment version
                    Title = currentArticle.Title,
                    Content = updatedContent, // Updated content with new template
                    UrlPath = currentArticle.UrlPath,
                    StatusCode = currentArticle.StatusCode,
                    Published = currentArticle.Published, // Preserve published date
                    Expires = currentArticle.Expires,
                    Updated = clock.UtcNow,
                    HeaderJavaScript = currentArticle.HeaderJavaScript,
                    FooterJavaScript = currentArticle.FooterJavaScript,
                    BannerImage = currentArticle.BannerImage,
                    UserId = userId.ToString(),
                    TemplateId = currentArticle.TemplateId,
                    ArticleType = currentArticle.ArticleType,
                    Category = currentArticle.Category,
                    Introduction = currentArticle.Introduction,
                    RedirectTarget = currentArticle.RedirectTarget,
                    BlogKey = currentArticle.BlogKey
                };

                // Add the new version to the database
                dbContext.Articles.Add(newArticle);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Created article version {VersionNumber} for article {ArticleNumber} with template update",
                    newArticle.VersionNumber,
                    newArticle.ArticleNumber);

                // If it was published, republish the new version
                if (newArticle.Published.HasValue)
                {
                    await publishingService.PublishAsync(newArticle, cancellationToken);
                    logger.LogInformation(
                        "Republished article {ArticleNumber} version {VersionNumber} with template update",
                        newArticle.ArticleNumber,
                        newArticle.VersionNumber);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error applying template to article {ArticleNumber}",
                    articleNumber);
            }
        }

        /// <summary>
        /// Merges template content with existing article content, preserving editable regions.
        /// </summary>
        /// <param name="articleContent">Current article content.</param>
        /// <param name="templateContent">New template content.</param>
        /// <returns>Merged HTML content.</returns>
        private string MergeTemplateWithArticleContent(string articleContent, string templateContent)
        {
            var articleHtmlDoc = new HtmlDocument();
            var templateHtmlDoc = new HtmlDocument();

            articleHtmlDoc.LoadHtml(articleContent);
            templateHtmlDoc.LoadHtml(templateContent);

            // Pull out the editable DIVs from both
            var originalEditableDivs = articleHtmlDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]");
            var templateEditableDivs = templateHtmlDoc.DocumentNode.SelectNodes("//*[@data-ccms-ceid]");

            // Check for null before iterating
            if (templateEditableDivs != null && originalEditableDivs != null)
            {
                foreach (var templateDiv in templateEditableDivs)
                {
                    var ceid = templateDiv.Attributes["data-ccms-ceid"]?.Value;
                    if (ceid != null)
                    {
                        var originalDiv = originalEditableDivs.FirstOrDefault(
                            div => div.Attributes["data-ccms-ceid"]?.Value == ceid);

                        if (originalDiv != null)
                        {
                            // Preserve the original editable content
                            templateDiv.InnerHtml = originalDiv.InnerHtml;
                        }
                    }
                }
            }

            return templateHtmlDoc.DocumentNode.OuterHtml;
        }
    }
}