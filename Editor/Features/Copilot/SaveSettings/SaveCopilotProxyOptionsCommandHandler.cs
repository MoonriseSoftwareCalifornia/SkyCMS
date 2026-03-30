// <copyright file="SaveCopilotProxyOptionsCommandHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Copilot.SaveSettings
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Sky.Editor.Models;
    using Sky.Editor.Services.Copilot;

    /// <summary>
    /// Handles saving Copilot proxy options.
    /// </summary>
    public class SaveCopilotProxyOptionsCommandHandler : ICommandHandler<SaveCopilotProxyOptionsCommand, CommandResult<CopilotProxyOptions>>
    {
        private const string CopilotProxySettingsGroupName = "COPILOTPROXYSETTINGS";

        private readonly ApplicationDbContext dbContext;
        private readonly ICopilotProxyOptionsService optionsService;
        private readonly ILogger<SaveCopilotProxyOptionsCommandHandler> logger;
        private readonly SaveCopilotProxyOptionsValidator validator = new SaveCopilotProxyOptionsValidator();

        /// <summary>
        /// Initializes a new instance of the <see cref="SaveCopilotProxyOptionsCommandHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Tenant-aware database context.</param>
        /// <param name="optionsService">Copilot options service.</param>
        /// <param name="logger">Logger instance.</param>
        public SaveCopilotProxyOptionsCommandHandler(
            ApplicationDbContext dbContext,
            ICopilotProxyOptionsService optionsService,
            ILogger<SaveCopilotProxyOptionsCommandHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.optionsService = optionsService ?? throw new ArgumentNullException(nameof(optionsService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the command.
        /// </summary>
        /// <param name="command">Save command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with saved options.</returns>
        public async Task<CommandResult<CopilotProxyOptions>> HandleAsync(
            SaveCopilotProxyOptionsCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var validationErrors = validator.Validate(command);

            if (validationErrors.Count > 0)
            {
                return CommandResult<CopilotProxyOptions>.Failure(validationErrors);
            }

            try
            {
                var setting = await dbContext.Settings
                    .FirstOrDefaultAsync(f => f.Group == CopilotProxySettingsGroupName, cancellationToken)
                    .ConfigureAwait(false);

                if (setting == null)
                {
                    setting = new Setting
                    {
                        Group = CopilotProxySettingsGroupName,
                        Name = nameof(CopilotProxyOptions),
                        Value = string.Empty,
                        Description = "Settings used by the Copilot completion proxy",
                    };

                    dbContext.Settings.Add(setting);
                }

                setting.Value = JsonConvert.SerializeObject(command.Options);

                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await optionsService.InvalidateCurrentTenantCacheAsync().ConfigureAwait(false);

                return CommandResult<CopilotProxyOptions>.Success(command.Options);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save Copilot proxy options.");
                return CommandResult<CopilotProxyOptions>.Failure("Failed to save Copilot proxy settings.");
            }
        }
    }
}
