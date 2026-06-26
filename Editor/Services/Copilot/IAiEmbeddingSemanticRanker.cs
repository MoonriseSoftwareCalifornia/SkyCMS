// <copyright file="IAiEmbeddingSemanticRanker.cs" company="Moonrise Software, LLC">
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
/// Provides optional embedding-based semantic reranking scores for candidate texts.
/// </summary>
public interface IAiEmbeddingSemanticRanker
{
    /// <summary>
    /// Scores candidates by semantic similarity to the query.
    /// </summary>
    /// <param name="query">User query.</param>
    /// <param name="candidates">Candidate searchable texts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of scores aligned to candidate indexes; empty when disabled or unavailable.
    /// </returns>
    Task<IReadOnlyList<AiEmbeddingSemanticScore>> ScoreAsync(string query, IReadOnlyList<string> candidates, CancellationToken cancellationToken = default);
}

/// <summary>
/// Embedding score for a ranked candidate.
/// </summary>
/// <param name="CandidateIndex">Index in the original candidate list.</param>
/// <param name="Score">Cosine similarity score.</param>
public sealed record AiEmbeddingSemanticScore(int CandidateIndex, double Score);
