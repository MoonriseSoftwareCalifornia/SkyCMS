using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "open" command: retrieves root or target directory contents.
/// </summary>
public class OpenCommandHandler : IRequestHandler<OpenCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public OpenCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(OpenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var response = new OpenResponse();

            // Determine target path
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

            // Get current directory info
            var cwdEntry = await _adapter.GetEntryAsync(targetPath, cancellationToken);
            if (cwdEntry == null)
            {
                return ElFinderErrorResponse.Open("Cannot open directory");
            }

            response.Cwd = ConvertToElFinderObject(cwdEntry, targetPath);

            // Get directory contents
            var entries = await _adapter.GetEntriesAsync(targetPath, cancellationToken);
            foreach (var entry in entries)
            {
                var entryPath = targetPath.TrimEnd('/') + "/" + entry.Name;
                response.Files.Add(ConvertToElFinderObject(entry, entryPath));
            }

            response.Api = "2.1";
            response.UplMaxSize = "2G";
            response.VolumeId = request.VolumeId;

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Open failed: {ex.Message}");
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
            Mime = entry.IsDirectory ? "directory" : GetMimeType(entry.Name),
            Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
            Read = 1,
            Write = 1,
            Locked = 0,
            Dirs = entry.IsDirectory ? 1 : 0,
            Tmb = null,
            Url = null
        };
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
