// <copyright file="SavePageDesignVersionHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Save
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
    /// Handles saving (updating) existing page design versions.
    /// </summary>
    public class SavePageDesignVersionHandler : ICommandHandler<SavePageDesignVersionCommand, CommandResult<PageDesignVersion>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IArticleHtmlService htmlService;
        private readonly IClock clock;
        private readonly ILogger<SavePageDesignVersionHandler> logger;
        private readonly SavePageDesignVersionValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SavePageDesignVersionHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="htmlService">HTML service.</param>
        /// <param name="clock">Clock service.</param>
        /// <param name="logger">Logger service.</param>
        public SavePageDesignVersionHandler(
            ApplicationDbContext dbContext,
            IArticleHtmlService htmlService,
            IClock clock,
            ILogger<SavePageDesignVersionHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.htmlService = htmlService ?? throw new ArgumentNullException(nameof(htmlService));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            validator = new SavePageDesignVersionValidator();
        }

        /// <summary>
        /// Handles the save page design version command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with the saved page design version.</returns>
        public async Task<CommandResult<PageDesignVersion>> HandleAsync(
            SavePageDesignVersionCommand command,
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
                    "Saving page design version {Id} '{Title}'",
                    command.Id,
                    command.Title);

                var pageDesignVersion = await dbContext.PageDesignVersions
                    .FirstOrDefaultAsync(v => v.Id == command.Id, cancellationToken);

                if (pageDesignVersion == null)
                {
                    logger.LogWarning("Page design version {Id} not found", command.Id);
                    return CommandResult<PageDesignVersion>.Failure($"Page design version {command.Id} not found.");
                }

                // Process HTML content
                var processedContent = htmlService.EnsureEditableMarkers(command.Content);

                // Update properties
                pageDesignVersion.LayoutId = command.LayoutId;
                pageDesignVersion.CommunityLayoutId = command.CommunityLayoutId;
                pageDesignVersion.Title = command.Title.Trim();
                pageDesignVersion.Description = command.Description;
                pageDesignVersion.Content = processedContent;
                pageDesignVersion.PageType = command.PageType;
                pageDesignVersion.Modified = clock.UtcNow;

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Successfully saved page design version {Id} version {Version}",
                    pageDesignVersion.Id,
                    pageDesignVersion.Version);

                return CommandResult<PageDesignVersion>.Success(pageDesignVersion);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error saving page design version {Id} '{Title}'",
                    command.Id,
                    command.Title);

                return CommandResult<PageDesignVersion>.Failure("An error occurred while saving the page design version.");
            }
        }
    }
}