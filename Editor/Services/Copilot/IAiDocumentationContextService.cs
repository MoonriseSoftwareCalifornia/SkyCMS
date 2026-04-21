// <copyright file="IAiDocumentationContextService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Retrieves documentation context for AI prompts.
/// </summary>
public interface IAiDocumentationContextService
{
    /// <summary>
    /// Gets documentation context text for a request.
    /// </summary>
    /// <param name="request">Context request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Documentation context result.</returns>
    Task<AiDocumentationContextResult> GetDocumentationContextAsync(AiContextEnrichmentRequest request, CancellationToken cancellationToken = default);
}
