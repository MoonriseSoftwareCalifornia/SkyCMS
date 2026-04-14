// <copyright file="CreateBlogPostCommandHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.CreatePost
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Features.Articles.Create;

    /// <summary>
    /// Handler for creating a new blog post within a blog stream.
    /// Validates that the blog stream exists, then delegates to the shared article creation pipeline.
    /// </summary>
    public class CreateBlogPostCommandHandler : ICommandHandler<CreateBlogPostCommand, CommandResult<CreateBlogPostCommandResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IMediator mediator;
        private readonly ILogger<CreateBlogPostCommandHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateBlogPostCommandHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="mediator">Mediator instance.</param>
        /// <param name="logger">Logger instance.</param>
        public CreateBlogPostCommandHandler(
            ApplicationDbContext dbContext,
            IMediator mediator,
            ILogger<CreateBlogPostCommandHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the create blog post command.
        /// </summary>
        /// <param name="command">The command containing blog post details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result containing the created blog post details.</returns>
        public async Task<CommandResult<CreateBlogPostCommandResult>> HandleAsync(
            CreateBlogPostCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (command.UserId == Guid.Empty)
            {
                logger.LogWarning("CreateBlogPost called with empty UserId");
                return CommandResult<CreateBlogPostCommandResult>.Failure("User ID is required.");
            }

            if (string.IsNullOrWhiteSpace(command.BlogKey))
            {
                logger.LogWarning("CreateBlogPost called with empty BlogKey");
                return CommandResult<CreateBlogPostCommandResult>.Failure("Blog key is required.");
            }

            if (string.IsNullOrWhiteSpace(command.Title))
            {
                logger.LogWarning("CreateBlogPost called with empty Title");
                return CommandResult<CreateBlogPostCommandResult>.Failure("Blog post title is required.");
            }

            try
            {
                var blogStreamType = (int)ArticleType.BlogStream;
                var statusCode = (int)StatusCodeEnum.Deleted;
                var blogKey = command.BlogKey.Trim();
                var parentStream = await dbContext.Articles
                    .FirstOrDefaultAsync(
                        a => a.BlogKey == blogKey &&
                             a.ArticleType == blogStreamType &&
                             a.StatusCode != statusCode,
                        cancellationToken);

                if (parentStream == null)
                {
                    logger.LogWarning("Parent blog stream not found for BlogKey: {BlogKey}", command.BlogKey);
                    return CommandResult<CreateBlogPostCommandResult>.Failure(
                        $"Blog stream '{command.BlogKey}' not found.");
                }

                logger.LogInformation(
                    "Creating blog post '{Title}' in stream {BlogKey} for user {UserId}",
                    command.Title,
                    command.BlogKey,
                    command.UserId);

                var createArticleCommand = new CreateArticleCommand
                {
                    Title = command.Title,
                    UserId = command.UserId,
                    TemplateId = command.TemplateId == Guid.Empty ? null : command.TemplateId,
                    BlogKey = command.BlogKey,
                    ArticleType = ArticleType.BlogPost,
                    Introduction = command.Introduction,
                    BannerImage = command.BannerImage,
                    ContentOverride = command.Content,
                    Published = command.Published
                };

                var createResult = await mediator.SendAsync<CommandResult<ArticleViewModel>>(createArticleCommand, cancellationToken);

                if (!createResult.IsSuccess || createResult.Data == null)
                {
                    return CommandResult<CreateBlogPostCommandResult>.Failure(
                        createResult.Errors ?? new Dictionary<string, string[]>
                        {
                            ["general"] = new[] { createResult.ErrorMessage ?? "Failed to create blog post." }
                        });
                }

                var createdArticle = createResult.Data;

                logger.LogInformation(
                    "Successfully created blog post {PostId} in stream {BlogKey}: '{Title}' (UrlPath: {UrlPath})",
                    createdArticle.Id,
                    command.BlogKey,
                    createdArticle.Title,
                    createdArticle.UrlPath);

                return CommandResult<CreateBlogPostCommandResult>.Success(
                    new CreateBlogPostCommandResult
                    {
                        Id = createdArticle.Id,
                        ArticleNumber = createdArticle.ArticleNumber,
                        UrlPath = createdArticle.UrlPath,
                        BlogKey = command.BlogKey
                    });
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Database error creating blog post in stream {BlogKey}", command.BlogKey);
                return CommandResult<CreateBlogPostCommandResult>.Failure(
                    $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error creating blog post in stream {BlogKey}", command.BlogKey);
                return CommandResult<CreateBlogPostCommandResult>.Failure(
                    "An unexpected error occurred while creating the blog post.");
            }
        }
    }
}
