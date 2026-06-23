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
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.DynamicConfig;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
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
            var tenantDomain = this.dynamicConfigurationProvider?.GetTenantDomainNameFromRequest() ?? string.Empty;
            return this.GetArticleTitlesByNumberAsync(articleNumbers, tenantDomain);
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

        public async Task<List<FileManagerEntry>> ProjectFriendlyEntriesAsync(
            IEnumerable<FileManagerEntry> entries,
            string listingParentPath,
            string tenantDomain,
            CancellationToken cancellationToken = default)
        {
            if (entries == null)
            {
                return new List<FileManagerEntry>();
            }

            var normalizedParent = FileEntryPathHelper.NormalizePath(listingParentPath);
            var normalizedEntries = entries.Select(entry =>
            {
                entry.Path = FileEntryPathHelper.ResolveEntryPath(normalizedParent, entry);
                return entry;
            }).ToList();

            if (normalizedEntries.Count == 0)
            {
                return normalizedEntries;
            }

            var articleNumbers = FileEntryPathHelper.ExtractArticleNumbersFromEntries(normalizedEntries);
            var articleStatusByNumber = await this.GetArticleTitleStatusByNumberAsync(
                articleNumbers,
                tenantDomain,
                cancellationToken);

            var deletedStatus = (int)StatusCodeEnum.Deleted;
            var visibleEntries = normalizedEntries
                .Where(e =>
                {
                    if (!FileEntryPathHelper.TryGetArticleNumberFromPath(e.Path, out var articleNumber))
                    {
                        return true;
                    }

                    return articleStatusByNumber.TryGetValue(articleNumber, out var articleInfo)
                        && articleInfo != null
                        && articleInfo.StatusCode != deletedStatus;
                })
                .ToList();

            var articleTitlesByNumber = articleStatusByNumber
                .Where(kvp => kvp.Value != null
                    && kvp.Value.StatusCode != deletedStatus
                    && !string.IsNullOrWhiteSpace(kvp.Value.Title))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Title);

            var templateTitlesById = await this.GetTemplateTitlesByIdAsync(visibleEntries);
            var isArticlesRootListing = string.Equals(normalizedParent, "/pub/articles", StringComparison.OrdinalIgnoreCase);

            foreach (var entry in visibleEntries)
            {
                entry.DisplayPath = FileEntryPathHelper.ResolveFriendlyDisplayPath(entry.Path, articleTitlesByNumber);

                if (isArticlesRootListing && entry.IsDirectory)
                {
                    var segments = FileEntryPathHelper.NormalizePath(entry.Path)
                        .Split('/', StringSplitOptions.RemoveEmptyEntries);

                    if (segments.Length == 3
                        && FileEntryPathHelper.TryGetArticleNumberFromPath(entry.Path, out var articleNumber)
                        && articleTitlesByNumber.TryGetValue(articleNumber, out var articleTitle)
                        && !string.IsNullOrWhiteSpace(articleTitle))
                    {
                        entry.Name = articleTitle;
                        continue;
                    }

                    entry.Name = entry.Name ?? string.Empty;
                    continue;
                }

                entry.Name = FileEntryPathHelper.ResolveFriendlyDisplayName(
                    normalizedParent,
                    entry,
                    articleTitlesByNumber,
                    templateTitlesById);
            }

            return visibleEntries;
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

        /// <summary>
        /// Retrieves article titles for a given set of article numbers, scoped to a tenant domain.
        /// </summary>
        /// <param name="articleNumbers">The article numbers to retrieve titles for.</param>
        /// <param name="tenantDomain">The tenant domain to scope the lookup to.</param>
        /// <returns>A dictionary mapping article numbers to their titles.</returns>
        public async Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<int> articleNumbers, string tenantDomain)
        {
            var records = await this.GetArticleTitleStatusByNumberAsync(articleNumbers, tenantDomain, default);
            return records.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Title);
        }

        /// <summary>
        /// Retrieves article number/title/status records for supplied numbers and optionally backfills missing catalog rows.
        /// </summary>
        /// <param name="articleNumbers">Article numbers to resolve.</param>
        /// <param name="tenantDomain">Tenant domain used for cache scoping.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>A dictionary keyed by article number with title and status details.</returns>
        public async Task<IReadOnlyDictionary<int, ArticleTitleAndStatus>> GetArticleTitleStatusByNumberAsync(
            IEnumerable<int> articleNumbers,
            string tenantDomain,
            CancellationToken cancellationToken = default)
        {
            if (articleNumbers == null)
            {
                return new Dictionary<int, ArticleTitleAndStatus>();
            }

            var numbers = articleNumbers.ToHashSet();
            if (numbers.Count == 0)
            {
                return new Dictionary<int, ArticleTitleAndStatus>();
            }

            var cacheKey = $"{tenantDomain}-articlenumber-title-status-map";
            if (!this.memoryCache.TryGetValue(cacheKey, out Dictionary<int, ArticleTitleAndStatus> cachedLookup))
            {
                try
                {
                    var catalogRows = await this.dbContext.ArticleCatalog
                        .ToListAsync(cancellationToken);

                    cachedLookup = catalogRows.ToDictionary(
                        a => a.ArticleNumber,
                        a => new ArticleTitleAndStatus
                        {
                            ArticleNumber = a.ArticleNumber,
                            Title = a.Title,
                            StatusCode = ResolveCatalogStatusCode(a.StatusCode ?? 0, a.Status),
                        });

                    this.memoryCache.Set(cacheKey, cachedLookup, TimeSpan.FromSeconds(10));
                }
                catch (Exception ex)
                {
                    // The crash will likely be from Cosmos EF because StatusCode is non-nullable but in the Cosmos DB it is missing or null.
                    // Backfill the missing values here
                    if (ex.Message.Equals("Nullable object must have a value."))
                    {
                        cachedLookup = await BackfillCatalog(numbers, cancellationToken);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            var result = cachedLookup
                .Where(kvp => numbers.Contains(kvp.Key))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ArticleTitleAndStatus
                    {
                        ArticleNumber = kvp.Value.ArticleNumber,
                        Title = kvp.Value.Title,
                        StatusCode = kvp.Value.StatusCode,
                    });

            // Check if any requested article numbers are missing from the catalog
            var missingNumbers = numbers.Except(result.Keys).ToList();
            if (missingNumbers.Count > 0)
            {
                var backfilledData = await BackfillCatalog(missingNumbers, cancellationToken);
                foreach (var kvp in backfilledData)
                {
                    result[kvp.Key] = kvp.Value;
                }

                // Update cache with the backfilled data
                foreach (var kvp in backfilledData)
                {
                    cachedLookup[kvp.Key] = kvp.Value;
                }

                this.memoryCache.Set(cacheKey, cachedLookup, TimeSpan.FromSeconds(10));
            }

            return result;
        }

        /// <summary>
        /// For any article numbers missing from the initial catalog lookup, queries the Articles table for their latest title and status,
        /// and updates the catalog and cache accordingly.
        /// </summary>
        /// <param name="articleNumbers">Article numbers to backfill.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The updated result dictionary.</returns>
        /// <remarks>This method updates the catalog and cache for any missing article numbers. It is mainly to fix older installations of SkyCMS.</remarks>
        private async Task<Dictionary<int, ArticleTitleAndStatus>> BackfillCatalog(IEnumerable<int> articleNumbers, CancellationToken cancellationToken)
        {
            var deletedStatus = (int)StatusCodeEnum.Deleted;
            var inactiveStatus = (int)StatusCodeEnum.Inactive;
            var pendingBackfillRows = new List<CatalogEntry>();
            var result = new Dictionary<int, ArticleTitleAndStatus>();

            foreach (var articleNumber in articleNumbers)
            {
                var article = await this.dbContext.Articles
                    .Where(a => a.ArticleNumber == articleNumber)
                    .OrderByDescending(a => a.VersionNumber)
                    .Select(a => new { a.ArticleNumber, a.Title, a.StatusCode })
                    .ToListAsync(cancellationToken);

                // Find the latest version with a non-empty title
                var latestNonEmptyArticle = article.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Title));

                // Using a Cosmos safe query to delete the existing catalog entry if it exists, since the presence of a bad row with null StatusCode is likely what caused the original query to fail.
                var existingCatalogEntry = await this.dbContext.ArticleCatalog
                   .Where(c => c.ArticleNumber == articleNumber)
                   .FirstOrDefaultAsync(cancellationToken);

                if (existingCatalogEntry != null)
                {
                    dbContext.ArticleCatalog.Remove(existingCatalogEntry);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                if (latestNonEmptyArticle == null)
                {
                    continue;
                }

                if (latestNonEmptyArticle.StatusCode == deletedStatus || latestNonEmptyArticle.StatusCode == inactiveStatus)
                {
                    pendingBackfillRows.Add(new CatalogEntry
                    {
                        ArticleNumber = latestNonEmptyArticle.ArticleNumber,
                        Title = latestNonEmptyArticle.Title,
                        Status = ConvertStatusCodeToString(latestNonEmptyArticle.StatusCode),
                        StatusCode = latestNonEmptyArticle.StatusCode,
                    });
                }

                result[latestNonEmptyArticle.ArticleNumber] = new ArticleTitleAndStatus
                {
                    ArticleNumber = latestNonEmptyArticle.ArticleNumber,
                    Title = latestNonEmptyArticle.Title,
                    StatusCode = latestNonEmptyArticle.StatusCode,
                };
            }

            if (pendingBackfillRows.Count > 0)
            {
                await this.dbContext.ArticleCatalog.AddRangeAsync(pendingBackfillRows, cancellationToken);
                await this.dbContext.SaveChangesAsync(cancellationToken);
            }

            return result;
        }

        private static int ConvertStatusToCode(string status)
        {
            return status switch
            {
                "Active" => (int)StatusCodeEnum.Active,
                "Inactive" => (int)StatusCodeEnum.Inactive,
                "Deleted" => (int)StatusCodeEnum.Deleted,
                "Redirect" => (int)StatusCodeEnum.Redirect,
                _ => -1,
            };
        }

        private static int ResolveCatalogStatusCode(int statusCode, string? status)
        {
            var convertedStatusCode = ConvertStatusToCode(status ?? string.Empty);
            return convertedStatusCode >= 0 ? convertedStatusCode : statusCode;
        }

        private static string ConvertStatusCodeToString(int statusCode)
        {
            return statusCode switch
            {
                (int)StatusCodeEnum.Active => "Active",
                (int)StatusCodeEnum.Inactive => "Inactive",
                (int)StatusCodeEnum.Deleted => "Deleted",
                (int)StatusCodeEnum.Redirect => "Redirect",
                _ => "Unknown",
            };
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
