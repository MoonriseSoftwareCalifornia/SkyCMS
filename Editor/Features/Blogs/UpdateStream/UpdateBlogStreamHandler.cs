// <copyright file="UpdateBlogStreamHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.UpdateStream
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Shared;
    using Sky.Editor.Services.BlogPublishing;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Titles;

    /// <summary>
    /// Handler for updating blog stream metadata and properties.
    /// Coordinates title changes, URL updates, blog rendering, and publishing.
    /// </summary>
    public class UpdateBlogStreamHandler : ICommandHandler<UpdateBlogStreamCommand, CommandResult<Article>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ISlugService slugService;
        private readonly ITitleChangeService titleChangeService;
        private readonly IBlogRenderingService blogRenderingService;
        private readonly ArticleEditLogic articleLogic;
        private readonly ILogger<UpdateBlogStreamHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateBlogStreamHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="slugService">Slug normalization service.</param>
        /// <param name="titleChangeService">Title change tracking service.</param>
        /// <param name="blogRenderingService">Blog stream HTML rendering service.</param>
        /// <param name="articleLogic">Article publishing logic.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public UpdateBlogStreamHandler(
            ApplicationDbContext dbContext,
            ISlugService slugService,
            ITitleChangeService titleChangeService,
            IBlogRenderingService blogRenderingService,
            ArticleEditLogic articleLogic,
            ILogger<UpdateBlogStreamHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.slugService = slugService ?? throw new ArgumentNullException(nameof(slugService));
            this.titleChangeService = titleChangeService ?? throw new ArgumentNullException(nameof(titleChangeService));
            this.blogRenderingService = blogRenderingService ?? throw new ArgumentNullException(nameof(blogRenderingService));
            this.articleLogic = articleLogic ?? throw new ArgumentNullException(nameof(articleLogic));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the update blog stream command.
        /// </summary>
        /// <param name="command">Update command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with updated article.</returns>
        public async Task<CommandResult<Article>> HandleAsync(
            UpdateBlogStreamCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            // Validation
            if (command.Id == Guid.Empty)
            {
                logger.LogWarning("UpdateBlogStream called with empty ID");
                return CommandResult<Article>.Failure("Blog stream ID is required.");
            }

            if (string.IsNullOrWhiteSpace(command.Title))
            {
                logger.LogWarning("UpdateBlogStream called with empty Title for blog {Id}", command.Id);
                return CommandResult<Article>.Failure("Blog stream title is required.");
            }

            try
            {
                // Retrieve the article
                var article = await dbContext.Articles
                    .FirstOrDefaultAsync(
                        f => f.Id == command.Id && 
                             f.ArticleType == (int)ArticleType.BlogStream &&
                             f.StatusCode != (int)StatusCodeEnum.Deleted, 
                        cancellationToken);

                if (article == null)
                {
                    logger.LogWarning("Blog stream {Id} not found for update", command.Id);
                    return CommandResult<Article>.Failure($"Blog stream with ID '{command.Id}' not found.");
                }

                // Validate title change (if title changed)
                if (!article.Title.Equals(command.Title, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (!await titleChangeService.ValidateTitle(command.Title, null))
                    {
                        logger.LogWarning(
                            "Title validation failed for blog stream {Id}: '{NewTitle}'",
                            command.Id,
                            command.Title);
                        return CommandResult<Article>.Failure("Blog key conflicts with existing page on this website.");
                    }
                }

                // Save old values for title change tracking
                var oldTitle = article.Title;
                var oldUrlPath = article.UrlPath;

                // Update article properties
                article.Title = command.Title.Trim();
                article.UrlPath = slugService.Normalize(command.Title);
                article.Introduction = command.Description ?? string.Empty;
                article.BannerImage = command.HeroImage ?? string.Empty;
                article.Published = command.Published;
                article.UserId = command.UserId.ToString();

                // Regenerate blog stream HTML
                article.Content = await blogRenderingService.GenerateBlogStreamHtml(article);

                // Save changes
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Successfully updated blog stream {Id} (Title: {Title}, BlogKey: {BlogKey})",
                    command.Id,
                    article.Title,
                    article.BlogKey);

                // Handle title change (creates redirects, updates catalog)
                if (oldTitle != article.Title)
                {
                    await titleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
                    logger.LogInformation(
                        "Processed title change for blog stream {Id}: '{OldTitle}' -> '{NewTitle}'",
                        command.Id,
                        oldTitle,
                        article.Title);
                }

                // Handle publishing
                if (article.Published.HasValue)
                {
                    await articleLogic.PublishArticle(article.Id, article.Published.Value);
                    logger.LogInformation(
                        "Published blog stream {Id} with publish date {Published}",
                        command.Id,
                        article.Published.Value);
                }

                return CommandResult<Article>.Success(article);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(
                    ex,
                    "Database error updating blog stream {Id}",
                    command.Id);
                return CommandResult<Article>.Failure($"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected error updating blog stream {Id}",
                    command.Id);
                return CommandResult<Article>.Failure($"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}
