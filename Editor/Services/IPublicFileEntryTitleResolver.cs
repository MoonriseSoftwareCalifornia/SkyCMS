// <copyright file="IPublicFileEntryTitleResolver.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Microsoft.Extensions.Caching.Memory;

    /// <summary>
    /// Resolves article and template titles from the database for file listing entries,
    /// and filters soft-deleted article folders from listings.
    /// </summary>
    public interface IPublicFileEntryTitleResolver
    {
        /// <summary>
        /// Extracts article numbers from file and folder entries and resolves their titles from the database.
        /// </summary>
        /// <param name="entries">File entries to process (only from /pub/articles directory).</param>
        /// <returns>Dictionary mapping article numbers to titles, empty if no matches found.</returns>
        Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<FileManagerEntry> entries);

        /// <summary>
        /// Retrieves article titles for a given set of article numbers.
        /// </summary>
        /// <param name="articleNumbers">Article numbers to look up.</param>
        /// <returns>Dictionary mapping article numbers to titles, empty if no matches found.</returns>
        Task<IReadOnlyDictionary<int, string>> GetArticleTitlesByNumberAsync(IEnumerable<int> articleNumbers);

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
        /// <paramref name="entries"/>. Results are cached for 30 seconds, scoped to the tenant domain.
        /// </summary>
        /// <param name="entries">Mutable list of entries to filter in place.</param>
        /// <param name="cache">Application memory cache used for short-lived deleted-number lookup.</param>
        /// <param name="tenantDomain">
        /// The tenant domain name used to scope the cache key. Obtain this from
        /// <c>IDynamicConfigurationProvider.GetTenantDomainNameFromRequest()</c> in controllers.
        /// Defaults to an empty string, which is safe for single-tenant and test scenarios.
        /// </param>
        /// <returns>A task that completes once filtering is done.</returns>
        Task FilterDeletedArticleEntriesAsync(IList<FileManagerEntry> entries, IMemoryCache cache, string tenantDomain = "");

        /// <summary>
        /// Resolves a friendly display path (containing article title) to its canonical numeric path.
        /// </summary>
        /// <param name="friendlyPath">The friendly path containing an article title (e.g., "/pub/articles/Getting Started Guide/banner.jpg").</param>
        /// <returns>
        /// The canonical path with article number (e.g., "/pub/articles/123/banner.jpg") if the title is found.
        /// Returns the original path if it's already canonical, not an article path, or the title cannot be resolved.
        /// </returns>
        /// <remarks>
        /// This method performs a database lookup to find the article number by title.
        /// If multiple articles share the same title, the first match (lowest article number) is returned.
        /// Use this for user-typed paths or friendly URLs that need to be converted for storage operations.
        /// </remarks>
        Task<string> ResolveCanonicalPathAsync(string friendlyPath);
    }
}
