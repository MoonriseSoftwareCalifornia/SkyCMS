// <copyright file="FileStorageContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService
{
    using Cosmos.BlobService.Drivers;
    using Cosmos.BlobService.Models;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Azure File Share storage context for file-based operations.
    /// </summary>
    /// <remarks>
    /// This class provides file share-specific operations using Azure File Storage.
    /// For blob storage operations (Azure Blob, Amazon S3, etc.), use <see cref="StorageContext"/> instead.
    /// </remarks>
    public sealed class FileStorageContext : IStorageContext
    {
        /// <summary>
        /// Azure file share driver, this is not handled in the collection.
        /// </summary>
        private readonly AzureFileStorage driver;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStorageContext"/> class.
        /// </summary>
        /// <param name="connectionString">File storage connection string.</param>
        /// <param name="sharename">File storage share name.</param>
        public FileStorageContext(string connectionString, string sharename)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(sharename))
            {
                throw new ArgumentNullException(nameof(sharename), "Share name cannot be null or empty.");
            }

            this.driver = new AzureFileStorage(connectionString, sharename);
        }

        /// <inheritdoc/>
        public async Task<bool> BlobExistsAsync(string path)
        {
            path = PathUtilities.NormalizePath(path);
            return await this.driver.BlobExistsAsync(path);
        }

        /// <inheritdoc/>
        public async Task CopyAsync(string target, string destination)
        {
            target = PathUtilities.NormalizePath(target);
            destination = PathUtilities.NormalizePath(destination);
            await this.driver.CopyBlobAsync(target, destination);
        }

        /// <inheritdoc/>
        public async Task<FileManagerEntry> CreateFolder(string path)
        {
            path = PathUtilities.NormalizePath(path);
            await this.driver.CreateFolderAsync(path);
            var folder = await this.driver.GetBlobAsync(path);
            return folder;
        }

        /// <inheritdoc/>
        [Obsolete("Use DeleteFileAsync instead to avoid blocking. This method will be removed in a future version.")]
        public void DeleteFile(string path)
        {
            DeleteFileAsync(path).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public async Task DeleteFileAsync(string path)
        {
            path = PathUtilities.NormalizePath(path);
            await this.driver.DeleteIfExistsAsync(path);
        }

        /// <inheritdoc/>
        public async Task DeleteFolderAsync(string folder)
        {
            folder = PathUtilities.NormalizePath(folder);
            await this.driver.DeleteFolderAsync(folder);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This operation is not supported for Azure File Shares.
        /// </remarks>
        /// <exception cref="NotSupportedException">Azure File Shares do not support static website features.</exception>
        public Task DisableAzureStaticWebsite()
        {
            throw new NotSupportedException("Azure File Shares do not support static website features.");
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This operation is not supported for Azure File Shares.
        /// </remarks>
        /// <exception cref="NotSupportedException">Azure File Shares do not support static website features.</exception>
        public Task EnableAzureStaticWebsite()
        {
            throw new NotSupportedException("Azure File Shares do not support static website features.");
        }

        /// <inheritdoc/>
        public async Task<FileManagerEntry> GetFileAsync(string path)
        {
            path = PathUtilities.NormalizePath(path);
            var fileManagerEntry = await this.driver.GetBlobAsync(path);
            return fileManagerEntry;
        }

        /// <inheritdoc/>
        public async Task<List<FileManagerEntry>> GetFilesAndDirectories(string path)
        {
            path = PathUtilities.NormalizePath(path);
            var entries = await this.driver.GetFilesAndDirectories(path);
            return entries;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This method is not implemented for Azure File Shares.
        /// Use <see cref="GetFilesAndDirectories"/> instead.
        /// </remarks>
        /// <exception cref="NotImplementedException">Use GetFilesAndDirectories for Azure File Shares.</exception>
        public Task<List<string>> GetFilesAsync(string path)
        {
            throw new NotImplementedException("Use GetFilesAndDirectories method for Azure File Shares.");
        }

        /// <inheritdoc/>
        public async Task<Stream> GetStreamAsync(string path)
        {
            path = PathUtilities.NormalizePath(path);
            return await this.driver.GetStreamAsync(path);
        }

        /// <inheritdoc/>
        public async Task MoveFileAsync(string sourceFile, string destinationFile)
        {
            sourceFile = PathUtilities.NormalizePath(sourceFile);
            destinationFile = PathUtilities.NormalizePath(destinationFile);
            await this.driver.MoveAsync(sourceFile, destinationFile);
        }

        /// <inheritdoc/>
        public async Task MoveFolderAsync(string sourceFolder, string destinationFolder)
        {
            sourceFolder = PathUtilities.NormalizePath(sourceFolder);
            destinationFolder = PathUtilities.NormalizePath(destinationFolder);
            await this.driver.MoveAsync(sourceFolder, destinationFolder);
        }

        /// <inheritdoc/>
        public async Task AppendBlob(MemoryStream stream, FileUploadMetaData fileMetaData, string mode = StorageConstants.UploadModeAppend)
        {
            await this.driver.AppendBlobAsync(stream.ToArray(), fileMetaData, DateTimeOffset.UtcNow, mode);
        }

        #region Legacy Methods (Obsolete - For Backward Compatibility)

        /// <summary>
        /// Gets the metadata for a file or folder object.
        /// </summary>
        /// <param name="path">Path to the object.</param>
        /// <returns>Returns object metadata as a <see cref="FileManagerEntry"/>.</returns>
        [Obsolete("Use GetFileAsync instead. This method will be removed in a future version.")]
        public async Task<FileManagerEntry> GetObjectAsync(string path)
        {
            return await GetFileAsync(path);
        }

        /// <summary>
        /// Gets files and subfolders for a given path.
        /// </summary>
        /// <param name="path">Path to get files and folders.</param>
        /// <returns>Returns the metadata of what is found as a <see cref="FileManagerEntry"/> list.</returns>
        [Obsolete("Use GetFilesAndDirectories instead. This method will be removed in a future version.")]
        public async Task<List<FileManagerEntry>> GetObjectsAsync(string path)
        {
            return await GetFilesAndDirectories(path);
        }

        /// <summary>
        /// Returns a response stream from the file share.
        /// </summary>
        /// <param name="target">Path to the file to open.</param>
        /// <returns>A <see cref="Stream"/> that reads bytes from a file.</returns>
        [Obsolete("Use GetStreamAsync instead. This method will be removed in a future version.")]
        public async Task<Stream> OpenBlobReadStreamAsync(string target)
        {
            return await GetStreamAsync(target);
        }

        /// <summary>
        /// Moves a file or folder to a specified destination.
        /// </summary>
        /// <param name="sourcePath">Path to source file or folder.</param>
        /// <param name="destFolderPath">Path to destination folder.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Obsolete("Use MoveFileAsync or MoveFolderAsync instead for clarity. This method will be removed in a future version.")]
        public async Task MoveAsync(string sourcePath, string destFolderPath)
        {
            await MoveFileAsync(sourcePath, destFolderPath);
        }

        /// <summary>
        /// Gets the contents for a folder.
        /// </summary>
        /// <param name="path">Path to folder to retrieve contents.</param>
        /// <returns>Returns the metadata of what is found as a <see cref="FileManagerEntry"/> list.</returns>
        [Obsolete("Use GetFilesAndDirectories instead. This method will be removed in a future version.")]
        public async Task<List<FileManagerEntry>> GetFolderContents(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                path = PathUtilities.NormalizePath(path);

                if (path == "/")
                {
                    path = string.Empty;
                }
                else
                {
                    if (!path.EndsWith("/"))
                    {
                        path = path + "/";
                    }
                }
            }

            return await GetFilesAndDirectories(path);
        }

        #endregion
    }
}
