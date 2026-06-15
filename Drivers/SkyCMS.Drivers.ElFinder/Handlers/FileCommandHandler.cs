using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MimeTypes;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "file" command: streams a file to the browser.
/// Returns a <see cref="FileResponse"/> — the controller must convert this to
/// a FileStreamResult rather than serialising to JSON.
/// See Docs/commands/file.md.
/// </summary>
public class FileCommandHandler : IElFinderHandler<FileCommand>
{
    private readonly IElFinderStorageAdapter _adapter;

    public FileCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> HandleAsync(FileCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
        {
            return ElFinderErrorResponse.InvalidParams("Target is required");
        }

        var path = _adapter.DecodePath(request.Target);
        if (path == null)
        {
            return ElFinderErrorResponse.InvalidParams("Invalid target hash");
        }

        if (!await _adapter.IsAccessibleAsync(path, cancellationToken))
        {
            return ElFinderErrorResponse.Access();
        }

        var stream = await _adapter.GetReadStreamAsync(path, cancellationToken);
        if (stream == null)
        {
            return ElFinderErrorResponse.NotFound();
        }

        var fileName = Path.GetFileName(path.TrimEnd('/'));
        var contentType = GetMimeType(fileName);
        var forceDownload = string.Equals(request.Download, "1", StringComparison.Ordinal);

        return new FileResponse
        {
            Stream = stream,
            ContentType = contentType,
            FileName = fileName,
            ForceDownload = forceDownload,
        };
    }

    private static string GetMimeType(string fileName)
    {
        try
        {
            return MimeTypeMap.GetMimeType(Path.GetExtension(fileName));
        }
        catch
        {
            return "application/octet-stream";
        }
    }
}
