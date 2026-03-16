// <copyright file="BlogPublishingContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.BlogPublishing
{
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Implementation of blog publishing context providing infrastructure dependencies.
    /// </summary>
    public class BlogPublishingContext : IBlogPublishingContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlogPublishingContext"/> class.
        /// </summary>
        /// <param name="database">The database context.</param>
        /// <param name="storage">The blob storage context.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        public BlogPublishingContext(
            ApplicationDbContext database,
            IStorageContext storage,
            IHttpContextAccessor httpContextAccessor)
        {
            Database = database;
            Storage = storage;
            HttpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public ApplicationDbContext Database { get; }

        /// <inheritdoc/>
        public IStorageContext Storage { get; }

        /// <inheritdoc/>
        public IHttpContextAccessor HttpContextAccessor { get; }
    }
}
