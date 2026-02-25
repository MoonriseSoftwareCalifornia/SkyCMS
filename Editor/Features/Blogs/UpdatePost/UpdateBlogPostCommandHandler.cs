// <copyright file="UpdateBlogPostCommandHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.UpdatePost
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
    /// Handler for updating an existing blog post.
    /// Creates a new version of the article with updated metadata and content.
    /// Handles publishing independently if requested.
    /// </summary>
    public class UpdateBlogPostCommandHandler : ICommandHandler<UpdateBlogPostCommand, CommandResult<UpdateBlogPostCommandResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<UpdateBlogPostCommandHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateBlogPostCommandHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="logger">Logger instance.</param>
        public UpdateBlogPostCommandHandler(
            ApplicationDbContext dbContext,
            ILogger<UpdateBlogPostCommandHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the update blog post command.
        /// </summary>
        /// <param name="command">The command containing blog post updates.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result containing the updated blog post details.</returns>
        public async Task<CommandResult<UpdateBlogPostCommandResult>> HandleAsync(
            UpdateBlogPostCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            // Validation
            if (command.ArticleNumber <= 0)
            {
                logger.LogWarning("UpdateBlogPost called with invalid ArticleNumber: {ArticleNumber}", command.ArticleNumber);
                return CommandResult<UpdateBlogPostCommandResult>.Failure("Valid article number is required.");
            }

            if (command.UserId == Guid.Empty)
            {
                logger.LogWarning("UpdateBlogPost called with empty UserId");
                return CommandResult<UpdateBlogPostCommandResult>.Failure("User ID is required.");
            }

            if (string.IsNullOrWhiteSpace(command.Title))
            {
                logger.LogWarning("UpdateBlogPost called with empty Title");
                return CommandResult<UpdateBlogPostCommandResult>.Failure("Blog post title is required.");
            }

            if (string.IsNullOrWhiteSpace(command.Content))
            {
                logger.LogWarning("UpdateBlogPost called with empty Content");
                return CommandResult<UpdateBlogPostCommandResult>.Failure("Blog post content is required.");
            }

            try
            {
                // Get the latest version of this blog post
                var currentArticle = await dbContext.Articles
                    .Where(a => a.ArticleNumber == command.ArticleNumber &&
                                a.ArticleType == (int)ArticleType.BlogPost &&
                                a.StatusCode != (int)StatusCodeEnum.Deleted)
                    .OrderByDescending(a => a.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken);

                if (currentArticle == null)
                {
                    logger.LogWarning("Blog post with ArticleNumber {ArticleNumber} not found", command.ArticleNumber);
                    return CommandResult<UpdateBlogPostCommandResult>.Failure(
                        $"Blog post with article number {command.ArticleNumber} not found.");
                }

                // Create new version
                var newVersion = new Article
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = currentArticle.ArticleNumber,
                    VersionNumber = currentArticle.VersionNumber + 1,
                    Title = command.Title.Trim(),
                    Content = command.Content,
                    Introduction = command.Introduction ?? string.Empty,
                    BannerImage = command.BannerImage ?? string.Empty,
                    UrlPath = currentArticle.UrlPath,  // Keep same URL path
                    BlogKey = currentArticle.BlogKey,  // Keep same blog key
                    ArticleType = currentArticle.ArticleType,
                    StatusCode = (int)StatusCodeEnum.Active,
                    TemplateId = currentArticle.TemplateId,
                    Published = command.Published,
                    UserId = command.UserId.ToString(),
                    Updated = DateTimeOffset.UtcNow
                };

                dbContext.Articles.Add(newVersion);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Successfully updated blog post {ArticleNumber} to version {VersionNumber}: '{Title}'",
                    newVersion.ArticleNumber,
                    newVersion.VersionNumber,
                    newVersion.Title);

                return CommandResult<UpdateBlogPostCommandResult>.Success(
                    new UpdateBlogPostCommandResult
                    {
                        Id = newVersion.Id,
                        ArticleNumber = newVersion.ArticleNumber,
                        VersionNumber = newVersion.VersionNumber,
                        UrlPath = newVersion.UrlPath
                    });
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Database error updating blog post {ArticleNumber}", command.ArticleNumber);
                return CommandResult<UpdateBlogPostCommandResult>.Failure(
                    $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error updating blog post {ArticleNumber}", command.ArticleNumber);
                return CommandResult<UpdateBlogPostCommandResult>.Failure(
                    $"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}
