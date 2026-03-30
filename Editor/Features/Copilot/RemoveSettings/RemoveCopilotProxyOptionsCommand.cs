// <copyright file="RemoveCopilotProxyOptionsCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Copilot.RemoveSettings
{
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to remove Copilot proxy options for the current tenant.
    /// </summary>
    public class RemoveCopilotProxyOptionsCommand : ICommand<CommandResult<bool>>
    {
    }
}
