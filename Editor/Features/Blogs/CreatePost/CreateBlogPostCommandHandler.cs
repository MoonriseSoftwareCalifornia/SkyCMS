// <copyright file="CreateBlogPostCommandHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.CreatePost
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Slugs;

    /// <summary>
    /// Handler for creating a new blog post within a blog stream.
    /// Validates that the blog stream exists, creates the article entity, and sets initial metadata.
    /// </summary>
    public class CreateBlogPostCommandHandler : ICommandHandler<CreateBlogPostCommand, CommandResult<CreateBlogPostCommandResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ISlugService slugService;
        private readonly ILogger<CreateBlogPostCommandHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateBlogPostCommandHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="slugService">Service for normalizing URLs and titles to slugs.</param>
        /// <param name="logger">Logger instance.</param>
        public CreateBlogPostCommandHandler(
            ApplicationDbContext dbContext,
            ISlugService slugService,
            ILogger<CreateBlogPostCommandHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.slugService = slugService ?? throw new ArgumentNullException(nameof(slugService));
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

            // Validation
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
                // Verify the parent blog stream exists
                var blogStreamType = (int)ArticleType.BlogStream;
                var parentStream = await dbContext.Articles
                    .FirstOrDefaultAsync(
                        a => a.BlogKey == command.BlogKey && 
                             a.ArticleType == blogStreamType &&
                             a.StatusCode != (int)StatusCodeEnum.Deleted,
                        cancellationToken);

                if (parentStream == null)
                {
                    logger.LogWarning("Parent blog stream not found for BlogKey: {BlogKey}", command.BlogKey);
                    return CommandResult<CreateBlogPostCommandResult>.Failure(
                        $"Blog stream '{command.BlogKey}' not found.");
                }

                // Create the URL path: blog_key/post_slug
                var postSlug = slugService.Normalize(command.Title);
                var urlPath = $"{command.BlogKey}/{postSlug}";

                // Get next article number
                var maxArticleNumber = await dbContext.Articles.MaxAsync(a => (int?)a.ArticleNumber, cancellationToken) ?? 0;
                var nextArticleNumber = maxArticleNumber + 1;

                // Create the article entity
                var article = new Article
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = nextArticleNumber,
                    VersionNumber = 1,
                    Title = command.Title.Trim(),
                    Content = command.Content,
                    Introduction = command.Introduction ?? string.Empty,
                    BannerImage = command.BannerImage ?? string.Empty,
                    UrlPath = urlPath,
                    BlogKey = command.BlogKey,
                    ArticleType = (int)ArticleType.BlogPost,
                    StatusCode = (int)StatusCodeEnum.Active,
                    TemplateId = command.TemplateId,
                    Published = command.Published,
                    UserId = command.UserId.ToString(),
                    Updated = DateTimeOffset.UtcNow
                };

                dbContext.Articles.Add(article);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Successfully created blog post {PostId} in stream {BlogKey}: '{Title}' (UrlPath: {UrlPath})",
                    article.Id,
                    command.BlogKey,
                    command.Title,
                    urlPath);

                return CommandResult<CreateBlogPostCommandResult>.Success(
                    new CreateBlogPostCommandResult
                    {
                        Id = article.Id,
                        ArticleNumber = article.ArticleNumber,
                        UrlPath = article.UrlPath,
                        BlogKey = article.BlogKey
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
                    $"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}
