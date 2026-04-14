// <copyright file="RestoreArticleHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Restore
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Slugs;

    /// <summary>
    /// Handler for RestoreArticleCommand. Restores a deleted article from trash.
    /// </summary>
    public class RestoreArticleHandler : ICommandHandler<RestoreArticleCommand, CommandResult<Unit>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ISlugService slugService;
        private readonly ILogger<RestoreArticleHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestoreArticleHandler"/> class.
        /// </summary>
        public RestoreArticleHandler(
            ApplicationDbContext dbContext,
            ISlugService slugService,
            ILogger<RestoreArticleHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.slugService = slugService ?? throw new ArgumentNullException(nameof(slugService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the restore article command.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<CommandResult<Unit>> HandleAsync(RestoreArticleCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = "Command cannot be null" };
            }

            try
            {
                var articles = await dbContext.Articles
                    .Where(w => w.ArticleNumber == command.ArticleNumber)
                    .ToListAsync(cancellationToken);

                if (articles == null || articles.Count == 0)
                {
                    logger.LogWarning("Article number {ArticleNumber} not found for restore", command.ArticleNumber);
                    return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = $"Article number {command.ArticleNumber} not found" };
                }

                // Check if article is deleted (only deleted articles can be restored)
                var firstArticle = articles.First();
                if (firstArticle.StatusCode != (int)StatusCodeEnum.Deleted)
                {
                    logger.LogWarning("Cannot restore article number {ArticleNumber} - not in deleted status", command.ArticleNumber);
                    return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = $"Article number {command.ArticleNumber} is not deleted and cannot be restored" };
                }

                var title = articles.First().Title.ToLower();

                // Check if title conflicts with another deleted article
                var deletedStatusCode = (int)StatusCodeEnum.Deleted;
                var titleConflict = await dbContext.Articles.Where(a =>
                    a.Title.ToLower() == title &&
                    a.ArticleNumber != command.ArticleNumber &&
                    a.StatusCode == deletedStatusCode).CosmosAnyAsync();

                if (titleConflict)
                {
                    var newTitle = title + " (" + await dbContext.Articles.CountAsync(cancellationToken: cancellationToken) + ")";
                    var url = slugService.Normalize(newTitle);
                    foreach (var article in articles)
                    {
                        article.Title = newTitle;
                        article.UrlPath = url;
                        article.StatusCode = (int)StatusCodeEnum.Active;
                        article.Published = null;
                    }
                }
                else
                {
                    foreach (var article in articles)
                    {
                        article.StatusCode = (int)StatusCodeEnum.Active;
                        article.Published = null;
                    }
                }

                var sample = articles.First();
                var existingCatalogEntry = await dbContext.ArticleCatalog
                    .FirstOrDefaultAsync(f => f.ArticleNumber == command.ArticleNumber, cancellationToken);

                if (existingCatalogEntry != null)
                {
                    dbContext.ArticleCatalog.Remove(existingCatalogEntry);
                }

                dbContext.ArticleCatalog.Add(new CatalogEntry
                {
                    ArticleNumber = sample.ArticleNumber,
                    Published = null,
                    Status = "Active",
                    Title = sample.Title,
                    Updated = DateTimeOffset.UtcNow,
                    UrlPath = sample.UrlPath
                });

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Article number {ArticleNumber} restored successfully by user {UserId}",
                    command.ArticleNumber,
                    command.UserId);

                return new CommandResult<Unit> { IsSuccess = true, Data = Unit.Value };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error restoring article {ArticleNumber}", command.ArticleNumber);
                return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = $"Error restoring article: {ex.Message}" };
            }
        }
    }
}
