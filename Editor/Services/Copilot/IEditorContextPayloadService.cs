// <copyright file="IEditorContextPayloadService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Assembles editor context payloads for AI chat and completion requests.
/// </summary>
public interface IEditorContextPayloadService
{
    /// <summary>
    /// Builds a context payload for an editor AI request.
    /// </summary>
    /// <param name="request">Payload request metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Context payload text, or empty when unavailable.</returns>
    Task<string> BuildPayloadAsync(EditorContextPayloadRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Input model for editor context payload assembly.
/// </summary>
public sealed class EditorContextPayloadRequest
{
    /// <summary>
    /// Gets or sets the editor surface identifier (monaco, ckeditor, help).
    /// </summary>
    public string? EditorSurface { get; set; }

    /// <summary>
    /// Gets or sets the document kind identifier.
    /// </summary>
    public string? DocumentKind { get; set; }

    /// <summary>
    /// Gets or sets the section kind identifier.
    /// </summary>
    public string? SectionKind { get; set; }

    /// <summary>
    /// Gets or sets the language identifier.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the current field name.
    /// </summary>
    public string? CurrentField { get; set; }

    /// <summary>
    /// Gets or sets the current field value.
    /// </summary>
    public string? CurrentFieldValue { get; set; }

    /// <summary>
    /// Gets or sets selected text.
    /// </summary>
    public string? Selection { get; set; }

    /// <summary>
    /// Gets or sets the article number.
    /// </summary>
    public string? ArticleNumber { get; set; }

    /// <summary>
    /// Gets or sets the layout identifier.
    /// </summary>
    public string? LayoutId { get; set; }

    /// <summary>
    /// Gets or sets the template identifier.
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the URL path.
    /// </summary>
    public string? UrlPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a lightweight payload is requested.
    /// </summary>
    public bool Lightweight { get; set; }
}