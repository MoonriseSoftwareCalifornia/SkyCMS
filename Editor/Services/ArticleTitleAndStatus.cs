// <copyright file="ArticleTitleAndStatus.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services;

/// <summary>
/// Lightweight DTO for article number, title, and status code used in title/status lookups.
/// </summary>
/// <remarks>This is used by the <see cref="FileEntryTitleService"/> for caching and lookup purposes.</remarks>
public class ArticleTitleAndStatus
{
    /// <summary>
    /// Gets the article number.
    /// </summary>
    public int ArticleNumber { get; internal set; }

    /// <summary>
    /// Gets the article title.
    /// </summary>
    public string Title { get; internal set; }

    /// <summary>
    /// Gets the article status code.
    /// </summary>
    public int StatusCode { get; internal set; }
}