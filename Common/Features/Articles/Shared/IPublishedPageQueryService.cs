// <copyright file="IPublishedPageQueryService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Articles.Shared;

using System;
using System.Threading.Tasks;
using Cosmos.Common.Models;

/// <summary>
/// Service for querying published page snapshots from the PublishedPage table.
/// Handles retrieval and caching of cached article snapshots published to the CDN.
/// </summary>
/// <remarks>
/// PublishedPage entities are immutable snapshots of published articles, optimized for reading.
/// This service encapsulates queries against the published page catalog, including caching
/// and conversion to view models for public-facing display.
/// </remarks>
public interface IPublishedPageQueryService
{
    /// <summary>
    /// Retrieves a published page by URL path with optional caching.
    /// </summary>
    /// <param name="urlPath">URL path (e.g., "blog/my-article"). Case-insensitive. Root page is "root".</param>
    /// <param name="lang">Language code (e.g., "en-US").</param>
    /// <param name="cacheSpan">Optional cache duration for the entire view model.</param>
    /// <param name="layoutCacheDuration">Optional separate cache duration for layout only.</param>
    /// <param name="includeLayout">Whether to include layout information in the result.</param>
    /// <returns>
    /// An ArticleViewModel for the published page, or null if not found or not yet published.
    /// Includes Open Graph metadata, layout information, and full content.
    /// </returns>
    /// <remarks>
    /// Cache keys: {url}-{lang}-{includeLayout}. Layout caching is separate from full model caching.
    /// Handles special "root" page case by normalizing to index path.
    /// </remarks>
    Task<ArticleViewModel?> GetPublishedPageByUrlAsync(
        string urlPath,
        string lang = "",
        TimeSpan? cacheSpan = null,
        TimeSpan? layoutCacheDuration = null,
        bool includeLayout = true);

    /// <summary>
    /// Lightweight header-only fetch of published page metadata (omits large content fields).
    /// Used for dependency checks or partial rendering scenarios.
    /// </summary>
    /// <param name="urlPath">URL path (e.g., "blog/my-article"). Case-insensitive. Root page is "root".</param>
    /// <returns>
    /// A minimal ArticleViewModel containing only: ArticleNumber, Id, Expires, Updated, VersionNumber.
    /// Useful for checking article existence or metadata without fetching full content.
    /// Returns null if not found or not published.
    /// </returns>
    Task<ArticleViewModel?> GetPublishedPageHeaderByUrlAsync(string urlPath);
}
