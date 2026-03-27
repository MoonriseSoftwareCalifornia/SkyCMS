// <copyright file="PubController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Controllers
{
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Services.Caching;
    using Cosmos.Publisher.Controllers;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.EditorSettings;

    /// <summary>
    /// Secure file access controller and proxy.
    /// </summary>
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class PubController : PubControllerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PubController"/> class.
        /// </summary>
        /// <param name="mediator">Mediator for CQRS queries.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="options">Editor settings.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="cacheService">Cache service for file entries.</param>
        /// <param name="cacheKeyProvider">Cache key provider.</param>
        public PubController(
            IMediator mediator,
            ApplicationDbContext dbContext,
            IStorageContext storageContext,
            IEditorSettings options,
            ILogger<PubController> logger,
            ICacheService<PubControllerBase.CachedFile> cacheService,
            ICacheKeyProvider cacheKeyProvider)
            : base(mediator, dbContext, storageContext, options.CosmosRequiresAuthentication, logger, cacheService, cacheKeyProvider)
        {
        }
    }
}
