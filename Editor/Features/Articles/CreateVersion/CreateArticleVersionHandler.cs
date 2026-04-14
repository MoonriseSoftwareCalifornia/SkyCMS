// <copyright file="CreateArticleVersionHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.CreateVersion
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Handler for CreateArticleVersionCommand. Creates a new version of an article.
    /// </summary>
    public class CreateArticleVersionHandler : ICommandHandler<CreateArticleVersionCommand, CommandResult<CreateArticleVersionCommandResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<CreateArticleVersionHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateArticleVersionHandler"/> class.
        /// </summary>
        public CreateArticleVersionHandler(
            ApplicationDbContext dbContext,
            ILogger<CreateArticleVersionHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the create article version command.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<CommandResult<CreateArticleVersionCommandResult>> HandleAsync(CreateArticleVersionCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return new CommandResult<CreateArticleVersionCommandResult> { IsSuccess = false, ErrorMessage = "Command cannot be null" };
            }

            try
            {
                // Get the latest version regardless
                var latest = await dbContext.Articles
                    .OrderByDescending(o => o.VersionNumber)
                    .FirstOrDefaultAsync(f => f.ArticleNumber == command.ArticleNumber, cancellationToken);

                if (latest == null)
                {
                    logger.LogWarning("Article number {ArticleNumber} not found", command.ArticleNumber);
                    return new CommandResult<CreateArticleVersionCommandResult>
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Article number {command.ArticleNumber} not found"
                    };
                }

                // Determine source article (either specified version or latest)
                Article sourceArticle;
                if (command.SourceVersionId.HasValue)
                {
                    sourceArticle = await dbContext.Articles
                        .FirstOrDefaultAsync(f => f.Id == command.SourceVersionId.Value, cancellationToken);

                    if (sourceArticle == null)
                    {
                        logger.LogWarning(
                            "Source version {SourceVersionId} not found for article {ArticleNumber}",
                            command.SourceVersionId,
                            command.ArticleNumber);
                        return new CommandResult<CreateArticleVersionCommandResult>
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Source version not found"
                        };
                    }
                }
                else
                {
                    sourceArticle = latest;
                }

                // Create new version
                var newVersion = new Article
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = sourceArticle.ArticleNumber,
                    VersionNumber = latest.VersionNumber + 1,
                    BannerImage = sourceArticle.BannerImage,
                    Content = sourceArticle.Content,
                    FooterJavaScript = sourceArticle.FooterJavaScript,
                    HeaderJavaScript = sourceArticle.HeaderJavaScript,
                    Published = null,
                    StatusCode = sourceArticle.StatusCode,
                    Title = sourceArticle.Title,
                    UrlPath = sourceArticle.UrlPath,
                    Updated = DateTimeOffset.UtcNow,
                    TemplateId = sourceArticle.TemplateId,
                    UserId = sourceArticle.UserId,
                    Expires = sourceArticle.Expires,
                    Introduction = sourceArticle.Introduction,
                    Category = sourceArticle.Category,
                    BlogKey = sourceArticle.BlogKey
                };

                dbContext.Articles.Add(newVersion);
                await dbContext.SaveChangesAsync(cancellationToken);

                // Build the view model by mapping from entity
                var viewModel = new ArticleViewModel
                {
                    Id = newVersion.Id,
                    ArticleNumber = newVersion.ArticleNumber,
                    VersionNumber = newVersion.VersionNumber,
                    Title = newVersion.Title,
                    Content = newVersion.Content,
                    BannerImage = newVersion.BannerImage,
                    HeadJavaScript = newVersion.HeaderJavaScript,
                    FooterJavaScript = newVersion.FooterJavaScript,
                    UrlPath = newVersion.UrlPath,
                    Published = newVersion.Published,
                    Updated = newVersion.Updated,
                    Introduction = newVersion.Introduction,
                    Category = newVersion.Category,
                    ArticleType = (ArticleType)newVersion.ArticleType
                };

                logger.LogInformation(
                    "Article {ArticleNumber} version {VersionNumber} created successfully",
                    newVersion.ArticleNumber,
                    newVersion.VersionNumber);

                return new CommandResult<CreateArticleVersionCommandResult>
                {
                    IsSuccess = true,
                    Data = new CreateArticleVersionCommandResult
                    {
                        Article = viewModel
                    }
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating version for article {ArticleNumber}", command.ArticleNumber);
                return new CommandResult<CreateArticleVersionCommandResult>
                {
                    IsSuccess = false,
                    ErrorMessage = $"Error creating version: {ex.Message}"
                };
            }
        }
    }
}
