// <copyright file="IPublishedBlogService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.PublishedBlog
{
    using Cosmos.Common.Data;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Service for accessing published blog content (streams and entries).
    /// </summary>
    /// <remarks>
    /// This service queries the <see cref="PublishedPage"/> table to provide read-only access
    /// to published blog streams and individual blog post entries. It handles:
    /// <list type="bullet">
    ///   <item>Fetching blog streams (landing pages) by blog key</item>
    ///   <item>Listing all published entries within a blog stream</item>
    ///   <item>Fetching individual blog posts by URL path</item>
    ///   <item>Computing previous/next entry navigation</item>
    ///   <item>Filtering by publication windows (Published/Expires dates)</item>
    /// </list>
    /// All methods respect publication dates and skip expired or unpublished content.
    /// </remarks>
    public interface IPublishedBlogService
    {
        /// <summary>
        /// Gets the published blog stream (landing page) for the specified blog key.
        /// </summary>
        /// <param name="blogKey">The unique identifier for the blog stream (e.g., "travel-adventures").</param>
        /// <returns>
        /// The published blog stream article, or null if no published stream exists for the given key.
        /// </returns>
        /// <remarks>
        /// A blog stream is a <see cref="PublishedPage"/> with <see cref="PublishedPage.ArticleType"/> == <c>BlogStream</c>
        /// (integer value 2) and a <c>UrlPath</c> equal to the <paramref name="blogKey"/>.
        /// Only returns published and non-expired entries.
        /// </remarks>
        Task<PublishedPage?> GetPublishedBlogStreamAsync(string blogKey);

        /// <summary>
        /// Gets a published blog entry (post) by its URL path.
        /// </summary>
        /// <param name="urlPath">The complete URL path of the entry (e.g., "travel-adventures/my-trip-to-japan").</param>
        /// <returns>
        /// The published blog entry, or null if no published entry exists at the given path.
        /// </returns>
        /// <remarks>
        /// Only returns published and non-expired entries.
        /// This method is used to render individual blog post pages when a visitor requests a specific URL.
        /// </remarks>
        Task<PublishedPage?> GetPublishedBlogEntryAsync(string urlPath);

        /// <summary>
        /// Lists all published entries within a specific blog stream with paging support.
        /// </summary>
        /// <param name="blogKey">The unique identifier for the blog stream.</param>
        /// <param name="pageSize">Number of entries per page (default: 10).</param>
        /// <param name="pageNumber">1-based page number (default: 1).</param>
        /// <returns>
        /// A collection of published blog entries sorted by <see cref="PublishedPage.Published"/> (descending, newest first),
        /// then by <see cref="PublishedPage.Title"/> (ascending) for tie-breaking.
        /// </returns>
        /// <remarks>
        /// Excludes the blog stream article itself (filters by ArticleType != BlogStream).
        /// Only returns published and non-expired entries.
        /// Results are sorted with newest posts first (descending publication date).
        /// </remarks>
        Task<IEnumerable<PublishedPage>> GetBlogEntriesAsync(
            string blogKey,
            int pageSize = 10,
            int pageNumber = 1);

        /// <summary>
        /// Gets the total count of published entries within a blog stream.
        /// </summary>
        /// <param name="blogKey">The unique identifier for the blog stream.</param>
        /// <returns>The count of published, non-expired entries in the stream (excluding the stream article itself).</returns>
        /// <remarks>
        /// Useful for calculating pagination metadata (total pages, etc.).
        /// Only counts published and non-expired entries.
        /// </remarks>
        Task<int> GetBlogEntryCountAsync(string blogKey);

        /// <summary>
        /// Gets the previous blog entry (chronologically older) relative to the specified published date.
        /// </summary>
        /// <param name="blogKey">The unique identifier for the blog stream.</param>
        /// <param name="publishedDate">The reference publication date to search backwards from.</param>
        /// <returns>
        /// The most recent published entry with a <see cref="PublishedPage.Published"/> date earlier than the given date,
        /// or null if no such entry exists.
        /// </returns>
        /// <remarks>
        /// Used for blog post navigation ("previous post" links).
        /// Returns null if this is the oldest entry in the stream.
        /// Only returns published and non-expired entries.
        /// </remarks>
        Task<PublishedPage?> GetPreviousBlogEntryAsync(string blogKey, DateTimeOffset publishedDate);

        /// <summary>
        /// Gets the next blog entry (chronologically newer) relative to the specified published date.
        /// </summary>
        /// <param name="blogKey">The unique identifier for the blog stream.</param>
        /// <param name="publishedDate">The reference publication date to search forward from.</param>
        /// <returns>
        /// The earliest published entry with a <see cref="PublishedPage.Published"/> date later than the given date,
        /// or null if no such entry exists.
        /// </returns>
        /// <remarks>
        /// Used for blog post navigation ("next post" links).
        /// Returns null if this is the newest entry in the stream.
        /// Only returns published and non-expired entries.
        /// </remarks>
        Task<PublishedPage?> GetNextBlogEntryAsync(string blogKey, DateTimeOffset publishedDate);
    }
}
