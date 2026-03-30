// <copyright file="RemoveCopilotProxyOptionsCommandHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Copilot.RemoveSettings
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Copilot;

    /// <summary>
    /// Handles removing Copilot proxy options.
    /// </summary>
    public class RemoveCopilotProxyOptionsCommandHandler : ICommandHandler<RemoveCopilotProxyOptionsCommand, CommandResult<bool>>
    {
        private const string CopilotProxySettingsGroupName = "COPILOTPROXYSETTINGS";

        private readonly ApplicationDbContext dbContext;
        private readonly ICopilotProxyOptionsService optionsService;
        private readonly ILogger<RemoveCopilotProxyOptionsCommandHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoveCopilotProxyOptionsCommandHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Tenant-aware database context.</param>
        /// <param name="optionsService">Copilot options service.</param>
        /// <param name="logger">Logger instance.</param>
        public RemoveCopilotProxyOptionsCommandHandler(
            ApplicationDbContext dbContext,
            ICopilotProxyOptionsService optionsService,
            ILogger<RemoveCopilotProxyOptionsCommandHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.optionsService = optionsService ?? throw new ArgumentNullException(nameof(optionsService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the command.
        /// </summary>
        /// <param name="command">Remove command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with removal status.</returns>
        public async Task<CommandResult<bool>> HandleAsync(
            RemoveCopilotProxyOptionsCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            try
            {
                var copilotSettings = await dbContext.Settings
                    .Where(f => f.Group == CopilotProxySettingsGroupName)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (copilotSettings.Any())
                {
                    dbContext.Settings.RemoveRange(copilotSettings);
                    await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                await optionsService.InvalidateCurrentTenantCacheAsync().ConfigureAwait(false);

                return CommandResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to remove Copilot proxy options.");
                return CommandResult<bool>.Failure("Failed to remove Copilot proxy settings.");
            }
        }
    }
}
