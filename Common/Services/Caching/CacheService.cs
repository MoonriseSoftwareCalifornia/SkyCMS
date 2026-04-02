// <copyright file="CacheService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Caching
{
    using System;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Implementation of <see cref="ICacheService{T}"/> using <see cref="IMemoryCache"/>.
    /// Provides generic caching operations with support for absolute and sliding expiration.
    /// </summary>
    /// <typeparam name="T">The type of object being cached.</typeparam>
    public class CacheService<T> : ICacheService<T>
    {
        private readonly IMemoryCache memoryCache;
        private readonly ILogger<CacheService<T>> logger;
        private readonly DynamicConfig.IDynamicConfigurationProvider dynamicConfigurationProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheService{T}"/> class.
        /// </summary>
        /// <param name="memoryCache">The memory cache instance.</param>
        /// <param name="logger">Logger instance for diagnostic information.</param>
        /// <param name="dynamicConfigurationProvider">Optional dynamic tenant configuration provider for tenant-isolated keys.</param>
        public CacheService(
            IMemoryCache memoryCache,
            ILogger<CacheService<T>> logger,
            DynamicConfig.IDynamicConfigurationProvider dynamicConfigurationProvider = null)
        {
            this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.dynamicConfigurationProvider = dynamicConfigurationProvider;
        }

        /// <inheritdoc/>
        public T Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            return this.memoryCache.Get<T>(this.GetScopedKey(key));
        }

        /// <inheritdoc/>
        public bool TryGet(string key, out T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            return this.memoryCache.TryGetValue(this.GetScopedKey(key), out value);
        }

        /// <inheritdoc/>
        public void Set(string key, T value, TimeSpan absoluteExpiration)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            try
            {
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(absoluteExpiration);

                this.memoryCache.Set(this.GetScopedKey(key), value, cacheOptions);
            }
            catch (Exception ex)
            {
                var safeKey = key?.Replace("\r", string.Empty).Replace("\n", string.Empty);
                this.logger.LogError(ex, "Error setting cache entry for key {CacheKey} with absolute expiration {Expiration}", safeKey, absoluteExpiration);
                throw;
            }
        }

        /// <inheritdoc/>
        public void Set(string key, T value, TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            try
            {
                var cacheOptions = new MemoryCacheEntryOptions();

                if (absoluteExpiration.HasValue)
                {
                    cacheOptions.SetAbsoluteExpiration(absoluteExpiration.Value);
                }

                if (slidingExpiration.HasValue)
                {
                    cacheOptions.SetSlidingExpiration(slidingExpiration.Value);
                }

                this.memoryCache.Set(this.GetScopedKey(key), value, cacheOptions);
            }
            catch (Exception ex)
            {
                var safeKey = key?.Replace("\r", string.Empty).Replace("\n", string.Empty);
                this.logger.LogError(
                    ex,
                    "Error setting cache entry for key {CacheKey} with absolute expiration {AbsoluteExpiration} and sliding expiration {SlidingExpiration}",
                    safeKey,
                    absoluteExpiration,
                    slidingExpiration);
                throw;
            }
        }

        /// <inheritdoc/>
        public void Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            try
            {
                this.memoryCache.Remove(this.GetScopedKey(key));
            }
            catch (Exception ex)
            {
                var safeKey = key?.Replace("\r", string.Empty).Replace("\n", string.Empty);
                this.logger.LogError(ex, "Error removing cache entry for key {CacheKey}", safeKey);
                throw;
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            try
            {
                // Note: IMemoryCache doesn't have a built-in Clear() method.
                // This is a limitation of the interface. Consider using a custom
                // implementation or dependency injection of a trackable cache if
                // clearing becomes a requirement.
                this.logger.LogWarning("Clear() called on CacheService but IMemoryCache does not support clearing all entries. Consider tracking keys separately if this is needed.");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error clearing cache");
                throw;
            }
        }

        private string GetScopedKey(string key)
        {
            if (dynamicConfigurationProvider == null)
            {
                return key;
            }

            try
            {
                var tenantDomain = dynamicConfigurationProvider.GetTenantDomainNameFromRequest();
                if (string.IsNullOrWhiteSpace(tenantDomain))
                {
                    return key;
                }

                return $"TENANT_CACHE::{tenantDomain.ToLowerInvariant()}::{key}";
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resolve tenant domain for cache key scoping. Falling back to unscoped key.");
                return key;
            }
        }
    }
}
