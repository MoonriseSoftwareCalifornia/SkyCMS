// <copyright file="CatalogService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Catalog
{
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Html;

    /// <summary>
    /// Provides catalog maintenance operations for articles.
    /// </summary>
    /// <remarks>
    /// Responsibilities:
    /// <list type="bullet">
    /// <item>Maintains a single, denormalized <see cref="CatalogEntry"/> for each logical article number.</item>
    /// <item>Projects selected fields from the latest persisted <see cref="Article"/> version.</item>
    /// <item>Ensures an introduction is populated (deriving one via <see cref="IArticleHtmlService"/> if missing).</item>
    /// <item>Normalizes the status textual representation (Active / Inactive).</item>
    /// </list>
    /// Behavioral Notes:
    /// <list type="bullet">
    /// <item>Upsert is implemented as remove + insert (no in-place update) to keep the row lean and avoid stale state.</item>
    /// <item>The method queries the latest version (by <see cref="Article.VersionNumber"/>) of the same <see cref="Article.ArticleNumber"/> to derive an introduction if needed.</item>
    /// <item>The service does not validate publish / expiration logic; it trusts the supplied <see cref="Article"/> instance.</item>
    /// <item>Author enrichment is currently a placeholder (empty), allowing future re-introduction without breaking shape.</item>
    /// </list>
    /// Concurrency:
    /// <list type="bullet">
    /// <item>No explicit concurrency token is used during delete/replace; last writer wins.</item>
    /// <item>Operations are executed in two SaveChanges calls (delete then insert) to keep logic simple; batching could be optimized later.</item>
    /// </list>
    /// </remarks>
    public sealed class CatalogService : ICatalogService
    {
        private readonly ApplicationDbContext db;
        private readonly IArticleHtmlService html;
        private readonly IClock clock;
        private readonly ILogger<CatalogService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CatalogService"/> class.
        /// </summary>
        /// <param name="db">Entity Framework application database context.</param>
        /// <param name="html">HTML utility service for extracting/normalizing article fragments.</param>
        /// <param name="clock">Clock abstraction (reserved for future timestamp logic or auditing).</param>
        /// <param name="logger">Logger instance for diagnostics (currently unused but reserved for expansion).</param>
        public CatalogService(ApplicationDbContext db, IArticleHtmlService html, IClock clock, ILogger<CatalogService> logger)
        {
            this.db = db;
            this.html = html;
            this.clock = clock;
            this.logger = logger;
        }

        /// <summary>
        /// Creates or replaces the catalog entry for the specified article number using the supplied article projection.
        /// </summary>
        /// <param name="article">
        /// The article instance whose data should be reflected in the catalog.
        /// Must contain at minimum a valid <see cref="Article.ArticleNumber"/> and identifying metadata fields.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>
        /// The newly created <see cref="CatalogEntry"/> representing the latest catalog view for the article number.
        /// </returns>
        /// <remarks>
        /// Process:
        /// <list type="number">
        /// <item>Locates the latest persisted article version for introduction derivation (if needed).</item>
        /// <item>Deletes any existing catalog row for the same article number.</item>
        /// <item>Derives introduction text if the provided article lacks one.</item>
        /// <item>Maps selected fields and inserts a new <see cref="CatalogEntry"/> row.</item>
        /// </list>
        /// Status Mapping:
        /// <list type="bullet">
        /// <item><c>StatusCode == 0</c> → "Inactive"</item>
        /// <item>Any other value → "Active"</item>
        /// </list>
        /// Future Enhancements:
        /// <list type="bullet">
        /// <item>Author information enrichment from <c>AuthorInfos</c> table.</item>
        /// <item>Batch save optimization to reduce round-trips.</item>
        /// <item>Optional optimistic concurrency handling.</item>
        /// </list>
        /// </remarks>
        public async Task<CatalogEntry> UpsertAsync(Article article, System.Threading.CancellationToken cancellationToken)
        {
            var latest = await db.Articles
                .AsNoTracking() // Prevent tracking this query result
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            var existing = await db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber, cancellationToken);
            if (existing != null)
            {
                db.ArticleCatalog.Remove(existing);
                await db.SaveChangesAsync(cancellationToken);
            }

            string authorInfo = string.Empty; // Placeholder: future enrichment via AuthorInfos.

            // Use a local variable for introduction instead of modifying the tracked entity
            string introduction = article.Introduction;
            if (string.IsNullOrWhiteSpace(introduction))
            {
                var intro = html.ExtractIntroduction(latest?.Content);
                if (!string.IsNullOrWhiteSpace(intro))
                {
                    introduction = intro;
                }
            }

            var entry = new CatalogEntry
            {
                ArticleNumber = article.ArticleNumber,
                BannerImage = article.BannerImage,
                BlogKey = article.BlogKey,
                Published = article.Published,
                Status = article.StatusCode == 0 ? "Inactive" : "Active",
                Title = article.Title,
                Updated = article.Updated,
                UrlPath = article.UrlPath,
                TemplateId = article.TemplateId,
                AuthorInfo = authorInfo,
                Introduction = introduction // Use local variable
            };

            db.ArticleCatalog.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
            return entry;
        }

        /// <summary>
        /// Deletes the catalog entry associated with the specified article number, if it exists.
        /// </summary>
        /// <param name="articleNumber">The logical article number whose catalog entry should be removed.</param>
        /// <returns>A task representing the asynchronous delete operation.</returns>
        /// <remarks>
        /// No exception is thrown if the entry does not exist (idempotent behavior).
        /// </remarks>
        public async Task DeleteAsync(int articleNumber)
        {
            var existing = await db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == articleNumber);
            if (existing != null)
            {
                db.ArticleCatalog.Remove(existing);
                await db.SaveChangesAsync();
            }
        }
    }
}
