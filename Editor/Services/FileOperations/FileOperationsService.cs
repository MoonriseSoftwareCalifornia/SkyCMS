// <copyright file="FileOperationsService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Default implementation of <see cref="IFileOperationsService"/>.
    /// Provides a consistent interface for file and folder operations with logging.
    /// </summary>
    public class FileOperationsService : IFileOperationsService
    {
        private readonly IStorageContext storageContext;
        private readonly ILogger<FileOperationsService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileOperationsService"/> class.
        /// </summary>
        /// <param name="storageContext">Storage context for blob operations.</param>
        /// <param name="logger">Logger instance.</param>
        public FileOperationsService(
            IStorageContext storageContext,
            ILogger<FileOperationsService> logger)
        {
            this.storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<FileManagerEntry?> GetFileAsync(string path)
        {
            try
            {
                var entry = await this.storageContext.GetFileAsync(path);
                return entry;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get file metadata for path: {Path}", path);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Stream?> GetFileStreamAsync(string path)
        {
            try
            {
                var stream = await this.storageContext.GetStreamAsync(path);
                return stream;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get file stream for path: {Path}", path);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task DeleteFileAsync(string path)
        {
            try
            {
                await this.storageContext.DeleteFileAsync(path);
                this.logger.LogInformation("Deleted file: {Path}", path);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to delete file: {Path}", path);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task DeleteFolderAsync(string path)
        {
            try
            {
                await this.storageContext.DeleteFolderAsync(path);
                this.logger.LogInformation("Deleted folder: {Path}", path);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to delete folder: {Path}", path);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<FileManagerEntry> CreateFolderAsync(string path)
        {
            try
            {
                var entry = await this.storageContext.CreateFolder(path);
                this.logger.LogInformation("Created folder: {Path}", path);
                return entry;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to create folder: {Path}", path);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task UploadFileAsync(string path, Stream content, FileUploadMetaData metadata)
        {
            try
            {
                // Convert Stream to MemoryStream if needed
                MemoryStream memoryStream;
                if (content is MemoryStream ms)
                {
                    memoryStream = ms;
                }
                else
                {
                    memoryStream = new MemoryStream();
                    await content.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                }

                await this.storageContext.AppendBlob(memoryStream, metadata, StorageConstants.UploadModeBlock);
                this.logger.LogInformation("Uploaded file to: {Path}", path);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to upload file to: {Path}", path);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task MoveFileAsync(string sourcePath, string destinationPath)
        {
            try
            {
                await this.storageContext.MoveFileAsync(sourcePath, destinationPath);
                this.logger.LogInformation("Moved file from {Source} to {Destination}", sourcePath, destinationPath);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to move file from {Source} to {Destination}", sourcePath, destinationPath);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task MoveFolderAsync(string sourcePath, string destinationPath)
        {
            try
            {
                await this.storageContext.MoveFolderAsync(sourcePath, destinationPath);
                this.logger.LogInformation("Moved folder from {Source} to {Destination}", sourcePath, destinationPath);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to move folder from {Source} to {Destination}", sourcePath, destinationPath);
                throw;
            }
        }
    }
}
