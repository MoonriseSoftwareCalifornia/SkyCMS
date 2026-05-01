using MediatR;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "get" command: downloads file content.
/// Returns file stream for download to the client.
/// </summary>
public class GetCommandHandler : IRequestHandler<GetCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public GetCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(GetCommand request, CancellationToken cancellationToken)
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

            // Get file stream
            var stream = await _adapter.GetReadStreamAsync(filePath, cancellationToken);
            if (stream == null)
            {
                return ElFinderErrorResponse.Open("Cannot open file");
            }

            var response = new GetResponse
            {
                Mime = GetMimeType(Path.GetFileName(filePath)),
                VolumeId = request.VolumeId
            };

            // Note: In actual controller usage, the file stream should be returned directly
            // to HttpContext rather than as JSON content in the response
            return response;
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Get failed: {ex.Message}");
        }
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
