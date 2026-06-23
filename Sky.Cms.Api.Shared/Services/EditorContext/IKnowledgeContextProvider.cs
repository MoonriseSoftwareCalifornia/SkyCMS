// <copyright file="IKnowledgeContextProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Services.EditorContext;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service interface for providing knowledge context (documentation, constraints, rules)
/// for AI editor assistance.
/// 
/// Implements Phase 2 of ADR 0044: Knowledge Context with Editorial and Technical Rules.
/// </summary>
public interface IKnowledgeContextProvider
{
    /// <summary>
    /// Gets knowledge context for a specific document and editor kind.
    /// </summary>
    /// <param name="documentKind">The document kind (article, layout, template, etc.).</param>
    /// <param name="editorKind">The editor kind (article, layout, template, etc.).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning knowledge context with documentation and rules.</returns>
    Task<KnowledgeContext> GetKnowledgeContextAsync(
        DocumentKind documentKind,
        EditorKind editorKind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets article-specific knowledge context (editorial rules, technical constraints, preservation rules).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning article knowledge context.</returns>
    Task<KnowledgeContext> GetArticleKnowledgeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets layout-specific knowledge context.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning layout knowledge context.</returns>
    Task<KnowledgeContext> GetLayoutKnowledgeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets template-specific knowledge context.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation, returning template knowledge context.</returns>
    Task<KnowledgeContext> GetTemplateKnowledgeAsync(CancellationToken cancellationToken = default);
}
