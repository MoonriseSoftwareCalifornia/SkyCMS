// <copyright file="FileEntryTitleService.cs" company="Moonrise Software, LLC">
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
    using Cosmos.DynamicConfig;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Sky.Editor.Data;
    using Sky.Editor.Models;
    using Sky.Editor.Services;

    /// <summary>
    /// Provides file-entry title enrichment for editor listings by resolving article and template
    /// identifiers from file-system-style paths into human-readable titles from persistence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service is used by file controllers to keep title lookup logic in one place, including
    /// extracting IDs from <see cref="FileManagerEntry"/> instances, performing batched database lookups,
    /// and returning dictionary-based maps that callers can apply when building UI responses.
    /// </para>
    /// <para>
    /// For articles, it supports both catalog-backed lookups and fallback resolution from versioned
    /// article records so draft content can still display a meaningful title. For templates, it resolves
    /// template folder identifiers to template titles.
    /// </para>
    /// </remarks>
    public class FileEntryTitleService : IFileEntryTitleService
    {
        private readonly IApplicationDbContext dbContext;
        private readonly IMemoryCache memoryCache;
        private readonly IDynamicConfigurationProvider dynamicConfigurationProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileEntryTitleService"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="memoryCache">Memory cache</param>
        /// <param name="dynamicConfigurationProvider">Dynamic configuration provider</param>
        /// <exception cref="ArgumentNullException"></exception>
        public FileEntryTitleService(IApplicationDbContext dbContext, IMemoryCache memoryCache, IDynamicConfigurationProvider dynamicConfigurationProvider)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.memoryCache = memoryCache;
            this.dynamicConfigurationProvider = dynamicConfigurationProvider;
        }

        /// <summary>
        /// Extracts article numbers from file and folder entries and resolves their titles from the database.
        /// </summary>
        /// <param name="entries">File entries to process (only from /pub/articles directory).</param>
        /// <param name="tenantDomain">
        /// Optional tenant domain associated with the current request.
        /// Defaults to an empty string, which is safe for single-tenant deployments.
        /// </param>
        /// <returns>Dictionary mapping article numbers to titles, empty if no matches found.</returns>
        /// <remarks>This assumes the entries already are known to start with '/pub/articles'. Don't run this if this is not the case. </remarks>
        public Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<FileManagerEntry> entries, string tenantDomain)
        {
            // Delegate extraction to the shared helper (handles null, normalisation, and deduplication)
            // and then reuse the numeric overload so all article-title lookup logic stays in one place.
            var articleNumbers = FileEntryPathHelper.ExtractArticleNumbersFromEntries(entries);
            return this.GetArticleTitlesByNumberAsync(articleNumbers, tenantDomain);
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
                if (FileEntryPathHelper.TryGetTemplateId(entry, out var templateId))
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
        public Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<int> articleNumbers)
        {
            return this.GetArticleTitlesByNumberAsync(articleNumbers, string.Empty);
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
        /// </remarks>
        /// <param name="entries">Mutable list of entries to filter in place.</param>
        /// <param name="tenantDomain">
        /// The tenant domain name associated with the current request.
        /// Defaults to an empty string, which is safe for single-tenant and test scenarios.
        /// </param>
        /// <returns>A task that completes once filtering is done.</returns>
        public async Task FilterDeletedArticleEntriesAsync(IList<FileManagerEntry> entries, string tenantDomain = "")
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var deletedNumbers = await this.GetDeletedArticleNumbersAsync(entries, tenantDomain);
            if (deletedNumbers.Count == 0)
            {
                return;
            }

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (FileEntryPathHelper.TryGetArticleNumberFromPath(entries[i].Path, out var num)
                    && deletedNumbers.Contains(num))
                {
                    entries.RemoveAt(i);
                }
            }
        }

        public async Task<bool> IsArticlePathDeletedAsync(string path, string tenantDomain)
        {
            if (!FileEntryPathHelper.TryGetArticleNumberFromPath(path, out var articleNumber))
            {
                return false;
            }

            // Scope the lookup to the current article number to keep query costs bounded.
            var entries = new List<FileManagerEntry>
            {
                new FileManagerEntry { Path = $"/pub/articles/{articleNumber}", IsDirectory = true },
            };

            var deletedNumbers = await this.GetDeletedArticleNumbersAsync(entries, tenantDomain);
            return deletedNumbers.Contains(articleNumber);
        }

        private async Task<HashSet<int>> GetDeletedArticleNumbersAsync(
            IEnumerable<FileManagerEntry> entries,
            string tenantDomain)
        {
            _ = tenantDomain;

            var allNumbers = FileEntryPathHelper.ExtractArticleNumbersFromEntries(entries);
            if (allNumbers.Count == 0)
            {
                return new HashSet<int>();
            }

            var deletedStatusCode = (int)StatusCodeEnum.Deleted;
            var deletedNumbers = new HashSet<int>();

            // Query each candidate article number by equality so Cosmos can use partition keys.
            foreach (var articleNumber in allNumbers)
            {
                var statuses = await this.dbContext.Articles
                    .Where(a => a.ArticleNumber == articleNumber)
                    .Select(a => a.StatusCode)
                    .ToListAsync();

                if (statuses.Count > 0 && statuses.All(s => s == deletedStatusCode))
                {
                    deletedNumbers.Add(articleNumber);
                }
            }

            return deletedNumbers;
        }

        /// <summary>
        /// Resolves a friendly display path (containing article title) to its canonical numeric path.
        /// </summary>
        /// <param name="friendlyPath">The friendly path containing an article title.</param>
        /// <returns>
        /// The canonical path with article number if the title is found.
        /// Returns the original path if it's already canonical, not an article path, or the title cannot be resolved.
        /// </returns>
        public Task<string> ResolveCanonicalPathAsync(string friendlyPath)
        {
            return this.ResolveCanonicalPathAsync(friendlyPath, string.Empty);
        }

        public async Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<int> articleNumbers, string tenantDomain)
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
            var articleRows = new List<ArticleTitleAndStatus>();

            // Important, DO NOT USE ArticleCatalog for lookups by article number that may include drafts,
            // because the catalog only contains published articles. We need to query the Articles table
            // directly to ensure we surface titles for draft articles that have no catalog entry yet.
            // This is a common scenario when resolving titles for article folders in the file manager,
            // which must show all articles including drafts and deleted (or articles in Trash) articles.

            if (this.dbContext.GetDatabaseProviderName().Contains("Cosmos", StringComparison.OrdinalIgnoreCase)
                && this.dbContext is ApplicationDbContext cosmosDbContext)
            {
                // Execute through Cosmos SQL/SDK and then select the latest non-empty title per article number client-side.
                var client = cosmosDbContext.Database.GetCosmosClient();
                var databaseId = cosmosDbContext.Database.GetCosmosDatabaseId();
                var queryService = new CosmosDbService(client, databaseId, "Articles");

                foreach (var chunk in numbersList.Chunk(100))
                {
                    var articleNumbersArray = $"[{string.Join(",", chunk)}]";
                    var query = $"SELECT c.ArticleNumber, c.VersionNumber, c.Title, c.StatusCode FROM c WHERE ARRAY_CONTAINS({articleNumbersArray}, c.ArticleNumber)";

                    var cosmosRows = await queryService.QueryWithGroupByAsync<CosmosArticleTitleRow>(query);
                    foreach (var group in cosmosRows.GroupBy(r => r.ArticleNumber))
                    {
                        var latest = group
                            .OrderByDescending(r => r.VersionNumber)
                            .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Title));

                        if (latest != null)
                        {
                            articleTitlesByNumber[latest.ArticleNumber] = latest.Title;
                        }
                    }
                }
            }
            else
            {
                articleRows = await this.dbContext.Articles
                    .Where(a => numbersList.Contains(a.ArticleNumber))
                    .Select(a => new ArticleTitleAndStatus
                    {
                        ArticleNumber = a.ArticleNumber,
                        Title = a.Title,
                        StatusCode = a.StatusCode,
                    })
                    .Distinct()
                    .ToListAsync();
            }

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
        /// Retrieves a list of article numbers, titles, and status codes for all articles in the system.
        /// </summary>
        /// <returns>A list of <see cref="ArticleTitleAndStatus"/> objects representing all articles.</returns>
        /// <remarks>This method retrieves the article information from the database and caches the results for 10 seconds.</remarks>
        public async Task<List<ArticleTitleAndStatus>> GetArticleNumberTitleStatusList()
        {
            var cacheKey = $"{this.dynamicConfigurationProvider.GetTenantDomainNameFromRequest}-articlenumber-title-status";
            if (memoryCache.TryGetValue(cacheKey, out var cachedValue))
            {
                return cachedValue as List<ArticleTitleAndStatus>;
            }

            var validStatuses = new[] { (int)StatusCodeEnum.Active, (int)StatusCodeEnum.Deleted };

            // Important: we must query the Articles table directly to get all articles including
            // drafts and deleted articles, since the catalog only contains published articles.
            var articleTitlesAndStatuses = await this.dbContext.Articles
                .Where(a => validStatuses.Contains(a.StatusCode))
                .Select(s => new ArticleTitleAndStatus
                {
                    ArticleNumber = s.ArticleNumber,
                    Title = s.Title,
                    StatusCode = s.StatusCode
                }).Distinct().AsNoTracking().ToListAsync();

            var model = articleTitlesAndStatuses.GroupBy(a => a.ArticleNumber)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(a => a.StatusCode).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Title));
                    return new ArticleTitleAndStatus
                    {
                        ArticleNumber = g.Key,
                        Title = latest?.Title ?? string.Empty,
                        StatusCode = latest?.StatusCode ?? (int)StatusCodeEnum.Active
                    };
                }).OrderBy(a => a.ArticleNumber).ToList();

            memoryCache.Set(cacheKey, model, DateTimeOffset.UtcNow.AddSeconds(10));

            return model;
        }

        /// <summary>
        /// Resolves a friendly display path containing an article title to its canonical path with the article number.
        /// </summary>
        /// <param name="friendlyPath">The friendly display path containing the article title.</param>
        /// <param name="tenantDomain">The tenant domain for which to resolve the path.</param>
        /// <returns>The canonical path with the article number, or the original path if not found.</returns>
        private sealed class CosmosArticleTitleRow
        {
            public int ArticleNumber { get; set; }

            public int VersionNumber { get; set; }

            public string Title { get; set; } = string.Empty;

            public int StatusCode { get; set; }
        }

        public async Task<string> ResolveCanonicalPathAsync(string friendlyPath, string tenantDomain)
        {
            if (string.IsNullOrWhiteSpace(friendlyPath))
            {
                return friendlyPath ?? string.Empty;
            }

            var normalizedPath = FileEntryPathHelper.NormalizePath(friendlyPath);

            // Check if already canonical (contains article number)
            if (FileEntryPathHelper.TryGetArticleNumberFromPath(normalizedPath, out _))
            {
                return normalizedPath; // Already canonical, pass through
            }

            // Parse the path to extract potential article title
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Must be at least /pub/articles/{title} format
            if (segments.Length < 3
                || !segments[0].Equals("pub", StringComparison.OrdinalIgnoreCase)
                || !segments[1].Equals("articles", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath; // Not an article path
            }

            var potentialTitle = segments[2];

            // Query database for article with this title.
            // Try ArticleCatalog first (published articles).
            var catalogMatch = await this.dbContext.ArticleCatalog
                .Where(a => a.Title == potentialTitle)
                .OrderBy(a => a.ArticleNumber) // Deterministic: return lowest number if collision
                .Select(a => a.ArticleNumber)
                .FirstOrDefaultAsync();

            if (catalogMatch > 0)
            {
                segments[2] = catalogMatch.ToString();
                return "/" + string.Join('/', segments);
            }

            // Fallback to Articles table (drafts).
            var articleMatch = await this.dbContext.Articles
                .Where(a => a.Title == potentialTitle)
                .OrderBy(a => a.ArticleNumber)
                .ThenByDescending(a => a.VersionNumber) // Latest version of lowest number
                .Select(a => a.ArticleNumber)
                .FirstOrDefaultAsync();

            if (articleMatch > 0)
            {
                segments[2] = articleMatch.ToString();
                return "/" + string.Join('/', segments);
            }

            // Title not found - return original path (will likely 404)
            return normalizedPath;
        }
    }
}
