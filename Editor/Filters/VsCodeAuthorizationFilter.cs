// <copyright file="VsCodeAuthorizeAttribute.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Filters
{
    using System;
    using System.Linq;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.Caching.Memory;

    /// <summary>
    /// Authorization attribute for VS Code extension API endpoints.
    /// Validates bearer token or authenticated user with editor role.
    /// Apply this to controller actions that require VS Code authorization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class VsCodeAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        /// <summary>
        /// Performs authorization check for VS Code requests.
        /// </summary>
        /// <param name="context">Authorization filter context.</param>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
            {
                return;
            }

            var memoryCache = context.HttpContext.RequestServices.GetService(typeof(IMemoryCache)) as IMemoryCache;
            if (memoryCache == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            var tokenCachePrefix = "VsCodeToken_";
            var allowedRoles = new[] { "Editors", "Administrators" };

            if (TryGetBearerIdentity(context, memoryCache, tokenCachePrefix, out var bearerIdentity))
            {
                if (bearerIdentity != null && IsAllowedRole(bearerIdentity.Role, allowedRoles))
                {
                    return;
                }

                context.Result = new ForbidResult();
                return;
            }

            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                var role = user.Claims.FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;
                if (!string.IsNullOrWhiteSpace(role) && IsAllowedRole(role, allowedRoles))
                {
                    return;
                }
            }

            context.Result = new UnauthorizedResult();
        }

        private static bool TryGetBearerIdentity(
            AuthorizationFilterContext context,
            IMemoryCache memoryCache,
            string tokenCachePrefix,
            out BearerTokenCacheEntry? identity)
        {
            identity = null;
            var token = ExtractBearerToken(context);
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (!memoryCache.TryGetValue(tokenCachePrefix + token, out BearerTokenCacheEntry? entry) || entry == null)
            {
                return false;
            }

            identity = entry;
            return true;
        }

        private static string? ExtractBearerToken(AuthorizationFilterContext context)
        {
            var auth = context.HttpContext.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return auth.Substring("Bearer ".Length).Trim();
        }

        private static bool IsAllowedRole(string role, string[] allowedRoles)
        {
            return allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Represents a bearer token cache entry.
        /// </summary>
        public class BearerTokenCacheEntry
        {
            /// <summary>
            /// Gets or sets the user's role.
            /// </summary>
            public string Role { get; set; } = string.Empty;
        }
    }
}
