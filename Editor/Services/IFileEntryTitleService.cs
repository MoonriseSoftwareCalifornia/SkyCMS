// <copyright file="IFileEntryTitleService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Sky.Editor.Services;

    /// <summary>
    /// Resolves article and template titles from the database for file listing entries,
    /// and filters soft-deleted article folders from listings.
    /// </summary>
    public interface IFileEntryTitleService
    {
        /// <summary>
        /// Extracts article numbers from file and folder entries and resolves their titles from the database.
        /// </summary>
        /// <param name="entries">File entries to process (only from /pub/articles directory).</param>
        /// <param name="tenantDomain">
        /// Optional tenant domain associated with the current request.
        /// Defaults to an empty string, which is safe for single-tenant deployments.
        /// </param>
        /// <returns>Dictionary mapping article numbers to titles, empty if no matches found.</returns>
        Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<FileManagerEntry> entries, string tenantDomain);

        /// <summary>
        /// Retrieves article titles for a given set of article numbers.
        /// </summary>
        /// <param name="articleNumbers">Article numbers to look up.</param>
        /// <param name="tenantDomain">
        /// Optional tenant domain associated with the current request.
        /// Defaults to an empty string, which is safe for single-tenant deployments.
        /// </param>
        /// <returns>Dictionary mapping article numbers to titles, empty if no matches found.</returns>
        Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<int> articleNumbers, string tenantDomain);

        /// <summary>
        /// Retrieves article number/title/status records for the supplied article numbers.
        /// Can optionally backfill missing catalog rows using the Articles table for legacy installs.
        /// </summary>
        /// <param name="articleNumbers">Article numbers to resolve.</param>
        /// <param name="tenantDomain">Tenant domain used for cache scoping.</param>
        /// <param name="cancellationToken">Cancellation token for async database operations.</param>
        /// <returns>A dictionary keyed by article number with title and status details.</returns>
        Task<IReadOnlyDictionary<int, ArticleTitleAndStatus>> GetArticleTitleStatusByNumberAsync(
            IEnumerable<int> articleNumbers,
            string tenantDomain,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts template IDs from file entries and resolves their titles from the database.
        /// </summary>
        /// <param name="entries">File entries to process (typically from /pub/templates directory).</param>
        /// <returns>Dictionary mapping template IDs to titles, empty if no matches found.</returns>
        Task<IReadOnlyDictionary<Guid, string>> GetTemplateTitlesByIdAsync(IEnumerable<FileManagerEntry> entries);

        /// <summary>
        /// Retrieves template titles for a given set of template IDs.
        /// </summary>
        /// <param name="templateIds">Template IDs to look up.</param>
        /// <returns>Dictionary mapping template IDs to titles, empty if no matches found.</returns>
        Task<IReadOnlyDictionary<Guid, string>> GetTemplateTitlesByIdAsync(IEnumerable<Guid> templateIds);

        /// <summary>
        /// Removes entries whose article number is associated with a soft-deleted article from
        /// <paramref name="entries"/>.
        /// </summary>
        /// <param name="entries">Mutable list of entries to filter in place.</param>
        /// <param name="tenantDomain">
        /// The tenant domain name associated with the current request.
        /// Defaults to an empty string, which is safe for single-tenant and test scenarios.
        /// </param>
        /// <returns>A task that completes once filtering is done.</returns>
        Task FilterDeletedArticleEntriesAsync(IList<FileManagerEntry> entries, string tenantDomain);

        /// <summary>
        /// Applies shared listing projection rules used by editor file APIs:
        /// resolves friendly names/display paths and filters deleted article entries.
        /// </summary>
        /// <param name="entries">Entries to project.</param>
        /// <param name="listingParentPath">Canonical path of the listing parent folder.</param>
        /// <param name="tenantDomain">Tenant domain associated with the current request.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Projected entries in original order, with deleted article entries removed.</returns>
        Task<List<FileManagerEntry>> ProjectFriendlyEntriesAsync(
            IEnumerable<FileManagerEntry> entries,
            string listingParentPath,
            string tenantDomain,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a canonical path is under a soft-deleted article folder.
        /// Paths outside <c>/pub/articles/{number}</c> always return <see langword="false"/>.
        /// </summary>
        /// <param name="path">Canonical path to check.</param>
        /// <param name="tenantDomain">Tenant domain associated with the current request.</param>
        /// <returns><see langword="true"/> when the path belongs to a deleted article; otherwise <see langword="false"/>.</returns>
        Task<bool> IsArticlePathDeletedAsync(string path, string tenantDomain);

        /// <summary>
        /// Resolves a friendly display path (containing article title) to its canonical numeric path.
        /// </summary>
        /// <param name="friendlyPath">The friendly path containing an article title (e.g., "/pub/articles/Getting Started Guide/banner.jpg").</param>
        /// <param name="tenantDomain">The tenant domain name used to scope the cache key. Obtain this from
        /// <c>IDynamicConfigurationProvider.GetTenantDomainNameFromRequest()</c> in controllers.
        /// Defaults to an empty string, which is safe for single-tenant and test scenarios.</param>
        /// <returns>
        /// The canonical path with article number (e.g., "/pub/articles/123/banner.jpg") if the title is found.
        /// Returns the original path if it's already canonical, not an article path, or the title cannot be resolved.
        /// </returns>
        /// <remarks>
        /// This method performs a database lookup to find the article number by title.
        /// If multiple articles share the same title, the first match (lowest article number) is returned.
        /// Use this for user-typed paths or friendly URLs that need to be converted for storage operations.
        /// </remarks>
        Task<string> ResolveCanonicalPathAsync(string friendlyPath, string tenantDomain);
    }
}
