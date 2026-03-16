// <copyright file="DeleteLayoutHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Delete
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handles deleting a layout and its associated templates.
    /// </summary>
    public class DeleteLayoutHandler : ICommandHandler<DeleteLayoutCommand, CommandResult<bool>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<DeleteLayoutHandler> logger;
        private readonly DeleteLayoutValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteLayoutHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="logger">Logger.</param>
        public DeleteLayoutHandler(
            ApplicationDbContext dbContext,
            ILogger<DeleteLayoutHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            validator = new DeleteLayoutValidator();
        }

        /// <inheritdoc/>
        public async Task<CommandResult<bool>> HandleAsync(
            DeleteLayoutCommand command,
            CancellationToken cancellationToken = default)
        {
            var validationErrors = validator.Validate(command);
            if (validationErrors.Any())
            {
                return CommandResult<bool>.Failure(validationErrors);
            }

            try
            {
                var entity = await dbContext.Layouts.FindAsync(new object[] { command.LayoutId }, cancellationToken);

                if (entity == null)
                {
                    return CommandResult<bool>.Failure($"Layout with ID {command.LayoutId} not found");
                }

                if (entity.IsDefault)
                {
                    return CommandResult<bool>.Failure("Cannot delete the default layout.");
                }

                var pages = await dbContext.Templates
                    .Where(t => t.LayoutId == command.LayoutId)
                    .ToListAsync(cancellationToken);

                dbContext.Templates.RemoveRange(pages);
                dbContext.Layouts.Remove(entity);

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Deleted layout {LayoutId} '{LayoutName}' and {TemplateCount} associated templates",
                    command.LayoutId,
                    entity.LayoutName,
                    pages.Count);

                return CommandResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting layout {LayoutId}", command.LayoutId);
                return CommandResult<bool>.Failure("An error occurred while deleting the layout");
            }
        }
    }
}
