// <copyright file="CreateArticleHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Create
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Services.Titles;

    /// <summary>
    /// Handles the creation of new articles.
    /// </summary>
    public class CreateArticleHandler : ICommandHandler<CreateArticleCommand, CommandResult<ArticleViewModel>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IArticleHtmlService htmlService;
        private readonly ICatalogService catalogService;
        private readonly IPublishingService publishingService;
        private readonly ITitleChangeService titleChangeService;
        private readonly IClock clock;
        private readonly ILogger<CreateArticleHandler> logger;
        private readonly CreateArticleValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateArticleHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="htmlService">HTML service.</param>
        /// <param name="catalogService">Catalog service.</param>
        /// <param name="publishingService">Publishing service.</param>
        /// <param name="titleChangeService">Title change service.</param>
        /// <param name="templateService">Template service.</param>
        /// <param name="clock">Clock service.</param>
        /// <param name="logger">Logger service.</param>
        public CreateArticleHandler(
            ApplicationDbContext dbContext,
            IArticleHtmlService htmlService,
            ICatalogService catalogService,
            IPublishingService publishingService,
            ITitleChangeService titleChangeService,
            ITemplateService templateService,
            IClock clock,
            ILogger<CreateArticleHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.htmlService = htmlService ?? throw new ArgumentNullException(nameof(htmlService));
            this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            this.publishingService = publishingService ?? throw new ArgumentNullException(nameof(publishingService));
            this.titleChangeService = titleChangeService ?? throw new ArgumentNullException(nameof(titleChangeService));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            validator = new CreateArticleValidator();
        }

        /// <summary>
        /// Handles create article command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with an ArticleViewModel.</returns>
        public async Task<CommandResult<ArticleViewModel>> HandleAsync(
            CreateArticleCommand command,
            CancellationToken cancellationToken = default)
        {
            // Validate command structure (Title not empty, etc.)
            var validationErrors = validator.Validate(command);
            if (validationErrors.Any())
            {
                return CommandResult<ArticleViewModel>.Failure(validationErrors);
            }

            // Validate title doesn't conflict with existing articles or reserved paths
            var titleIsValid = await titleChangeService.ValidateTitle(command.Title, null);
            if (!titleIsValid)
            {
                logger.LogWarning(
                    "Article creation failed: Title '{Title}' conflicts with an existing article or reserved path",
                    command.Title);

                return CommandResult<ArticleViewModel>.Failure(
                    new Dictionary<string, string[]>
                    {
                        ["Title"] = new[] { $"Title '{command.Title}' conflicts with an existing article or reserved path." }
                    });
            }

            try
            {
                logger.LogInformation(
                    "Creating article '{Title}' (Type: {ArticleType}) for user {UserId}",
                    command.Title,
                    command.ArticleType,
                    command.UserId);

                var isFirstArticle = await dbContext.Articles.CountAsync(cancellationToken) == 0;
                var defaultTemplate = await GetTemplateContentAsync(command.TemplateId, cancellationToken);

                var nextArticleNumber = await GetNextArticleNumberAsync(isFirstArticle, cancellationToken);

                var title = command.Title.Trim('/');
                var now = clock.UtcNow;

                // Determine content: ContentOverride > Template > Default Lorem Ipsum
                var content = command.ContentOverride
                    ?? htmlService.EnsureEditableMarkers(defaultTemplate);

                // Determine published state: explicit > auto-publish first article > null
                var published = command.Published
                    ?? (isFirstArticle ? now : (DateTimeOffset?)null);

                // Determine status code: explicit > default Active
                var statusCode = (int)(command.StatusCode ?? StatusCodeEnum.Active);

                var article = new Article
                {
                    BlogKey = command.BlogKey,
                    ArticleNumber = nextArticleNumber,
                    ArticleType = (int)command.ArticleType,
                    Content = content,
                    StatusCode = statusCode,
                    Title = title,
                    Updated = now,
                    VersionNumber = 1,
                    Published = published,
                    UserId = command.UserId.ToString(),
                    TemplateId = command.TemplateId,

                    // Apply optional overrides (only properties that exist on Article entity)
                    Category = command.Category ?? string.Empty,
                    Introduction = command.Introduction ?? string.Empty,
                    BannerImage = command.BannerImage ?? string.Empty,
                    HeaderJavaScript = command.HeadJavaScript ?? string.Empty,
                    FooterJavaScript = command.FooterJavaScript ?? string.Empty
                };

                // Generate URL path: explicit override > "root" for first > generated from title
                article.UrlPath = command.UrlPathOverride
                    ?? (isFirstArticle ? "root" : titleChangeService.BuildArticleUrl(article));

                dbContext.Articles.Add(article);
                dbContext.ArticleNumbers.Add(new ArticleNumber { LastNumber = nextArticleNumber });

                await dbContext.SaveChangesAsync(cancellationToken);

                // Update catalog
                await catalogService.UpsertAsync(article, cancellationToken);

                // Auto-publish if needed
                if (article.Published.HasValue)
                {
                    await publishingService.PublishAsync(article);
                }

                logger.LogInformation(
                    "Successfully created article {ArticleNumber} with title '{Title}' (Type: {ArticleType}, Published: {Published})",
                    article.ArticleNumber,
                    article.Title,
                    command.ArticleType,
                    article.Published.HasValue);

                // Build view model (only Article entity properties)
                var viewModel = new ArticleViewModel
                {
                    Id = article.Id,
                    ArticleNumber = article.ArticleNumber,
                    Title = article.Title,
                    Content = article.Content,
                    UrlPath = article.UrlPath,
                    Published = article.Published,
                    Updated = article.Updated,
                    VersionNumber = article.VersionNumber,
                    StatusCode = (StatusCodeEnum)article.StatusCode,
                    ArticleType = command.ArticleType,
                    BannerImage = article.BannerImage,
                    Category = article.Category,
                    Introduction = article.Introduction,
                    HeadJavaScript = article.HeaderJavaScript,
                    FooterJavaScript = article.FooterJavaScript
                };

                return CommandResult<ArticleViewModel>.Success(viewModel);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating article '{Title}'", command.Title);
                return CommandResult<ArticleViewModel>.Failure("An error occurred while creating the article.");
            }
        }

        private async Task<string> GetTemplateContentAsync(Guid? templateId, CancellationToken cancellationToken)
        {
            if (!templateId.HasValue)
            {
                return GetDefaultLoremIpsumContent();
            }

            var template = await dbContext.Templates
                .FirstOrDefaultAsync(f => f.Id == templateId.Value, cancellationToken);

            if (template == null)
            {
                return GetDefaultLoremIpsumContent();
            }

            var content = htmlService.EnsureEditableMarkers(template.Content);
            if (!content.Equals(template.Content))
            {
                template.Content = content;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return template.Content;
        }

        private async Task<int> GetNextArticleNumberAsync(bool isFirstArticle, CancellationToken cancellationToken)
        {
            if (isFirstArticle)
            {
                return 1;
            }

            return await dbContext.ArticleNumbers.MaxAsync(m => m.LastNumber, cancellationToken) + 1;
        }

        private static string GetDefaultLoremIpsumContent() =>
            "<div style='width: 100%;padding-left: 20px;padding-right: 20px;margin-left: auto;margin-right: auto;'>" +
            "<div><h1>Why Lorem Ipsum?</h1><p>" +
            LoremIpsum.WhyLoremIpsum + "</p></div></div>";
    }
}
