// <copyright file="PubController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Cosmos.BlobService;
using Cosmos.Cms.Common.Services.Configurations;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cosmos.Publisher.Controllers
{
    /// <summary>
    /// Secure file access controller and proxy.
    /// </summary>
    [AllowAnonymous]
    public class PubController : PubControllerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PubController"/> class.
        /// </summary>
        /// <param name="mediator">Mediator for CQRS queries.</param>
        /// <param name="options">Cosmos options.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="cacheService">Cache service for file caching.</param>
        /// <param name="cacheKeyProvider">Cache key provider.</param>
        public PubController(
            IMediator mediator,
            IOptions<SiteSettings> options,
            ApplicationDbContext dbContext,
            IStorageContext storageContext,
            ILogger<PubController> logger,
            ICacheService<CachedFile> cacheService,
            ICacheKeyProvider cacheKeyProvider)
            : base(mediator, dbContext, storageContext, options.Value.CosmosRequiresAuthentication, logger, cacheService, cacheKeyProvider)
        {
        }
    }
}
