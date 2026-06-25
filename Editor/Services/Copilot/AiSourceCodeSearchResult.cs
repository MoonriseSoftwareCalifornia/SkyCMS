// <copyright file="AiSourceCodeSearchResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

/// <summary>
/// Represents a ranked source code match.
/// </summary>
public sealed class AiSourceCodeSearchResult
{
    /// <summary>
    /// Gets or sets the file path relative to the repo root.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type or symbol name.
    /// </summary>
    public string? SymbolName { get; set; }

    /// <summary>
    /// Gets or sets the method or member signature.
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// Gets or sets a short snippet around the match.
    /// </summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the GitHub permalink for the match.
    /// </summary>
    public string GitHubUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relevance score.
    /// </summary>
    public int RelevanceScore { get; set; }
}