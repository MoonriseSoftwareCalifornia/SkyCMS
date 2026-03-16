// <copyright file="IPublishingContext.cs" company="Moonrise Software, LLC">
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
    /// Provides access to core infrastructure dependencies required for publishing operations.
    /// </summary>
    /// <remarks>
    /// This composite groups cohesive infrastructure services that represent the publishing environment:
    /// database context, blob storage, configuration settings, HTTP request context, and query services.
    /// By grouping these dependencies, we reduce constructor parameter count while maintaining
    /// clear separation between infrastructure (this interface) and business logic services.
    /// </remarks>
    public interface IPublishingContext
    {
        /// <summary>
        /// Gets the database context for article and page persistence.
        /// </summary>
        ApplicationDbContext Database { get; }

        /// <summary>
        /// Gets the blob storage context for static file uploads.
        /// </summary>
        IStorageContext Storage { get; }

        /// <summary>
        /// Gets the editor settings for configuration values.
        /// </summary>
        IEditorSettings Settings { get; }

        /// <summary>
        /// Gets the HTTP context accessor for accessing current request information.
        /// </summary>
        /// <remarks>
        /// Used primarily for extracting the current user ID from claims.
        /// May be null in background job scenarios.
        /// </remarks>
        IHttpContextAccessor HttpContextAccessor { get; }

        /// <summary>
        /// Gets the article catalog query service for querying published articles.
        /// </summary>
        /// <remarks>
        /// Provides read-only query operations over the article catalog,
        /// used for TOC generation, blog rendering, and article lookups.
        /// </remarks>
        IArticleCatalogQueryService ArticleCatalogQueryService { get; }
    }
}
