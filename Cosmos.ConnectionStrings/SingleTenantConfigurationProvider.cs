// <copyright file="SingleTenantConfigurationProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Cosmos.DynamicConfig
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Single-tenant implementation of IDynamicConfigurationProvider.
    /// Reads configuration from standard .NET configuration sources (environment variables, appsettings.json, etc.)
    /// instead of a multi-tenant database.
    /// </summary>
    public class SingleTenantConfigurationProvider : IDynamicConfigurationProvider
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="SingleTenantConfigurationProvider"/> class.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        public SingleTenantConfigurationProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <inheritdoc/>
        public bool IsMultiTenantConfigured => false;

        /// <inheritdoc/>
        public Task<string?> GetDatabaseConnectionStringAsync(string domainName = "", CancellationToken cancellationToken = default)
        {
            var connectionString = _configuration.GetConnectionString("ApplicationDbContextConnection");
            return Task.FromResult(connectionString);
        }

        /// <inheritdoc/>
        public Task<string?> GetStorageConnectionStringAsync(string domainName = "", CancellationToken cancellationToken = default)
        {
            var storageConnectionString = _configuration.GetConnectionString("StorageConnectionString");
            return Task.FromResult(storageConnectionString);
        }

        /// <inheritdoc/>
        public string? GetConfigurationValue(string key)
        {
            return _configuration[key];
        }

        /// <inheritdoc/>
        public string? GetConnectionStringByName(string name)
        {
            return _configuration.GetConnectionString(name);
        }

        /// <inheritdoc/>
        public string GetTenantDomainNameFromRequest()
        {
            return string.Empty;
        }

        /// <inheritdoc/>
        public Task<List<string>> GetAllDomainNamesAsync()
        {
            return Task.FromResult(new List<string>());
        }

        /// <inheritdoc/>
        public Task<Connection?> GetTenantConnectionAsync(string domainName, CancellationToken cancellationToken = default)
        {
            var connection = new Connection
            {
                Id = Guid.Empty,
                DomainNames = new[] { domainName },
                DbConn = _configuration.GetConnectionString("ApplicationDbContextConnection"),
                StorageConn = _configuration.GetConnectionString("StorageConnectionString")
            };
            return Task.FromResult<Connection?>(connection);
        }

        /// <inheritdoc/>
        public Task PreloadAllConnectionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<bool> ValidateDomainName(string domainName)
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<Guid?> GetCurrentTenantIdAsync()
        {
            return Task.FromResult<Guid?>(Guid.Empty);
        }
    }
}