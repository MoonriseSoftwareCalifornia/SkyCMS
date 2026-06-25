// <copyright file="IAiDocumentationIndexService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Searches the SkyCMS documentation index and returns compact context snippets.
/// </summary>
public interface IAiDocumentationIndexService
{
    /// <summary>
    /// Searches the docs index for the supplied query text.
    /// </summary>
    /// <param name="query">User query text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compact documentation context or an empty result.</returns>
    Task<AiDocumentationContextResult> SearchDocsAsync(string query, CancellationToken cancellationToken = default);
}