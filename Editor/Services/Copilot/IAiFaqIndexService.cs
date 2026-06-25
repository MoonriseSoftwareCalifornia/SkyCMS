// <copyright file="IAiFaqIndexService.cs" company="Moonrise Software, LLC">
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
/// Searches the SkyCMS documentation index for FAQ-style question and answer content.
/// </summary>
public interface IAiFaqIndexService
{
    /// <summary>
    /// Returns the top FAQ matches for the supplied query.
    /// </summary>
    /// <param name="query">User query text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked FAQ matches.</returns>
    Task<IReadOnlyList<AiFaqMatch>> SearchFaqAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// A single FAQ match extracted from the documentation index.
/// </summary>
public sealed class AiFaqMatch
{
    /// <summary>
    /// Gets or sets the question text (doc entry title or inferred from heading).
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the answer snippet.
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source documentation URL.
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relevance score relative to the query.
    /// </summary>
    public int RelevanceScore { get; set; }
}
