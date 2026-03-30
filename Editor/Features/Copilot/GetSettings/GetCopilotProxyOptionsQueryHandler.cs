// <copyright file="GetCopilotProxyOptionsQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Copilot.GetSettings
{
    using Cosmos.Common.Features.Shared;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Models;
    using Sky.Editor.Services.Copilot;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handles loading Copilot proxy options.
    /// </summary>
    public class GetCopilotProxyOptionsQueryHandler : IQueryHandler<GetCopilotProxyOptionsQuery, CommandResult<CopilotProxyOptions>>
    {
        private readonly ICopilotProxyOptionsService optionsService;
        private readonly ILogger<GetCopilotProxyOptionsQueryHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetCopilotProxyOptionsQueryHandler"/> class.
        /// </summary>
        /// <param name="optionsService">Copilot options service.</param>
        /// <param name="logger">Logger instance.</param>
        public GetCopilotProxyOptionsQueryHandler(
            ICopilotProxyOptionsService optionsService,
            ILogger<GetCopilotProxyOptionsQueryHandler> logger)
        {
            this.optionsService = optionsService ?? throw new ArgumentNullException(nameof(optionsService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the query.
        /// </summary>
        /// <param name="query">Query instance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with Copilot options.</returns>
        public async Task<CommandResult<CopilotProxyOptions>> HandleAsync(
            GetCopilotProxyOptionsQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            try
            {
                var options = await optionsService.GetOptionsAsync().ConfigureAwait(false);
                return CommandResult<CopilotProxyOptions>.Success(options);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load Copilot proxy options.");
                return CommandResult<CopilotProxyOptions>.Failure("Failed to load Copilot proxy settings.");
            }
        }
    }
}
