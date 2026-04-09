// <copyright file="DeleteBlogStreamHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.DeleteStream
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Data.Logic;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler for deleting blog streams with cascade deletion of all associated blog entries.
    /// Ensures all blog posts within the stream are removed before deleting the stream itself.
    /// </summary>
    public class DeleteBlogStreamHandler : ICommandHandler<DeleteBlogStreamCommand, CommandResult<bool>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ArticleEditLogic articleLogic;
        private readonly ILogger<DeleteBlogStreamHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteBlogStreamHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="articleLogic">Article deletion logic service.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public DeleteBlogStreamHandler(
            ApplicationDbContext dbContext,
            ArticleEditLogic articleLogic,
            ILogger<DeleteBlogStreamHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.articleLogic = articleLogic ?? throw new ArgumentNullException(nameof(articleLogic));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the delete blog stream command with cascade deletion.
        /// </summary>
        /// <param name="command">Delete command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result indicating success or failure.</returns>
        public async Task<CommandResult<bool>> HandleAsync(
            DeleteBlogStreamCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            // Validation
            if (command.Id == Guid.Empty)
            {
                logger.LogWarning("DeleteBlogStream called with empty ID");
                return CommandResult<bool>.Failure("Blog stream ID is required.");
            }

            try
            {
                // Retrieve the blog stream article
                var blogStreamType = (int)ArticleType.BlogStream;
                var deletedStatusCode = (int)StatusCodeEnum.Deleted;
                var article = await dbContext.Articles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        b => b.Id == command.Id &&
                             b.ArticleType == blogStreamType &&
                             b.StatusCode != deletedStatusCode,
                        cancellationToken);

                if (article == null)
                {
                    logger.LogWarning("Blog stream {Id} not found for deletion", command.Id);
                    return CommandResult<bool>.Failure($"Blog stream with ID '{command.Id}' not found.");
                }

                var blogKey = article.BlogKey;
                var streamArticleNumber = article.ArticleNumber;

                logger.LogInformation(
                    "Starting deletion of blog stream {Id} (BlogKey: {BlogKey}, ArticleNumber: {ArticleNumber})",
                    command.Id,
                    blogKey,
                    streamArticleNumber);

                // Find all blog entries (excluding the stream article itself)
                var entryArticleNumbers = await dbContext.Articles
                    .Where(c => c.BlogKey == blogKey &&
                                c.ArticleNumber != streamArticleNumber &&
                                c.StatusCode != deletedStatusCode)
                    .Select(c => c.ArticleNumber)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                logger.LogInformation(
                    "Found {Count} blog entries to delete for stream {BlogKey}",
                    entryArticleNumbers.Count,
                    blogKey);

                // Delete each blog entry
                int deletedEntries = 0;
                foreach (var entryNumber in entryArticleNumbers)
                {
                    try
                    {
                        await articleLogic.DeleteArticle(entryNumber);
                        deletedEntries++;

                        logger.LogDebug(
                            "Deleted blog entry {ArticleNumber} from stream {BlogKey}",
                            entryNumber,
                            blogKey);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Error deleting blog entry {ArticleNumber} from stream {BlogKey}",
                            entryNumber,
                            blogKey);
                        // Continue with other entries even if one fails
                    }
                }

                logger.LogInformation(
                    "Successfully deleted {DeletedCount} of {TotalCount} blog entries for stream {BlogKey}",
                    deletedEntries,
                    entryArticleNumbers.Count,
                    blogKey);

                // Delete the blog stream article itself
                await articleLogic.DeleteArticle(streamArticleNumber);

                logger.LogInformation(
                    "Successfully deleted blog stream {Id} (BlogKey: {BlogKey}) and {EntryCount} entries",
                    command.Id,
                    blogKey,
                    deletedEntries);

                return CommandResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error deleting blog stream {Id}",
                    command.Id);
                return CommandResult<bool>.Failure($"Error deleting blog stream: {ex.Message}");
            }
        }
    }
}
