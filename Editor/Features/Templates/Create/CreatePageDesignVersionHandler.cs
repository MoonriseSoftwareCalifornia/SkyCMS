// <copyright file="CreatePageDesignVersionHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Create
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Html;

    /// <summary>
    /// Handles the creation of new page design versions.
    /// </summary>
    public class CreatePageDesignVersionHandler : ICommandHandler<CreatePageDesignVersionCommand, CommandResult<PageDesignVersion>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IArticleHtmlService htmlService;
        private readonly IClock clock;
        private readonly ILogger<CreatePageDesignVersionHandler> logger;
        private readonly CreatePageDesignVersionValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreatePageDesignVersionHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="htmlService">HTML service.</param>
        /// <param name="clock">Clock service.</param>
        /// <param name="logger">Logger service.</param>
        public CreatePageDesignVersionHandler(
            ApplicationDbContext dbContext,
            IArticleHtmlService htmlService,
            IClock clock,
            ILogger<CreatePageDesignVersionHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.htmlService = htmlService ?? throw new ArgumentNullException(nameof(htmlService));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            validator = new CreatePageDesignVersionValidator();
        }

        /// <summary>
        /// Handles the create page design version command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with the created page design version.</returns>
        public async Task<CommandResult<PageDesignVersion>> HandleAsync(
            CreatePageDesignVersionCommand command,
            CancellationToken cancellationToken = default)
        {
            // Validate
            var validationErrors = validator.Validate(command);
            if (validationErrors.Any())
            {
                return CommandResult<PageDesignVersion>.Failure(validationErrors);
            }

            try
            {
                logger.LogInformation(
                    "Creating page design version for template {TemplateId} '{Title}'",
                    command.TemplateId,
                    command.Title);

                // Get the latest version number for this template
                var latestVersion = await dbContext.PageDesignVersions
                    .Where(v => v.TemplateId == command.TemplateId)
                    .OrderByDescending(v => v.Version)
                    .Select(v => v.Version)
                    .FirstOrDefaultAsync(cancellationToken);

                var nextVersion = latestVersion + 1;

                // Process HTML content to ensure editable markers
                var processedContent = htmlService.EnsureEditableMarkers(command.Content);

                var now = clock.UtcNow;

                var pageDesignVersion = new PageDesignVersion
                {
                    Id = Guid.NewGuid(),
                    TemplateId = command.TemplateId,
                    LayoutId = command.LayoutId,
                    CommunityLayoutId = command.CommunityLayoutId,
                    Version = nextVersion,
                    Title = command.Title.Trim(),
                    Description = command.Description,
                    Content = processedContent,
                    PageType = command.PageType,
                    Published = null, // New versions are unpublished
                    Modified = now
                };

                dbContext.PageDesignVersions.Add(pageDesignVersion);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Successfully created page design version {Version} for template {TemplateId}",
                    pageDesignVersion.Version,
                    pageDesignVersion.TemplateId);

                return CommandResult<PageDesignVersion>.Success(pageDesignVersion);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error creating page design version for template {TemplateId} '{Title}'",
                    command.TemplateId,
                    command.Title);

                return CommandResult<PageDesignVersion>.Failure("An error occurred while creating the page design version.");
            }
        }
    }
}