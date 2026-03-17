// <copyright file="RequestContextProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System.Security.Claims;

namespace Cosmos.Publisher.Services
{
    /// <summary>
    /// Provides access to HTTP request context information.
    /// Implements <see cref="IRequestContextProvider"/> for scoped-per-request usage.
    /// </summary>
    public class RequestContextProvider : IRequestContextProvider
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestContextProvider"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor for accessing request context.</param>
        public RequestContextProvider(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// Gets the HTTP context.
        /// </summary>
        private HttpContext HttpContext => this.httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is not available");

        /// <inheritdoc/>
        public string GetPath()
        {
            return this.HttpContext.Request.Path.ToString();
        }

        /// <inheritdoc/>
        public Microsoft.AspNetCore.Http.PathString GetPathValue()
        {
            return this.HttpContext.Request.Path;
        }

        /// <inheritdoc/>
        public ClaimsPrincipal GetUser()
        {
            return this.HttpContext.User;
        }

        /// <inheritdoc/>
        public string GetQueryParameter(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentNullException(nameof(parameterName));
            }

            return this.HttpContext.Request.Query[parameterName].ToString();
        }

        /// <inheritdoc/>
        public string GetHeader(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            return this.HttpContext.Request.Headers[headerName].ToString();
        }

        /// <inheritdoc/>
        public string GetHostName()
        {
            return this.HttpContext.Request.Host.Host;
        }

        /// <inheritdoc/>
        public bool IsUserAuthenticated()
        {
            return this.HttpContext.User?.Identity?.IsAuthenticated ?? false;
        }

        /// <inheritdoc/>
        public string GetUserName()
        {
            return this.HttpContext.User?.Identity?.Name;
        }

        /// <inheritdoc/>
        public string GetUserEmail()
        {
            return this.GetClaimValue(ClaimTypes.Email);
        }

        /// <inheritdoc/>
        public string GetClaimValue(string claimType)
        {
            if (string.IsNullOrWhiteSpace(claimType))
            {
                throw new ArgumentNullException(nameof(claimType));
            }

            return this.HttpContext.User?.FindFirstValue(claimType);
        }

        /// <inheritdoc/>
        public HttpContext GetHttpContext()
        {
            return this.HttpContext;
        }
    }
}
