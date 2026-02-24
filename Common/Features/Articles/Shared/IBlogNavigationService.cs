// <copyright file="IBlogNavigationService.cs" company="Moonrise Software, LLC">
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
/// Service for blog post navigation - previous/next post links and enrichment.
/// Provides navigation metadata for blog post view models.
/// </summary>
/// <remarks>
/// Blog posts benefit from chronological navigation (previous/next article by publish date).
/// This service encapsulates blog-specific navigation queries and view model enrichment.
/// </remarks>
public interface IBlogNavigationService
{
    /// <summary>
    /// Fetches the previous and next published blog posts relative to a given publish timestamp.
    /// </summary>
    /// <param name="published">The publish timestamp to compare against for finding adjacent posts.</param>
    /// <returns>
    /// A tuple containing (previous, next) TableOfContentsItem entries.
    /// Either or both can be null if no adjacent posts exist.
    /// </returns>
    Task<(TableOfContentsItem? previous, TableOfContentsItem? next)> GetAdjacentBlogPostsAsync(DateTimeOffset published);

    /// <summary>
    /// Enriches a blog post view model with previous/next navigation links when applicable.
    /// </summary>
    /// <param name="model">Blog post view model to enrich. Must be a blog post type with published date.</param>
    /// <returns>Task representing the asynchronous enrichment operation.</returns>
    /// <remarks>
    /// No-op if model is null, not a blog post type, or unpublished.
    /// Sets PreviousTitle, PreviousUrl, NextTitle, NextUrl on the model in-place.
    /// Normalizes URLs (converts "root" to "/").
    /// </remarks>
    Task EnrichBlogNavigationAsync(ArticleViewModel? model);
}
