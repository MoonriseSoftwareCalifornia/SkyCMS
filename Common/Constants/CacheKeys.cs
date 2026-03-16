// <copyright file="CacheKeys.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Constants
{
    using System;

    /// <summary>
    /// Centralized cache key definitions for IMemoryCache operations.
    /// </summary>
    /// <remarks>
    /// This class provides a single source of truth for all cache keys used throughout the application.
    /// Using static methods for parameterized keys ensures type safety and prevents typos.
    /// </remarks>
    public static class CacheKeys
    {
        /// <summary>
        /// Cache key for the full site sitemap.
        /// </summary>
        /// <remarks>
        /// Recommended TTL: 30-60 minutes.
        /// Invalidated when: Articles published/unpublished, layouts changed.
        /// </remarks>
        public const string Sitemap = "Sitemap";

        /// <summary>
        /// Cache key for checking if a default layout exists.
        /// </summary>
        /// <remarks>
        /// Recommended TTL: 5-10 minutes.
        /// Invalidated when: Layouts published/unpublished.
        /// </remarks>
        public const string DefaultLayoutExists = "DefaultLayoutExists";

        /// <summary>
        /// Cache key for the default layout entity.
        /// </summary>
        /// <remarks>
        /// Recommended TTL: 10-30 minutes.
        /// Invalidated when: Default layout published.
        /// </remarks>
        public const string DefaultLayout = "defLayout";

        /// <summary>
        /// Cache key for the full article redirects list.
        /// </summary>
        /// <remarks>
        /// Recommended TTL: 5-10 minutes.
        /// Invalidated when: Articles published/unpublished (status changes).
        /// </remarks>
        public const string ArticleRedirects = "ArticleRedirects";

        /// <summary>
        /// Gets the cache key for a specific article's catalog entry.
        /// </summary>
        /// <param name="articleNumber">The article number.</param>
        /// <returns>Cache key string in format "ArticleCatalog_{articleNumber}".</returns>
        /// <remarks>
        /// Recommended TTL: 5-15 minutes.
        /// Invalidated when: Article published/unpublished, catalog updated.
        /// </remarks>
        public static string ArticleCatalog(int articleNumber) => $"ArticleCatalog_{articleNumber}";

        /// <summary>
        /// Gets the cache key for a specific layout by ID.
        /// </summary>
        /// <param name="layoutId">The layout GUID.</param>
        /// <returns>Cache key string in format "Layout_{layoutId}".</returns>
        /// <remarks>
        /// Recommended TTL: 10-30 minutes.
        /// Invalidated when: Layout published/updated.
        /// </remarks>
        public static string Layout(Guid layoutId) => $"Layout_{layoutId}";

        /// <summary>
        /// Gets the cache key for a specific article's last published date.
        /// </summary>
        /// <param name="articleNumber">The article number.</param>
        /// <returns>Cache key string in format "LastPublished_{articleNumber}".</returns>
        /// <remarks>
        /// Recommended TTL: 5-10 minutes.
        /// Invalidated when: Article published/unpublished.
        /// </remarks>
        public static string LastPublished(int articleNumber) => $"LastPublished_{articleNumber}";

        /// <summary>
        /// Gets the cache key for a blog stream by its key.
        /// </summary>
        /// <param name="blogKey">The blog key (e.g., "cat-wash").</param>
        /// <returns>Cache key string in format "blog-stream-{blogKey}".</returns>
        /// <remarks>
        /// Recommended TTL: 10-20 minutes.
        /// Invalidated when: Blog stream or blog posts published/unpublished.
        /// </remarks>
        public static string BlogStream(string blogKey) => $"blog-stream-{blogKey}";
    }
}
