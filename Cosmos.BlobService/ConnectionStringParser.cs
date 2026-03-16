// <copyright file="ConnectionStringParser.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService
{
    using Azure.Identity;
    using Azure.Storage.Blobs;
    using Cosmos.BlobService.Exceptions;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides utility methods for parsing cloud storage connection strings.
    /// </summary>
    public static class ConnectionStringParser
    {
        /// <summary>
        /// Determines the storage provider type from a connection string.
        /// </summary>
        /// <param name="connectionString">The connection string to analyze.</param>
        /// <returns>The storage provider type.</returns>
        public static CloudStorageProvider DetermineProvider(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return CloudStorageProvider.Unknown;
            }

            if (connectionString.StartsWith("DefaultEndpointsProtocol=", StringComparison.OrdinalIgnoreCase))
            {
                return CloudStorageProvider.Azure;
            }

            if (connectionString.Contains("accountid", StringComparison.OrdinalIgnoreCase) &&
                connectionString.Contains("bucket", StringComparison.OrdinalIgnoreCase))
            {
                return CloudStorageProvider.CloudflareR2;
            }

            if (connectionString.Contains("bucket", StringComparison.OrdinalIgnoreCase) &&
                connectionString.Contains("region", StringComparison.OrdinalIgnoreCase))
            {
                return CloudStorageProvider.AmazonS3;
            }

            return CloudStorageProvider.Unknown;
        }

        /// <summary>
        /// Parses an Azure Blob Storage connection string into its components.
        /// </summary>
        /// <param name="connectionString">The Azure connection string.</param>
        /// <returns>Parsed Azure connection string components.</returns>
        /// <exception cref="InvalidConnectionStringException">Thrown when the connection string format is invalid.</exception>
        public static AzureConnectionStringComponents ParseAzureConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidConnectionStringException("Connection string cannot be null or empty.")
                {
                    AttemptedProvider = CloudStorageProvider.Azure
                };
            }

            var dict = ParseConnectionString(connectionString);

            if (!dict.TryGetValue("AccountName", out var accountName) || string.IsNullOrWhiteSpace(accountName))
            {
                throw new InvalidConnectionStringException("Invalid Azure connection string: missing AccountName.")
                {
                    AttemptedProvider = CloudStorageProvider.Azure
                };
            }

            var usesAccessToken = dict.TryGetValue("AccountKey", out var accountKey) &&
                                  accountKey.Equals("AccessToken", StringComparison.OrdinalIgnoreCase);

            return new AzureConnectionStringComponents
            {
                AccountName = accountName,
                UsesAccessToken = usesAccessToken,
                FullConnectionString = connectionString
            };
        }

        /// <summary>
        /// Parses an Amazon S3 connection string into its components.
        /// </summary>
        /// <param name="connectionString">The Amazon S3 connection string.</param>
        /// <returns>Parsed Amazon S3 connection string components.</returns>
        /// <exception cref="InvalidConnectionStringException">Thrown when the connection string format is invalid.</exception>
        public static AmazonConnectionStringComponents ParseAmazonConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidConnectionStringException("Connection string cannot be null or empty.")
                {
                    AttemptedProvider = CloudStorageProvider.AmazonS3
                };
            }

            var dict = ParseConnectionString(connectionString);

            if (!dict.TryGetValue("Bucket", out var bucket))
            {
                throw new InvalidConnectionStringException("Invalid Amazon connection string: missing Bucket parameter.")
                {
                    AttemptedProvider = CloudStorageProvider.AmazonS3
                };
            }

            if (!dict.TryGetValue("KeyId", out var keyId))
            {
                throw new InvalidConnectionStringException("Invalid Amazon connection string: missing KeyId parameter.")
                {
                    AttemptedProvider = CloudStorageProvider.AmazonS3
                };
            }

            if (!dict.TryGetValue("Key", out var key))
            {
                throw new InvalidConnectionStringException("Invalid Amazon connection string: missing Key parameter.")
                {
                    AttemptedProvider = CloudStorageProvider.AmazonS3
                };
            }

            // Region is optional for Cloudflare R2
            dict.TryGetValue("Region", out var region);
            dict.TryGetValue("AccountId", out var accountId);

            return new AmazonConnectionStringComponents
            {
                BucketName = bucket,
                Region = region,
                KeyId = keyId,
                Key = key,
                AccountId = accountId
            };
        }

        /// <summary>
        /// Creates a BlobServiceClient from an Azure connection string.
        /// </summary>
        /// <param name="connectionString">The Azure connection string.</param>
        /// <param name="defaultAzureCredential">Optional Azure credential for token-based auth.</param>
        /// <returns>A configured BlobServiceClient instance.</returns>
        public static BlobServiceClient CreateBlobServiceClient(
            string connectionString,
            DefaultAzureCredential defaultAzureCredential = null)
        {
            var components = ParseAzureConnectionString(connectionString);

            if (components.UsesAccessToken)
            {
                if (defaultAzureCredential == null)
                {
                    throw new InvalidConnectionStringException(
                        "DefaultAzureCredential is required when using AccessToken authentication.")
                    {
                        AttemptedProvider = CloudStorageProvider.Azure
                    };
                }

                var blobUri = new Uri($"https://{components.AccountName}.blob.core.windows.net/");
                return new BlobServiceClient(blobUri, defaultAzureCredential);
            }

            return new BlobServiceClient(connectionString);
        }

        /// <summary>
        /// Checks if the Azure connection string is for Azurite (local emulator).
        /// </summary>
        /// <param name="connectionString">The Azure connection string.</param>
        /// <returns>True if the connection string is for Azurite, false otherwise.</returns>
        public static bool IsAzurite(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            return connectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("devstoreaccount1", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> ParseConnectionString(string connectionString)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawPart in parts)
            {
                var part = rawPart.Trim();
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                var separatorIndex = part.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex == part.Length - 1)
                {
                    continue;
                }

                var key = part[..separatorIndex].Trim();
                var value = part.Substring(separatorIndex + 1).Trim();

                if (!string.IsNullOrWhiteSpace(key))
                {
                    dict[key] = value;
                }
            }

            return dict;
        }
    }

    /// <summary>
    /// Represents the type of cloud storage provider.
    /// </summary>
    public enum CloudStorageProvider
    {
        /// <summary>
        /// Unknown or unsupported provider.
        /// </summary>
        Unknown,

        /// <summary>
        /// Microsoft Azure Blob Storage.
        /// </summary>
        Azure,

        /// <summary>
        /// Amazon S3.
        /// </summary>
        AmazonS3,

        /// <summary>
        /// Cloudflare R2 (S3-compatible).
        /// </summary>
        CloudflareR2
    }

    /// <summary>
    /// Contains parsed components of an Azure Blob Storage connection string.
    /// </summary>
    public class AzureConnectionStringComponents
    {
        /// <summary>
        /// Gets or sets the storage account name.
        /// </summary>
        public required string AccountName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the connection uses Azure AD access tokens.
        /// </summary>
        public bool UsesAccessToken { get; set; }

        /// <summary>
        /// Gets or sets the full connection string.
        /// </summary>
        public required string FullConnectionString { get; set; }
    }

    /// <summary>
    /// Contains parsed components of an Amazon S3/Cloudflare R2 connection string.
    /// </summary>
    public class AmazonConnectionStringComponents
    {
        /// <summary>
        /// Gets or sets the bucket name.
        /// </summary>
        public required string BucketName { get; set; }

        /// <summary>
        /// Gets or sets the AWS region (optional for Cloudflare R2).
        /// </summary>
        public string? Region { get; set; }

        /// <summary>
        /// Gets or sets the access key ID.
        /// </summary>
        public required string KeyId { get; set; }

        /// <summary>
        /// Gets or sets the secret access key.
        /// </summary>
        public required string Key { get; set; }

        /// <summary>
        /// Gets or sets the Cloudflare account ID (for R2 only).
        /// </summary>
        public string? AccountId { get; set; }
    }
}
