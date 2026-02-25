// <copyright file="CreateHomePageHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Editor.Features.Articles.CreateHomePage
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Slugs;

    /// <summary>
    /// Handler for CreateHomePageCommand. Reassigns the home page and republishes both old and new.
    /// </summary>
    public class CreateHomePageHandler : ICommandHandler<CreateHomePageCommand, CommandResult<Unit>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ISlugService slugService;
        private readonly IPublishingService publishingService;
        private readonly ICatalogService catalogService;
        private readonly IClock clock;
        private readonly ILogger<CreateHomePageHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateHomePageHandler"/> class.
        /// </summary>
        public CreateHomePageHandler(
            ApplicationDbContext dbContext,
            ISlugService slugService,
            IPublishingService publishingService,
            ICatalogService catalogService,
            IClock clock,
            ILogger<CreateHomePageHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.slugService = slugService ?? throw new ArgumentNullException(nameof(slugService));
            this.publishingService = publishingService ?? throw new ArgumentNullException(nameof(publishingService));
            this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the create home page command.
        /// </summary>
        public async Task<CommandResult<Unit>> HandleAsync(CreateHomePageCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = "Command cannot be null" };
            }

            try
            {
                // Find current home page
                var oldHomeArticles = await dbContext.Articles
                    .Where(w => w.UrlPath.ToLower() == "root")
                    .ToListAsync(cancellationToken);

                if (oldHomeArticles.Count == 0)
                {
                    logger.LogWarning("No existing home page found");
                    return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = "No existing home page found" };
                }

                // Find new home page article
                var newHomeArticles = await dbContext.Articles
                    .Where(w => w.ArticleNumber == command.ArticleNumber)
                    .ToListAsync(cancellationToken);

                if (newHomeArticles.Count == 0)
                {
                    logger.LogWarning("New home page article number {ArticleNumber} not found", command.ArticleNumber);
                    return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = $"Article number {command.ArticleNumber} not found" };
                }

                // Generate new URL for old home page
                var newUrl = slugService.Normalize(oldHomeArticles.First().Title);
                foreach (var article in oldHomeArticles)
                {
                    article.UrlPath = newUrl;
                }
                await dbContext.SaveChangesAsync(cancellationToken);

                // Reassign new home page to root
                foreach (var article in newHomeArticles)
                {
                    article.UrlPath = "root";
                }
                await dbContext.SaveChangesAsync(cancellationToken);

                // Get published versions
                var oldHome = oldHomeArticles
                    .OrderBy(o => o.VersionNumber)
                    .LastOrDefault(f => f.Published.HasValue);

                var newHome = newHomeArticles
                    .OrderBy(o => o.VersionNumber)
                    .LastOrDefault(f => f.Published.HasValue);

                // Republish both
                if (oldHome != null)
                {
                    oldHome.Published = clock.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await publishingService.PublishAsync(oldHome);
                    await catalogService.UpsertAsync(oldHome);
                }

                if (newHome != null)
                {
                    newHome.Published = clock.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await publishingService.PublishAsync(newHome);
                    await catalogService.UpsertAsync(newHome);
                }

                logger.LogInformation(
                    "Home page changed from article {OldArticleNumber} to {NewArticleNumber}",
                    oldHomeArticles.First().ArticleNumber,
                    command.ArticleNumber);

                return new CommandResult<Unit> { IsSuccess = true, Data = Unit.Value };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating home page for article {ArticleNumber}", command.ArticleNumber);
                return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = $"Error creating home page: {ex.Message}" };
            }
        }
    }
}
