using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "tree" command: returns directory structure for tree view.
/// </summary>
public class TreeCommandHandler : IRequestHandler<TreeCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public TreeCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(TreeCommand request, CancellationToken cancellationToken)
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
                var entryPath = targetPath.TrimEnd('/') + "/" + entry.Name;
                response.Tree.Add(ConvertToElFinderObject(entry, entryPath));
            }

            response.VolumeId = request.VolumeId;
            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Tree failed: {ex.Message}");
        }
    }

    private ElFinderObject ConvertToElFinderObject(Cosmos.BlobService.FileManagerEntry entry, string path)
    {
        var hash = _adapter.EncodePath(path);
        var parentPath = path.TrimEnd('/');
        var lastSlash = parentPath.LastIndexOf('/');
        var phash = lastSlash >= 0 ? _adapter.EncodePath(parentPath.Substring(0, lastSlash + 1)) : _adapter.EncodePath("/");

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
            Tmb = null,
            Url = null
        };
    }
}
