using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Helpers;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "rename" command: renames or moves a file/folder.
/// </summary>
public class RenameCommandHandler : IElFinderHandler<RenameCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public RenameCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> HandleAsync(RenameCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Target))
            {
                return ElFinderErrorResponse.InvalidParams("Target is required");
            }

            if (string.IsNullOrEmpty(request.Name))
            {
                return ElFinderErrorResponse.InvalidParams("Name is required");
            }

            var sourcePath = _adapter.DecodePath(request.Target);
            if (sourcePath == null)
            {
                return ElFinderErrorResponse.InvalidParams("Invalid target hash");
            }

            // Resolve the entry once; this covers both the accessibility check and the metadata needed for rename.
            var sourceEntry = await _adapter.GetEntryAsync(sourcePath, cancellationToken);
            if (sourceEntry == null)
            {
                return ElFinderErrorResponse.Access("Access denied");
            }

            // Get parent path
            var parentPath = sourcePath.TrimEnd('/');
            var lastSlash = parentPath.LastIndexOf('/');
            var parentDir = lastSlash >= 0 ? parentPath.Substring(0, lastSlash + 1) : "/";

            // Construct new path
            var newPath = parentDir.TrimEnd('/') + "/" + request.Name;

            // Rename/move the item using the pre-resolved entry
            var renamedEntry = await _adapter.RenameAsync(sourceEntry, newPath, cancellationToken);

            var response = new RenameResponse
            {
                VolumeId = request.VolumeId
            };

            if (renamedEntry != null)
            {
                response.Added.Add(ConvertToElFinderObject(renamedEntry, newPath));
                response.Removed = new List<string> { request.Target };
            }

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Rename failed: {ex.Message}");
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
            Mime = entry.IsDirectory ? "directory" : ElFinderMimeHelper.GetMimeType(entry.Name),
            Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
            Read = 1,
            Write = 1,
            Locked = 0,
            Dirs = entry.IsDirectory ? 1 : 0,
        };
    }
}
