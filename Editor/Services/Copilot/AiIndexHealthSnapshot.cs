// <copyright file="AiIndexHealthSnapshot.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Copilot;

using System;

/// <summary>
/// Captures health and freshness metadata for an AI index source.
/// </summary>
public sealed class AiIndexHealthSnapshot
{
    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the most recent successful refresh timestamp (UTC).
    /// </summary>
    public DateTimeOffset? LastSuccessfulRefreshUtc { get; set; }

    /// <summary>
    /// Gets or sets the most recent refresh attempt timestamp (UTC).
    /// </summary>
    public DateTimeOffset? LastAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of indexed entries from the last successful refresh.
    /// </summary>
    public int LastIndexedEntryCount { get; set; }

    /// <summary>
    /// Gets or sets the latest fetch error message.
    /// </summary>
    public string? LastFetchError { get; set; }

    /// <summary>
    /// Gets or sets the latest fetch error timestamp (UTC).
    /// </summary>
    public DateTimeOffset? LastFetchErrorUtc { get; set; }

    /// <summary>
    /// Gets or sets the latest parse error message.
    /// </summary>
    public string? LastParseError { get; set; }

    /// <summary>
    /// Gets or sets the latest parse error timestamp (UTC).
    /// </summary>
    public DateTimeOffset? LastParseErrorUtc { get; set; }
}
