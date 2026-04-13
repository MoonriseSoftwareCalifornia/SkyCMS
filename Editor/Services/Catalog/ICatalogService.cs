// <copyright file="ICatalogService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Catalog
{
    using Cosmos.Common.Data;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines operations for creating, updating, and removing catalog entries
    /// derived from <see cref="Article" /> content.
    /// </summary>
    /// <remarks>
    /// Implementations are responsible for:
    /// <list type="bullet">
    /// <item>Projecting persisted <see cref="Article" /> data into a <see cref="CatalogEntry" />.</item>
    /// <item>Ensuring idempotent create/update (upsert) behavior.</item>
    /// <item>Maintaining the single source of truth for lightweight listing / navigation metadata.</item>
    /// </list>
    /// </remarks>
    public interface ICatalogService
    {
        /// <summary>
        /// Creates or updates the <see cref="CatalogEntry" /> that represents the supplied <paramref name="article"/>.
        /// </summary>
        /// <param name="article">
        /// The full article domain object whose current state should be reflected in the catalog.
        /// Must contain a valid <c>ArticleNumber</c> and any fields required to project catalog metadata
        /// (e.g. Title, StatusCode, UrlPath, Published, Updated, BannerImage, Introduction, TemplateId, permissions, etc.).
        /// </param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>
        /// A task that resolves to the up-to-date <see cref="CatalogEntry" /> snapshot after persistence.
        /// </returns>
        /// <remarks>
        /// Typical implementation responsibilities:
        /// <list type="bullet">
        /// <item>Map relevant fields from <see cref="Article" /> to <see cref="CatalogEntry" />.</item>
        /// <item>Create a new entry if one does not already exist for the given article number.</item>
        /// <item>Update existing entry fields if it already exists.</item>
        /// <item>Handle any optimistic concurrency concerns at the persistence layer.</item>
        /// </list>
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="article"/> is null.</exception>
        Task<CatalogEntry> UpsertAsync(Article article, System.Threading.CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the catalog entry associated with the specified <paramref name="articleNumber"/>.
        /// </summary>
        /// <param name="articleNumber">The logical article number identifying the catalog entry to remove.</param>
        /// <returns>A task representing the asynchronous delete operation.</returns>
        /// <remarks>
        /// Implementations should be resilient if the entry does not exist (i.e. no-op). Any cascading
        /// cleanup (e.g. permissions) should be handled internally.
        /// </remarks>
        Task DeleteAsync(int articleNumber);
    }
}
