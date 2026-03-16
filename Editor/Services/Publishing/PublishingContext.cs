// <copyright file="PublishingContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Publishing
{
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Shared;
    using Microsoft.AspNetCore.Http;
    using Sky.Editor.Services.EditorSettings;

    /// <summary>
    /// Implementation of publishing context that provides access to core infrastructure dependencies.
    /// </summary>
    /// <remarks>
    /// This class groups five infrastructure services required for publishing operations:
    /// <list type="bullet">
    ///   <item><description>Database context for persisting articles and pages</description></item>
    ///   <item><description>Blob storage for uploading static HTML files</description></item>
    ///   <item><description>Configuration settings for publishing behavior</description></item>
    ///   <item><description>HTTP context for accessing current user information</description></item>
    ///   <item><description>Article catalog query service for read-only article operations</description></item>
    /// </list>
    /// All dependencies are injected via constructor and exposed as read-only properties.
    /// This composite is registered as scoped in the DI container to match the lifetime
    /// of ApplicationDbContext.
    /// </remarks>
    public class PublishingContext : IPublishingContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PublishingContext"/> class.
        /// </summary>
        /// <param name="database">The database context.</param>
        /// <param name="storage">The blob storage context.</param>
        /// <param name="settings">The editor settings.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <param name="articleCatalogQueryService">The article catalog query service.</param>
        public PublishingContext(
            ApplicationDbContext database,
            IStorageContext storage,
            IEditorSettings settings,
            IHttpContextAccessor httpContextAccessor,
            IArticleCatalogQueryService articleCatalogQueryService)
        {
            Database = database;
            Storage = storage;
            Settings = settings;
            HttpContextAccessor = httpContextAccessor;
            ArticleCatalogQueryService = articleCatalogQueryService;
        }

        /// <inheritdoc/>
        public ApplicationDbContext Database { get; }

        /// <inheritdoc/>
        public IStorageContext Storage { get; }

        /// <inheritdoc/>
        public IEditorSettings Settings { get; }

        /// <inheritdoc/>
        public IHttpContextAccessor HttpContextAccessor { get; }

        /// <inheritdoc/>
        public IArticleCatalogQueryService ArticleCatalogQueryService { get; }
    }
}
