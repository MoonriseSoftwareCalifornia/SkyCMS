// <copyright file="IAiSourceCodeIndexService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Indexes local SkyCMS source code for help-query lookups.
/// </summary>
public interface IAiSourceCodeIndexService
{
    /// <summary>
    /// Searches the source code index for the supplied query.
    /// </summary>
    /// <param name="query">User query text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked source code results.</returns>
    Task<IReadOnlyList<AiSourceCodeSearchResult>> SearchSourceCodeAsync(string query, CancellationToken cancellationToken = default);
}