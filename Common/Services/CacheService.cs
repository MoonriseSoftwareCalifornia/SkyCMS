// <copyright file="CacheService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cosmos.Common.Services
{
    /// <summary>
    /// Implementation of <see cref="ICacheService{T}"/> using <see cref="IMemoryCache"/>.
    /// Provides generic caching operations with support for absolute and sliding expiration.
    /// </summary>
    /// <typeparam name="T">The type of object being cached.</typeparam>
    public class CacheService<T> : ICacheService<T>
    {
        private readonly IMemoryCache memoryCache;
        private readonly ILogger<CacheService<T>> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheService{T}"/> class.
        /// </summary>
        /// <param name="memoryCache">The memory cache instance.</param>
        /// <param name="logger">Logger instance for diagnostic information.</param>
        public CacheService(IMemoryCache memoryCache, ILogger<CacheService<T>> logger)
        {
            this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public T Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            return this.memoryCache.Get<T>(key);
        }

        /// <inheritdoc/>
        public bool TryGet(string key, out T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            return this.memoryCache.TryGetValue(key, out value);
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

                this.memoryCache.Set(key, value, cacheOptions);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error setting cache entry for key {CacheKey} with absolute expiration {Expiration}", key, absoluteExpiration);
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

                this.memoryCache.Set(key, value, cacheOptions);
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Error setting cache entry for key {CacheKey} with absolute expiration {AbsoluteExpiration} and sliding expiration {SlidingExpiration}",
                    key,
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
                this.memoryCache.Remove(key);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error removing cache entry for key {CacheKey}", key);
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
    }

    /// <summary>
    /// Implementation of <see cref="ICacheKeyProvider"/> for generating cache keys.
    /// Provides consistent cache key formatting across the application.
    /// </summary>
    public class CacheKeyProvider : ICacheKeyProvider
    {
        /// <inheritdoc/>
        public string GenerateFileKey(string host, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            return $"{host}-{path}";
        }

        /// <inheritdoc/>
        public string GenerateSpaCheckKey(string articleUrl)
        {
            if (string.IsNullOrWhiteSpace(articleUrl))
            {
                throw new ArgumentNullException(nameof(articleUrl));
            }

            return $"SPA_CHECK_{articleUrl}";
        }
    }
}
