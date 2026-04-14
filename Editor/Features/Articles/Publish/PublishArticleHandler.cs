// <copyright file="PublishArticleHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Publish
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.Publishing;

    /// <summary>
    /// Handler for PublishArticleCommand. Publishes an article version and updates catalog.
    /// </summary>
    public class PublishArticleHandler : ICommandHandler<PublishArticleCommand, CommandResult<PublishArticleCommandResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IClock clock;
        private readonly IPublishingService publishingService;
        private readonly ICatalogService catalogService;
        private readonly ILogger<PublishArticleHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishArticleHandler"/> class.
        /// </summary>
        public PublishArticleHandler(
            ApplicationDbContext dbContext,
            IClock clock,
            IPublishingService publishingService,
            ICatalogService catalogService,
            ILogger<PublishArticleHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.publishingService = publishingService ?? throw new ArgumentNullException(nameof(publishingService));
            this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the publish article command.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<CommandResult<PublishArticleCommandResult>> HandleAsync(PublishArticleCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return new CommandResult<PublishArticleCommandResult> { IsSuccess = false, ErrorMessage = "Command cannot be null" };
            }

            try
            {
                var article = await dbContext.Articles.FirstOrDefaultAsync(a => a.Id == command.ArticleId, cancellationToken);

                if (article == null)
                {
                    logger.LogWarning("Article with ID {ArticleId} not found for publishing", command.ArticleId);
                    return new CommandResult<PublishArticleCommandResult> { IsSuccess = false, ErrorMessage = $"Article with ID {command.ArticleId} not found" };
                }

                // Set the publish timestamp
                var publishTime = command.PublishTime ?? clock.UtcNow;
                article.Published = publishTime;

                // Save the article with updated publish time
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Article {ArticleNumber} version {VersionNumber} published at {PublishedTime}",
                    article.ArticleNumber,
                    article.VersionNumber,
                    publishTime);

                // Publish to CDN and update catalog
                var cdnResults = await publishingService.PublishAsync(article);
                await catalogService.UpsertAsync(article);

                return new CommandResult<PublishArticleCommandResult>
                {
                    IsSuccess = true,
                    Data = new PublishArticleCommandResult
                    {
                        CdnResults = cdnResults ?? new List<Sky.Editor.Services.CDN.CdnResult>()
                    }
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing article {ArticleId}", command.ArticleId);
                return new CommandResult<PublishArticleCommandResult> { IsSuccess = false, ErrorMessage = $"Error publishing article: {ex.Message}" };
            }
        }
    }
}
