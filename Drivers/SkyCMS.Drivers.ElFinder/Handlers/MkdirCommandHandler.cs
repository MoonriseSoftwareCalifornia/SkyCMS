using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "mkdir" command: creates a new directory.
/// </summary>
public class MkdirCommandHandler : IElFinderHandler<MkdirCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public MkdirCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> HandleAsync(MkdirCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Target))
            {
                return ElFinderErrorResponse.InvalidParams("Target is required");
            }

            var hasBatchDirs = request.Dirs is { Count: > 0 };

            if (!hasBatchDirs && string.IsNullOrEmpty(request.Name))
            {
                return ElFinderErrorResponse.InvalidParams("Name is required");
            }

            var parentPath = _adapter.DecodePath(request.Target);
            if (parentPath == null)
            {
                return ElFinderErrorResponse.InvalidParams("Invalid target hash");
            }

            // Check accessibility
            if (!await _adapter.IsAccessibleAsync(parentPath, cancellationToken))
            {
                return ElFinderErrorResponse.Access("Access denied");
            }

            var response = new MkdirResponse
            {
                VolumeId = request.VolumeId,
            };

            // Single-directory creation (the standard path)
            if (!string.IsNullOrEmpty(request.Name))
            {
                var newFolderPath = parentPath.TrimEnd('/') + "/" + request.Name;
                var createdEntry = await _adapter.CreateFolderAsync(newFolderPath, cancellationToken);
                if (createdEntry != null)
                {
                    response.Added.Add(ConvertToElFinderObject(createdEntry, newFolderPath));
                }
            }

            // Batch directory creation via dirs[] (elFinder 2.1 protocol extension)
            if (hasBatchDirs)
            {
                response.Hashes = new Dictionary<string, string>();
                foreach (var dirName in request.Dirs!)
                {
                    if (string.IsNullOrWhiteSpace(dirName))
                    {
                        continue;
                    }

                    var batchPath = parentPath.TrimEnd('/') + "/" + dirName;
                    var batchEntry = await _adapter.CreateFolderAsync(batchPath, cancellationToken);
                    if (batchEntry != null)
                    {
                        var obj = ConvertToElFinderObject(batchEntry, batchPath);
                        response.Added.Add(obj);
                        response.Hashes[dirName] = obj.Hash;
                    }
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Mkdir failed: {ex.Message}");
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
        };
    }
}
