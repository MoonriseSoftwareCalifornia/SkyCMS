// <copyright file="IPublishingAuxiliaryServices.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Publishing
{
    using Sky.Editor.Services.BlogPublishing;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.StaticFiles;
    using Sky.Editor.Services.TableOfContents;

    /// <summary>
    /// Composite service providing access to auxiliary publishing services.
    /// </summary>
    /// <remarks>
    /// This service groups cohesive publishing-related support services to reduce
    /// constructor parameter count in <see cref="PublishingService"/> and improve
    /// maintainability. All services in this composite are stateless and registered
    /// as transient services in the DI container.
    /// </remarks>
    public interface IPublishingAuxiliaryServices
    {
        /// <summary>
        /// Gets the CDN purge service for cache invalidation.
        /// </summary>
        ICdnPurgeService CdnPurgeService { get; }

        /// <summary>
        /// Gets the table of contents service for generating TOC JSON files.
        /// </summary>
        ITocService TocService { get; }

        /// <summary>
        /// Gets the static file service for generating and managing static HTML files.
        /// </summary>
        IStaticFileService StaticFileService { get; }

        /// <summary>
        /// Gets the blog publishing service for blog-specific operations.
        /// </summary>
        IBlogPublishingService BlogPublishingService { get; }
    }
}
