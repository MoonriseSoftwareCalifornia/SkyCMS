// <copyright file="ConnectionStringProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Cosmos.DynamicConfig.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Cosmos.DynamicConfig
{
    /// <summary>
    /// Gets connection strings and configuration values from the configuration file.
    /// </summary>
    /// <remarks>
    /// If in a multi-tenant environment, the connection string names are prefixed by the domain name.
    /// </remarks>
    public class DynamicConfigurationProvider : IDynamicConfigurationProvider
    {
        private readonly IConfiguration configuration;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IMemoryCache memoryCache;
        private readonly StringBuilder errorMessages = new();
        private readonly string connectionString;
        private readonly ILogger<DynamicConfigurationProvider> _logger;
        private readonly ProxySettings proxySettings;
        private readonly HashSet<IPAddress> trustedProxyIPs;

        private static readonly Regex DnsRegex = new(
            @"^(?=.{1,255}$)(?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9]?)(\.[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*$",
            RegexOptions.Compiled);


        private const string CacheKeyPrefix = "tenant:connection:";

        private readonly SemaphoreSlim _preloadLock = new(1, 1);
        private DateTime _lastPreloadTime = DateTime.MinValue;
        private const int PreloadIntervalMinutes = 30;

        /// <summary>
        /// Gets the database connection
        /// </summary>
        //private readonly Connection? connection;

        /// <summary>
        /// Gets a value indicating whether the connection is configured for multi-tenant.
        /// </summary>
        public bool IsMultiTenantConfigured { get { return configuration.GetValue<bool?>("MultiTenant") ?? false; } }

        /// <summary>
        /// Gets a value indicating the error messages that may exist.
        /// </summary>
        public string ErrorMesages => errorMessages.ToString();

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicConfigurationProvider"/> class.
        /// </summary>
        /// <param name="configuration">Connection configuration.</param>
        /// <param name="httpContextAccessor">HTTP context accessor.</param>
        /// <param name="logger">Log service.</param>
        /// <param name="memoryCache">Memory cache.</param>
        /// <param name="proxyOptions">Proxy settings.</param>
        /// <remarks>
        /// For unit tests, use <see cref="TestableConfigurationProvider"/> to avoid real database connections.
        /// </remarks>
        public DynamicConfigurationProvider(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache memoryCache,
            ILogger<DynamicConfigurationProvider> logger,
            IOptions<ProxySettings> proxyOptions)
        {
            this.configuration = configuration;
            this.proxySettings = proxyOptions.Value;
            this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

            connectionString = this.configuration.GetConnectionString("ConfigDbConnectionString") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string 'ConfigDbConnectionString' not found or is empty.");
            }

            this.memoryCache = memoryCache;
            _logger = logger;

            // Parse trusted proxy IPs (IPv4 and IPv6 supported)
            trustedProxyIPs = new HashSet<IPAddress>();
            if (proxySettings.TrustedProxyIPs != null)
            {
                foreach (var ip in proxySettings.TrustedProxyIPs)
                {
                    if (IPAddress.TryParse(ip, out var parsedIp))
                    {
                        trustedProxyIPs.Add(parsedIp);
                    }
                    else
                    {
                        _logger?.LogWarning("Invalid IP address in TrustedProxyIPs: {IP}", ip);
                    }
                }
            }

        }

        // Helper to check if the request is from a trusted proxy
        private bool IsFromTrustedProxy(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;
            return remoteIp != null && trustedProxyIPs.Contains(remoteIp);
        }

        /// <summary>
        /// Gets the database connection string.
        /// </summary>
        /// <param name="domainName">Domain name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Database connection string.</returns>
        /// <exception cref="InvalidOperationException">Thrown when HttpContext is unavailable and no domain is provided.</exception>
        public async Task<string?> GetDatabaseConnectionStringAsync(string domainName = "", CancellationToken cancellationToken = default)
        {
            if (httpContextAccessor.HttpContext == null)
            {
                if (string.IsNullOrWhiteSpace(domainName))
                {
                    _logger?.LogError("Cannot resolve tenant connection: HttpContext unavailable and no domain provided");
                    throw new InvalidOperationException(
                        "Cannot resolve tenant connection: HttpContext unavailable and no domain provided. " +
                        "For background jobs or operations outside HTTP context, you must explicitly provide the domain name.");
                }

                _logger?.LogWarning("HttpContext not available - using provided domain: {Domain}", domainName);
            }

            if (string.IsNullOrWhiteSpace(domainName))
            {
                domainName = GetTenantDomainNameFromRequest();
            }

            // Normalize domain name
            domainName = NormalizeDomainName(domainName);

            var connection = await GetTenantConnectionAsync(domainName, cancellationToken);

            if (connection == null)
            {
                _logger?.LogWarning("No connection found for domain: {Domain}", domainName);
                return null;
            }

            return connection.DbConn;
        }

        /// <summary>
        /// Gets the storage connection string.
        /// </summary>
        /// <param name="domainName">Domain name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Storage connection string.</returns>
        /// <exception cref="InvalidOperationException">Thrown when HttpContext is unavailable and no domain is provided.</exception>
        public async Task<string?> GetStorageConnectionStringAsync(string domainName = "", CancellationToken cancellationToken = default)
        {
            if (httpContextAccessor.HttpContext == null)
            {
                if (string.IsNullOrWhiteSpace(domainName))
                {
                    _logger?.LogError("Cannot resolve tenant storage connection: HttpContext unavailable and no domain provided");
                    throw new InvalidOperationException(
                        "Cannot resolve tenant storage connection: HttpContext unavailable and no domain provided. " +
                        "For background jobs or operations outside HTTP context, you must explicitly provide the domain name.");
                }

                _logger?.LogWarning("HttpContext not available for storage connection - using provided domain: {Domain}", domainName);
            }

            if (string.IsNullOrWhiteSpace(domainName))
            {
                domainName = GetTenantDomainNameFromRequest();
            }

            // Normalize domain name
            domainName = NormalizeDomainName(domainName);

            var connection = await GetTenantConnectionAsync(domainName, cancellationToken);

            if (connection == null)
            {
                _logger?.LogWarning("No storage connection found for domain: {Domain}", domainName);
                return null;
            }

            return connection.StorageConn;
        }

        /// <summary>
        /// Gets the configuration value.
        /// </summary>
        /// <param name="key">Key name.</param>
        /// <returns>Key value.</returns>
        public string? GetConfigurationValue(string key)
        {
            return configuration.GetValue<string>(key);
        }

        /// <summary>
        /// Gets the connection string by its name.
        /// </summary>
        /// <param name="name">Connection string name.</param>
        /// <returns>Database connection string.</returns>
        public string? GetConnectionStringByName(string name)
        {
            return configuration.GetConnectionString(name);
        }

        /// <summary>
        /// Gets the tenant website domain name from the request.
        /// </summary>
        /// <param name="useReferer">Get the domain name from the referer instead of the domain name of website.</param>
        /// <returns>Domain Name.</returns>
        /// <remarks>
        /// <para>Returns the domain name by looking at the incomming request.  Here is the order:</para>
        /// <list type="number">
        /// <item>x-origin-hostname host header.</item>
        /// <item>Otherwise returns the host name of the request.</item>
        /// </list>
        /// <para>Note: This should ONLY be used for multi-tenant, single editor website setup.</para>
        /// </remarks>
        public string GetTenantDomainNameFromRequest()
        {
            if (httpContextAccessor.HttpContext == null)
            {
                _logger?.LogWarning("HttpContext is null when attempting to get tenant domain name from request");
                return string.Empty;
            }

            if (httpContextAccessor.HttpContext.Request == null)
            {
                throw new InvalidOperationException("HTTP request is not available.");
            }

            // Only trust x-origin-hostname if enabled and from a trusted proxy
            // Hardened x-origin-hostname header handling to gracefully reject malformed hostnames
            if (proxySettings.TrustXOriginHostname && IsFromTrustedProxy(httpContextAccessor.HttpContext))
            {
                var xhostHeader = GetValidHostName(httpContextAccessor.HttpContext.Request.Headers["x-origin-hostname"].ToString());
                if (!string.IsNullOrWhiteSpace(xhostHeader))
                {
                    // Attempt to parse as URI, fallback to basic hostname validation
                    string? safeHost = null;
                    if (Uri.TryCreate("http://" + xhostHeader, UriKind.Absolute, out var uri))
                    {
                        safeHost = uri.Host.ToLowerInvariant();
                    }
                    else
                    {
                        // Basic hostname validation: allow only letters, digits, hyphens, and dots
                        var hostPattern = @"^[a-zA-Z0-9\-\.]+$";
                        if (System.Text.RegularExpressions.Regex.IsMatch(xhostHeader, hostPattern))
                        {
                            safeHost = xhostHeader.ToLowerInvariant();
                        }
                        else
                        {
                            _logger?.LogWarning("Rejected malformed x-origin-hostname header: {Header}", xhostHeader);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(safeHost))
                    {
                        return safeHost;
                    }
                }
            }

            var hostDomain = httpContextAccessor.HttpContext.Request.Host.Host.ToLowerInvariant();
            return GetValidHostName(hostDomain);
        }

        /// <summary>
        /// Handles possibility that a user entered a URI instead of a domain name, and returns just the host name.
        /// </summary>
        /// <param name="value">URI or domain value.</param>
        /// <returns>Host name only.</returns>
        public static string CleanUpDomainName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var referrerUri))
            {
                return referrerUri.Host.ToLowerInvariant();
            }

            return value.ToLowerInvariant();
        }

        /// <summary>
        /// Tests to see if there is a connection defined for the specified domain name.
        /// </summary>
        /// <param name="domainName">Domain name to validate.</param>
        /// <returns>Domain is valid (true) or not (false).</returns>
        /// <exception cref="ArgumentException">Thrown when ConfigDbConnectionString is not configured.</exception>
        public async Task<bool> ValidateDomainName(string domainName)
        {
            if (string.IsNullOrWhiteSpace(domainName))
            {
                _logger?.LogWarning("ValidateDomainName called with null or empty domain name");
                return false;
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string 'ConfigDbConnectionString' not found.");
            }

            // Normalize domain name for consistency
            domainName = NormalizeDomainName(domainName);

            using var dbContext = GetDbContext();
            var allConnections = await dbContext.Connections.ToListAsync();
            var result = allConnections.FirstOrDefault(c => c.DomainNames != null && c.DomainNames.Contains(domainName, StringComparer.OrdinalIgnoreCase));

            var isValid = result != null;

            if (!isValid)
            {
                _logger?.LogWarning("Domain validation failed for: {Domain}", domainName);
            }

            return isValid;
        }

        /// <summary>
        /// Gets all primary domain names defined in the configuration database.
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> GetAllDomainNamesAsync()
        {
            using var dbContext = GetDbContext();
            var allConnections = await dbContext.Connections.ToListAsync();
            var domainNames = new List<string>();
            foreach (var connection in allConnections)
            {
                if (connection.DomainNames != null)
                {
                    var domainName = connection.DomainNames.FirstOrDefault();
                    if (domainName != null)
                    {
                        domainNames.Add(domainName);
                    }
                }
            }
            return domainNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Gets a validated host name from the provided hostname.
        /// </summary>
        /// <param name="hostname"></param>
        /// <returns></returns>
        /// <remarks>If the host name is invalid, returns string.Empty and writes an entry to the log.</remarks>
        public string GetValidHostName(string hostname)
        {
            if (!string.IsNullOrWhiteSpace(hostname))
            {
                try
                {
                    var idn = new IdnMapping();
                    string ascii = idn.GetAscii(hostname);
                    if (!DnsRegex.IsMatch(ascii)) return string.Empty;
                    if (Uri.CheckHostName(ascii) == UriHostNameType.Dns)
                    {
                        // Success!
                        return ascii.ToLowerInvariant();
                    }

                    _logger?.LogWarning($"Rejected malformed host name: {hostname}", hostname);
                }
                catch (Exception e)
                {
                    _logger?.LogError($"Rejected malformed host name: {hostname}", hostname, e);
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the dynamic configuration database context.
        /// </summary>
        /// <returns></returns>
        protected virtual DynamicConfigDbContext GetDbContext()
        {
            var options = AspNetCore.Identity.FlexDb.CosmosDbOptionsBuilder.GetDbOptions<DynamicConfigDbContext>(this.connectionString);
            return new DynamicConfigDbContext(options);
        }

        /// <summary>
        /// Gets the tenant connection for the domain name.
        /// </summary>
        /// <param name="domainName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Connection?> GetTenantConnectionAsync(string domainName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domainName))
            {
                _logger?.LogDebug("GetTenantConnection called with null or empty domain name");
                return null;
            }

            // Normalize domain name
            domainName = NormalizeDomainName(domainName);

            // Use namespaced cache key to prevent cache poisoning
            var cacheKey = GetCacheKey(domainName);

            if (!memoryCache.TryGetValue<Connection>(cacheKey, out var connection))
            {
                await using var dbContext = GetDbContext();
                connection = await dbContext.Connections.FirstOrDefaultAsync(c =>
                    c.DomainNames != null &&
                    c.DomainNames.Contains(domainName));

                if (connection == null)
                {
                    _logger?.LogDebug("Connection data not found in database for domain: {Domain}.", domainName);
                    return null;
                }

                // Cache with longer expiration since connection strings rarely change
                // Use sliding expiration to keep frequently accessed tenants in cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(20))
                    .SetPriority(CacheItemPriority.High)
                    .RegisterPostEvictionCallback((key, value, reason, state) =>
                    {
                        _logger?.LogDebug("Cache entry evicted: {Key}, Reason: {Reason}", key, reason);
                    });

                memoryCache.Set(cacheKey, connection, cacheOptions);
            }

            return connection;
        }

        /// <summary>
        /// Preloads all tenant connections into cache on startup or periodically.
        /// Call this from a background service or startup configuration.
        /// </summary>
        public async Task PreloadAllConnectionsAsync(CancellationToken cancellationToken = default)
        {
            await _preloadLock.WaitAsync(cancellationToken);
            try
            {
                // Prevent too frequent preloads
                if (DateTime.UtcNow - _lastPreloadTime < TimeSpan.FromMinutes(PreloadIntervalMinutes))
                {
                    return;
                }

                _logger?.LogInformation("Preloading all tenant connections into cache");

                await using var dbContext = GetDbContext();
                var allConnections = await dbContext.Connections
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(30))
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                    .SetPriority(CacheItemPriority.High);

                foreach (var connection in allConnections)
                {
                    if (connection.DomainNames != null)
                    {
                        foreach (var domain in connection.DomainNames)
                        {
                            var normalizedDomain = NormalizeDomainName(domain);
                            var cacheKey = GetCacheKey(normalizedDomain);
                            memoryCache.Set(cacheKey, connection, cacheOptions);
                        }
                    }
                }

                _lastPreloadTime = DateTime.UtcNow;
                _logger?.LogInformation("Preloaded {Count} tenant connections for {DomainCount} domains",
                    allConnections.Count,
                    allConnections.SelectMany(c => c.DomainNames ?? Array.Empty<string>()).Count());
            }
            finally
            {
                _preloadLock.Release();
            }
        }

        /// <summary>
        /// Gets the current tenant's unique identifier from the request context.
        /// </summary>
        /// <returns>Tenant ID (Connection.Id), or null if not in a tenant context or if HttpContext is unavailable.</returns>
        /// <remarks>
        /// This method uses the request headers to determine the domain name, then retrieves the corresponding
        /// Connection entity to return its unique ID. The result is cached for performance via GetTenantConnectionAsync.
        /// </remarks>
        public async Task<Guid?> GetCurrentTenantIdAsync()
        {
            // Get domain name from the current request
            var domainName = GetTenantDomainNameFromRequest();
            
            if (string.IsNullOrWhiteSpace(domainName))
            {
                _logger?.LogWarning("Could not determine tenant domain from request - HttpContext may be unavailable");
                return null;
            }
            
            // Get the connection entity (leverages existing caching for performance)
            var connection = await GetTenantConnectionAsync(domainName);
            
            if (connection == null)
            {
                _logger?.LogWarning("No tenant connection found for domain: {Domain}", domainName);
                return null;
            }
            
            _logger?.LogDebug("Resolved tenant ID {TenantId} for domain {Domain}", connection.Id, domainName);
            return connection.Id;
        }

        /// <summary>
        /// Normalizes a domain name to lowercase for consistent comparison and caching.
        /// </summary>
        /// <param name="domainName">Domain name to normalize.</param>
        /// <returns>Normalized domain name.</returns>
        private static string NormalizeDomainName(string domainName)
        {
            if (string.IsNullOrWhiteSpace(domainName))
            {
                return domainName;
            }

            return domainName.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Gets the cache key for a domain name with proper namespacing.
        /// </summary>
        /// <param name="domainName">Domain name.</param>
        /// <returns>Cache key.</returns>
        private static string GetCacheKey(string domainName)
        {
            return $"{CacheKeyPrefix}{NormalizeDomainName(domainName)}";
        }
    }
}
