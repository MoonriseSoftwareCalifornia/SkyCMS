// <copyright file="GetBlogPostNavigationQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Features.Blogs.EditorQueries
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler for retrieving navigation information for blog posts within a stream.
    /// Provides previous/next post links and optionally all posts in the stream.
    /// </summary>
    public class GetBlogPostNavigationQueryHandler : IQueryHandler<GetBlogPostNavigationQuery, GetBlogPostNavigationQueryResult>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetBlogPostNavigationQueryHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="memoryCache">Memory cache for caching results.</param>
        public GetBlogPostNavigationQueryHandler(ApplicationDbContext dbContext, IMemoryCache memoryCache)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        /// <summary>
        /// Handles the get blog post navigation query.
        /// </summary>
        /// <param name="query">The query containing blog key and current post URL.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The blog post navigation query result.</returns>
        public async Task<GetBlogPostNavigationQueryResult> HandleAsync(
            GetBlogPostNavigationQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            // Normalize blog key
            var normalizedBlogKey = NormalizeBlogKey(query.BlogKey);
            var normalizedCurrentUrl = query.CurrentPostUrlPath.ToLowerInvariant().Trim('/');

            // Try cache first
            var cacheKey = $"blog-nav-{normalizedBlogKey}-{(query.IncludeAllPosts ? "full" : "simple")}";
            if (memoryCache.TryGetValue(cacheKey, out GetBlogPostNavigationQueryResult? cachedResult))
            {
                return cachedResult;
            }

            // Fetch all published posts in this blog stream, ordered by publication date (newest first)
            var now = DateTimeOffset.UtcNow;
            var allPosts = await dbContext.Articles
                .AsNoTracking()
                .Where(a => a.BlogKey == normalizedBlogKey &&
                            a.ArticleType == (int)Cosmos.Cms.Common.ArticleType.BlogPost &&
                            a.Published.HasValue &&
                            a.Published <= now)
                .OrderByDescending(a => a.Published)
                .ToListAsync(cancellationToken);

            // Build result
            var result = new GetBlogPostNavigationQueryResult
            {
                BlogKey = normalizedBlogKey,
                TotalPostCount = allPosts.Count
            };

            // Find current post and build navigation
            var currentPostIndex = -1;
            for (int i = 0; i < allPosts.Count; i++)
            {
                if (allPosts[i].UrlPath.Equals(normalizedCurrentUrl, StringComparison.OrdinalIgnoreCase))
                {
                    currentPostIndex = i;
                    result.CurrentPostPosition = i + 1; // 1-based index
                    break;
                }
            }

            // Add previous post (index i-1, which is newer/higher in the list)
            if (currentPostIndex > 0)
            {
                var prevPost = allPosts[currentPostIndex - 1];
                result.PreviousPost = new BlogPostNavigationItem
                {
                    UrlPath = prevPost.UrlPath,
                    Title = prevPost.Title,
                    Published = prevPost.Published,
                    Position = currentPostIndex // 1-based
                };
            }

            // Add next post (index i+1, which is older/lower in the list)
            if (currentPostIndex >= 0 && currentPostIndex < allPosts.Count - 1)
            {
                var nextPost = allPosts[currentPostIndex + 1];
                result.NextPost = new BlogPostNavigationItem
                {
                    UrlPath = nextPost.UrlPath,
                    Title = nextPost.Title,
                    Published = nextPost.Published,
                    Position = currentPostIndex + 2 // 1-based
                };
            }

            // Add all posts if requested
            if (query.IncludeAllPosts)
            {
                result.AllPosts = allPosts
                    .Select((post, index) => new BlogPostNavigationItem
                    {
                        UrlPath = post.UrlPath,
                        Title = post.Title,
                        Published = post.Published,
                        Position = index + 1 // 1-based
                    })
                    .ToList();
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
        /// </summary>
        private static string NormalizeBlogKey(string blogKey)
        {
            return blogKey.Replace("_", "-").ToLowerInvariant();
        }
    }
}
