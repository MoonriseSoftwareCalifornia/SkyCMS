// <copyright file="SetupMiddlewareExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sky.Editor.Services.Setup;

namespace Sky.Editor.Middleware;

/// <summary>
/// Extension methods for configuring setup-related middleware.
/// </summary>
public static class SetupMiddlewareExtensions
{
    private const string SETUP_CACHE_KEY_PREFIX = "SetupComplete";
    private const string HEADER_ORIGIN_HOSTNAME = "x-origin-hostname";

    /// <summary>
    /// Adds middleware that redirects users to the setup wizard if setup is incomplete.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="isMultiTenantEditor">Whether the application is running in multi-tenant mode.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseSetupDetection(
        this IApplicationBuilder app,
        bool isMultiTenantEditor)
    {
        app.Use(async (context, next) =>
        {
            // Skip setup check for setup wizard pages, static files, and health checks
            if (ShouldSkipSetupCheck(context.Request.Path))
            {
                await next();
                return;
            }

            // Get hostname for cache key (works for both single and multi-tenant)
            var hostname = GetHostname(context);
            var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
            var cacheKey = GetSetupCacheKey(hostname);
            bool requiresSetup;

            if (!cache.TryGetValue(cacheKey, out requiresSetup))
            {
                // Call the appropriate service based on deployment mode
                if (isMultiTenantEditor)
                {
                    var multiTenantSetupService = context.RequestServices.GetService<IMultiTenantSetupService>();
                    requiresSetup = multiTenantSetupService != null
                        ? await multiTenantSetupService.TenantRequiresSetupAsync()
                        : false;
                }
                else
                {
                    var setupService = context.RequestServices.GetRequiredService<ISetupService>();
                    var isComplete = await setupService.IsSetupCompleteAsync();
                    requiresSetup = !isComplete;
                }

                // Cache: 24 hours if complete, 2-5 minutes if incomplete
                var cacheExpiration = !requiresSetup
                    ? TimeSpan.FromHours(24)
                    : (isMultiTenantEditor ? TimeSpan.FromMinutes(2) : TimeSpan.FromMinutes(5));

                cache.Set(cacheKey, requiresSetup, cacheExpiration);
            }

            if (requiresSetup)
            {
                context.Response.Redirect("/___setup");
                return;
            }

            await next();
        });

        return app;
    }

    /// <summary>
    /// Adds middleware that prevents access to the setup wizard when setup is complete.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="isMultiTenantEditor">Whether the application is running in multi-tenant mode.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseSetupAccessControl(
        this IApplicationBuilder app,
        bool isMultiTenantEditor)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/___setup"))
            {
                // Check if setup is still allowed/needed using cached value
                var hostname = GetHostname(context);
                var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
                var cacheKey = GetSetupCacheKey(hostname);

                // Try to get cached value first
                if (cache.TryGetValue(cacheKey, out bool requiresSetup) && !requiresSetup)
                {
                    // Setup is complete (cached) - redirect away from setup wizard
                    context.Response.Redirect("/");
                    return;
                }

                // If not in cache or setup is still needed, allow access to setup wizard
                // Also verify configuration allows setup
                if (!isMultiTenantEditor)
                {
                    var config = context.RequestServices.GetRequiredService<IConfiguration>();
                    var allowSetup = config.GetValue<bool?>("CosmosAllowSetup") ?? false;
                    if (!allowSetup)
                    {
                        context.Response.Redirect("/");
                        return;
                    }
                }
            }

            await next();
        });

        return app;
    }

    /// <summary>
    /// Extracts the hostname from the request, checking x-origin-hostname header first.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The hostname in lowercase.</returns>
    private static string GetHostname(HttpContext context)
    {
        var hostname = context.Request.Headers[HEADER_ORIGIN_HOSTNAME].ToString().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(hostname)
            ? context.Request.Host.Host.ToLowerInvariant()
            : hostname;
    }

    /// <summary>
    /// Generates a cache key for setup completion status.
    /// </summary>
    /// <param name="hostname">The hostname.</param>
    /// <returns>The cache key.</returns>
    private static string GetSetupCacheKey(string hostname) => $"{SETUP_CACHE_KEY_PREFIX}:{hostname}";

    /// <summary>
    /// Determines if a request path should skip setup detection checks.
    /// </summary>
    /// <param name="path">The request path to check.</param>
    /// <returns>True if the path should skip setup checks; otherwise false.</returns>
    private static bool ShouldSkipSetupCheck(PathString path)
    {
        return path.StartsWithSegments("/___setup") ||
               path.StartsWithSegments("/setup") ||
               path.StartsWithSegments("/lib") ||
               path.StartsWithSegments("/css") ||
               path.StartsWithSegments("/js") ||
               path.StartsWithSegments("/images") ||
               path.StartsWithSegments("/fonts") ||
               path.Value.EndsWith(".css") ||
               path.Value.EndsWith(".js") ||
               path.Value.EndsWith(".map") ||
               path.StartsWithSegments("/___healthz") ||
               path.StartsWithSegments("/healthz") ||
               path.StartsWithSegments("/.well-known");
    }
}
