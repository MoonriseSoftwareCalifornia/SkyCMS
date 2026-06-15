using Cosmos.BlobService;
using Cosmos.BlobService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace SkyCMS.Drivers.ElFinder.Adapters;

/// <summary>
/// Provides a storage adapter that connects elFinder file operations to the configured storage system.
/// </summary>
/// <remarks>
/// This class is responsible for turning high-level elFinder actions (such as list, read, create, rename,
/// move, delete, copy, and search) into calls to <see cref="IStorageContext"/>.
/// It also normalizes and validates paths before talking to storage so behavior is consistent and safe.
/// Most methods are intentionally tolerant: if storage throws an exception, methods usually return
/// a safe fallback value instead of rethrowing.
/// </remarks>
public class ElFinderStorageAdapter : IElFinderStorageAdapter
{
    private const string VolumeId = "l1_";
    private readonly IStorageContext _storageContext;
    private readonly IPathNormalizer _pathNormalizer;
    private readonly IPathValidator _pathValidator;

    /// <summary>
    /// Creates a new instance of <see cref="ElFinderStorageAdapter"/>.
    /// </summary>
    /// <param name="storageContext">
    /// The storage context used to perform low-level file and folder operations.
    /// </param>
    /// <param name="pathNormalizer">
    /// The path normalizer used to make incoming paths consistent before they are used.
    /// </param>
    /// <param name="pathValidator">
    /// The path validator used to reject invalid or unsafe paths before storage access.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required dependency is <see langword="null"/>.
    /// </exception>
    public ElFinderStorageAdapter(
        IStorageContext storageContext,
        IPathNormalizer pathNormalizer,
        IPathValidator pathValidator)
    {
        _storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));
        _pathNormalizer = pathNormalizer ?? throw new ArgumentNullException(nameof(pathNormalizer));
        _pathValidator = pathValidator ?? throw new ArgumentNullException(nameof(pathValidator));
    }

    /// <summary>
    /// Normalizes a path and encodes it into an elFinder-safe hash value.
    /// </summary>
    /// <param name="path">
    /// The input path to encode. If <see langword="null"/>, an empty path is used.
    /// </param>
    /// <returns>
    /// A hash string that elFinder can safely use as an item identifier.
    /// </returns>
    public string EncodePath(string path)
    {
        var normalized = _pathNormalizer.Normalize(path ?? string.Empty);
        return ElFinderHashEncoder.Encode(normalized);
    }

    /// <summary>
    /// Decodes an elFinder hash value back into a normalized path.
    /// </summary>
    /// <param name="hash">The encoded elFinder hash value.</param>
    /// <returns>
    /// The decoded normalized path with a leading slash, or <see langword="null"/> when decoding fails.
    /// </returns>
    public string? DecodePath(string hash)
    {
        var decoded = ElFinderHashEncoder.Decode(hash);
        if (decoded == null)
        {
            return null;
        }

        return _pathNormalizer.NormalizeWithLeadingSlash(decoded.TrimStart('/'));
    }

    /// <summary>
    /// Gets files and folders directly under the given path.
    /// </summary>
    /// <param name="path">The path whose immediate children should be listed.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// A list of entries under the path. Returns an empty list when the path is invalid
    /// or when storage access fails.
    /// </returns>
    public async Task<List<FileManagerEntry>> GetEntriesAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return new List<FileManagerEntry>();
        }

        try
        {
            var entries = await _storageContext.GetFilesAndDirectories(path).ConfigureAwait(false);
            return entries;
        }
        catch
        {
            return new List<FileManagerEntry>();
        }
    }

    /// <summary>
    /// Gets metadata for a single file or folder at the specified path.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The matching file or folder entry, or <see langword="null"/> when not found, invalid,
    /// or unavailable.
    /// </returns>
    /// <remarks>
    /// For virtual directories in blob storage, this method can synthesize a directory entry
    /// when the directory does not have its own marker blob but can still be inferred from children
    /// or from the parent listing.
    /// </remarks>
    public async Task<FileManagerEntry?> GetEntryAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return null;
        }

        try
        {
            var entry = await _storageContext.GetFileAsync(normalized).ConfigureAwait(false);
            if (entry != null)
            {
                return entry;
            }

            // Blob storage uses virtual directory paths that have no marker blob.
            // Synthesize a directory entry if the path has any children.
            var children = await _storageContext.GetFilesAndDirectories(normalized).ConfigureAwait(false);
            if (children.Count > 0)
            {
                var name = normalized.TrimEnd('/');
                var slash = name.LastIndexOf('/');
                name = slash >= 0 ? name[(slash + 1)..] : name;
                return new FileManagerEntry
                {
                    Path = normalized,
                    Name = name,
                    IsDirectory = true,
                    Modified = DateTime.UtcNow,
                    Size = 0,
                };
            }

            // Empty virtual directories may exist only as entries in the parent listing.
            // Treat them as accessible directories so elFinder operations (rename/parents)
            // can still resolve metadata.
            var parent = GetParentPath(normalized);
            if (!string.IsNullOrEmpty(parent))
            {
                var name = Path.GetFileName(normalized.TrimEnd('/'));
                var siblings = await _storageContext.GetFilesAndDirectories(parent).ConfigureAwait(false);
                var match = siblings.FirstOrDefault(e =>
                    e.IsDirectory
                    && !string.IsNullOrWhiteSpace(e.Name)
                    && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    return new FileManagerEntry
                    {
                        Path = normalized,
                        Name = match.Name,
                        IsDirectory = true,
                        Modified = match.Modified,
                        Size = 0,
                    };
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a folder at the specified path.
    /// </summary>
    /// <param name="path">The folder path to create.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The created folder entry, or <see langword="null"/> when the path is invalid
    /// or creation fails.
    /// </returns>
    public async Task<FileManagerEntry?> CreateFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return null;
        }

        try
        {
            return await _storageContext.CreateFolder(normalized).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates an empty file at the specified path.
    /// </summary>
    /// <param name="path">The full file path to create.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The created file entry, or <see langword="null"/> when the path is invalid
    /// or creation fails.
    /// </returns>
    /// <remarks>
    /// This method uploads a zero-byte stream with default content type
    /// <c>application/octet-stream</c>.
    /// </remarks>
    public async Task<FileManagerEntry?> CreateFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return null;
        }

        try
        {
            var fileName = Path.GetFileName(normalized);
            var relativePath = normalized;

            using var memoryStream = new MemoryStream(Array.Empty<byte>());
            var fileMetaData = new FileUploadMetaData
            {
                ChunkIndex = 0,
                ContentType = "application/octet-stream",
                FileName = fileName,
                RelativePath = relativePath,
                TotalChunks = 1,
                TotalFileSize = 0,
                UploadUid = Guid.NewGuid().ToString()
            };

            await _storageContext.AppendBlob(memoryStream, fileMetaData).ConfigureAwait(false);
            return await _storageContext.GetFileAsync(normalized).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Renames or moves an item by source and destination paths.
    /// </summary>
    /// <param name="sourcePath">The existing source path.</param>
    /// <param name="destinationPath">The target path after rename or move.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The updated entry at the destination path, or <see langword="null"/> when
    /// validation fails, source does not exist, or the operation fails.
    /// </returns>
    public async Task<FileManagerEntry?> RenameAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var normalizedSource = _pathNormalizer.Normalize(sourcePath);
        var normalizedDestination = _pathNormalizer.Normalize(destinationPath);

        if (!_pathValidator.ValidatePath(normalizedSource).IsValid || !_pathValidator.ValidatePath(normalizedDestination).IsValid)
        {
            return null;
        }

        var sourceEntry = await GetEntryAsync(normalizedSource, cancellationToken).ConfigureAwait(false);
        if (sourceEntry == null)
        {
            return null;
        }

        return await RenameAsync(sourceEntry, normalizedDestination, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a file or folder at the specified path.
    /// </summary>
    /// <param name="path">The path to delete.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <remarks>
    /// This method is intentionally tolerant for batch operations.
    /// Invalid paths, missing items, and storage exceptions are ignored.
    /// </remarks>
    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return;
        }

        try
        {
            var entry = await GetEntryAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (entry == null)
            {
                return;
            }

            await DeleteAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Keep behavior tolerant for batch delete scenarios.
        }
    }

    /// <summary>
    /// Deletes the provided file or folder entry.
    /// </summary>
    /// <param name="entry">The entry to delete.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <remarks>
    /// This method is intentionally tolerant for batch operations and silently returns
    /// if the input entry is <see langword="null"/>, invalid, missing, or fails in storage.
    /// </remarks>
    public async Task DeleteAsync(FileManagerEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry == null)
        {
            return;
        }

        var normalized = _pathNormalizer.Normalize(entry.Path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return;
        }

        try
        {
            if (entry.IsDirectory)
            {
                await _storageContext.DeleteFolderAsync(normalized).ConfigureAwait(false);
            }
            else
            {
                await _storageContext.DeleteFileAsync(normalized).ConfigureAwait(false);
            }
        }
        catch
        {
            // Keep behavior tolerant for batch delete scenarios.
        }
    }

    /// <summary>
    /// Renames or moves the provided entry to a destination path.
    /// </summary>
    /// <param name="entry">The source entry to rename or move.</param>
    /// <param name="destinationPath">The destination path.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The resolved destination entry, or <see langword="null"/> when input is invalid
    /// or the operation fails.
    /// </returns>
    public async Task<FileManagerEntry?> RenameAsync(FileManagerEntry entry, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (entry == null)
        {
            return null;
        }

        var normalizedSource = _pathNormalizer.Normalize(entry.Path);
        var normalizedDestination = _pathNormalizer.Normalize(destinationPath);

        if (!_pathValidator.ValidatePath(normalizedSource).IsValid || !_pathValidator.ValidatePath(normalizedDestination).IsValid)
        {
            return null;
        }

        try
        {
            if (entry.IsDirectory)
            {
                await _storageContext.MoveFolderAsync(normalizedSource, normalizedDestination).ConfigureAwait(false);
            }
            else
            {
                await _storageContext.MoveFileAsync(normalizedSource, normalizedDestination).ConfigureAwait(false);
            }

            return await GetEntryAsync(normalizedDestination, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Moves the provided entry to a destination path.
    /// </summary>
    /// <param name="entry">The entry to move.</param>
    /// <param name="destinationPath">The destination path.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The moved entry at the destination path, or <see langword="null"/> when moving fails.
    /// </returns>
    /// <remarks>
    /// This is a convenience wrapper that delegates to <see cref="RenameAsync(FileManagerEntry, string, CancellationToken)"/>.
    /// </remarks>
    public Task<FileManagerEntry?> MoveAsync(FileManagerEntry entry, string destinationPath, CancellationToken cancellationToken = default)
    {
        return RenameAsync(entry, destinationPath, cancellationToken);
    }

    /// <summary>
    /// Opens a readable stream for a file path.
    /// </summary>
    /// <param name="path">The file path to open.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// A readable stream when the file is accessible; otherwise <see langword="null"/>.
    /// </returns>
    public async Task<Stream?> GetReadStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return null;
        }

        try
        {
            return await _storageContext.GetStreamAsync(normalized).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Uploads file content to the specified path.
    /// </summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="content">The file content stream to upload.</param>
    /// <param name="contentType">The content type to store for the uploaded file.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The uploaded file entry, or <see langword="null"/> when validation or upload fails.
    /// </returns>
    /// <remarks>
    /// If <paramref name="contentType"/> is empty, the default content type
    /// <c>application/octet-stream</c> is used.
    /// </remarks>
    public async Task<FileManagerEntry?> UploadFileAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return null;
        }

        try
        {
            var fileName = Path.GetFileName(normalized);
            var relativePath = normalized;
            using var memoryStream = new MemoryStream();
            await content.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            memoryStream.Position = 0;

            var fileMetaData = new FileUploadMetaData
            {
                ChunkIndex = 0,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                FileName = fileName,
                RelativePath = relativePath,
                TotalChunks = 1,
                TotalFileSize = memoryStream.Length,
                UploadUid = Guid.NewGuid().ToString()
            };

            await _storageContext.AppendBlob(memoryStream, fileMetaData).ConfigureAwait(false);
            return await _storageContext.GetFileAsync(normalized).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Copies a file or folder from source path to destination path.
    /// </summary>
    /// <param name="sourcePath">The source path to copy from.</param>
    /// <param name="destinationPath">The destination path to copy to.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The copied destination entry, or <see langword="null"/> when validation or copy fails.
    /// </returns>
    public async Task<FileManagerEntry?> CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var normalizedSource = _pathNormalizer.Normalize(sourcePath);
        var normalizedDestination = _pathNormalizer.Normalize(destinationPath);

        if (!_pathValidator.ValidatePath(normalizedSource).IsValid || !_pathValidator.ValidatePath(normalizedDestination).IsValid)
        {
            return null;
        }

        try
        {
            await _storageContext.CopyAsync(normalizedSource, normalizedDestination).ConfigureAwait(false);
            return await GetEntryAsync(normalizedDestination, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Moves an item by source and destination paths.
    /// </summary>
    /// <param name="sourcePath">The source path.</param>
    /// <param name="destinationPath">The destination path.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The moved destination entry, or <see langword="null"/> when moving fails.
    /// </returns>
    /// <remarks>
    /// This is a convenience wrapper that delegates to <see cref="RenameAsync(string, string, CancellationToken)"/>.
    /// </remarks>
    public Task<FileManagerEntry?> MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        return RenameAsync(sourcePath, destinationPath, cancellationToken);
    }

    /// <summary>
    /// Gets ancestor folders for a given path, ordered from top-level to nearest parent.
    /// </summary>
    /// <param name="path">The path whose ancestor folders should be resolved.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// A list of ancestor folder entries. Returns an empty list when the path is invalid
    /// or when resolution fails.
    /// </returns>
    public async Task<List<FileManagerEntry>> GetAncestorsAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        var ancestors = new List<FileManagerEntry>();

        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return ancestors;
        }

        try
        {
            var current = normalized;
            while (!string.IsNullOrWhiteSpace(current) && current.Contains('/'))
            {
                var parent = current[..current.LastIndexOf('/')];
                if (string.IsNullOrEmpty(parent))
                {
                    break;
                }

                var parentEntry = await GetEntryAsync(parent, cancellationToken).ConfigureAwait(false);
                if (parentEntry != null)
                {
                    ancestors.Add(parentEntry);
                }

                current = parent;
            }

            ancestors.Reverse();
            return ancestors;
        }
        catch
        {
            return new List<FileManagerEntry>();
        }
    }

    /// <summary>
    /// Checks whether a path is accessible in storage.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the path resolves to an existing entry;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public async Task<bool> IsAccessibleAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return false;
        }

        try
        {
            return await GetEntryAsync(normalized, cancellationToken).ConfigureAwait(false) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the parent path from a normalized path.
    /// </summary>
    /// <param name="normalizedPath">The normalized input path.</param>
    /// <returns>
    /// The parent path, or <see langword="null"/> when there is no valid parent.
    /// </returns>
    private static string? GetParentPath(string normalizedPath)
    {
        var trimmed = normalizedPath?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return null;
        }

        return trimmed[..lastSlash];
    }

    /// <summary>
    /// Gets the size in bytes for a file or folder.
    /// </summary>
    /// <param name="path">The file or folder path.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The file size in bytes for files, or the recursive total size for folders.
    /// Returns <c>0</c> for invalid paths, missing entries, or failures.
    /// </returns>
    public async Task<long> GetSizeAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return 0;
        }

        try
        {
            var entry = await _storageContext.GetFileAsync(normalized).ConfigureAwait(false);
            if (entry == null)
            {
                return 0;
            }

            if (!entry.IsDirectory)
            {
                return entry.Size;
            }

            var children = await _storageContext.GetFilesAndDirectories(normalized).ConfigureAwait(false);
            var total = 0L;
            foreach (var child in children)
            {
                var childPath = string.IsNullOrEmpty(normalized) ? child.Path : child.Path;
                total += await GetSizeAsync(childPath, cancellationToken).ConfigureAwait(false);
            }

            return total;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Searches for entries whose names contain a text query, starting from a root path.
    /// </summary>
    /// <param name="query">The text to find in entry names (case-insensitive).</param>
    /// <param name="rootPath">The root path where search traversal begins.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// A list of matches. Each match includes the entry metadata and the computed full path.
    /// </returns>
    /// <remarks>
    /// The search uses breadth-first traversal over directories.
    /// Invalid query values return an empty result.
    /// </remarks>
    public async Task<List<(FileManagerEntry Entry, string FullPath)>> SearchAsync(string query, string rootPath, CancellationToken cancellationToken = default)
    {
        var results = new List<(FileManagerEntry, string)>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        var normalized = _pathNormalizer.Normalize(rootPath);
        var queue = new Queue<string>();
        queue.Enqueue(normalized);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            List<FileManagerEntry> entries;
            try
            {
                entries = await _storageContext.GetFilesAndDirectories(normalized).ConfigureAwait(false);
            }
            catch
            {
                continue;
            }

            foreach (var entry in entries)
            {
                var entryName = entry.Name ?? string.Empty;
                var fullPath = current.TrimEnd('/') + "/" + entryName;

                if (entryName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add((entry, fullPath));
                }

                if (entry.IsDirectory)
                {
                    queue.Enqueue(fullPath);
                }
            }
        }
        return results;
    }
}