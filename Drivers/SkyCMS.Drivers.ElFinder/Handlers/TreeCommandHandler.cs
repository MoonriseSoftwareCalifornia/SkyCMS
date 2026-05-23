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
    private readonly IElFinderNameResolver _nameResolver;

    public TreeCommandHandler(IElFinderStorageAdapter adapter, IElFinderNameResolver nameResolver)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
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
                var entryPath = "/" + (targetPath.TrimEnd('/') + "/" + entry.Name).TrimStart('/');
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

        var resolvedName = await _nameResolver.ResolveNameAsync(path, entry.Name ?? string.Empty, cancellationToken);
        var rawName = entry.Name ?? string.Empty;
        var nameWasSubstituted = !string.Equals(resolvedName, rawName, StringComparison.Ordinal);
        var normalizedPath = "/" + path.Trim('/');

        return new ElFinderObject
        {
            Hash = hash,
            PHash = phash,
            Name = resolvedName,
            Size = entry.Size,
            Mime = "directory",
            Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
            Read = 1,
            Write = 1,
            Locked = 0,
            Dirs = 1,
            RealPath = nameWasSubstituted ? normalizedPath : null,
        };
    }
}
