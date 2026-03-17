// <copyright file="PublisherConfigurationKeys.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Publisher.Configuration
{
    /// <summary>
    /// Configuration keys and constants for the Cosmos Publisher application.
    /// Centralizes all magic string configuration keys to improve maintainability and reduce typos.
    /// </summary>
    public static class PublisherConfigurationKeys
    {
        /// <summary>
        /// Gets the configuration section name for Publisher-specific settings.
        /// </summary>
        public const string SectionName = "Publisher";

        /// <summary>
        /// Configuration key for determining if static website mode is enabled.
        /// </summary>
        public const string CosmosStaticWebPages = nameof(CosmosStaticWebPages);

        /// <summary>
        /// Configuration key for Cosmos database identity name.
        /// </summary>
        public const string CosmosIdentityDbName = nameof(CosmosIdentityDbName);

        /// <summary>
        /// Default Cosmos database identity name.
        /// </summary>
        public const string DefaultCosmosIdentityDbName = "cosmoscms";

        /// <summary>
        /// Configuration key for allowed CORS origins (comma-separated).
        /// </summary>
        public const string CorsAllowedOrigins = nameof(CorsAllowedOrigins);

        /// <summary>
        /// Configuration key for whether Cosmos requires authentication.
        /// </summary>
        public const string CosmosRequiresAuthentication = nameof(CosmosRequiresAuthentication);

        /// <summary>
        /// Configuration key for whether local accounts are allowed.
        /// </summary>
        public const string AllowLocalAccounts = nameof(AllowLocalAccounts);

        /// <summary>
        /// Configuration key for valid Entra ID user groups (semicolon-separated).
        /// </summary>
        public const string EntraIdValidUserGroups = nameof(EntraIdValidUserGroups);

        /// <summary>
        /// Connection string key for the application database.
        /// </summary>
        public const string ApplicationDbContextConnection = "ApplicationDbContextConnection";

        /// <summary>
        /// CORS policy name for allowed origins.
        /// </summary>
        public const string CorsPolicyName = "AllowedOrigPolicy";

        /// <summary>
        /// Rate limiting policy name for API throttling.
        /// </summary>
        public const string RateLimitingPolicyName = "fixed";

        /// <summary>
        /// Distributed cache container name.
        /// </summary>
        public const string CacheContainerName = "PublisherCache";

        /// <summary>
        /// Gets rate limiter settings.
        /// </summary>
        public static class RateLimiter
        {
            /// <summary>
            /// Default permit limit for rate limiter (4 requests).
            /// </summary>
            public const int DefaultPermitLimit = 4;

            /// <summary>
            /// Default window duration for rate limiter (8 seconds).
            /// </summary>
            public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(8);

            /// <summary>
            /// Default queue limit for rate limiter (2 requests).
            /// </summary>
            public const int DefaultQueueLimit = 2;
        }

        /// <summary>
        /// Gets response caching settings.
        /// </summary>
        public static class ResponseCaching
        {
            /// <summary>
            /// Maximum body size for response caching (1024 bytes).
            /// </summary>
            public const int MaximumBodySize = 1024;

            /// <summary>
            /// Whether to use case-sensitive paths in caching.
            /// </summary>
            public const bool UseCaseSensitivePaths = true;
        }

        /// <summary>
        /// Gets static cache expiration settings.
        /// </summary>
        public static class FileCache
        {
            /// <summary>
            /// Cache duration for index.html files (10 seconds).
            /// </summary>
            public static readonly TimeSpan IndexHtmlExpiration = TimeSpan.FromSeconds(10);

            /// <summary>
            /// Cache duration for other static files (5 minutes).
            /// </summary>
            public static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

            /// <summary>
            /// Cache duration for SPA check results (5 minutes).
            /// </summary>
            public static readonly TimeSpan SpaCheckExpiration = TimeSpan.FromMinutes(5);

            /// <summary>
            /// Cache duration for published page files (4 minutes with sliding expiration).
            /// </summary>
            public static readonly TimeSpan PublicFileExpiration = TimeSpan.FromMinutes(4);

            /// <summary>
            /// Cache control header value for public files.
            /// </summary>
            public const string PublicCacheControl = "public, max-age=3600";

            /// <summary>
            /// Cache control header value for private/authenticated files.
            /// </summary>
            public const string PrivateCacheControl = "private, no-cache, no-store, must-revalidate";
        }

        /// <summary>
        /// Gets OAuth configuration keys.
        /// </summary>
        public static class OAuth
        {
            /// <summary>
            /// Configuration section for Google OAuth settings.
            /// </summary>
            public const string GoogleSection = "GoogleOAuth";

            /// <summary>
            /// Configuration section for Microsoft Entra ID OAuth settings.
            /// </summary>
            public const string MicrosoftSection = "MicrosoftOAuth";
        }

        /// <summary>
        /// Gets authentication cookie settings.
        /// </summary>
        public static class Authentication
        {
            /// <summary>
            /// Name of the authentication cookie.
            /// </summary>
            public const string CookieName = "CosmosAuthCookie";

            /// <summary>
            /// Default authentication cookie expiration (5 days).
            /// </summary>
            public static readonly TimeSpan DefaultExpireTimeSpan = TimeSpan.FromDays(5);

            /// <summary>
            /// Whether sliding expiration is enabled for authentication cookies.
            /// </summary>
            public const bool SlidingExpirationEnabled = true;

            /// <summary>
            /// Whether confirmed account is required.
            /// </summary>
            public const bool RequireConfirmedAccount = true;
        }

        /// <summary>
        /// Gets response header settings.
        /// </summary>
        public static class ResponseHeaders
        {
            /// <summary>
            /// Cache control header for static assets (30 seconds).
            /// </summary>
            public static readonly TimeSpan StaticAssetsCacheControl = TimeSpan.FromSeconds(30);
        }
    }
}
