// <copyright file="ImportLayoutHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Layouts.Import
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
    /// Handles importing a community layout.
    /// </summary>
    public class ImportLayoutHandler : ICommandHandler<ImportLayoutCommand, CommandResult<bool>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IMediator mediator;
        private readonly ILayoutImportService layoutImportService;
        private readonly ILayoutVersioningService layoutVersioningService;
        private readonly ILogger<ImportLayoutHandler> logger;
        private readonly ImportLayoutValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportLayoutHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="mediator">Mediator for CQRS queries.</param>
        /// <param name="layoutImportService">Layout import service.</param>
        /// <param name="layoutVersioningService">Layout versioning service.</param>
        /// <param name="logger">Logger.</param>
        public ImportLayoutHandler(
            ApplicationDbContext dbContext,
            IMediator mediator,
            ILayoutImportService layoutImportService,
            ILayoutVersioningService layoutVersioningService,
            ILogger<ImportLayoutHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            this.layoutImportService = layoutImportService ?? throw new ArgumentNullException(nameof(layoutImportService));
            this.layoutVersioningService = layoutVersioningService ?? throw new ArgumentNullException(nameof(layoutVersioningService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            validator = new ImportLayoutValidator();
        }

        /// <inheritdoc/>
        public async Task<CommandResult<bool>> HandleAsync(
            ImportLayoutCommand command,
            CancellationToken cancellationToken = default)
        {
            var validationErrors = validator.Validate(command);
            if (validationErrors.Any())
            {
                return CommandResult<bool>.Failure(validationErrors);
            }

            try
            {
                if (await dbContext.Layouts.Where(c => c.CommunityLayoutId == command.CommunityLayoutId).CosmosAnyAsync())
                {
                    return CommandResult<bool>.Failure("Design already loaded.");
                }

                var layout = await layoutImportService.GetCommunityLayoutAsync(command.CommunityLayoutId, false);
                var communityPages = await layoutImportService.GetCommunityTemplatePagesAsync(command.CommunityLayoutId);

                var maxLayoutNumber = await dbContext.Layouts
                    .Where(l => l.LayoutNumber > 0)
                    .MaxAsync(l => (int?)l.LayoutNumber, cancellationToken) ?? 0;

                layout.LayoutNumber = maxLayoutNumber + 1;

                if (!await mediator.QueryAsync(new Cosmos.Common.Features.Layouts.Queries.CheckDefaultLayoutExistsQuery()))
                {
                    layout.Version = 1;
                    layout.IsDefault = true;
                }
                else
                {
                    layout.Version = (await dbContext.Layouts.CountAsync(cancellationToken)) + 1;
                    layout.IsDefault = false;
                }

                dbContext.Layouts.Add(layout);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Imported community layout {CommunityLayoutId} as layout {LayoutId} with LayoutNumber={LayoutNumber}",
                    command.CommunityLayoutId,
                    layout.Id,
                    layout.LayoutNumber);

                if (communityPages != null && communityPages.Any())
                {
                    await layoutVersioningService.ImportCommunityTemplatesAsync(
                        communityPages,
                        layout.Id,
                        layout.LayoutNumber,
                        cancellationToken);
                }

                return CommandResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error importing community layout {CommunityLayoutId}", command.CommunityLayoutId);
                return CommandResult<bool>.Failure(ex.Message);
            }
        }
    }
}
