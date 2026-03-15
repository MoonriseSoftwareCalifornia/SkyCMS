// <copyright file="DeleteArticleHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Editor.Features.Articles.Delete
{
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Publishing;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler for DeleteArticleCommand. Soft-deletes an article and removes artifacts.
    /// </summary>
    public class DeleteArticleHandler : ICommandHandler<DeleteArticleCommand, CommandResult<Unit>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IPublishingService publishingService;
        private readonly IStorageContext storageContext;
        private readonly IEditorSettings settings;
        private readonly ILogger<DeleteArticleHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteArticleHandler"/> class.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="publishingService">The publishing service.</param>
        /// <param name="storageContext">The storage context.</param>
        /// <param name="settings">The editor settings.</param>
        /// <param name="logger">The logger.</param>
        public DeleteArticleHandler(
            ApplicationDbContext dbContext,
            IPublishingService publishingService,
            IStorageContext storageContext,
            IEditorSettings settings,
            ILogger<DeleteArticleHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.publishingService = publishingService ?? throw new ArgumentNullException(nameof(publishingService));
            this.storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the delete article command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The command result.</returns>
        public async Task<CommandResult<Unit>> HandleAsync(DeleteArticleCommand command, CancellationToken cancellationToken = default)
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
                    logger.LogWarning("Article number {ArticleNumber} not found for deletion", command.ArticleNumber);
                    return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = $"Article number {command.ArticleNumber} not found" };
                }

                // Prevent deletion of home page
                if (articles.Exists(a => a.UrlPath.Equals("root", StringComparison.OrdinalIgnoreCase)))
                {
                    logger.LogWarning("Attempted to delete home page (article number {ArticleNumber})", command.ArticleNumber);
                    return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = "Cannot trash the home page. Replace it then delete." };
                }

                var urlPath = articles.FirstOrDefault()?.UrlPath;

                // Mark all versions as deleted
                foreach (var article in articles)
                {
                    article.StatusCode = (int)StatusCodeEnum.Deleted;
                }

                // Remove related pages
                var relatedPages = await dbContext.Pages
                    .Where(w => w.ArticleNumber == command.ArticleNumber)
                    .ToListAsync(cancellationToken);
                dbContext.Pages.RemoveRange(relatedPages);

                await dbContext.SaveChangesAsync(cancellationToken);

                // Clean up catalog and artifacts
                await DeleteCatalogEntry(command.ArticleNumber, cancellationToken);
                DeleteStaticWebpage(urlPath);
                await publishingService.WriteTocAsync();

                logger.LogInformation(
                    "Article number {ArticleNumber} deleted successfully",
                    command.ArticleNumber);

                return new CommandResult<Unit> { IsSuccess = true, Data = Unit.Value };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting article {ArticleNumber}", command.ArticleNumber);
                return new CommandResult<Unit> { IsSuccess = false, ErrorMessage = $"Error deleting article: {ex.Message}" };
            }
        }

        private void DeleteStaticWebpage(string filePath)
        {
            if (!settings.StaticWebPages)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (filePath.StartsWith("/pub", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot remove web page from path /pub.");
            }

            filePath = filePath.Equals("root", StringComparison.OrdinalIgnoreCase) ? "/index.html" : filePath;
            storageContext.DeleteFile(filePath);
        }

        private async Task DeleteCatalogEntry(int articleNumber, CancellationToken cancellationToken)
        {
            var catalogEntry = await dbContext.ArticleCatalog
                .FirstOrDefaultAsync(f => f.ArticleNumber == articleNumber, cancellationToken);
            if (catalogEntry != null)
            {
                dbContext.ArticleCatalog.Remove(catalogEntry);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
