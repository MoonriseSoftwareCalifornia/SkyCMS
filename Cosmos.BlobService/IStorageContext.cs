// <copyright file="IStorageContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Cosmos.BlobService.Models;

    /// <summary>
    /// Defines an abstraction over storage operations for files and folders.
    /// </summary>
    /// <remarks>
    /// All paths are subject to automatic normalization and validation:
    /// - Paths are normalized to a canonical form (forward slashes, no leading/trailing separators, collapsed consecutive separators).
    /// - Paths are validated to prevent traversal attacks (e.g., `../../../etc/passwd`), reserved names (e.g., `CON`, `PRN`), and invalid structures.
    /// - Invalid paths result in a <see cref="StorageException"/> being thrown.
    /// This defense-in-depth approach ensures:
    /// - Consistent behavior across storage providers (Azure, S3, local, etc.)
    /// - Protection against common file system attacks
    /// - Predictable error handling for invalid inputs
    /// </remarks>
    public interface IStorageContext
    {
        /// <summary>
        /// Appends the specified <see cref="MemoryStream"/> to a blob using the provided file metadata.
        /// </summary>
        /// <param name="stream">The in-memory stream containing the data to append.</param>
        /// <param name="fileMetaData">The metadata associated with the file. Paths in metadata are normalized by the implementation.</param>
        /// <param name="mode">
        /// The append mode to use for the blob operation. Defaults to <c>"append"</c>.
        /// Provider-specific implementations may support additional modes.
        /// </param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous append operation.</returns>
        Task AppendBlob(MemoryStream stream, FileUploadMetaData fileMetaData, string mode = StorageConstants.UploadModeAppend);

        /// <summary>
        /// Determines whether a blob exists at the specified path.
        /// </summary>
        /// <param name="path">The path of the blob to check. Paths with leading slashes or backslashes are normalized to a canonical form.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> whose result is <see langword="true"/> if the blob exists;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        Task<bool> BlobExistsAsync(string path);

        /// <summary>
        /// Copies a blob or folder from the specified target path to the specified destination path.
        /// </summary>
        /// <param name="target">The source path to copy from. Paths are normalized to a canonical form.</param>
        /// <param name="destination">The destination path to copy to. Paths are normalized to a canonical form.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous copy operation.</returns>
        Task CopyAsync(string target, string destination);

        /// <summary>
        /// Creates a folder at the specified path.
        /// </summary>
        /// <param name="path">The path at which to create the folder. Paths are normalized to a canonical form.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> whose result is a <see cref="FileManagerEntry"/>
        /// representing the created folder.
        /// </returns>
        Task<FileManagerEntry> CreateFolder(string path);

        /// <summary>
        /// Deletes the file at the specified path.
        /// </summary>
        /// <param name="path">The path of the file to delete. Paths are normalized to a canonical form.</param>
        [Obsolete("Use DeleteFileAsync instead to avoid blocking. This method will be removed in a future version.")]
        void DeleteFile(string path);

        /// <summary>
        /// Deletes the file at the specified path asynchronously.
        /// </summary>
        /// <param name="path">The path of the file to delete. Paths are normalized to a canonical form.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous delete operation.</returns>
        Task DeleteFileAsync(string path);

        /// <summary>
        /// Deletes the folder at the specified path, including any contained files and subfolders.
        /// </summary>
        /// <param name="path">The path of the folder to delete. Paths are normalized to a canonical form.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous delete operation.</returns>
        Task DeleteFolderAsync(string path);

        /// <summary>
        /// Disables the Azure static website feature for the underlying storage account, if supported.
        /// </summary>
        /// <returns>A <see cref="Task"/> that represents the asynchronous disable operation.</returns>
        Task DisableAzureStaticWebsite();

        /// <summary>
        /// Enables the Azure static website feature for the underlying storage account, if supported.
        /// </summary>
        /// <returns>A <see cref="Task"/> that represents the asynchronous enable operation.</returns>
        Task EnableAzureStaticWebsite();

        /// <summary>
        /// Gets metadata for the file at the specified path.
        /// </summary>
        /// <param name="path">The path of the file to retrieve. Paths are normalized to a canonical form.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> whose result is a <see cref="FileManagerEntry"/>
        /// representing the requested file.
        /// </returns>
        Task<FileManagerEntry> GetFileAsync(string path);

        /// <summary>
        /// Gets a list of files and directories at the specified path.
        /// </summary>
        /// <param name="path">The path from which to retrieve files and directories. Paths are normalized to a canonical form.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> whose result is a list of <see cref="FileManagerEntry"/>
        /// items representing the files and directories.
        /// </returns>
        Task<List<FileManagerEntry>> GetFilesAndDirectories(string path);

        /// <summary>
        /// Gets a list of file paths located under the specified path.
        /// </summary>
        /// <param name="path">The path from which to retrieve file paths. Paths are normalized to a canonical form.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> whose result is a list of file paths.
        /// </returns>
        Task<List<string>> GetFilesAsync(string path);

        /// <summary>
        /// Gets a readable <see cref="Stream"/> for the file at the specified path.
        /// </summary>
        /// <param name="path">The path of the file to open. Paths are normalized to a canonical form.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> whose result is a <see cref="Stream"/>
        /// for reading the contents of the file.
        /// </returns>
        Task<Stream> GetStreamAsync(string path);

        /// <summary>
        /// Moves a file from the specified source path to the specified destination path.
        /// </summary>
        /// <param name="sourceFile">The source file path. Paths are normalized to a canonical form.</param>
        /// <param name="destinationFile">The destination file path. Paths are normalized to a canonical form.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous move operation.</returns>
        Task MoveFileAsync(string sourceFile, string destinationFile);

        /// <summary>
        /// Moves a folder from the specified source path to the specified destination path,
        /// including any contained files and subfolders.
        /// </summary>
        /// <param name="sourceFolder">The source folder path. Paths are normalized to a canonical form.</param>
        /// <param name="destinationFolder">The destination folder path. Paths are normalized to a canonical form.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous move operation.</returns>
        Task MoveFolderAsync(string sourceFolder, string destinationFolder);

        /// <summary>
        /// Invalidates the cached storage driver for the given connection string.
        /// Call this method after uploading files to ensure ElFinder and other file
        /// managers see the newly uploaded files on their next refresh.
        /// </summary>
        /// <param name="connectionString">The storage connection string whose driver cache should be invalidated.</param>
        void InvalidateStorageDriverCache(string connectionString);
    }
}