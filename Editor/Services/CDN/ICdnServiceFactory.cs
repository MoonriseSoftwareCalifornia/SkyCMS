// <copyright file="ICdnServiceFactory.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.CDN
{
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    /// <summary>
    /// Factory interface for creating CDN service instances.
    /// </summary>
    public interface ICdnServiceFactory
    {
        /// <summary>
        /// Creates a CDN service instance.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="httpContext">HTTP context.</param>
        /// <returns>CDN service instance.</returns>
        Task<CdnService> CreateCdnServiceAsync(ApplicationDbContext dbContext, ILogger logger, HttpContext httpContext);
    }
}
