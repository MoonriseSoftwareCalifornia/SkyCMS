using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.BlobService;
using Cosmos.BlobService.Models;
using SkyCMS.Drivers.ElFinder;

namespace SkyCMS.Drivers.ElFinder.Adapters;

/// <summary>
/// Concrete implementation of IElFinderStorageAdapter.
/// Bridges elFinder protocol operations to underlying storage context.
/// </summary>
public class ElFinderStorageAdapter : IElFinderStorageAdapter
{
    private const string VolumeId = "l1_";
    private readonly IStorageContext _storageContext;
    private readonly IPathNormalizer _pathNormalizer;
    private readonly IPathValidator _pathValidator;

    public ElFinderStorageAdapter(
        IStorageContext storageContext,
        IPathNormalizer pathNormalizer,
        IPathValidator pathValidator)
    {
        _storageContext = storageContext ?? throw new ArgumentNullException(nameof(storageContext));
        _pathNormalizer = pathNormalizer ?? throw new ArgumentNullException(nameof(pathNormalizer));
        _pathValidator = pathValidator ?? throw new ArgumentNullException(nameof(pathValidator));
    }

    public string EncodePath(string path)
    {
        var normalized = _pathNormalizer.Normalize(path ?? string.Empty);
        return ElFinderHashEncoder.Encode(normalized);
    }

    public string? DecodePath(string hash)
    {
        var decoded = ElFinderHashEncoder.Decode(hash);
        if (decoded == null)
        {
            return null;
        }

        return _pathNormalizer.NormalizeWithLeadingSlash(decoded.TrimStart('/'));
    }

    public async Task<List<FileManagerEntry>> GetEntriesAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = _pathNormalizer.Normalize(path);
        if (!_pathValidator.ValidatePath(normalized).IsValid)
        {
            return new List<FileManagerEntry>();
        }

        try
        {
            return await _storageContext.GetFilesAndDirectories(normalized).ConfigureAwait(false);
        }
        catch
        {
            return new List<FileManagerEntry>();
        }
    }

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

    public Task<FileManagerEntry?> MoveAsync(FileManagerEntry entry, string destinationPath, CancellationToken cancellationToken = default)
    {
        return RenameAsync(entry, destinationPath, cancellationToken);
    }

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

    public Task<FileManagerEntry?> MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        return RenameAsync(sourcePath, destinationPath, cancellationToken);
    }

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
            if(entry == null)
            {
                return 0;
            }

            if(!entry.IsDirectory)
            {
                return entry.Size;
            }

            var children = await _storageContext.GetFilesAndDirectories(normalized).ConfigureAwait(false);
            var total = 0L;
            foreach(var child in children)
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
                entries = await _storageContext.GetFilesAndDirectories(current).ConfigureAwait(false);
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
