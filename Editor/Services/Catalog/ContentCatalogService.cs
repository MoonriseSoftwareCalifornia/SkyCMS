// <copyright file="ContentCatalogService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Default implementation of <see cref="IContentCatalogService"/>.
    /// Queries article and template catalog metadata from the database.
    /// </summary>
    public class ContentCatalogService : IContentCatalogService
    {
        private readonly IApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentCatalogService"/> class.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        public ContentCatalogService(IApplicationDbContext dbContext)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <inheritdoc/>
        public async Task<List<ArticleCatalogSummary>> GetArticlesAsync()
        {
            var raw = await this.dbContext.ArticleCatalog
                .Select(s => new { s.ArticleNumber, s.Title, s.Updated })
                .ToListAsync();

            return raw.Select(s => new ArticleCatalogSummary
            {
                ArticleNumber = s.ArticleNumber,
                Title = s.Title,
                Updated = s.Updated,
            }).ToList();
        }

        /// <inheritdoc/>
        public async Task<List<TemplateCatalogSummary>> GetTemplatesAsync(int layoutNumber)
        {
            var raw = await this.dbContext.Templates
                .Where(t => t.LayoutNumber == layoutNumber)
                .Select(s => new { s.Id, s.Title, s.LayoutNumber })
                .ToListAsync();

            return raw.Select(s => new TemplateCatalogSummary
            {
                Id = s.Id,
                Title = s.Title ?? string.Empty,
                LayoutNumber = s.LayoutNumber,
            }).ToList();
        }

        /// <inheritdoc/>
        public async Task<List<BlogStreamSummary>> GetBlogStreamsAsync()
        {
            var blogStreamType = (int)Cosmos.Cms.Common.ArticleType.BlogStream;
            var all = await this.dbContext.Articles
                .AsNoTracking()
                .Where(a => a.ArticleType == blogStreamType)
                .Select(a => new
                {
                    a.ArticleNumber,
                    a.VersionNumber,
                    a.Title,
                    a.BlogKey,
                })
                .ToListAsync();

            var latest = all
                .GroupBy(a => a.ArticleNumber)
                .Select(g => g.OrderByDescending(a => a.VersionNumber).First())
                .OrderBy(a => a.Title)
                .Select(a => new BlogStreamSummary
                {
                    ArticleNumber = a.ArticleNumber,
                    Title = a.Title ?? string.Empty,
                    BlogKey = a.BlogKey ?? string.Empty,
                })
                .ToList();

            return latest;
        }

        /// <inheritdoc/>
        public async Task<List<BlogPostSummary>> GetBlogPostsAsync(string blogKey)
        {
            if (string.IsNullOrWhiteSpace(blogKey))
            {
                return new List<BlogPostSummary>();
            }

            var blogPostType = (int)Cosmos.Cms.Common.ArticleType.BlogPost;
            var all = await this.dbContext.Articles
                .AsNoTracking()
                .Where(a => a.BlogKey == blogKey && a.ArticleType == blogPostType)
                .Select(a => new
                {
                    a.Id,
                    a.ArticleNumber,
                    a.VersionNumber,
                    a.Title,
                    a.Published,
                })
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            var latest = all
                .GroupBy(a => a.ArticleNumber)
                .Select(g => g.OrderByDescending(a => a.VersionNumber).First())
                .OrderByDescending(a => a.Published ?? DateTimeOffset.MinValue)
                .Select(a => new BlogPostSummary
                {
                    Id = a.Id,
                    ArticleNumber = a.ArticleNumber,
                    Title = a.Title ?? string.Empty,
                    IsPublished = a.Published.HasValue && a.Published <= now,
                    Published = a.Published,
                })
                .ToList();

            return latest;
        }

        /// <inheritdoc/>
        public async Task<string?> ResolveArticleTitleAsync(int articleNumber)
        {
            var entry = await this.dbContext.ArticleCatalog
                .Where(a => a.ArticleNumber == articleNumber)
                .Select(a => a.Title)
                .FirstOrDefaultAsync();

            return entry;
        }

        /// <inheritdoc/>
        public async Task<string?> ResolveTemplateTitleAsync(Guid templateId)
        {
            var entry = await this.dbContext.Templates
                .Where(t => t.Id == templateId)
                .Select(t => t.Title)
                .FirstOrDefaultAsync();

            return entry;
        }
    }
}
