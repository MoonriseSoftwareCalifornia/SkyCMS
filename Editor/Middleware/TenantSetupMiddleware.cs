// <copyright file="TenantSetupMiddleware.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sky.Editor.Services.Setup;
using System.Threading.Tasks;

namespace Sky.Editor.Middleware
{
    /// <summary>
    /// Middleware to redirect users to tenant setup if required.
    /// </summary>
    public class TenantSetupMiddleware
    {
        private readonly RequestDelegate next;

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantSetupMiddleware"/> class.
        /// </summary>
        public TenantSetupMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            // Skip for static files, health checks, diagnostics, and setup pages
            if (context.Request.Path.StartsWithSegments("/___setup") ||
                context.Request.Path.StartsWithSegments("/___diagnostics") ||
                context.Request.Path.StartsWithSegments("/api") || // ✅ Skip API endpoints
                context.Request.Path.StartsWithSegments("/health") ||
                context.Request.Path.StartsWithSegments("/healthz") ||
                context.Request.Path.StartsWithSegments("/lib") ||
                context.Request.Path.StartsWithSegments("/css") ||
                context.Request.Path.StartsWithSegments("/js") ||
                context.Request.Path.StartsWithSegments("/Identity") ||
                context.Request.Path.Value?.Contains(".") == true)
            {
                await next(context);
                return;
            }

            // Check if tenant requires setup
            var setupService = context.RequestServices.GetService<IMultiTenantSetupService>();
            if (setupService != null)
            {
                var requiresSetup = await setupService.TenantRequiresSetupAsync();
                if (requiresSetup)
                {
                    context.Response.Redirect("/___setup/tenant");
                    return;
                }
            }

            await next(context);
        }
    }

    /// <summary>
    /// Extension methods for TenantSetupMiddleware.
    /// </summary>
    public static class TenantSetupMiddlewareExtensions
    {
        /// <summary>
        /// Adds the TenantSetupMiddleware to the application pipeline.
        /// </summary>
        public static IApplicationBuilder UseTenantSetupRedirect(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TenantSetupMiddleware>();
        }
    }
}
