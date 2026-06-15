using Cosmos.BlobService;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Helpers;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "paste" command: copies or moves files/folders.
/// </summary>
public class PasteCommandHandler : IElFinderHandler<PasteCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public PasteCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> HandleAsync(PasteCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Target))
            {
                return ElFinderErrorResponse.InvalidParams("Target is required");
            }

            if (string.IsNullOrEmpty(request.Sources))
            {
                return ElFinderErrorResponse.InvalidParams("Sources are required");
            }

            var destPath = _adapter.DecodePath(request.Target);
            if (destPath == null)
            {
                return ElFinderErrorResponse.InvalidParams("Invalid target hash");
            }

            // Check destination accessibility
            if (!await _adapter.IsAccessibleAsync(destPath, cancellationToken))
            {
                return ElFinderErrorResponse.Access("Access denied");
            }

            var response = new PasteResponse { VolumeId = request.VolumeId };

            // Handle source items (comma-separated hashes)
            var sources = request.Sources.Split(',', StringSplitOptions.RemoveEmptyEntries);
            bool isCut = !string.IsNullOrEmpty(request.Cut) && request.Cut.Equals("1", StringComparison.OrdinalIgnoreCase);

            foreach (var sourceHash in sources)
            {
                var sourcePath = _adapter.DecodePath(sourceHash.Trim());
                if (sourcePath == null)
                {
                    continue;
                }

                // Resolve entry once; covers accessibility check and provides metadata for move/copy.
                var sourceEntry = await _adapter.GetEntryAsync(sourcePath, cancellationToken);
                if (sourceEntry == null)
                {
                    continue;
                }

                try
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var newPath = destPath.TrimEnd('/') + "/" + fileName;

                    FileManagerEntry? resultEntry;

                    if (isCut)
                    {
                        // Move operation
                        resultEntry = await _adapter.MoveAsync(sourceEntry, newPath, cancellationToken);
                        if (resultEntry != null)
                        {
                            response.Removed = response.Removed ?? new List<string>();
                            response.Removed.Add(sourceHash.Trim());
                        }
                    }
                    else
                    {
                        // Copy operation
                        resultEntry = await _adapter.CopyAsync(sourcePath, newPath, cancellationToken);
                    }

                    if (resultEntry != null)
                    {
                        response.Added.Add(ConvertToElFinderObject(resultEntry, newPath));
                    }
                }
                catch
                {
                    // Continue with next source on error
                    continue;
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Paste failed: {ex.Message}");
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
