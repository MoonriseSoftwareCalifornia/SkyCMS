// <copyright file="ILoginAssetBlobClient.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Editor.Services
{
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction for the blob operations used by the login asset bootstrapper.
    /// </summary>
    public interface ILoginAssetBlobClient
    {
        /// <summary>
        /// Ensures the target container exists.
        /// </summary>
        /// <param name="connectionString">Blob storage connection string.</param>
        /// <param name="containerName">Container name.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EnsureContainerExistsAsync(string connectionString, string containerName);

        /// <summary>
        /// Determines whether the specified blob exists.
        /// </summary>
        /// <param name="connectionString">Blob storage connection string.</param>
        /// <param name="containerName">Container name.</param>
        /// <param name="blobName">Blob name.</param>
        /// <returns>A task that returns true when the blob exists.</returns>
        Task<bool> BlobExistsAsync(string connectionString, string containerName, string blobName);

        /// <summary>
        /// Uploads a blob with the specified content type.
        /// </summary>
        /// <param name="connectionString">Blob storage connection string.</param>
        /// <param name="containerName">Container name.</param>
        /// <param name="blobName">Blob name.</param>
        /// <param name="content">Blob content.</param>
        /// <param name="contentType">Content type.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UploadAsync(string connectionString, string containerName, string blobName, Stream content, string contentType);
    }
}
