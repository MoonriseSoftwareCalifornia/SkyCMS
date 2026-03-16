// <copyright file="IBlogPublishingContext.cs" company="Moonrise Software, LLC">
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
    /// Provides access to infrastructure dependencies required for blog publishing operations.
    /// </summary>
    /// <remarks>
    /// This composite groups infrastructure services used by blog publishing:
    /// database context, blob storage, and HTTP request context.
    /// Separates infrastructure concerns from business logic (rendering, mediator, publishing service).
    /// </remarks>
    public interface IBlogPublishingContext
    {
        /// <summary>
        /// Gets the database context for article persistence.
        /// </summary>
        ApplicationDbContext Database { get; }

        /// <summary>
        /// Gets the blob storage context for uploading blog stream wrappers.
        /// </summary>
        IStorageContext Storage { get; }

        /// <summary>
        /// Gets the HTTP context accessor for accessing current user claims.
        /// </summary>
        /// <remarks>
        /// Used to extract the current user ID for article authorship.
        /// </remarks>
        IHttpContextAccessor HttpContextAccessor { get; }
    }
}
