// <copyright file="IBlogStreamRenderingService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.BlogPublishing
{
    using System.Threading.Tasks;
    using Cosmos.Common.Data;

    /// <summary>
    /// Service for rendering blog streams and posts with client-side orchestration.
    /// </summary>
    /// <remarks>
    /// This service generates static-friendly HTML for blog streams and posts,
    /// supporting a hybrid static/client-side architecture where:
    /// - Blog stream wrappers contain embedded JSON metadata.
    /// - Individual posts render as standalone article snippets.
    /// - JavaScript loads and combines them on the client.
    /// </remarks>
    public interface IBlogStreamRenderingService
    {
        /// <summary>
        /// Generates a blog stream wrapper HTML document with embedded post metadata.
        /// </summary>
        /// <param name="article">The blog stream article containing title, introduction, and banner image.</param>
        /// <param name="blogKey">The blog stream key (used to fetch post metadata).</param>
        /// <returns>Complete HTML wrapper document with embedded JSON and loader script reference.</returns>
        /// <remarks>
        /// The wrapper HTML includes:
        /// - Stream header with title, introduction, and optional banner image
        /// - Embedded `<script type="application/json">` containing post metadata array
        /// - Empty `<div id="post-list">` for dynamic content insertion
        /// - Pagination nav container `<ul id="pagination">`
        /// - Script reference to `/js/blog-stream-loader.js`
        /// - No layout/master page wrapping (suitable for both static and dynamic modes)
        /// </remarks>
        Task<string> GenerateBlogStreamWrapperAsync(Article article, string blogKey);

        /// <summary>
        /// Generates a JSON array of blog post metadata for a given blog stream.
        /// </summary>
        /// <param name="blogKey">The blog stream key.</param>
        /// <returns>JSON string with array of post metadata objects.</returns>
        /// <remarks>
        /// Returns JSON with structure:
        /// ```json
        /// [
        ///   {
        ///     "urlPath": "/blog/painting/post-1",
        ///     "title": "Post Title",
        ///     "published": "2025-01-15T10:00:00Z",
        ///     "updated": "2025-01-16T14:30:00Z",
        ///     "introduction": "Short excerpt...",
        ///     "bannerImage": "/images/banner.jpg"
        ///   }
        /// ]
        /// ```
        /// Posts are ordered by published date (newest first), then by title for stability.
        /// Only published, non-expired posts are included.
        /// </remarks>
        Task<string> GenerateBlogPostMetadataJsonAsync(string blogKey);

        /// <summary>
        /// Generates a standalone `<article>` snippet for a blog post.
        /// </summary>
        /// <param name="article">The article to render.</param>
        /// <returns>HTML `<article>` element containing the post content.</returns>
        /// <remarks>
        /// The snippet includes:
        /// - Optional banner image
        /// - Post title
        /// - Content (pre-rendered HTML)
        /// - Author and date information
        /// - No outer layout/wrapper
        /// 
        /// This is suitable for dynamic insertion into a blog stream wrapper.
        /// </remarks>
        Task<string> GenerateBlogPostSnippetAsync(Article article);
    }
}
