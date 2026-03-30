// <copyright file="GetCopilotProxyOptionsQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Copilot.GetSettings
{
    using Cosmos.Common.Features.Shared;
    using Sky.Editor.Models;

    /// <summary>
    /// Query to load Copilot proxy options for the current tenant.
    /// </summary>
    public class GetCopilotProxyOptionsQuery : IQuery<CommandResult<CopilotProxyOptions>>
    {
    }
}
