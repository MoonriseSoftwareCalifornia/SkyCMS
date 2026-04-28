using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "upload" command: processes file uploads.
/// </summary>
public class UploadCommandHandler : IRequestHandler<UploadCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public UploadCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(UploadCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Target))
            {
                return ElFinderErrorResponse.InvalidParams("Target is required");
            }

            if (request.FileStream == null)
            {
                return ElFinderErrorResponse.InvalidParams("File stream is required");
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

            // Determine filename
            var filename = request.Filename ?? "uploaded_file";
            var uploadPath = parentPath.TrimEnd('/') + "/" + filename;

            // Upload file
            var mimeType = GetMimeType(filename);
            var uploadedEntry = await _adapter.UploadFileAsync(uploadPath, request.FileStream, mimeType, cancellationToken);

            var response = new UploadResponse
            {
                VolumeId = request.VolumeId
            };

            if (uploadedEntry != null)
            {
                response.Added.Add(ConvertToElFinderObject(uploadedEntry, uploadPath));
            }

            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Upload failed: {ex.Message}");
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
