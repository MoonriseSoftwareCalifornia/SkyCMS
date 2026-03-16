// <copyright file="GetBlogStreamQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Blogs.Queries
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler for retrieving a blog stream by its key, including the latest post preview.
    /// </summary>
    public class GetBlogStreamQueryHandler : IQueryHandler<GetBlogStreamQuery, GetBlogStreamQueryResult>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetBlogStreamQueryHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="memoryCache">Memory cache for caching results.</param>
        public GetBlogStreamQueryHandler(ApplicationDbContext dbContext, IMemoryCache memoryCache)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        /// <summary>
        /// Handles the get blog stream query.
        /// </summary>
        /// <param name="query">The query containing the blog key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The blog stream query result or null if not found.</returns>
        public async Task<GetBlogStreamQueryResult?> HandleAsync(
            GetBlogStreamQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (string.IsNullOrWhiteSpace(query.BlogKey))
            {
                return null;
            }

            // Normalize the blog key (handle both underscore and hyphen formats)
            var normalizedBlogKey = NormalizeBlogKey(query.BlogKey);

            // Try cache first
            var cacheKey = $"blog-stream-{normalizedBlogKey}";
            if (memoryCache.TryGetValue(cacheKey, out GetBlogStreamQueryResult? cachedResult))
            {
                return cachedResult;
            }

            // Fetch the blog stream from database
            var now = DateTimeOffset.UtcNow;
            var blogStream = await dbContext.Articles
                .AsNoTracking()
                .Where(a => (a.BlogKey == normalizedBlogKey || a.UrlPath == normalizedBlogKey) &&
                            a.ArticleType == (int)Cosmos.Cms.Common.ArticleType.BlogStream &&
                            a.Published.HasValue &&
                            a.Published <= now)
                .OrderByDescending(a => a.Published)
                .FirstOrDefaultAsync(cancellationToken);

            if (blogStream == null)
            {
                return null;
            }

            // Fetch the latest blog post in this stream
            var latestPost = await dbContext.Articles
                .AsNoTracking()
                .Where(a => a.BlogKey == normalizedBlogKey &&
                            a.ArticleType == (int)Cosmos.Cms.Common.ArticleType.BlogPost &&
                            a.Published.HasValue &&
                            a.Published <= now)
                .OrderByDescending(a => a.Published)
                .FirstOrDefaultAsync(cancellationToken);

            // Count published posts
            var publishedPostCount = await dbContext.Articles
                .Where(a => a.BlogKey == normalizedBlogKey &&
                            a.ArticleType == (int)Cosmos.Cms.Common.ArticleType.BlogPost &&
                            a.Published.HasValue &&
                            a.Published <= now)
                .CountAsync(cancellationToken);

            // Build result
            var result = new GetBlogStreamQueryResult
            {
                StreamId = blogStream.Id,
                Title = blogStream.Title,
                Description = blogStream.Introduction ?? string.Empty,
                HeroImage = blogStream.BannerImage ?? string.Empty,
                UrlPath = blogStream.UrlPath,
                BlogKey = blogStream.BlogKey,
                Published = blogStream.Published,
                Updated = blogStream.Updated,
                PublishedPostCount = publishedPostCount
            };

            // Add latest post preview if available
            if (latestPost != null)
            {
                result.LatestPost = new BlogPostPreview
                {
                    Id = latestPost.Id,
                    Title = latestPost.Title,
                    UrlPath = latestPost.UrlPath,
                    Published = latestPost.Published,
                    Updated = latestPost.Updated,
                    Excerpt = latestPost.Introduction ?? string.Empty,
                    Author = latestPost.UserId ?? string.Empty
                };
            }

            // Cache the result
            if (query.CacheDuration.HasValue)
            {
                memoryCache.Set(cacheKey, result, query.CacheDuration.Value);
            }

            return result;
        }

        /// <summary>
        /// Normalizes a blog key by converting underscores to hyphens.
        /// This allows the handler to find streams regardless of slug format.
        /// </summary>
        private static string NormalizeBlogKey(string blogKey)
        {
            return blogKey.Replace("_", "-").ToLowerInvariant();
        }
    }
}
