// <copyright file="CacheKeyProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Caching
{
    using System;

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
