// <copyright file="PublicFileEntryTitleResolver.cs" company="Moonrise Software, LLC">
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
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Sky.Editor.Models;

    /// <summary>
    /// Async helper for resolving article and template titles from the database.
    /// Centralizes title-lookup patterns used across file controllers to reduce duplication.
    /// </summary>
    internal class PublicFileEntryTitleResolver
    {
        private readonly IApplicationDbContext dbContext;

        public PublicFileEntryTitleResolver(IApplicationDbContext dbContext)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Extracts article numbers from file entries and resolves their titles from the database.
        /// </summary>
        /// <param name="entries">File entries to process (typically from /pub/articles directory).</param>
        /// <returns>Dictionary mapping article numbers to titles, empty if no matches found.</returns>
        public async Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<FileManagerEntry> entries)
        {
            if (entries == null)
            {
                return new Dictionary<int, string>();
            }

            var articleNumbers = new HashSet<int>();
            foreach (var entry in entries.Where(e => e.IsDirectory))
            {
                if (PublicFileEntryHelper.TryGetArticleNumber(entry, out var articleNumber))
                {
                    articleNumbers.Add(articleNumber);
                }
            }

            if (articleNumbers.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var articleTitlesByNumber = new Dictionary<int, string>();
            var articleRows = await this.dbContext.ArticleCatalog
                .Where(a => articleNumbers.Contains(a.ArticleNumber))
                .Select(a => new { a.ArticleNumber, a.Title })
                .ToListAsync();

            foreach (var row in articleRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Title))
                {
                    articleTitlesByNumber[row.ArticleNumber] = row.Title;
                }
            }

            return articleTitlesByNumber;
        }

        /// <summary>
        /// Extracts template IDs from file entries and resolves their titles from the database.
        /// </summary>
        /// <param name="entries">File entries to process (typically from /pub/templates directory).</param>
        /// <returns>Dictionary mapping template IDs to titles, empty if no matches found.</returns>
        public async Task<IReadOnlyDictionary<Guid, string>> GetTemplateTitlesByIdAsync(IEnumerable<FileManagerEntry> entries)
        {
            if (entries == null)
            {
                return new Dictionary<Guid, string>();
            }

            var templateIds = new HashSet<Guid>();
            foreach (var entry in entries.Where(e => e.IsDirectory))
            {
                if (PublicFileEntryHelper.TryGetTemplateId(entry, out var templateId))
                {
                    templateIds.Add(templateId);
                }
            }

            if (templateIds.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            var templateTitlesById = new Dictionary<Guid, string>();
            var templateRows = await this.dbContext.Templates
                .Where(t => templateIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Title })
                .ToListAsync();

            foreach (var row in templateRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Title))
                {
                    templateTitlesById[row.Id] = row.Title;
                }
            }

            return templateTitlesById;
        }

        /// <summary>
        /// Retrieves article titles for a given set of article numbers.
        /// Useful for batch lookups without requiring file entries.
        /// </summary>
        /// <param name="articleNumbers">Article numbers to look up.</param>
        /// <returns>Dictionary mapping article numbers to titles, empty if no matches found.</returns>
        public async Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<int> articleNumbers)
        {
            if (articleNumbers == null)
            {
                return new Dictionary<int, string>();
            }

            var numbers = articleNumbers.ToHashSet();
            if (numbers.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var articleTitlesByNumber = new Dictionary<int, string>();
            var articleRows = await this.dbContext.ArticleCatalog
                .Where(a => numbers.Contains(a.ArticleNumber))
                .Select(a => new { a.ArticleNumber, a.Title })
                .ToListAsync();

            foreach (var row in articleRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Title))
                {
                    articleTitlesByNumber[row.ArticleNumber] = row.Title;
                }
            }

            return articleTitlesByNumber;
        }

        /// <summary>
        /// Retrieves template titles for a given set of template IDs.
        /// Useful for batch lookups without requiring file entries.
        /// </summary>
        /// <param name="templateIds">Template IDs to look up.</param>
        /// <returns>Dictionary mapping template IDs to titles, empty if no matches found.</returns>
        public async Task<IReadOnlyDictionary<Guid, string>> GetTemplateTitlesByIdAsync(IEnumerable<Guid> templateIds)
        {
            if (templateIds == null)
            {
                return new Dictionary<Guid, string>();
            }

            var ids = templateIds.ToHashSet();
            if (ids.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            var templateTitlesById = new Dictionary<Guid, string>();
            var templateRows = await this.dbContext.Templates
                .Where(t => ids.Contains(t.Id))
                .Select(t => new { t.Id, t.Title })
                .ToListAsync();

            foreach (var row in templateRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Title))
                {
                    templateTitlesById[row.Id] = row.Title;
                }
            }

            return templateTitlesById;
        }
    }
}
