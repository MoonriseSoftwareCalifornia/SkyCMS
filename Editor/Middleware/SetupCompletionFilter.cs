// <copyright file="SetupCompletionFilter.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Sky.Editor.Services.Setup;
using System;
using System.Threading.Tasks;

namespace Sky.Editor.Middleware;

/// <summary>
/// Endpoint filter that ensures setup is complete before allowing access to protected endpoints.
/// This is an alternative to middleware-based setup detection.
/// </summary>
/// <remarks>
/// To use this filter, apply it to specific endpoints or route groups:
/// <code>
/// app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}")
///    .AddEndpointFilter&lt;SetupCompletionFilter&gt;();
/// </code>
/// </remarks>
public class SetupCompletionFilter : IEndpointFilter
{
    private const string SETUP_CACHE_KEY_PREFIX = "SetupComplete";
    private const string HEADER_ORIGIN_HOSTNAME = "x-origin-hostname";

    private readonly bool isMultiTenantEditor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetupCompletionFilter"/> class.
    /// </summary>
    /// <param name="isMultiTenantEditor">Whether the application is running in multi-tenant mode.</param>
    public SetupCompletionFilter(bool isMultiTenantEditor)
    {
        this.isMultiTenantEditor = isMultiTenantEditor;
    }

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Get hostname for cache key
        var hostname = GetHostname(httpContext);
        var cache = httpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var cacheKey = GetSetupCacheKey(hostname);
        bool requiresSetup;

        if (!cache.TryGetValue(cacheKey, out requiresSetup))
        {
            // Call the appropriate service based on deployment mode
            if (isMultiTenantEditor)
            {
                var multiTenantSetupService = httpContext.RequestServices.GetService<IMultiTenantSetupService>();
                requiresSetup = multiTenantSetupService != null
                    ? await multiTenantSetupService.TenantRequiresSetupAsync()
                    : false;
            }
            else
            {
                var setupService = httpContext.RequestServices.GetRequiredService<ISetupService>();
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
            httpContext.Response.Redirect("/___setup");
            return null;
        }

        return await next(context);
    }

    private static string GetHostname(HttpContext context)
    {
        var hostname = context.Request.Headers[HEADER_ORIGIN_HOSTNAME].ToString().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(hostname)
            ? context.Request.Host.Host.ToLowerInvariant()
            : hostname;
    }

    private static string GetSetupCacheKey(string hostname) => $"{SETUP_CACHE_KEY_PREFIX}:{hostname}";
}
