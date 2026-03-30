// <copyright file="ICopilotProxyOptionsService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using Sky.Editor.Models;
using System.Threading.Tasks;

/// <summary>
/// Resolves Copilot proxy options for the current request tenant.
/// </summary>
public interface ICopilotProxyOptionsService
{
    /// <summary>
    /// Gets Copilot proxy options for the current request.
    /// </summary>
    /// <returns>Copilot proxy options.</returns>
    Task<CopilotProxyOptions> GetOptionsAsync();

    /// <summary>
    /// Invalidates the cached Copilot proxy options for the current tenant.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidateCurrentTenantCacheAsync();
}
