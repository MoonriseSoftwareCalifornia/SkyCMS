// <copyright file="LoginAssetBlobClient.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and contributors participating to project.
// </copyright>

namespace Cosmos.Editor.Services
{
    using System.IO;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;

    /// <summary>
    /// Blob storage client used by the login asset bootstrapper.
    /// </summary>
    public sealed class LoginAssetBlobClient : ILoginAssetBlobClient
    {
        /// <inheritdoc/>
        public async Task EnsureContainerExistsAsync(string connectionString, string containerName)
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var container = blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> BlobExistsAsync(string connectionString, string containerName, string blobName)
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var container = blobServiceClient.GetBlobContainerClient(containerName);
            return await container.GetBlobClient(blobName).ExistsAsync();
        }

        /// <inheritdoc/>
        public async Task UploadAsync(string connectionString, string containerName, string blobName, Stream content, string contentType)
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var container = blobServiceClient.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(blobName);
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                },
            });
        }
    }
}
