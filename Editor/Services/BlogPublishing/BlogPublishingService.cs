// <copyright file="BlogPublishingService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.BlogPublishing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Constants;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Layouts.Queries;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services.BlogPublishing;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Sky.Cms.Services;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.Publishing;

    /// <summary>
    /// Service for publishing blog streams and blog posts with specialized rendering.
    /// </summary>
    public class BlogPublishingService : IBlogPublishingService
    {
        private readonly IBlogPublishingContext context;
        private readonly IBlogStreamRenderingService blogStreamRenderingService;
        private readonly IViewRenderService viewRenderService;
        private readonly IMediator mediator;
        private readonly IPublishingService publishingService;
        private readonly ILogger<BlogPublishingService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlogPublishingService"/> class.
        /// </summary>
        /// <param name="context">Blog publishing context providing database, storage, and HTTP context.</param>
        /// <param name="blogStreamRenderingService">The blog stream rendering service.</param>
        /// <param name="viewRenderService">View rendering service.</param>
        /// <param name="mediator">Mediator for CQRS queries.</param>
        /// <param name="publishingService">The publishing service for delegating core publishing operations.</param>
        /// <param name="logger">The logger.</param>
        public BlogPublishingService(
            IBlogPublishingContext context,
            IBlogStreamRenderingService blogStreamRenderingService,
            IViewRenderService viewRenderService,
            IMediator mediator,
            IPublishingService publishingService,
            ILogger<BlogPublishingService> logger)
        {
            this.context = context;
            this.blogStreamRenderingService = blogStreamRenderingService;
            this.viewRenderService = viewRenderService;
            this.mediator = mediator;
            this.publishingService = publishingService;
            this.logger = logger;
        }

        private Guid UserId => Guid.Parse(context.HttpContextAccessor.HttpContext.User.Claims
            .FirstOrDefault(f => f.Type == "sub")?.Value ?? Guid.Empty.ToString());

        /// <inheritdoc/>
        public async Task<List<CdnResult>> PublishBlogStreamAsync(Article blog, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Publishing blog stream for blog key: {BlogKey}", blog.BlogKey);

            var blogStreamType = (int)ArticleType.BlogStream;
            var article = await context.Database.Articles
                .Where(a => a.BlogKey == blog.BlogKey && a.ArticleType == blogStreamType)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (article == null)
            {
                logger.LogDebug("Creating new blog stream article for blog key: {BlogKey}", blog.BlogKey);

                var articleNumber = (await context.Database.Articles.AnyAsync(cancellationToken)) ?
                    (await context.Database.Articles.Select(s => s.ArticleNumber).MaxAsync(cancellationToken)) + 1 : 1;

                article = new Article
                {
                    ArticleNumber = articleNumber,
                    UrlPath = blog.BlogKey,
                    VersionNumber = 1,
                    Published = DateTimeOffset.UtcNow,
                    Expires = null,
                    Title = blog.Title,
                    Content = string.Empty,
                    Updated = blog.Updated,
                    BannerImage = blog.BannerImage,
                    HeaderJavaScript = string.Empty,
                    FooterJavaScript = string.Empty,
                    UserId = UserId.ToString(),
                    StatusCode = (int)StatusCodeEnum.Active,
                    ArticleType = (int)ArticleType.BlogStream,
                    Category = "blog-stream",
                    Introduction = blog.Introduction,
                    BlogKey = blog.BlogKey
                };

                context.Database.Articles.Add(article);
            }
            else
            {
                logger.LogDebug("Updating existing blog stream article (ArticleNumber: {ArticleNumber}) for blog key: {BlogKey}", article.ArticleNumber, blog.BlogKey);

                article.UrlPath = blog.BlogKey;
                article.Published = DateTimeOffset.UtcNow;
                article.Title = blog.Title;
                article.Updated = blog.Updated;
                article.BannerImage = blog.BannerImage;
                article.Introduction = blog.Introduction;
                article.UserId = UserId.ToString();
                article.StatusCode = (int)StatusCodeEnum.Active;
                article.VersionNumber += 1;
            }

            // Generate wrapper HTML with embedded JSON metadata
            article.Content = await blogStreamRenderingService.GenerateBlogStreamWrapperAsync(article, blog.BlogKey);

            // Publish the blog stream article
            logger.LogDebug("Delegating to PublishingService for blog stream article");
            var cdnResults = await publishingService.PublishAsync(article, cancellationToken);

            // Additionally publish the versioned wrapper as a static file for direct access
            var wrapperPath = GetWrapperVersionedPath(blog.BlogKey);
            logger.LogDebug("Uploading versioned wrapper to: {WrapperPath}", wrapperPath);

            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(article.Content));
            await context.Storage.AppendBlob(ms, new FileUploadMetaData
            {
                ChunkIndex = 0,
                ContentType = "text/html",
                FileName = Path.GetFileName(wrapperPath),
                RelativePath = wrapperPath,
                TotalChunks = 1,
                TotalFileSize = ms.Length,
                UploadUid = Guid.NewGuid().ToString()
            });

            logger.LogInformation("Blog stream published successfully for blog key: {BlogKey}, ArticleNumber: {ArticleNumber}", blog.BlogKey, article.ArticleNumber);

            return cdnResults;
        }

        /// <inheritdoc/>
        public async Task<PublishedPage> RenderBlogPostPageAsync(Article article, string authorInfo)
        {
            if (article.ArticleType != (int)ArticleType.BlogPost)
            {
                throw new ArgumentException($"Article must be of type BlogPost. Received: {(ArticleType)(article.ArticleType ?? 0)}", nameof(article));
            }

            logger.LogDebug("Rendering blog post page for ArticleNumber: {ArticleNumber}, UrlPath: {UrlPath}", article.ArticleNumber, article.UrlPath);

            // Get the default layout for rendering
            var layout = await mediator.QueryAsync(new GetDefaultLayoutQuery());

            var model = new ArticleViewModel()
            {
                ArticleNumber = article.ArticleNumber,
                Title = article.Title,
                Content = article.Content,
                HeadJavaScript = article.HeaderJavaScript,
                FooterJavaScript = article.FooterJavaScript,
                Updated = article.Updated,
                Published = article.Published,
                Expires = article.Expires,
                BannerImage = article.BannerImage,
                UrlPath = article.UrlPath,
                ArticleType = ArticleType.BlogPost,
                Category = article.Category,
                Introduction = article.Introduction,
                Id = article.Id,
                EditModeOn = false,
                PreviewMode = false,
                ReadWriteMode = false,
                VersionNumber = article.VersionNumber,
                CacheDuration = 0,
                Layout = layout
            };

            var blogContent = await viewRenderService.RenderToStringAsync("~/Views/Home/Index.cshtml", model);

            var page = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = article.ArticleNumber,
                StatusCode = article.StatusCode,
                UrlPath = article.UrlPath,
                VersionNumber = article.VersionNumber,
                Published = article.Published,
                Expires = article.Expires,
                Title = article.Title,
                Content = blogContent,
                Updated = article.Updated,
                BannerImage = article.BannerImage,
                HeaderJavaScript = article.HeaderJavaScript,
                FooterJavaScript = article.FooterJavaScript,
                ParentUrlPath = article.UrlPath.Contains('/')
                    ? article.UrlPath[..article.UrlPath.LastIndexOf('/')]
                    : string.Empty,
                AuthorInfo = authorInfo ?? string.Empty,
                ArticleType = article.ArticleType,
                Category = article.Category,
                Introduction = article.Introduction,
                BlogKey = article.BlogKey
            };

            logger.LogDebug("Blog post page rendered successfully for ArticleNumber: {ArticleNumber}", article.ArticleNumber);

            return page;
        }

        /// <summary>
        /// Gets a versioned wrapper filename using UTC ticks for cache busting.
        /// </summary>
        /// <param name="blogKey">The blog stream key.</param>
        /// <returns>Relative file path for the versioned wrapper (e.g., /blog/painting/blog-stream-wrapper-638708432156789123.html).</returns>
        private string GetWrapperVersionedPath(string blogKey)
        {
            var ticks = DateTimeOffset.UtcNow.Ticks;
            return $"/{blogKey.TrimStart('/')}/blog-stream-wrapper-{ticks}.html";
        }
    }
}
