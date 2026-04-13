// <copyright file="SaveCopilotProxyOptionsCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Copilot.SaveSettings
{
    using Cosmos.Common.Features.Shared;
    using Sky.Editor.Models;

    /// <summary>
    /// Command to save Copilot proxy options for the current tenant.
    /// </summary>
    public class SaveCopilotProxyOptionsCommand : ICommand<CommandResult<CopilotProxyOptions>>
    {
        /// <summary>
        /// Gets the options to save.
        /// </summary>
        public CopilotProxyOptions Options { get; init; } = new CopilotProxyOptions();
    }
}
