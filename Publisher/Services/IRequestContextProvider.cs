// <copyright file="IRequestContextProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System.Security.Claims;

namespace Cosmos.Publisher.Services
{
    /// <summary>
    /// Provides abstraction for accessing HTTP request context information.
    /// This allows decoupling of business logic from direct HttpContext dependency.
    /// </summary>
    public interface IRequestContextProvider
    {
        /// <summary>
        /// Gets the current request path.
        /// </summary>
        /// <returns>The current request path as a string.</returns>
        string GetPath();

        /// <summary>
        /// Gets the current request path value (Microsoft.AspNetCore.Http.PathString).
        /// </summary>
        /// <returns>The current request path value.</returns>
        Microsoft.AspNetCore.Http.PathString GetPathValue();

        /// <summary>
        /// Gets the current authenticated user principal.
        /// </summary>
        /// <returns>The current user principal.</returns>
        ClaimsPrincipal GetUser();

        /// <summary>
        /// Gets the value of the specified query string parameter.
        /// </summary>
        /// <param name="parameterName">The name of the query string parameter.</param>
        /// <returns>The parameter value, or null if not present.</returns>
        string GetQueryParameter(string parameterName);

        /// <summary>
        /// Gets the value of the specified request header.
        /// </summary>
        /// <param name="headerName">The name of the header.</param>
        /// <returns>The header value, or null if not present.</returns>
        string GetHeader(string headerName);

        /// <summary>
        /// Gets the request host name.
        /// </summary>
        /// <returns>The current request host name.</returns>
        string GetHostName();

        /// <summary>
        /// Gets a value indicating whether the user is authenticated.
        /// </summary>
        /// <returns><see langword="true"/> if the current user is authenticated; otherwise, <see langword="false"/>.</returns>
        bool IsUserAuthenticated();

        /// <summary>
        /// Gets the authenticated user's name.
        /// </summary>
        /// <returns>The authenticated user's name, or null if unavailable.</returns>
        string GetUserName();

        /// <summary>
        /// Gets the authenticated user's email address.
        /// </summary>
        /// <returns>The authenticated user's email address, or null if unavailable.</returns>
        string GetUserEmail();

        /// <summary>
        /// Gets a claim value for the current user.
        /// </summary>
        /// <param name="claimType">The type of claim to retrieve.</param>
        /// <returns>The claim value, or null if not found.</returns>
        string GetClaimValue(string claimType);

        /// <summary>
        /// Gets the HttpContext directly (useful when abstraction is insufficient).
        /// </summary>
        /// <returns>The current HttpContext instance.</returns>
        HttpContext GetHttpContext();
    }
}
