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
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Sky.Editor.Models;

    /// <summary>
    /// Async helper for resolving article and template titles from the database.
    /// Centralizes title-lookup patterns used across file controllers to reduce duplication.
    /// </summary>
    internal class PublicFileEntryTitleResolver
    {
        private readonly IApplicationDbContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublicFileEntryTitleResolver"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public PublicFileEntryTitleResolver(IApplicationDbContext dbContext)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Extracts article numbers from file and folder entries and resolves their titles from the database.
        /// </summary>
        /// <param name="entries">File entries to process (only from /pub/articles directory).</param>
        /// <returns>Dictionary mapping article numbers to titles, empty if no matches found.</returns>
        /// <remarks>This assumes the entries already are known to start with '/pub/articles'. Don't run this if this is not the case. </remarks>
        public async Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<FileManagerEntry> entries)
        {
            // Delegate extraction to the shared helper (handles null, normalisation, and deduplication).
            var articleNumbers = PublicFileEntryHelper.ExtractArticleNumbersFromEntries(entries);

            if (articleNumbers.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            // Exclude only truly unusable statuses (Deleted, Redirect). Active and Inactive
            // articles are both valid non-trashed articles that can have files on disk.
            var deletedStatusCode = (int)StatusCodeEnum.Deleted;
            var redirectStatusCode = (int)StatusCodeEnum.Redirect;

            var articleTitlesByNumber = new Dictionary<int, string>();
            var articleRows = await this.dbContext.Articles
                .Where(a => articleNumbers.Contains(a.ArticleNumber)
                            && a.StatusCode != deletedStatusCode
                            && a.StatusCode != redirectStatusCode)
                .Select(a => new { a.ArticleNumber, a.Title })
                .Distinct()
                .ToListAsync();

            foreach (var row in articleRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Title))
                {
                    articleTitlesByNumber[row.ArticleNumber] = row.Title;
                }
            }

            // Fall back for any article numbers still unresolved (e.g. all versions were deleted/redirect).
            await this.FillMissingTitlesFromArticlesAsync(articleTitlesByNumber, articleNumbers);

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

            // Delegate to the ID-based overload to avoid duplicating the DB query.
            return await this.GetTemplateTitlesByIdAsync(templateIds);
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

            var numbersList = numbers.ToList();
            var articleTitlesByNumber = new Dictionary<int, string>();
            var articleRows = await this.dbContext.ArticleCatalog
                .Where(a => numbersList.Contains(a.ArticleNumber))
                .Select(a => new { a.ArticleNumber, a.Title })
                .ToListAsync();

            foreach (var row in articleRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Title))
                {
                    articleTitlesByNumber[row.ArticleNumber] = row.Title;
                }
            }

            // Fall back to the Articles table for any numbers not in the catalog.
            await this.FillMissingTitlesFromArticlesAsync(articleTitlesByNumber, numbersList);

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
                .Distinct()
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
        /// For each article number in <paramref name="allNumbers"/> that is not yet present in
        /// <paramref name="result"/>, queries the <c>Articles</c> table and fills in the title
        /// from the highest-version record found. This surfaces titles for draft articles that
        /// have no <c>ArticleCatalog</c> entry.
        /// </summary>
        private async Task FillMissingTitlesFromArticlesAsync(Dictionary<int, string> result, List<int> allNumbers)
        {
            var missing = allNumbers.Where(n => !result.ContainsKey(n)).ToList();
            if (missing.Count == 0)
            {
                return;
            }

            // Fetch all article versions for the missing IDs in one round-trip,
            // then pick the highest-version title per article number client-side.
            // This is Cosmos DB-compatible: no joins, no inline casts.
            var rows = await this.dbContext.Articles
                .Where(a => missing.Contains(a.ArticleNumber))
                .Select(a => new { a.ArticleNumber, a.VersionNumber, a.Title })
                .ToListAsync();

            foreach (var row in rows.OrderByDescending(r => r.VersionNumber))
            {
                if (!result.ContainsKey(row.ArticleNumber) && !string.IsNullOrWhiteSpace(row.Title))
                {
                    result[row.ArticleNumber] = row.Title;
                }
            }
        }

        /// <summary>
        /// Removes entries whose article number is associated with a soft-deleted
        /// (<see cref="StatusCodeEnum.Deleted"/>) article from <paramref name="entries"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An article is considered deleted when <em>all</em> of its versions in the
        /// <c>Articles</c> table carry <see cref="StatusCodeEnum.Deleted"/>. If even one
        /// non-deleted version exists the article is still live and its folder must not
        /// be hidden. Permanently trashed articles are already gone from both the DB and
        /// blob storage, so they will simply have no matching entries to filter.
        /// </para>
        /// <para>
        /// Results are cached via <paramref name="cache"/> for 30 seconds to avoid a
        /// round-trip to the database on every listing request. The short TTL means that
        /// a restore or hard-delete will take effect within one cache window.
        /// </para>
        /// <para>
        /// The cache key is scoped to <paramref name="tenantDomain"/> so that separate
        /// tenants sharing a single process never see each other's deleted-article sets.
        /// In single-tenant deployments the host name is used, which is equally safe.
        /// Pass an empty string only from tests where tenant isolation is not relevant.
        /// </para>
        /// </remarks>
        /// <param name="entries">Mutable list of entries to filter in place.</param>
        /// <param name="cache">Application memory cache used for short-lived deleted-number lookup.</param>
        /// <param name="tenantDomain">
        /// The tenant domain name used to scope the cache key. Obtain this from
        /// <c>IDynamicConfigurationProvider.GetTenantDomainNameFromRequest()</c> in controllers.
        /// Defaults to an empty string, which is safe for single-tenant and test scenarios.
        /// </param>
        /// <returns>A task that completes once filtering is done.</returns>
        public async Task FilterDeletedArticleEntriesAsync(IList<FileManagerEntry> entries, IMemoryCache cache, string tenantDomain = "")
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            // Scope the cache key to the tenant so that multiple tenants sharing a
            // single in-process IMemoryCache cannot bleed deleted-article sets into
            // each other's file listings.
            var cacheKey = $"PublicFileEntryTitleResolver:DeletedArticleNumbers:{tenantDomain}";

            if (!cache.TryGetValue(cacheKey, out HashSet<int>? deletedNumbers) || deletedNumbers == null)
            {
                var deletedStatusCode = (int)StatusCodeEnum.Deleted;

                // Materialise all article numbers that have at least one non-deleted version.
                // Any article number NOT in this set is considered fully deleted.
                var liveNumbers = await this.dbContext.Articles
                    .Where(a => a.StatusCode != deletedStatusCode)
                    .Select(a => a.ArticleNumber)
                    .Distinct()
                    .ToListAsync();

                // Also pull every number in blob storage entries to know which ones are deleted.
                var allNumbers = PublicFileEntryHelper.ExtractArticleNumbersFromEntries(entries);

                deletedNumbers = new HashSet<int>(
                    allNumbers.Where(n => !liveNumbers.Contains(n)));

                cache.Set(
                    cacheKey,
                    deletedNumbers,
                    new MemoryCacheEntryOptions
                    {
                        SlidingExpiration = TimeSpan.FromSeconds(30),
                    });
            }

            if (deletedNumbers.Count == 0)
            {
                return;
            }

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (PublicFileEntryHelper.TryGetArticleNumberFromPath(entries[i].Path, out var num)
                    && deletedNumbers.Contains(num))
                {
                    entries.RemoveAt(i);
                }
            }
        }
    }
}
