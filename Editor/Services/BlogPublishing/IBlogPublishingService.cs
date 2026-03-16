// <copyright file="IBlogPublishingService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.BlogPublishing
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.BlobService.Models;
    using Cosmos.Common.Data;
    using Cosmos.Common.Models;
    using Sky.Editor.Services.CDN;

    /// <summary>
    /// Service for publishing blog streams and blog posts with specialized rendering.
    /// </summary>
    /// <remarks>
    /// This service handles blog-specific publishing operations including:
    /// - Blog stream wrapper generation and publishing
    /// - Blog post full-page rendering with layouts
    /// - Versioned wrapper file management for cache busting
    /// </remarks>
    public interface IBlogPublishingService
    {
        /// <summary>
        /// Publishes (or updates) a blog stream page for the specified blog key and user.
        /// </summary>
        /// <param name="blog">The blog stream metadata and content input. The <see cref="Article.BlogKey"/> identifies the stream.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of CDN purge results indicating cache invalidation status per provider after publishing.</returns>
        /// <remarks>
        /// <para>
        /// If a blog stream article already exists for the given <see cref="Article.BlogKey"/>,
        /// its metadata is updated and the <see cref="Article.VersionNumber"/> is incremented;
        /// otherwise a new article record is created.
        /// </para>
        /// <para>
        /// The operation performs the following actions:
        /// </para>
        /// <list type="number">
        ///   <item><description>Creates or updates the blog stream article record</description></item>
        ///   <item><description>Generates wrapper HTML with embedded JSON metadata</description></item>
        ///   <item><description>Publishes the blog stream article (creates PublishedPage, static files, TOC)</description></item>
        ///   <item><description>Uploads versioned wrapper for direct access (cache busting)</description></item>
        ///   <item><description>Purges CDN cache</description></item>
        /// </list>
        /// </remarks>
        Task<List<CdnResult>> PublishBlogStreamAsync(Article blog, CancellationToken cancellationToken = default);

        /// <summary>
        /// Renders a blog post article with full layout for publishing.
        /// </summary>
        /// <param name="article">The blog post article to render. Must have <see cref="Article.ArticleType"/> set to <see cref="ArticleType.BlogPost"/>.</param>
        /// <param name="authorInfo">The author information to include in the rendered page.</param>
        /// <returns>A <see cref="PublishedPage"/> with fully rendered HTML content.</returns>
        /// <remarks>
        /// <para>
        /// Generates a complete HTML page for blog posts with:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Default site layout</description></item>
        ///   <item><description>Article content rendered via Razor views</description></item>
        ///   <item><description>Author metadata</description></item>
        ///   <item><description>JavaScript includes (header and footer)</description></item>
        /// </list>
        /// <para>
        /// This is used for direct access to individual blog posts (e.g., /blog/my-post).
        /// Snippet versions for stream display are generated on-demand via <see cref="Cosmos.Common.Services.BlogPublishing.IBlogStreamRenderingService"/>.
        /// </para>
        /// </remarks>
        Task<PublishedPage> RenderBlogPostPageAsync(Article article, string authorInfo);
    }
}
