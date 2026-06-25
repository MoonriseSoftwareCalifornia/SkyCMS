// <copyright file="IAiHelpQueryContextService.cs" company="Moonrise Software, LLC">
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
/// Builds contextual knowledge payloads for help-query prompts.
/// </summary>
public interface IAiHelpQueryContextService
{
    /// <summary>
    /// Builds a compact help-query context payload.
    /// </summary>
    /// <param name="request">Help query context request metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Context text and source attributions.</returns>
    Task<AiHelpQueryContextResult> BuildContextAsync(AiHelpQueryContextRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request metadata for help-query context assembly.
/// </summary>
public sealed class AiHelpQueryContextRequest
{
    /// <summary>
    /// Gets or sets the help query text.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chat mode.
    /// </summary>
    public string? ChatMode { get; set; }

    /// <summary>
    /// Gets or sets the active document kind.
    /// </summary>
    public string? DocumentKind { get; set; }

    /// <summary>
    /// Gets or sets the active section kind.
    /// </summary>
    public string? SectionKind { get; set; }

    /// <summary>
    /// Gets or sets the active article number.
    /// </summary>
    public string? ArticleNumber { get; set; }

    /// <summary>
    /// Gets or sets the active template identifier.
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the active layout identifier.
    /// </summary>
    public string? LayoutId { get; set; }

    /// <summary>
    /// Gets or sets the active URL path.
    /// </summary>
    public string? UrlPath { get; set; }
}

/// <summary>
/// Response payload for help-query context assembly.
/// </summary>
public sealed class AiHelpQueryContextResult
{
    /// <summary>
    /// Gets or sets the compact context text.
    /// </summary>
    public string ContextText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets source attributions referenced in the context.
    /// </summary>
    public List<AiHelpSourceAttribution> Sources { get; set; } = [];
}

/// <summary>
/// Source attribution metadata for help-query context.
/// </summary>
public sealed class AiHelpSourceAttribution
{
    /// <summary>
    /// Gets or sets the source type (docs, code, faq).
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display title for the source.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional relevance score.
    /// </summary>
    public int? RelevanceScore { get; set; }
}
