// <copyright file="PublishLayoutHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Publish
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Handles publishing a layout as the default layout.
    /// </summary>
    public class PublishLayoutHandler : ICommandHandler<PublishLayoutCommand, CommandResult<bool>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<PublishLayoutHandler> logger;
        private readonly PublishLayoutValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishLayoutHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="logger">Logger.</param>
        public PublishLayoutHandler(
            ApplicationDbContext dbContext,
            ILogger<PublishLayoutHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            validator = new PublishLayoutValidator();
        }

        /// <inheritdoc/>
        public async Task<CommandResult<bool>> HandleAsync(
            PublishLayoutCommand command,
            CancellationToken cancellationToken = default)
        {
            var validationErrors = validator.Validate(command);
            if (validationErrors.Any())
            {
                return CommandResult<bool>.Failure(validationErrors);
            }

            try
            {
                var layout = await dbContext.Layouts
                    .FirstOrDefaultAsync(f => f.Id == command.LayoutId, cancellationToken);

                if (layout == null)
                {
                    return CommandResult<bool>.Failure($"Layout with ID {command.LayoutId} not found");
                }

                if (layout.IsDefault)
                {
                    // Return true to indicate layout was already default (no action needed)
                    return CommandResult<bool>.Success(true);
                }

                layout.IsDefault = true;
                layout.Published = DateTimeOffset.UtcNow;

                var others = await dbContext.Layouts
                    .Where(w => w.Id != command.LayoutId && w.IsDefault)
                    .ToListAsync(cancellationToken);

                foreach (var item in others)
                {
                    item.IsDefault = false;
                    item.Published = null;
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Published layout {LayoutId} as default", command.LayoutId);

                // Return false to indicate layout was newly published (action was taken)
                return CommandResult<bool>.Success(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing layout {LayoutId}", command.LayoutId);
                return CommandResult<bool>.Failure("An error occurred while publishing the layout");
            }
        }
    }
}
