// <copyright file="SaveArticleHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Save
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Titles;

    /// <summary>
    /// Handles saving (updating) existing articles with full workflow coordination.
    /// Orchestrates HTML processing, title change tracking, catalog updates, and CDN publishing.
    /// </summary>
    public class SaveArticleHandler : ICommandHandler<SaveArticleCommand, CommandResult<ArticleUpdateResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IArticleHtmlService htmlService;
        private readonly ICatalogService catalogService;
        private readonly IPublishingService publishingService;
        private readonly ITitleChangeService titleChangeService;
        private readonly IClock clock;
        private readonly ILogger<SaveArticleHandler> logger;
        private readonly SaveArticleValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SaveArticleHandler"/> class.
        /// </summary>
        /// <param name="dbContext">The database context for article persistence.</param>
        /// <param name="htmlService">Service for processing and validating HTML content.</param>
        /// <param name="catalogService">Service for managing article catalog entries.</param>
        /// <param name="publishingService">Service for publishing articles to CDN.</param>
        /// <param name="titleChangeService">Service for handling article title changes and redirects.</param>
        /// <param name="clock">Abstraction for current time (supports testing).</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public SaveArticleHandler(
            ApplicationDbContext dbContext,
            IArticleHtmlService htmlService,
            ICatalogService catalogService,
            IPublishingService publishingService,
            ITitleChangeService titleChangeService,
            IClock clock,
            ILogger<SaveArticleHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.htmlService = htmlService ?? throw new ArgumentNullException(nameof(htmlService));
            this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            this.publishingService = publishingService ?? throw new ArgumentNullException(nameof(publishingService));
            this.titleChangeService = titleChangeService ?? throw new ArgumentNullException(nameof(titleChangeService));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            validator = new SaveArticleValidator();
        }

        /// <summary>
        /// Handles the save article command by updating an existing article in the database.
        /// Processes HTML content, tracks title changes, updates the catalog, and publishes to CDN if applicable.
        /// </summary>
        /// <param name="command">The command containing article update data.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="CommandResult{T}"/> containing the updated article or validation errors.</returns>
        /// <exception cref="InvalidOperationException">Thrown when business validation fails (e.g., duplicate titles).</exception>
        public async Task<CommandResult<ArticleUpdateResult>> HandleAsync(
            SaveArticleCommand command,
            CancellationToken cancellationToken = default)
        {
            // Validate
            var validationErrors = validator.Validate(command);
            if (validationErrors.Any())
            {
                return CommandResult<ArticleUpdateResult>.Failure(validationErrors);
            }

            Article? currentArticle = null;
            
            try
            {
                logger.LogInformation(
                    "Saving article {ArticleNumber} '{Title}' by user {UserId}",
                    command.ArticleNumber,
                    command.Title,
                    command.UserId);

                // Get latest version of the article
                currentArticle = await dbContext.Articles
                    .Where(a => a.ArticleNumber == command.ArticleNumber)
                    .OrderByDescending(o => o.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken);

                if (currentArticle == null)
                {
                    logger.LogWarning("Article {ArticleNumber} not found", command.ArticleNumber);
                    return CommandResult<ArticleUpdateResult>.Failure($"Article {command.ArticleNumber} not found.");
                }

                // Capture old title and URL path BEFORE making changes
                var oldTitle = currentArticle.Title;
                var oldUrlPath = currentArticle.UrlPath;

                var normalizedContent = command.Content ?? string.Empty;
                var normalizedHeadJavaScript = command.HeadJavaScript ?? string.Empty;
                var normalizedFooterJavaScript = command.FooterJavaScript ?? string.Empty;
                var normalizedBannerImage = command.BannerImage ?? string.Empty;
                var normalizedCategory = command.Category ?? string.Empty;
                var normalizedIntroduction = command.Introduction ?? string.Empty;

                // Process HTML content
                var processedContent = htmlService.EnsureEditableMarkers(normalizedContent);
                htmlService.EnsureAngularBase(normalizedHeadJavaScript, command.UrlPath ?? currentArticle.UrlPath);

                // Update the existing article in-place
                currentArticle.Content = processedContent;
                currentArticle.Title = command.Title.Trim();
                currentArticle.Updated = clock.UtcNow;
                currentArticle.HeaderJavaScript = normalizedHeadJavaScript;
                currentArticle.FooterJavaScript = normalizedFooterJavaScript;
                currentArticle.BannerImage = normalizedBannerImage;
                currentArticle.UserId = command.UserId.ToString();
                currentArticle.ArticleType = (int)command.ArticleType;
                currentArticle.Category = normalizedCategory;
                currentArticle.Introduction = normalizedIntroduction;
                currentArticle.Published = command.Published;
                currentArticle.UrlPath = command.UrlPath ?? currentArticle.UrlPath;
                currentArticle.VersionNumber++; // Increment version on each save

                // Check if title is changing
                var isTitleChanging = !oldTitle.Equals(currentArticle.Title);

                // Only save immediately if title is NOT changing
                // If title is changing, TitleChangeService will handle the save within its transaction
                if (!isTitleChanging)
                {
                    // Save with concurrency handling
                    var saved = await SaveWithRetryAsync(currentArticle, cancellationToken);
                    if (!saved)
                    {
                        return CommandResult<ArticleUpdateResult>.Failure("Failed to save article due to concurrent modification.");
                    }
                }

                // Handle title change with BOTH old title and old URL path
                if (isTitleChanging)
                {
                    logger.LogInformation(
                        "Title changed from '{OldTitle}' to '{NewTitle}' for article {ArticleNumber}",
                        oldTitle,
                        currentArticle.Title,
                        currentArticle.ArticleNumber);

                    // TitleChangeService will save changes within its transaction
                    await titleChangeService.HandleTitleChangeAsync(currentArticle, oldTitle, oldUrlPath);
                    
                    // No need for additional SaveChangesAsync - TitleChangeService handles it
                }

                // Update catalog
                await catalogService.UpsertAsync(currentArticle, cancellationToken);

                // Publish if needed
                var cdnResults = new List<CdnResult>();
                if (currentArticle.Published.HasValue)
                {
                    cdnResults = await publishingService.PublishAsync(currentArticle, cancellationToken);
                }

                logger.LogInformation(
                    "Successfully saved article {ArticleNumber} version {VersionNumber}",
                    currentArticle.ArticleNumber,
                    currentArticle.VersionNumber);

                // Build result
                var viewModel = MapToViewModel(currentArticle, command);
                var result = new ArticleUpdateResult
                {
                    ServerSideSuccess = true,
                    Model = viewModel,
                    CdnResults = cdnResults
                };

                return CommandResult<ArticleUpdateResult>.Success(result);
            }
            catch (Exception ex)
            {
                // Rethrow business validation exceptions that should propagate to the caller
                if (ex is InvalidOperationException)
                {
                    // For in-memory databases that don't support transactions, we need to explicitly
                    // reload the entity to discard tracked changes
                    if (currentArticle != null)
                    {
                        var entry = dbContext.Entry(currentArticle);
                        if (entry.State != EntityState.Detached)
                        {
                            try
                            {
                                await entry.ReloadAsync(cancellationToken);
                            }
                            catch
                            {
                                // If reload fails, at least detach the entity
                                entry.State = EntityState.Detached;
                            }
                        }
                    }
                    
                    throw;
                }

                logger.LogError(
                    ex,
                    "Error saving article {ArticleNumber} '{Title}'",
                    command.ArticleNumber,
                    command.Title);

                return CommandResult<ArticleUpdateResult>.Failure("An error occurred while saving the article.");
            }
        }

        /// <summary>
        /// Saves the article with retry logic for concurrency conflicts.
        /// Attempts up to two saves, reloading the entity on the first concurrency exception.
        /// </summary>
        /// <param name="article">The article entity to save.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns><c>true</c> if the article was saved successfully; otherwise, <c>false</c>.</returns>
        /// <exception cref="DbUpdateConcurrencyException">Thrown if concurrency conflicts persist after retry.</exception>
        private async Task<bool> SaveWithRetryAsync(Article article, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (attempt == 1)
                    {
                        logger.LogError(ex, "Concurrency conflict saving article {ArticleNumber}", article.ArticleNumber);
                        throw;
                    }

                    logger.LogWarning("Concurrency conflict on attempt {Attempt}, retrying...", attempt + 1);
                    await dbContext.Entry(article).ReloadAsync(cancellationToken);
                }
            }

            return false;
        }

        /// <summary>
        /// Maps an Article entity to an ArticleViewModel for presentation.
        /// </summary>
        /// <param name="article">The article entity to map.</param>
        /// <param name="command">The original command containing additional metadata.</param>
        /// <returns>An <see cref="ArticleViewModel"/> populated with article data.</returns>
        private static ArticleViewModel MapToViewModel(Article article, SaveArticleCommand command)
        {
            return new ArticleViewModel
            {
                Id = article.Id,
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Content = article.Content,
                UrlPath = article.UrlPath,
                HeadJavaScript = article.HeaderJavaScript,
                FooterJavaScript = article.FooterJavaScript,
                BannerImage = article.BannerImage,
                ArticleType = command.ArticleType,
                Category = article.Category,
                Introduction = article.Introduction,
                Published = article.Published,
                Updated = article.Updated,
                VersionNumber = article.VersionNumber,
                StatusCode = (StatusCodeEnum)article.StatusCode
            };
        }
    }
}
