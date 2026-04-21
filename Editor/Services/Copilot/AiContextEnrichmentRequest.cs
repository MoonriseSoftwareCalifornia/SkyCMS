// <copyright file="AiContextEnrichmentRequest.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

/// <summary>
/// Request model used to resolve AI context enrichment content.
/// </summary>
public sealed class AiContextEnrichmentRequest
{
    /// <summary>
    /// Gets or sets the active document kind.
    /// </summary>
    public string? DocumentKind { get; set; }

    /// <summary>
    /// Gets or sets the active section kind.
    /// </summary>
    public string? SectionKind { get; set; }

    /// <summary>
    /// Gets or sets the user chat message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the active article number.
    /// </summary>
    public string? ArticleNumber { get; set; }

    /// <summary>
    /// Gets or sets the active template id.
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the active layout id.
    /// </summary>
    public string? LayoutId { get; set; }

    /// <summary>
    /// Gets or sets the active URL path.
    /// </summary>
    public string? UrlPath { get; set; }
}

/// <summary>
/// Result model for documentation context retrieval.
/// </summary>
public sealed class AiDocumentationContextResult
{
    /// <summary>
    /// Gets or sets compact documentation context text.
    /// </summary>
    public string ContextText { get; set; } = string.Empty;
}

/// <summary>
/// Result model for layout context retrieval.
/// </summary>
public sealed class AiLayoutContextResult
{
    /// <summary>
    /// Gets or sets compact layout context text.
    /// </summary>
    public string ContextText { get; set; } = string.Empty;
}
