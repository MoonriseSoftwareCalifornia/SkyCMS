// <copyright file="IFileOperationsService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System.IO;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;

    /// <summary>
    /// Service for common file and folder operations across controllers.
    /// Wraps storage context with logging, validation, and normalization.
    /// </summary>
    public interface IFileOperationsService
    {
        /// <summary>
        /// Gets file metadata for the specified path.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>File entry metadata.</returns>
        Task<FileManagerEntry?> GetFileAsync(string path);

        /// <summary>
        /// Gets a readable stream for the file at the specified path.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>File stream.</returns>
        Task<Stream?> GetFileStreamAsync(string path);

        /// <summary>
        /// Deletes the file at the specified path.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>Task representing the async operation.</returns>
        Task DeleteFileAsync(string path);

        /// <summary>
        /// Deletes the folder at the specified path.
        /// </summary>
        /// <param name="path">Folder path.</param>
        /// <returns>Task representing the async operation.</returns>
        Task DeleteFolderAsync(string path);

        /// <summary>
        /// Creates a new folder at the specified path.
        /// </summary>
        /// <param name="path">Folder path.</param>
        /// <returns>File entry for the created folder.</returns>
        Task<FileManagerEntry> CreateFolderAsync(string path);

        /// <summary>
        /// Uploads a file to the specified path with metadata.
        /// </summary>
        /// <param name="path">Destination path.</param>
        /// <param name="content">File content stream.</param>
        /// <param name="metadata">File upload metadata.</param>
        /// <returns>Task representing the async operation.</returns>
        Task UploadFileAsync(string path, Stream content, FileUploadMetaData metadata);

        /// <summary>
        /// Moves a file from source to destination path.
        /// </summary>
        /// <param name="sourcePath">Source file path.</param>
        /// <param name="destinationPath">Destination file path.</param>
        /// <returns>Task representing the async operation.</returns>
        Task MoveFileAsync(string sourcePath, string destinationPath);

        /// <summary>
        /// Moves a folder from source to destination path.
        /// </summary>
        /// <param name="sourcePath">Source folder path.</param>
        /// <param name="destinationPath">Destination folder path.</param>
        /// <returns>Task representing the async operation.</returns>
        Task MoveFolderAsync(string sourcePath, string destinationPath);
    }
}
