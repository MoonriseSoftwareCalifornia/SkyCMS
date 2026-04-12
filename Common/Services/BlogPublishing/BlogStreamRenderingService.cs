// <copyright file="BlogStreamRenderingService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.BlogPublishing
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Renders blog streams and posts using a client-side orchestration model.
    /// </summary>
    /// <remarks>
    /// This service supports both static site generation and dynamic rendering modes:
    /// - Static: Generates versioned wrapper files with embedded JSON, individual post snippets
    /// - Dynamic: Generates wrappers and post content on-demand
    /// 
    /// The architecture minimizes server-side rendering overhead by:
    /// - Embedding post metadata as JSON (no fetch required).
    /// - Rendering individual posts as standalone snippets.
    /// - Letting JavaScript orchestrate pagination and content insertion.
    /// </remarks>
    public class BlogStreamRenderingService : IBlogStreamRenderingService
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlogStreamRenderingService"/> class.
        /// </summary>
        /// <param name="db">The application database context.</param>
        public BlogStreamRenderingService(ApplicationDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <inheritdoc/>
        public async Task<string> GenerateBlogStreamWrapperAsync(Article article, string blogKey)
        {
            if (article == null)
            {
                throw new ArgumentNullException(nameof(article));
            }

            if (string.IsNullOrWhiteSpace(blogKey))
            {
                throw new ArgumentException("Blog key cannot be null or empty.", nameof(blogKey));
            }

            // Generate metadata for embedding
            var metadataJson = await GenerateBlogPostMetadataJsonAsync(blogKey);

            // Generate the wrapper HTML
            return GenerateWrapperHtml(article, metadataJson);
        }

        /// <inheritdoc/>
        public async Task<string> GenerateBlogPostMetadataJsonAsync(string blogKey, int maxPosts = 500)
        {
            if (string.IsNullOrWhiteSpace(blogKey))
            {
                throw new ArgumentException("Blog key cannot be null or empty.", nameof(blogKey));
            }

            if (maxPosts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPosts), "maxPosts must be greater than zero.");
            }

            var now = DateTimeOffset.UtcNow;
            var blogPostType = (int)ArticleType.BlogPost;

            var posts = await _db.Pages
                .Where(p => p.BlogKey == blogKey
                    && p.ArticleType == blogPostType
                    && p.Published.HasValue
                    && p.Published <= now
                    && (p.Expires == null || p.Expires > now))
                .OrderByDescending(p => p.Published)
                .ThenBy(p => p.Title)
                .Take(maxPosts)
                .Select(p => new
                {
                    urlPath = p.UrlPath,
                    title = p.Title,
                    published = p.Published.Value.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    updated = p.Updated.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    introduction = p.Introduction ?? string.Empty,
                    bannerImage = p.BannerImage ?? string.Empty
                })
                .ToListAsync();

            return JsonConvert.SerializeObject(posts);
        }

        /// <inheritdoc/>
        public async Task<string> GenerateBlogPostSnippetAsync(Article article)
        {
            if (article == null)
            {
                throw new ArgumentNullException(nameof(article));
            }

            var bannerHtml = string.IsNullOrWhiteSpace(article.BannerImage)
                ? string.Empty
                : $"<figure class=\"sky-blog-stream-figure\"><img src=\"{System.Net.WebUtility.HtmlEncode(article.BannerImage)}\" alt=\"{System.Net.WebUtility.HtmlEncode(article.Title)}\" class=\"sky-blog-post-image\"></figure>";

            var html = $@"<article class=""sky-blog-post-article"">
    {bannerHtml}
    <div class=""sky-blog-post-body"">
        <p class=""sky-blog-post-info"">
            <time class=""sky-blog-post-info-updated"" datetime=""{System.Net.WebUtility.HtmlEncode(article.Updated.ToString("yyyy-MM-ddTHH:mm:sszzz"))}"">
                {article.Updated:MMMM d, yyyy}
            </time>
        </p>
        <h2 class=""sky-blog-post-title"">{System.Net.WebUtility.HtmlEncode(article.Title)}</h2>
        <div class=""sky-blog-post-content"">
            {article.Content}
        </div>
    </div>
</article>";

            return await Task.FromResult(html);
        }

        /// <summary>
        /// Generates the blog stream wrapper HTML with embedded JSON metadata.
        /// </summary>
        /// <param name="article">The blog stream article.</param>
        /// <param name="metadataJson">The JSON string of blog post metadata.</param>
        /// <returns>Complete HTML wrapper document.</returns>
        private string GenerateWrapperHtml(Article article, string metadataJson)
        {
            var bannerHtml = string.IsNullOrWhiteSpace(article.BannerImage)
                ? string.Empty
                : $"<figure class=\"sky-blog-stream-figure\"><img src=\"{System.Net.WebUtility.HtmlEncode(article.BannerImage)}\" alt=\"{System.Net.WebUtility.HtmlEncode(article.Title)}\" class=\"sky-blog-stream-card-image\"></figure>";

            var introHtml = string.IsNullOrWhiteSpace(article.Introduction)
                ? string.Empty
                : $"<p class=\"sky-blog-stream-intro\">{System.Net.WebUtility.HtmlEncode(article.Introduction)}</p>";

            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{System.Net.WebUtility.HtmlEncode(article.Title)}</title>
    <link rel=""stylesheet"" href=""/css/sky-blog.css"">
</head>
<body>
    <section class=""sky-blog-stream-section"" aria-labelledby=""blog-heading"">
        <div class=""sky-blog-stream-container"">
            <header class=""sky-blog-stream-header"">
                {bannerHtml}
                <h1 class=""sky-blog-stream-h1"" id=""blog-heading"">{System.Net.WebUtility.HtmlEncode(article.Title)}</h1>
                {introHtml}
            </header>
            
            <script type=""application/json"" id=""blog-posts-meta"">
{metadataJson}
            </script>
            
            <div class=""sky-blog-stream-row"" id=""post-list""></div>
            
            <nav class=""sky-blog-stream-nav-container"" aria-label=""Blog pagination"">
                <ul class=""sky-blog-stream-nav-pagination"" id=""pagination""></ul>
            </nav>
        </div>
    </section>
    
    <script src=""/js/blog-stream-loader.js""></script>
</body>
</html>";

            return html;
        }
    }
}
