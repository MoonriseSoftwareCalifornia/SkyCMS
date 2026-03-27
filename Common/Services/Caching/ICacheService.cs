// <copyright file="ICacheService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Caching
{
    /// <summary>
    /// Provides a generic interface for cache operations.
    /// Abstracts caching logic to support different cache backends (memory, distributed, etc.).
    /// </summary>
    /// <typeparam name="T">The type of object being cached.</typeparam>
    public interface ICacheService<T>
    {
        /// <summary>
        /// Gets a value from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <returns>The cached value, or null if not found.</returns>
        T Get(string key);

        /// <summary>
        /// Tries to get a value from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The cached value if found; otherwise null.</param>
        /// <returns>True if the value was found in cache; otherwise false.</returns>
        bool TryGet(string key, out T value);

        /// <summary>
        /// Sets a value in the cache with an absolute expiration.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="absoluteExpiration">Absolute expiration duration.</param>
        void Set(string key, T value, global::System.TimeSpan absoluteExpiration);

        /// <summary>
        /// Sets a value in the cache with an absolute expiration.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="absoluteExpiration">Absolute expiration duration.</param>
        /// <param name="slidingExpiration">Sliding expiration duration (resets on access).</param>
        void Set(string key, T value, global::System.TimeSpan? absoluteExpiration, global::System.TimeSpan? slidingExpiration);

        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        void Remove(string key);

        /// <summary>
        /// Clears all values from the cache.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Provides cache key generation strategies.
    /// </summary>
    public interface ICacheKeyProvider
    {
        /// <summary>
        /// Generates a cache key for a file path.
        /// </summary>
        /// <param name="host">The request host.</param>
        /// <param name="path">The file path.</param>
        /// <returns>A cache key string.</returns>
        string GenerateFileKey(string host, string path);

        /// <summary>
        /// Generates a cache key for a SPA check.
        /// </summary>
        /// <param name="articleUrl">The article URL.</param>
        /// <returns>A cache key string.</returns>
        string GenerateSpaCheckKey(string articleUrl);
    }
}
