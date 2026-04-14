// <copyright file="StorageParallelismHelper.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Publishing
{
    using System;
    using System.Reflection;  // ✅ ADD THIS LINE
    using Cosmos.BlobService;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Determines optimal parallelism for static page generation based on storage backend characteristics.
    /// </summary>
    public static class StorageParallelismHelper
    {
        /// <summary>
        /// Gets the recommended degree of parallelism for the current storage context.
        /// </summary>
        /// <param name="storage">The storage context to analyze.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        /// <param name="configuredOverride">Optional admin-configured override value.</param>
        /// <returns>The optimal degree of parallelism for concurrent uploads.</returns>
        /// <remarks>
        /// <para>
        /// Parallelism recommendations by storage type:
        /// </para>
        /// <list type="bullet">
        ///   <item><description><b>Azure Blob Storage (Production):</b> 8 - High throughput, excellent concurrency support</description></item>
        ///   <item><description><b>AWS S3:</b> 8 - Similar high throughput capabilities</description></item>
        ///   <item><description><b>Cloudflare R2:</b> 8 - S3-compatible with high throughput</description></item>
        ///   <item><description><b>Azure Emulator (Azurite):</b> 2 - Limited by emulator performance</description></item>
        ///   <item><description><b>Local File System:</b> 4 - Disk I/O bound, moderate parallelism</description></item>
        ///   <item><description><b>Unknown/Other:</b> 4 - Conservative default</description></item>
        /// </list>
        /// <para>
        /// Admin-configured overrides take precedence over auto-detection. This allows tuning for
        /// specific deployment scenarios (e.g., throttled storage accounts, high-performance SSDs).
        /// </para>
        /// </remarks>
        public static int GetOptimalParallelism(
            IStorageContext storage,
            ILogger logger,
            int? configuredOverride = null)
        {
            // Admin override takes precedence
            if (configuredOverride.HasValue && configuredOverride.Value > 0)
            {
                logger.LogInformation(
                    "Using configured static page parallelism override: {Parallelism}",
                    configuredOverride.Value);
                return configuredOverride.Value;
            }

            var storageType = DetectStorageType(storage);
            var parallelism = storageType switch
            {
                StorageType.AzureBlobProduction => 8,  // High throughput cloud storage
                StorageType.AzureBlobEmulator => 2,    // Azurite has lower limits
                StorageType.AwsS3 => 8,                // Similar to Azure production
                StorageType.CloudflareR2 => 8,      // ✅ ADD THIS
                StorageType.LocalFileSystem => 4,       // Disk I/O bound
                _ => 4 // Conservative default
            };

            logger.LogInformation(
                "Auto-detected storage type: {StorageType}. Using parallelism: {Parallelism}",
                storageType,
                parallelism);

            return parallelism;
        }

        /// <summary>
        /// Detects the storage provider type from the storage context.
        /// </summary>
        /// <param name="storage">The storage context to analyze.</param>
        /// <returns>The detected storage type.</returns>
        private static StorageType DetectStorageType(IStorageContext storage)
        {
            var typeName = storage.GetType().Name;

            // Try to get storage URL from various possible properties
            var storageUrl = GetStorageUrl(storage);

            // Check for Cloudflare R2
            if (IsCloudflareR2(typeName, storageUrl))
            {
                return StorageType.CloudflareR2;
            }

            // Check for Azure Blob Emulator (Azurite) - must be before production Azure check
            if (storageUrl.Contains("127.0.0.1") ||
                storageUrl.Contains("localhost") ||
                storageUrl.Contains("devstoreaccount1"))
            {
                return StorageType.AzureBlobEmulator;
            }

            // Check for Azure Blob Storage (Production)
            if (typeName.Contains("Azure", StringComparison.OrdinalIgnoreCase) ||
                storageUrl.Contains("blob.core.windows.net"))
            {
                return StorageType.AzureBlobProduction;
            }

            // Check for AWS S3 (after R2 check, since R2 is S3-compatible)
            if (typeName.Contains("S3", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Amazon", StringComparison.OrdinalIgnoreCase) ||
                storageUrl.Contains("amazonaws.com"))
            {
                return StorageType.AwsS3;
            }

            // Check for local file system
            if (typeName.Contains("File", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Local", StringComparison.OrdinalIgnoreCase))
            {
                return StorageType.LocalFileSystem;
            }

            return StorageType.Unknown;
        }

        /// <summary>
        /// Determines if the storage is Cloudflare R2 based on type name and URL patterns.
        /// </summary>
        private static bool IsCloudflareR2(string typeName, string storageUrl)
        {
            // Check type name hints
            if (typeName.Contains("R2", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check R2-specific URL patterns
            if (storageUrl.Contains(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase) ||
                storageUrl.Contains(".r2.dev", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to extract storage URL from IStorageContext using reflection.
        /// </summary>
        private static string GetStorageUrl(IStorageContext storage)
        {
            // Use reflection to find URL properties since IStorageContext doesn't define them
            var type = storage.GetType();
            var urlProperties = new[]
            {
                "AzureBlobStorageUrl",
                "StorageEndpointUrl",
                "ServiceUrl",
                "Endpoint",
                "BaseUrl",
                "S3ServiceUrl",
                "BucketUrl"
            };

            foreach (var propName in urlProperties)
            {
                var prop = type.GetProperty(
                    propName,
                    BindingFlags.Public | BindingFlags.Instance);

                if (prop?.PropertyType == typeof(string))
                {
                    var value = prop.GetValue(storage) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.ToLowerInvariant();
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Storage provider types with different performance characteristics.
        /// </summary>
        private enum StorageType
        {
            /// <summary>Azure Blob Storage (production cloud).</summary>
            AzureBlobProduction,

            /// <summary>Azure Blob Storage Emulator (Azurite).</summary>
            AzureBlobEmulator,

            /// <summary>AWS S3.</summary>
            AwsS3,

            /// <summary>Local file system.</summary>
            LocalFileSystem,

            /// <summary>Cloudflare R2.</summary>
            CloudflareR2,

            /// <summary>Unknown or unsupported storage type.</summary>
            Unknown
        }
    }
}