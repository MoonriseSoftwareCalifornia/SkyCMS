// <copyright file="CloneArticleHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Articles.Clone
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Shared;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Titles;

    /// <summary>
    /// Handles cloning of existing articles with new titles.
    /// </summary>
    /// <remarks>
    /// This handler creates a complete copy of an existing article, preserving all content,
    /// scripts, and configuration while assigning a new title and URL path.
    /// </remarks>
    public class CloneArticleHandler : ICommandHandler<CloneArticleCommand, CommandResult<ArticleViewModel>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ITitleChangeService titleChangeService;
        private readonly IMediator mediator;
        private readonly IClock clock;
        private readonly ILogger<CloneArticleHandler> logger;
        private readonly CloneArticleValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloneArticleHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="titleChangeService">Title validation and URL generation service.</param>
        /// <param name="mediator">Mediator for dispatching create command.</param>
        /// <param name="clock">Clock service for timestamps.</param>
        /// <param name="logger">Logger service.</param>
        public CloneArticleHandler(
            ApplicationDbContext dbContext,
            ITitleChangeService titleChangeService,
            IMediator mediator,
            IClock clock,
            ILogger<CloneArticleHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.titleChangeService = titleChangeService ?? throw new ArgumentNullException(nameof(titleChangeService));
            this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            validator = new CloneArticleValidator();
        }

        public async Task<CommandResult<ArticleViewModel>> HandleAsync(
            CloneArticleCommand command,
            CancellationToken cancellationToken = default)
        {
            // Validate command structure
            var validationErrors = validator.Validate(command);
            if (validationErrors.Any())
            {
                return CommandResult<ArticleViewModel>.Failure(validationErrors);
            }

            // Validate title doesn't conflict
            var titleIsValid = await titleChangeService.ValidateTitle(command.NewTitle, null);
            if (!titleIsValid)
            {
                logger.LogWarning(
                    "Clone failed: Title '{Title}' conflicts with an existing article or reserved path",
                    command.NewTitle);

                return CommandResult<ArticleViewModel>.Failure(
                    new Dictionary<string, string[]>
                    {
                        ["NewTitle"] = new[] { $"Title '{command.NewTitle}' conflicts with an existing article or reserved path." }
                    });
            }

            try
            {
                logger.LogInformation(
                    "Cloning article {SourceArticleId} with new title '{NewTitle}' for user {UserId}",
                    command.SourceArticleId,
                    command.NewTitle,
                    command.UserId);

                // Retrieve the source article
                var sourceArticle = await dbContext.Articles.FirstOrDefaultAsync(f => f.Id == command.SourceArticleId, cancellationToken);

                if (sourceArticle == null)
                {
                    logger.LogWarning("Source article {SourceArticleId} not found", command.SourceArticleId);
                    return CommandResult<ArticleViewModel>.Failure("Source article not found.");
                }

                // REFACTORED: Use CreateArticleCommand to create the clone
                var createCommand = new CreateArticleCommand
                {
                    Title = command.NewTitle.Trim('/'),
                    UserId = command.UserId,
                    TemplateId = sourceArticle.TemplateId,
                    BlogKey = sourceArticle.BlogKey ?? string.Empty,
                    ArticleType = (Cosmos.Cms.Common.ArticleType)sourceArticle.ArticleType,
                    
                    // Copy all properties from source article
                    Category = sourceArticle.Category,
                    Introduction = sourceArticle.Introduction,
                    BannerImage = sourceArticle.BannerImage,
                    ContentOverride = sourceArticle.Content,  // Use source content
                    Published = command.Published,  // From clone command (may differ from source)
                    StatusCode = (StatusCodeEnum)sourceArticle.StatusCode,
                    HeadJavaScript = sourceArticle.HeaderJavaScript,
                    FooterJavaScript = sourceArticle.FooterJavaScript
                };

                var result = await mediator.SendAsync(createCommand);

                if (!result.IsSuccess)
                {
                    logger.LogError("CreateArticleCommand failed for clone: {Errors}",
                        string.Join(", ", result.Errors?.SelectMany(e => e.Value) ?? Array.Empty<string>()));

                    return CommandResult<ArticleViewModel>.Failure(
                        result.Errors ?? new Dictionary<string, string[]>
                        {
                            ["Create"] = new[] { "Failed to create cloned article." }
                        });
                }

                logger.LogInformation(
                    "Successfully cloned article {SourceArticleId} to new article {ArticleNumber} with title '{Title}'",
                    command.SourceArticleId,
                    result.Data.ArticleNumber,
                    result.Data.Title);

                return CommandResult<ArticleViewModel>.Success(result.Data);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cloning article {SourceArticleId}", command.SourceArticleId);
                return CommandResult<ArticleViewModel>.Failure("An error occurred while cloning the article.");
            }
        }
    }
}