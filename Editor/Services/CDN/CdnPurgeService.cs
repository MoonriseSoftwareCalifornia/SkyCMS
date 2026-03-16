// <copyright file="CdnPurgeService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.CDN
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.EditorSettings;

    /// <summary>
    /// Service for purging CDN caches when content is published or updated.
    /// </summary>
    /// <remarks>
    /// This service coordinates cache invalidation with configured CDN providers (Azure CDN, Cloudflare, etc.)
    /// to ensure updated content is served immediately. Purge failures are logged as warnings but do not
    /// interrupt the publishing workflow.
    /// </remarks>
    public sealed class CdnPurgeService : ICdnPurgeService
    {
        private readonly ApplicationDbContext db;
        private readonly ILogger<CdnPurgeService> logger;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IEditorSettings settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="CdnPurgeService"/> class.
        /// </summary>
        /// <param name="db">Application database context.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="httpContextAccessor">HTTP context accessor for tenant-aware CDN configuration.</param>
        /// <param name="settings">Editor settings for publisher URL configuration.</param>
        public CdnPurgeService(
            ApplicationDbContext db,
            ILogger<CdnPurgeService> logger,
            IHttpContextAccessor httpContextAccessor,
            IEditorSettings settings)
        {
            this.db = db;
            this.logger = logger;
            this.httpContextAccessor = httpContextAccessor;
            this.settings = settings;
        }

        /// <inheritdoc/>
        public async Task<List<CdnResult>> PurgePageCacheAsync(PublishedPage page)
        {
            var results = new List<CdnResult>();
            try
            {
                var cdnService = await CdnService.GetCdnServiceAsync(db, logger, httpContextAccessor.HttpContext);
                if (cdnService == null)
                {
                    return results;
                }

                var path = BuildPurgePath(page.UrlPath);
                var paths = new List<string> { path };

                results = await cdnService.PurgeCdn(paths);

                logger.LogDebug("Purged CDN cache for page: {UrlPath}", page.UrlPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CDN purge failed for page: {UrlPath}", page.UrlPath);
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<List<CdnResult>> PurgePagesCacheAsync(IEnumerable<PublishedPage> pages)
        {
            var results = new List<CdnResult>();
            try
            {
                var cdnService = await CdnService.GetCdnServiceAsync(db, logger, httpContextAccessor.HttpContext);
                if (cdnService == null)
                {
                    return results;
                }

                var paths = pages.Select(p => BuildPurgePath(p.UrlPath)).Distinct().ToList();
                if (!paths.Any())
                {
                    return results;
                }

                results = await cdnService.PurgeCdn(paths);

                logger.LogDebug("Purged CDN cache for {Count} pages", paths.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CDN purge failed for multiple pages");
            }

            return results;
        }

        /// <summary>
        /// Builds a CDN purge path from a published page URL path.
        /// </summary>
        /// <param name="urlPath">The URL path from the published page.</param>
        /// <returns>The fully qualified purge path for CDN invalidation.</returns>
        /// <remarks>
        /// Converts:
        /// <list type="bullet">
        ///   <item><description>"root" → "/" (homepage)</description></item>
        ///   <item><description>Other paths → "{PublisherUrl}/{urlPath}"</description></item>
        /// </list>
        /// </remarks>
        private string BuildPurgePath(string urlPath)
        {
            return urlPath.Equals("root", StringComparison.OrdinalIgnoreCase)
                ? "/"
                : $"{settings.PublisherUrl.TrimEnd('/')}/{urlPath.TrimStart('/')}";
        }
    }
}
