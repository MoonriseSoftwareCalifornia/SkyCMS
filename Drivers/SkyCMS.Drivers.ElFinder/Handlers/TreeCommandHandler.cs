using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Helpers;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "tree" command: returns directory structure for tree view.
/// </summary>
public class TreeCommandHandler : IElFinderHandler<TreeCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public TreeCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> HandleAsync(TreeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var response = new TreeResponse();

            // Determine target path (root if not specified)
            string targetPath = "/";
            if (!string.IsNullOrEmpty(request.Target))
            {
                var decodedPath = _adapter.DecodePath(request.Target);
                if (decodedPath == null)
                {
                    return ElFinderErrorResponse.InvalidParams("Invalid target hash");
                }
                targetPath = decodedPath;
            }

            // Check accessibility
            if (!await _adapter.IsAccessibleAsync(targetPath, cancellationToken))
            {
                return ElFinderErrorResponse.Access("Access denied");
            }

            // Get entries recursively for tree
            var entries = await _adapter.GetEntriesAsync(targetPath, cancellationToken);
            foreach (var entry in entries.Where(e => e.IsDirectory))
            {
                var entryPath = GetEntryPath(targetPath, entry);
                response.Tree.Add(await ConvertToElFinderObjectAsync(entry, entryPath, cancellationToken));
            }

            response.VolumeId = request.VolumeId;
            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Tree failed: {ex.Message}");
        }
    }

    private async Task<ElFinderObject> ConvertToElFinderObjectAsync(Cosmos.BlobService.FileManagerEntry entry, string path, CancellationToken cancellationToken)
    {
        var hash = _adapter.EncodePath(path);
        var parentPath = path.TrimEnd('/');
        var lastSlash = parentPath.LastIndexOf('/');
        var phash = lastSlash >= 0 ? _adapter.EncodePath(parentPath.Substring(0, lastSlash + 1)) : _adapter.EncodePath("/");

        //  var resolvedName = await _nameResolver.ResolveNameAsync(path, entry.Name ?? string.Empty, cancellationToken);
        var normalizedPath = "/" + path.Trim('/');

        // Only emit RealPath when the name was substituted to a friendly display value.
        // Plain folders keep their canonical path implicit in the hash, matching the
        // documented tree contract and avoiding extra JSON noise.
        //  var nameWasSubstituted = !string.Equals(resolvedName, entry.Name, StringComparison.Ordinal);

        return new ElFinderObject
        {
            Hash = hash,
            PHash = phash,
            Name = entry.Name,
            Size = entry.Size,
            Mime = "directory",
            Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
            Read = 1,
            Write = 1,
            Locked = 0,
            Dirs = 1,
            RealPath = normalizedPath,
            DisplayPath = path,
        };
    }

    private static string GetEntryPath(string parentPath, Cosmos.BlobService.FileManagerEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Path))
        {
            return "/" + entry.Path.Trim('/');
        }

        return "/" + (parentPath.TrimEnd('/') + "/" + entry.Name).TrimStart('/');
    }
}
