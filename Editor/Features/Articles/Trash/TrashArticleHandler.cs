// <copyright file="TrashArticleHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Trash
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Publishing;

    /// <summary>
    /// Handles permanent trash operations for previously deleted articles.
    /// </summary>
    public class TrashArticleHandler : ICommandHandler<TrashArticleCommand, CommandResult<Unit>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IPublishingService publishingService;
        private readonly IStorageContext storageContext;
        private readonly ILogger<TrashArticleHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrashArticleHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="publishingService">Publishing service.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="logger">Logger.</param>
        public TrashArticleHandler(
            ApplicationDbContext dbContext,
            IPublishingService publishingService,
            IStorageContext storageContext,
            ILogger<TrashArticleHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.publishingService = publishingService ?? throw new ArgumentNullException(nameof(publishingService));
            this.storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Permanently removes a deleted article and related entities.
        /// </summary>
        /// <param name="command">Trash command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result.</returns>
        public async Task<CommandResult<Unit>> HandleAsync(TrashArticleCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return CommandResult<Unit>.Failure("Command cannot be null.");
            }

            try
            {
                var validator = new TrashArticleValidator(this.dbContext);
                var errors = await validator.ValidateAsync(command, cancellationToken);
                if (errors.Count > 0)
                {
                    return new CommandResult<Unit>
                    {
                        IsSuccess = false,
                        Errors = errors,
                        ErrorMessage = string.Join(", ", errors.SelectMany(e => e.Value))
                    };
                }

                var articles = await this.dbContext.Articles
                    .Where(a => a.ArticleNumber == command.ArticleNumber)
                    .ToListAsync(cancellationToken);

                var articleIds = articles.Select(a => a.Id).ToArray();

                var catalogEntries = await this.dbContext.ArticleCatalog
                    .Where(c => c.ArticleNumber == command.ArticleNumber)
                    .ToListAsync(cancellationToken);

                var pages = await this.dbContext.Pages
                    .Where(p => p.ArticleNumber == command.ArticleNumber)
                    .ToListAsync(cancellationToken);

                var locks = await this.dbContext.ArticleLocks
                    .Where(l => articleIds.Contains(l.ArticleId))
                    .ToListAsync(cancellationToken);

                var logs = await this.dbContext.ArticleLogs
                    .Where(l => articleIds.Contains(l.ArticleId))
                    .ToListAsync(cancellationToken);

                this.dbContext.ArticleLogs.RemoveRange(logs);
                this.dbContext.ArticleLocks.RemoveRange(locks);
                this.dbContext.Pages.RemoveRange(pages);
                this.dbContext.ArticleCatalog.RemoveRange(catalogEntries);
                this.dbContext.Articles.RemoveRange(articles);

                await this.dbContext.SaveChangesAsync(cancellationToken);

                await this.storageContext.DeleteFolderAsync($"/pub/articles/{command.ArticleNumber}");
                await this.publishingService.WriteTocAsync();

                this.logger.LogInformation(
                    "Permanently trashed article number {ArticleNumber}. Removed {VersionCount} version(s), {PageCount} page(s), {CatalogCount} catalog entry(ies), {LockCount} lock(s), and {LogCount} log(s).",
                    command.ArticleNumber,
                    articles.Count,
                    pages.Count,
                    catalogEntries.Count,
                    locks.Count,
                    logs.Count);

                return CommandResult<Unit>.Success(Unit.Value);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error permanently trashing article number {ArticleNumber}", command.ArticleNumber);
                return CommandResult<Unit>.Failure($"Error permanently trashing article: {ex.Message}");
            }
        }
    }
}
