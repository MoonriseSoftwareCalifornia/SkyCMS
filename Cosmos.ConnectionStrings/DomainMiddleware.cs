// <copyright file="DomainMiddleware.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cosmos.DynamicConfig
{
    /// <summary>
    /// Domain name middleware service. Gets the domain name of the current request and validates tenant access.
    /// </summary>
    public class DomainMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger<DomainMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainMiddleware"/> class.
        /// </summary>
        /// <param name="next">Request delegate.</param>
        /// <param name="logger">Logger service.</param>
        public DomainMiddleware(RequestDelegate next, ILogger<DomainMiddleware> logger)
        {
            this.next = next;
            this._logger = logger;
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        /// <param name="context">Current HTTP context.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var domain = context.Request.Host.Host.ToLowerInvariant();
            context.Items["Domain"] = domain;

            _logger.LogDebug("Domain middleware processing request for domain: {Domain}", domain);

            var configProvider = context.RequestServices.GetService<IDynamicConfigurationProvider>();
            if (configProvider == null || !configProvider.IsMultiTenantConfigured)
            {
                await next(context);
                return;
            }

            try
            {
                var connectionString = await configProvider.GetDatabaseConnectionStringAsync(domain, context.RequestAborted);
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogWarning("Unauthorized domain access attempt: {Domain}, Path: {Path}, IP: {IP}",
                        domain,
                        context.Request.Path,
                        context.Connection.RemoteIpAddress?.ToString());

                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("Not Found");
                    return;
                }

                _logger.LogInformation("Valid domain access: {Domain}", domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating domain: {Domain}", domain);
                // Continue processing - fail open for availability, but log the error
            }

            await next(context);
        }
    }
}
