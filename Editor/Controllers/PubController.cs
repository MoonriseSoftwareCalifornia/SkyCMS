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
    using Cosmos.Publisher.Controllers;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Caching.Memory;
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
        /// <param name="dbContext">Database context.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="options">Editor settings.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="memoryCache">Memory cache.</param>
        public PubController(
            ApplicationDbContext dbContext,
            IStorageContext storageContext,
            IEditorSettings options,
            ILogger<PubController> logger,
            IMemoryCache memoryCache)
            : base(dbContext, storageContext, options.CosmosRequiresAuthentication, logger, memoryCache)
        {
        }
    }
}
