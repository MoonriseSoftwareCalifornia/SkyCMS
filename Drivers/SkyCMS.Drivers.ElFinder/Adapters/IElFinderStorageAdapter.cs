// <copyright file="IElFinderStorageAdapter.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder.Adapters
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.BlobService;

    /// <summary>
    /// Adapter interface that bridges elFinder protocol layer to storage operations.
    /// </summary>
    public interface IElFinderStorageAdapter
    {
        /// <summary>
        /// Encodes a normalized path to an elFinder hash.
        /// </summary>
        /// <param name="path">The normalized path.</param>
        /// <returns>The elFinder hash.</returns>
        string EncodePath(string path);

        /// <summary>
        /// Decodes an elFinder hash to a normalized path.
        /// </summary>
        /// <param name="hash">The elFinder hash.</param>
        /// <returns>The normalized path, or null if hash is invalid.</returns>
        string? DecodePath(string hash);

        /// <summary>
        /// Gets the directory listing for a path.
        /// </summary>
        /// <param name="path">The normalized path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of file/folder entries.</returns>
        Task<List<FileManagerEntry>> GetEntriesAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a single file entry by path.
        /// </summary>
        /// <param name="path">The normalized path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file entry, or null if not found.</returns>
        Task<FileManagerEntry?> GetEntryAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a folder at the specified path.
        /// </summary>
        /// <param name="path">The normalized target folder path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created folder entry.</returns>
        Task<FileManagerEntry?> CreateFolderAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an empty file at the specified path.
        /// </summary>
        /// <param name="path">The normalized target file path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created file entry.</returns>
        Task<FileManagerEntry?> CreateFileAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Renames or moves a file or folder.
        /// </summary>
        /// <param name="sourcePath">The source normalized path.</param>
        /// <param name="destinationPath">The destination normalized path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated entry.</returns>
        Task<FileManagerEntry?> RenameAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file or folder.
        /// </summary>
        /// <param name="path">The normalized path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the read stream for a file.
        /// </summary>
        /// <param name="path">The normalized path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A readable stream, or null if not found.</returns>
        Task<Stream?> GetReadStreamAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads file data to the specified path.
        /// </summary>
        /// <param name="path">The normalized target file path.</param>
        /// <param name="content">The file content stream.</param>
        /// <param name="contentType">The MIME type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created/updated file entry.</returns>
        Task<FileManagerEntry?> UploadFileAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Copies a file or folder from source to destination.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="destinationPath">The destination path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The copied entry.</returns>
        Task<FileManagerEntry?> CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves a file or folder from source to destination.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="destinationPath">The destination path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The moved entry.</returns>
        Task<FileManagerEntry?> MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all ancestor entries from root to target parent.
        /// </summary>
        /// <param name="path">The normalized path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of ancestor entries.</returns>
        Task<List<FileManagerEntry>> GetAncestorsAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a path is accessible by the current user/tenant.
        /// </summary>
        /// <param name="path">The normalized path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if accessible, false otherwise.</returns>
        Task<bool> IsAccessibleAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the aggregate size of a path (file size or folder size recursively).
        /// </summary>
        /// <param name="path">The normalized path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Size in bytes.</returns>
        Task<long> GetSizeAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for files and directories whose name contains the query string.
        /// </summary>
        /// <param name="query">Substring to match against item names (case-insensitive).</param>
        /// <param name="rootPath">Root path to search within (recursive).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of matching entries with their full paths.</returns>
        Task<List<(FileManagerEntry Entry, string FullPath)>> SearchAsync(string query, string rootPath, CancellationToken cancellationToken = default);
    }
}
