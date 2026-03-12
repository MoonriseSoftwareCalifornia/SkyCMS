// <copyright file="PromoteLayoutHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Promote
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Layouts;

    /// <summary>
    /// Handles promoting a layout to a new version.
    /// </summary>
    public class PromoteLayoutHandler : ICommandHandler<PromoteLayoutCommand, CommandResult<int>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<PromoteLayoutHandler> logger;
        private readonly PromoteLayoutValidator validator;
        private readonly ILayoutVersioningService layoutVersioningService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PromoteLayoutHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="layoutVersioningService">Layout versioning service.</param>
        /// <param name="logger">Logger.</param>
        public PromoteLayoutHandler(
            ApplicationDbContext dbContext,
            ILayoutVersioningService layoutVersioningService,
            ILogger<PromoteLayoutHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.layoutVersioningService = layoutVersioningService ?? throw new ArgumentNullException(nameof(layoutVersioningService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            validator = new PromoteLayoutValidator();
        }

        /// <inheritdoc/>
        public async Task<CommandResult<int>> HandleAsync(
            PromoteLayoutCommand command,
            CancellationToken cancellationToken = default)
        {
            var validationErrors = validator.Validate(command);
            if (validationErrors.Any())
            {
                return CommandResult<int>.Failure(validationErrors);
            }

            try
            {
                var layout = await dbContext.Layouts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == command.LayoutId, cancellationToken);

                if (layout == null)
                {
                    return CommandResult<int>.Failure($"Layout with ID {command.LayoutId} not found");
                }

                var newLayout = await layoutVersioningService.CreateNewVersionAsync(layout, cancellationToken);

                logger.LogInformation(
                    "Promoted layout {OldLayoutId} to new version {NewLayoutId} with version number {Version}",
                    command.LayoutId,
                    newLayout.Id,
                    newLayout.Version);

                return CommandResult<int>.Success(newLayout.Version ?? 0);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error promoting layout {LayoutId}", command.LayoutId);
                return CommandResult<int>.Failure("An error occurred while promoting the layout");
            }
        }
    }
}
