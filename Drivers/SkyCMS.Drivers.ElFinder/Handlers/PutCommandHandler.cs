using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "put" command: edits/updates file content.
/// </summary>
public class PutCommandHandler : IRequestHandler<PutCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public PutCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(PutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Target))
            {
                return ElFinderErrorResponse.InvalidParams("Target is required");
            }

            var filePath = _adapter.DecodePath(request.Target);
            if (filePath == null)
            {
                return ElFinderErrorResponse.InvalidParams("Invalid target hash");
            }

            // Check accessibility
            if (!await _adapter.IsAccessibleAsync(filePath, cancellationToken))
            {
                return ElFinderErrorResponse.Access("Access denied");
            }

            // Write content to file
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(request.Content ?? string.Empty)))
            {
                var mimeType = GetMimeType(Path.GetFileName(filePath));
                var updatedEntry = await _adapter.UploadFileAsync(filePath, stream, mimeType, cancellationToken);

                var response = new PutResponse
                {
                    VolumeId = request.VolumeId
                };

                if (updatedEntry != null)
                {
                    response.Changed.Add(ConvertToElFinderObject(updatedEntry, filePath));
                }

                return response;
            }
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Put failed: {ex.Message}");
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
