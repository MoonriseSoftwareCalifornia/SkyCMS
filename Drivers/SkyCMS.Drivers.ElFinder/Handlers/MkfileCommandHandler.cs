using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "mkfile" command: creates a new empty file.
/// </summary>
public class MkfileCommandHandler : IRequestHandler<MkfileCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public MkfileCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(MkfileCommand request, CancellationToken cancellationToken)
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

            // Create empty file
            var newFilePath = parentPath.TrimEnd('/') + "/" + request.Name;
            var createdEntry = await _adapter.CreateFileAsync(newFilePath, cancellationToken);

            var response = new MkfileResponse
            {
                VolumeId = request.VolumeId
            };

            if (createdEntry != null)
            {
                response.Added.Add(ConvertToElFinderObject(createdEntry, newFilePath));
            }

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Mkfile failed: {ex.Message}");
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
            Mime = GetMimeType(entry.Name),
            Ts = new DateTimeOffset(entry.Modified).ToUnixTimeSeconds(),
            Read = 1,
            Write = 1,
            Locked = 0,
            Dirs = 0,
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
