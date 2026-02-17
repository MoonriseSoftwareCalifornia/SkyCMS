// <copyright file="PublishedBlogService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.PublishedBlog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Service for accessing published blog content from the database.
    /// </summary>
    /// <remarks>
    /// This service provides read-only access to published blog streams and entries.
    /// All queries filter by publication status (Published date) and expiration (Expires date)
    /// to ensure only currently-active content is returned.
    /// </remarks>
    public class PublishedBlogService : IPublishedBlogService
    {
        private readonly ApplicationDbContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishedBlogService"/> class.
        /// </summary>
        /// <param name="dbContext">The application database context.</param>
        public PublishedBlogService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc/>
        public async Task<PublishedPage?> GetPublishedBlogStreamAsync(string blogKey)
        {
            if (string.IsNullOrWhiteSpace(blogKey))
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var blogStreamType = (int)ArticleType.BlogStream;

            return await _dbContext.Pages
                .Where(p => p.BlogKey == blogKey
                    && p.ArticleType == blogStreamType
                    && p.Published.HasValue
                    && p.Published <= now
                    && (p.Expires == null || p.Expires > now))
                .OrderByDescending(p => p.Published)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<PublishedPage?> GetPublishedBlogEntryAsync(string urlPath)
        {
            if (string.IsNullOrWhiteSpace(urlPath))
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var blogPostType = (int)ArticleType.BlogPost;

            return await _dbContext.Pages
                .Where(p => p.UrlPath == urlPath
                    && p.ArticleType == blogPostType
                    && p.Published.HasValue
                    && p.Published <= now
                    && (p.Expires == null || p.Expires > now))
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<PublishedPage>> GetBlogEntriesAsync(
            string blogKey,
            int pageSize = 10,
            int pageNumber = 1)
        {
            if (string.IsNullOrWhiteSpace(blogKey))
            {
                return Enumerable.Empty<PublishedPage>();
            }

            // Validate pagination parameters
            pageSize = Math.Max(1, Math.Min(pageSize, 100)); // Clamp between 1 and 100
            pageNumber = Math.Max(1, pageNumber);

            var now = DateTimeOffset.UtcNow;
            var blogPostType = (int)ArticleType.BlogPost;
            var blogStreamType = (int)ArticleType.BlogStream;

            var skip = (pageNumber - 1) * pageSize;

            return await _dbContext.Pages
                .Where(p => p.BlogKey == blogKey
                    && p.ArticleType == blogPostType  // Exclude the blog stream article itself
                    && p.Published.HasValue
                    && p.Published <= now
                    && (p.Expires == null || p.Expires > now))
                .OrderByDescending(p => p.Published)
                .ThenBy(p => p.Title)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<int> GetBlogEntryCountAsync(string blogKey)
        {
            if (string.IsNullOrWhiteSpace(blogKey))
            {
                return 0;
            }

            var now = DateTimeOffset.UtcNow;
            var blogPostType = (int)ArticleType.BlogPost;

            return await _dbContext.Pages
                .Where(p => p.BlogKey == blogKey
                    && p.ArticleType == blogPostType
                    && p.Published.HasValue
                    && p.Published <= now
                    && (p.Expires == null || p.Expires > now))
                .CountAsync();
        }

        /// <inheritdoc/>
        public async Task<PublishedPage?> GetPreviousBlogEntryAsync(string blogKey, DateTimeOffset publishedDate)
        {
            if (string.IsNullOrWhiteSpace(blogKey))
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var blogPostType = (int)ArticleType.BlogPost;

            // Get the most recent published entry with a date earlier than the reference date
            return await _dbContext.Pages
                .Where(p => p.BlogKey == blogKey
                    && p.ArticleType == blogPostType
                    && p.Published.HasValue
                    && p.Published < publishedDate  // Strictly less than (earlier)
                    && p.Published <= now
                    && (p.Expires == null || p.Expires > now))
                .OrderByDescending(p => p.Published)  // Most recent older entry
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<PublishedPage?> GetNextBlogEntryAsync(string blogKey, DateTimeOffset publishedDate)
        {
            if (string.IsNullOrWhiteSpace(blogKey))
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var blogPostType = (int)ArticleType.BlogPost;

            // Get the earliest published entry with a date later than the reference date
            return await _dbContext.Pages
                .Where(p => p.BlogKey == blogKey
                    && p.ArticleType == blogPostType
                    && p.Published.HasValue
                    && p.Published > publishedDate  // Strictly greater than (newer)
                    && p.Published <= now
                    && (p.Expires == null || p.Expires > now))
                .OrderBy(p => p.Published)  // Earliest newer entry
                .FirstOrDefaultAsync();
        }
    }
}
