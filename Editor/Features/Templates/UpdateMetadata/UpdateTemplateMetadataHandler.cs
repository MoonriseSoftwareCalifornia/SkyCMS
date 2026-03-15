// <copyright file="UpdateTemplateMetadataHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.UpdateMetadata
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler for updating template metadata (title and description).
    /// This handler only updates the Template entity's metadata fields.
    /// Content changes should use SavePageDesignVersionHandler.
    /// </summary>
    public class UpdateTemplateMetadataHandler : ICommandHandler<UpdateTemplateMetadataCommand, CommandResult<Template>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<UpdateTemplateMetadataHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateTemplateMetadataHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public UpdateTemplateMetadataHandler(
            ApplicationDbContext dbContext,
            ILogger<UpdateTemplateMetadataHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the update template metadata command.
        /// </summary>
        /// <param name="command">Update metadata command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with updated template.</returns>
        public async Task<CommandResult<Template>> HandleAsync(
            UpdateTemplateMetadataCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            // Validate command
            if (command.TemplateId == Guid.Empty)
            {
                logger.LogWarning("UpdateTemplateMetadata called with empty TemplateId");
                return CommandResult<Template>.Failure("Template ID is required.");
            }

            if (string.IsNullOrWhiteSpace(command.Title))
            {
                logger.LogWarning("UpdateTemplateMetadata called with empty Title for template {TemplateId}", command.TemplateId);
                return CommandResult<Template>.Failure("Template title is required.");
            }

            try
            {
                // Retrieve the template
                var template = await dbContext.Templates
                    .FirstOrDefaultAsync(t => t.Id == command.TemplateId, cancellationToken);

                if (template == null)
                {
                    logger.LogWarning("Template {TemplateId} not found for metadata update", command.TemplateId);
                    return CommandResult<Template>.Failure($"Template with ID '{command.TemplateId}' not found.");
                }

                // Update metadata fields only
                template.Title = command.Title.Trim();
                template.Description = command.Description ?? string.Empty;

                // Save changes
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Successfully updated metadata for template {TemplateId} (Title: {Title})",
                    command.TemplateId,
                    command.Title);

                return CommandResult<Template>.Success(template);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(
                    ex,
                    "Database error updating metadata for template {TemplateId}",
                    command.TemplateId);
                return CommandResult<Template>.Failure($"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected error updating metadata for template {TemplateId}",
                    command.TemplateId);
                return CommandResult<Template>.Failure($"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}
