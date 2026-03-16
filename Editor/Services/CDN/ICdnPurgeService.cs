// <copyright file="ICdnPurgeService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.CDN
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;

    /// <summary>
    /// Service for purging CDN caches when content is published or updated.
    /// </summary>
    /// <remarks>
    /// This service abstracts CDN cache invalidation logic away from the publishing workflow,
    /// supporting multiple CDN providers (Azure CDN, Cloudflare, CloudFront, Sucuri, Fastly).
    /// </remarks>
    public interface ICdnPurgeService
    {
        /// <summary>
        /// Purges the CDN cache for the specified published page.
        /// </summary>
        /// <param name="page">The published page whose CDN cache should be invalidated.</param>
        /// <returns>
        /// A list of <see cref="CdnResult"/> objects representing the outcome of CDN purge operations.
        /// Returns an empty list if no CDN service is configured or if the operation fails.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Coordinates cache invalidation with configured CDN providers to ensure updated content
        /// is served immediately rather than after cache expiration.
        /// </para>
        /// <para>
        /// The purge path is constructed from the page's URL:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>"root" → "/" (site homepage)</description></item>
        ///   <item><description>Other paths → "{PublisherUrl}/{urlPath}" (fully qualified URL)</description></item>
        /// </list>
        /// <para>
        /// CDN purge failures are logged as warnings but do not throw exceptions, allowing publish
        /// operations to complete successfully even when CDN communication fails.
        /// </para>
        /// </remarks>
        Task<List<CdnResult>> PurgePageCacheAsync(PublishedPage page);

        /// <summary>
        /// Purges the CDN cache for multiple published pages in a single operation.
        /// </summary>
        /// <param name="pages">Collection of published pages whose CDN cache should be invalidated.</param>
        /// <returns>
        /// A list of <see cref="CdnResult"/> objects representing the outcome of CDN purge operations.
        /// </returns>
        /// <remarks>
        /// Batches purge requests for better performance when multiple pages are published simultaneously.
        /// </remarks>
        Task<List<CdnResult>> PurgePagesCacheAsync(IEnumerable<PublishedPage> pages);
    }
}
