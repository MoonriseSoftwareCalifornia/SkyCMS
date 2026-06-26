// <copyright file="CopilotProxyOptions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Sky.Editor.Models;

/// <summary>
/// Configuration for the server-side AI completion proxy.
/// </summary>
public sealed class CopilotProxyOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether proxy calls are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the upstream OpenAI-compatible chat completions endpoint.
    /// </summary>
    [Url]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the upstream model identifier.
    /// Use "auto" to let SkyCMS select the provider default behavior.
    /// </summary>
    public string Model { get; set; } = "auto";

    /// <summary>
    /// Gets or sets the bearer token used to authenticate with the upstream provider.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 8000;

    /// <summary>
    /// Gets or sets the completion temperature.
    /// </summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// Gets or sets the maximum completion tokens returned by the upstream provider.
    /// </summary>
    public int MaxTokens { get; set; } = 160;

    /// <summary>
    /// Gets or sets a value indicating whether embedding-based semantic reranking is enabled.
    /// </summary>
    public bool EnableEmbeddingSemanticRerank { get; set; }

    /// <summary>
    /// Gets or sets the embedding model used when semantic reranking is enabled.
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}