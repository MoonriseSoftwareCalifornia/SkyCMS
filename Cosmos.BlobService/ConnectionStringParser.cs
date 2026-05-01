// <copyright file="ConnectionStringParser.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService
{
    using System;
    using System.Collections.Generic;
    using Azure.Core;
    using Azure.Storage.Blobs;
    using Cosmos.BlobService.Exceptions;

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
        /// Creates a BlobServiceClient from an Azure connection string.
        /// </summary>
        /// <param name="connectionString">The Azure connection string.</param>
        /// <param name="tokenCredential">Optional token credential for token-based auth.</param>
        /// <returns>A configured BlobServiceClient instance.</returns>
        public static BlobServiceClient CreateBlobServiceClient(
            string connectionString,
            TokenCredential tokenCredential = null)
        {
            var components = ParseAzureConnectionString(connectionString);

            if (components.UsesAccessToken)
            {
                if (tokenCredential == null)
                {
                    throw new InvalidConnectionStringException(
                        "TokenCredential is required when using AccessToken authentication.")
                    {
                        AttemptedProvider = CloudStorageProvider.Azure
                    };
                }

                var blobUri = new Uri($"https://{components.AccountName}.blob.core.windows.net/");
                return new BlobServiceClient(blobUri, tokenCredential);
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
}
