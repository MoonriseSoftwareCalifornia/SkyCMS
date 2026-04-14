// <copyright file="DeleteBlogPostCommandHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.DeletePost
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Handler for deleting a blog post.
    /// Performs a soft delete by marking all versions of the article as deleted.
    /// </summary>
    public class DeleteBlogPostCommandHandler : ICommandHandler<DeleteBlogPostCommand, CommandResult<DeleteBlogPostCommandResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<DeleteBlogPostCommandHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteBlogPostCommandHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="logger">Logger instance.</param>
        public DeleteBlogPostCommandHandler(
            ApplicationDbContext dbContext,
            ILogger<DeleteBlogPostCommandHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the delete blog post command.
        /// </summary>
        /// <param name="command">The command containing blog post deletion details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result indicating successful deletion.</returns>
        public async Task<CommandResult<DeleteBlogPostCommandResult>> HandleAsync(
            DeleteBlogPostCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            // Validation
            if (command.ArticleNumber <= 0)
            {
                logger.LogWarning("DeleteBlogPost called with invalid ArticleNumber: {ArticleNumber}", command.ArticleNumber);
                return CommandResult<DeleteBlogPostCommandResult>.Failure("Valid article number is required.");
            }

            if (command.UserId == Guid.Empty)
            {
                logger.LogWarning("DeleteBlogPost called with empty UserId");
                return CommandResult<DeleteBlogPostCommandResult>.Failure("User ID is required.");
            }

            if (string.IsNullOrWhiteSpace(command.BlogKey))
            {
                logger.LogWarning("DeleteBlogPost called with empty BlogKey");
                return CommandResult<DeleteBlogPostCommandResult>.Failure("Blog key is required.");
            }

            try
            {
                // Get all versions of this blog post
                var blogPostType = (int)ArticleType.BlogPost;
                var deletedStatusCode = (int)StatusCodeEnum.Deleted;
                var articleVersions = await dbContext.Articles
                    .Where(a => a.ArticleNumber == command.ArticleNumber &&
                                a.ArticleType == blogPostType &&
                                a.BlogKey == command.BlogKey &&
                                a.StatusCode != deletedStatusCode)
                    .ToListAsync(cancellationToken);

                if (articleVersions.Count == 0)
                {
                    logger.LogWarning(
                        "Blog post with ArticleNumber {ArticleNumber} and BlogKey {BlogKey} not found",
                        command.ArticleNumber,
                        command.BlogKey);
                    return CommandResult<DeleteBlogPostCommandResult>.Failure(
                        $"Blog post not found.");
                }

                // Mark all versions as deleted
                foreach (var version in articleVersions)
                {
                    version.StatusCode = (int)StatusCodeEnum.Deleted;
                    version.Updated = DateTimeOffset.UtcNow;
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Successfully deleted blog post {ArticleNumber} from blog stream {BlogKey} ({VersionCount} versions marked as deleted)",
                    command.ArticleNumber,
                    command.BlogKey,
                    articleVersions.Count);

                return CommandResult<DeleteBlogPostCommandResult>.Success(
                    new DeleteBlogPostCommandResult
                    {
                        ArticleNumber = command.ArticleNumber,
                        Message = $"Blog post and all {articleVersions.Count} version(s) deleted successfully."
                    });
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Database error deleting blog post {ArticleNumber}", command.ArticleNumber);
                return CommandResult<DeleteBlogPostCommandResult>.Failure(
                    $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error deleting blog post {ArticleNumber}", command.ArticleNumber);
                return CommandResult<DeleteBlogPostCommandResult>.Failure(
                    $"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}
