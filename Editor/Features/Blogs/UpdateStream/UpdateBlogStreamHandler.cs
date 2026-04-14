// <copyright file="UpdateBlogStreamHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.UpdateStream
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Services.BlogPublishing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Titles;

    /// <summary>
    /// Handler for updating blog stream metadata and properties.
    /// Coordinates title changes, URL updates, blog rendering, and publishing.
    /// </summary>
    public class UpdateBlogStreamHandler : ICommandHandler<UpdateBlogStreamCommand, CommandResult<Article>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ISlugService slugService;
        private readonly ITitleChangeService titleChangeService;
        private readonly IBlogStreamRenderingService blogRenderingService;
        private readonly ArticleEditLogic articleLogic;
        private readonly ILogger<UpdateBlogStreamHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateBlogStreamHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="slugService">Slug normalization service.</param>
        /// <param name="titleChangeService">Title change tracking service.</param>
        /// <param name="blogRenderingService">Blog stream HTML rendering service.</param>
        /// <param name="articleLogic">Article publishing logic.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public UpdateBlogStreamHandler(
            ApplicationDbContext dbContext,
            ISlugService slugService,
            ITitleChangeService titleChangeService,
            IBlogStreamRenderingService blogRenderingService,
            ArticleEditLogic articleLogic,
            ILogger<UpdateBlogStreamHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.slugService = slugService ?? throw new ArgumentNullException(nameof(slugService));
            this.titleChangeService = titleChangeService ?? throw new ArgumentNullException(nameof(titleChangeService));
            this.blogRenderingService = blogRenderingService ?? throw new ArgumentNullException(nameof(blogRenderingService));
            this.articleLogic = articleLogic ?? throw new ArgumentNullException(nameof(articleLogic));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the update blog stream command.
        /// </summary>
        /// <param name="command">Update command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with updated article.</returns>
        public async Task<CommandResult<Article>> HandleAsync(
            UpdateBlogStreamCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            // Validation
            if (command.Id == Guid.Empty)
            {
                logger.LogWarning("UpdateBlogStream called with empty ID");
                return CommandResult<Article>.Failure("Blog stream ID is required.");
            }

            if (string.IsNullOrWhiteSpace(command.Title))
            {
                logger.LogWarning("UpdateBlogStream called with empty Title for blog {Id}", command.Id);
                return CommandResult<Article>.Failure("Blog stream title is required.");
            }

            try
            {
                // Retrieve the article
                var blogStreamType = (int)ArticleType.BlogStream;
                var deletedStatusCode = (int)StatusCodeEnum.Deleted;
                var article = await dbContext.Articles
                    .FirstOrDefaultAsync(
                        f => f.Id == command.Id &&
                             f.ArticleType == blogStreamType &&
                             f.StatusCode != deletedStatusCode,
                        cancellationToken);

                if (article == null)
                {
                    logger.LogWarning("Blog stream {Id} not found for update", command.Id);
                    return CommandResult<Article>.Failure($"Blog stream with ID '{command.Id}' not found.");
                }

                // Validate title change (if title changed)
                if (!article.Title.Equals(command.Title, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (!await titleChangeService.ValidateTitle(command.Title, null))
                    {
                        logger.LogWarning(
                            "Title validation failed for blog stream {Id}: '{NewTitle}'",
                            command.Id,
                            command.Title);
                        return CommandResult<Article>.Failure("Blog key conflicts with existing page on this website.");
                    }
                }

                // Save old values for title change tracking
                var oldTitle = article.Title;
                var oldUrlPath = article.UrlPath;
                var oldBlogKey = article.BlogKey;

                // Update article properties
                article.Title = command.Title.Trim();
                var newUrlPath = slugService.Normalize(command.Title);
                article.UrlPath = newUrlPath;
                article.BlogKey = newUrlPath;  // BlogKey changes with UrlPath
                article.Introduction = command.Description ?? string.Empty;
                article.BannerImage = command.HeroImage ?? string.Empty;
                article.Published = command.Published;
                article.UserId = command.UserId.ToString();

                // If UrlPath changed, update all child blog posts
                if (oldUrlPath != newUrlPath)
                {
                    await UpdateChildBlogPostsUrlPath(oldUrlPath, newUrlPath, oldBlogKey, newUrlPath, cancellationToken);
                }

                // Handle publishing/unpublishing of the stream and its posts
                if (command.Published.HasValue)
                {
                    await UpdateBlogStreamPublishingState(article, command.Published.Value, cancellationToken);
                }
                else
                {
                    // Unpublish the stream and all its posts
                    await UnpublishBlogStream(article, oldBlogKey, cancellationToken);
                }

                // Regenerate blog stream HTML
                article.Content = await blogRenderingService.GenerateBlogStreamWrapperAsync(article, article.BlogKey);

                // Save changes
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Successfully updated blog stream {Id} (Title: {Title}, BlogKey: {BlogKey})",
                    command.Id,
                    article.Title,
                    article.BlogKey);

                // Handle title change (creates redirects for stream and all posts)
                if (oldTitle != article.Title || oldUrlPath != newUrlPath)
                {
                    await titleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
                    logger.LogInformation(
                        "Processed title change for blog stream {Id}: '{OldTitle}' -> '{NewTitle}' (UrlPath: '{OldUrlPath}' -> '{NewUrlPath}')",
                        command.Id,
                        oldTitle,
                        article.Title,
                        oldUrlPath,
                        newUrlPath);
                }

                return CommandResult<Article>.Success(article);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(
                    ex,
                    "Database error updating blog stream {Id}",
                    command.Id);
                return CommandResult<Article>.Failure($"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected error updating blog stream {Id}",
                    command.Id);
                return CommandResult<Article>.Failure($"An unexpected error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates all child blog posts' UrlPath when the parent stream's UrlPath changes.
        /// Maintains the format: stream_path/post_slug.
        /// </summary>
        private async Task UpdateChildBlogPostsUrlPath(
            string oldStreamUrlPath,
            string newStreamUrlPath,
            string oldBlogKey,
            string newBlogKey,
            CancellationToken cancellationToken)
        {
            // Get all blog posts that belong to this stream (by old BlogKey)
            var blogPostType = (int)ArticleType.BlogPost;
            var deletedStatusCode = (int)StatusCodeEnum.Deleted;
            var blogPosts = await dbContext.Articles
                .Where(a => a.BlogKey == oldBlogKey &&
                            a.ArticleType == blogPostType &&
                            a.StatusCode != deletedStatusCode)
                .ToListAsync(cancellationToken);

            foreach (var post in blogPosts)
            {
                // Extract the post slug (everything after the last '/')
                var parts = post.UrlPath.Split('/');
                var postSlug = parts.Length > 1 ? parts[^1] : post.UrlPath;

                // Rebuild UrlPath with new stream prefix
                post.UrlPath = $"{newStreamUrlPath}/{postSlug}";
                post.BlogKey = newBlogKey;  // Update BlogKey to match new stream
            }

            logger.LogInformation(
                "Updated UrlPath for {Count} blog posts. Stream path changed from '{OldPath}' to '{NewPath}'",
                blogPosts.Count,
                oldStreamUrlPath,
                newStreamUrlPath);
        }

        /// <summary>
        /// Publishes a blog stream and all its child blog posts.
        /// </summary>
        private async Task UpdateBlogStreamPublishingState(
            Article blogStream,
            DateTimeOffset publishDate,
            CancellationToken cancellationToken)
        {
            // Publish the stream
            await articleLogic.PublishArticle(blogStream.Id, publishDate);

            // Publish all child blog posts with the same publish date
            var blogPostType = (int)ArticleType.BlogPost;
            var deletedStatusCode = (int)StatusCodeEnum.Deleted;
            var blogPosts = await dbContext.Articles
                .Where(a => a.BlogKey == blogStream.BlogKey &&
                            a.ArticleType == blogPostType &&
                            a.StatusCode != deletedStatusCode)
                .ToListAsync(cancellationToken);

            foreach (var post in blogPosts)
            {
                await articleLogic.PublishArticle(post.Id, publishDate);
            }

            logger.LogInformation(
                "Published blog stream {StreamId} and {PostCount} child posts with date {PublishDate}",
                blogStream.Id,
                blogPosts.Count,
                publishDate);
        }

        /// <summary>
        /// Unpublishes a blog stream and all its child blog posts.
        /// </summary>
        private async Task UnpublishBlogStream(
            Article blogStream,
            string blogKey,
            CancellationToken cancellationToken)
        {
            // Get all blog posts that belong to this stream
            var blogPostType = (int)ArticleType.BlogPost;
            var deletedStatusCode = (int)StatusCodeEnum.Deleted;
            var blogPosts = await dbContext.Articles
                .Where(a => a.BlogKey == blogKey &&
                            a.ArticleType == blogPostType &&
                            a.StatusCode != deletedStatusCode)
                .ToListAsync(cancellationToken);

            // Unpublish all posts
            foreach (var post in blogPosts)
            {
                post.Published = null;
            }

            logger.LogInformation(
                "Unpublished blog stream {StreamId} and {PostCount} child posts",
                blogStream.Id,
                blogPosts.Count);
        }
    }
}
