// <copyright file="GetBlogPostQueryHandler.cs" company="Moonrise Software, LLC">
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
    /// Handler for retrieving a single blog post by its URL path.
    /// Optionally includes navigation information for prev/next posts.
    /// </summary>
    public class GetBlogPostQueryHandler : IQueryHandler<GetBlogPostQuery, GetBlogPostQueryResult>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetBlogPostQueryHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="memoryCache">Memory cache for caching results.</param>
        public GetBlogPostQueryHandler(ApplicationDbContext dbContext, IMemoryCache memoryCache)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        /// <summary>
        /// Handles the get blog post query.
        /// </summary>
        /// <param name="query">The query containing the post URL path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The blog post query result or null if not found.</returns>
        public async Task<GetBlogPostQueryResult?> HandleAsync(
            GetBlogPostQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (string.IsNullOrWhiteSpace(query.UrlPath))
            {
                return null;
            }

            // Normalize the URL path
            var normalizedUrlPath = query.UrlPath.ToLowerInvariant().Trim('/');

            // Try cache first
            var cacheKey = $"blog-post-{normalizedUrlPath}";
            if (memoryCache.TryGetValue(cacheKey, out GetBlogPostQueryResult? cachedResult))
            {
                return cachedResult;
            }

            // Fetch the blog post from database
            var now = DateTimeOffset.UtcNow;
            var blogPost = await dbContext.Articles
                .AsNoTracking()
                .Where(a => a.UrlPath == normalizedUrlPath &&
                            a.ArticleType == (int)Cosmos.Cms.Common.ArticleType.BlogPost &&
                            a.Published.HasValue &&
                            a.Published <= now)
                .FirstOrDefaultAsync(cancellationToken);

            if (blogPost == null)
            {
                return null;
            }

            // Build result
            var result = new GetBlogPostQueryResult
            {
                Id = blogPost.Id,
                Title = blogPost.Title,
                Content = blogPost.Content ?? string.Empty,
                Introduction = blogPost.Introduction ?? string.Empty,
                UrlPath = blogPost.UrlPath,
                BlogKey = blogPost.BlogKey,
                BannerImage = blogPost.BannerImage ?? string.Empty,
                Published = blogPost.Published,
                Updated = blogPost.Updated,
                Author = blogPost.UserId ?? string.Empty
            };

            // Fetch the parent blog stream info
            if (!string.IsNullOrWhiteSpace(blogPost.BlogKey))
            {
                var parentStream = await dbContext.Articles
                    .AsNoTracking()
                    .Where(a => a.BlogKey == blogPost.BlogKey &&
                                a.ArticleType == (int)Cosmos.Cms.Common.ArticleType.BlogStream &&
                                a.Published.HasValue &&
                                a.Published <= now)
                    .FirstOrDefaultAsync(cancellationToken);

                if (parentStream != null)
                {
                    result.BlogStreamTitle = parentStream.Title;
                    result.BlogStreamUrl = parentStream.UrlPath;
                }
            }

            // Add navigation if requested
            if (query.IncludeNavigation && !string.IsNullOrWhiteSpace(blogPost.BlogKey))
            {
                result.Navigation = await GetBlogPostNavigation(
                    blogPost.BlogKey,
                    blogPost.Published ?? now,
                    cancellationToken);
            }

            // Cache the result
            if (query.CacheDuration.HasValue)
            {
                memoryCache.Set(cacheKey, result, query.CacheDuration.Value);
            }

            return result;
        }

        /// <summary>
        /// Gets navigation information (previous and next posts) for a blog post.
        /// </summary>
        private async Task<BlogPostNavigation> GetBlogPostNavigation(
            string blogKey,
            DateTimeOffset currentPostDate,
            CancellationToken cancellationToken)
        {
            var navigation = new BlogPostNavigation();
            var now = DateTimeOffset.UtcNow;

            // Get previous post (newer - published after current)
            var previousPost = await dbContext.Articles
                .AsNoTracking()
                .Where(a => a.BlogKey == blogKey &&
                            a.ArticleType == (int)Cosmos.Cms.Common.ArticleType.BlogPost &&
                            a.Published.HasValue &&
                            a.Published <= now &&
                            a.Published > currentPostDate)
                .OrderByDescending(a => a.Published)
                .FirstOrDefaultAsync(cancellationToken);

            if (previousPost != null)
            {
                navigation.PreviousPost = new BlogPostLink
                {
                    Title = previousPost.Title,
                    UrlPath = previousPost.UrlPath,
                    Published = previousPost.Published
                };
            }

            // Get next post (older - published before current)
            var nextPost = await dbContext.Articles
                .AsNoTracking()
                .Where(a => a.BlogKey == blogKey &&
                            a.ArticleType == (int)Cosmos.Cms.Common.ArticleType.BlogPost &&
                            a.Published.HasValue &&
                            a.Published < currentPostDate)
                .OrderByDescending(a => a.Published)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextPost != null)
            {
                navigation.NextPost = new BlogPostLink
                {
                    Title = nextPost.Title,
                    UrlPath = nextPost.UrlPath,
                    Published = nextPost.Published
                };
            }

            return navigation;
        }
    }
}
