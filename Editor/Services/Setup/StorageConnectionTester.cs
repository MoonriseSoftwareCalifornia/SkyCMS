// <copyright file="StorageConnectionTester.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using Cosmos.BlobService;
    using Microsoft.Extensions.Caching.Memory;
    using System.Threading.Tasks;

    /// <summary>
    /// Default runtime implementation for setup storage connectivity validation.
    /// </summary>
    public class StorageConnectionTester : IStorageConnectionTester
    {
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageConnectionTester"/> class.
        /// </summary>
        /// <param name="memoryCache">Memory cache used by storage context.</param>
        public StorageConnectionTester(IMemoryCache memoryCache)
        {
            this.memoryCache = memoryCache;
        }

        /// <inheritdoc/>
        public async Task<TestResult> TestConnectionAsync(string connectionString)
        {
            var storageContext = new StorageContext(connectionString, memoryCache);

            // Enable static website to validate provider-level configuration.
            await storageContext.EnableAzureStaticWebsite();

            var result = await storageContext.GetFilesAndDirectories("/");
            if (result == null)
            {
                return new TestResult
                {
                    Success = false,
                    Message = "Unable to connect to storage"
                };
            }

            return new TestResult
            {
                Success = true,
                Message = $"Storage connection successful. Found {result.Count} items in root."
            };
        }
    }
}
