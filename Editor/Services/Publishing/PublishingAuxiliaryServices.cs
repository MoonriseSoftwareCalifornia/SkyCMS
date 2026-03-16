// <copyright file="PublishingAuxiliaryServices.cs" company="Moonrise Software, LLC">
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
    /// Composite service implementation providing access to auxiliary publishing services.
    /// </summary>
    /// <remarks>
    /// This implementation groups four cohesive publishing-related support services:
    /// <list type="bullet">
    ///   <item><description>CDN purge service for cache invalidation</description></item>
    ///   <item><description>Table of contents service for TOC JSON generation</description></item>
    ///   <item><description>Static file service for HTML file generation</description></item>
    ///   <item><description>Blog publishing service for blog-specific operations</description></item>
    /// </list>
    /// All services are injected via constructor and are stateless transient services.
    /// </remarks>
    public class PublishingAuxiliaryServices : IPublishingAuxiliaryServices
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PublishingAuxiliaryServices"/> class.
        /// </summary>
        /// <param name="cdnPurgeService">The CDN purge service.</param>
        /// <param name="tocService">The table of contents service.</param>
        /// <param name="staticFileService">The static file service.</param>
        /// <param name="blogPublishingService">The blog publishing service.</param>
        public PublishingAuxiliaryServices(
            ICdnPurgeService cdnPurgeService,
            ITocService tocService,
            IStaticFileService staticFileService,
            IBlogPublishingService blogPublishingService)
        {
            CdnPurgeService = cdnPurgeService;
            TocService = tocService;
            StaticFileService = staticFileService;
            BlogPublishingService = blogPublishingService;
        }

        /// <inheritdoc/>
        public ICdnPurgeService CdnPurgeService { get; }

        /// <inheritdoc/>
        public ITocService TocService { get; }

        /// <inheritdoc/>
        public IStaticFileService StaticFileService { get; }

        /// <inheritdoc/>
        public IBlogPublishingService BlogPublishingService { get; }
    }
}
