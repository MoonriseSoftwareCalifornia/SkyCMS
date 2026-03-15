// <copyright file="IConnectionStringProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>
namespace Cosmos.DynamicConfig
{
    /// <summary>
    /// Connection string provider interface.
    /// </summary>
    public interface IDynamicConfigurationProvider
    {
        /// <summary>
        /// Gets a value indicating if the service is configured.
        /// </summary>
        bool IsMultiTenantConfigured { get; }

        /// <summary>
        /// Get database connection string based on domain.
        /// </summary>
        /// <param name="domainName">domain name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Connection string.</returns>
        Task<string?> GetDatabaseConnectionStringAsync(string domainName = "", CancellationToken cancellationToken = default);

        /// <summary>
        /// Get storage connection string based on domain.
        /// </summary>
        /// <param name="domainName">Domain name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Connection string.</returns>
        Task<string?> GetStorageConnectionStringAsync(string domainName = "", CancellationToken cancellationToken = default);

        /// <summary>
        /// Get configuration value.
        /// </summary>
        /// <param name="key">Key name.</param>
        /// <returns>Key value.</returns>
        string? GetConfigurationValue(string key);

        /// <summary>
        /// Get connection string by its name.
        /// </summary>
        /// <param name="name">Connection string name.</param>
        /// <returns>Connection string.</returns>
        string? GetConnectionStringByName(string name);

        /// <summary>
        /// Gets the tenant website domain name from the request.
        /// </summary>
        /// <param name="useReferer">Get the domain name from the referer instead of the domain name of website.</param>
        /// <returns>Domain Name.</returns>
        /// <remarks>
        /// <para>Returns the domain name by looking at the incomming request.  Here is the order:</para>
        /// <list type="number">
        /// <item>x-origin-hostname host header.</item>
        /// <item>Referer request header value if requested.</item>
        /// <item>Otherwise returns the host name of the request.</item>
        /// </list>
        /// <para>Note: This should ONLY be used for multi-tenant, single editor website setup.</para>
        /// </remarks>
        string GetTenantDomainNameFromRequest();

        /// <summary>
        /// Get all primary domain names for each tenant.
        /// </summary>
        /// <returns></returns>
        Task<List<string>> GetAllDomainNamesAsync();

        /// <summary>
        /// Get the tenant connection information based on domain name.
        /// </summary>
        /// <param name="domainName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Connection?> GetTenantConnectionAsync(string domainName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Preload all connections asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        Task PreloadAllConnectionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests to see if there is a connection defined for the specified domain name.
        /// </summary>
        /// <param name="domainName">Domain name to validate.</param>
        /// <returns>Domain is valid (true) or not (false).</returns>
        /// <exception cref="ArgumentException">Thrown when ConfigDbConnectionString is not configured.</exception>
        Task<bool> ValidateDomainName(string domainName);

        /// <summary>
        /// Gets the current tenant's unique identifier from the request context.
        /// </summary>
        /// <returns>Tenant ID (Connection.Id), or null if not in a tenant context or if HttpContext is unavailable.</returns>
        /// <remarks>
        /// This method uses the request headers to determine the domain name, then retrieves the corresponding
        /// Connection entity to return its unique ID. The result is cached for performance.
        /// Useful for scoping operations per tenant in multi-tenant scenarios.
        /// </remarks>
        Task<Guid?> GetCurrentTenantIdAsync();
    }
}

