using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "upload" command: handles file uploads.
/// Typically sent as multipart form data.
/// </summary>
public class UploadCommand : IElFinderRequest
{
    /// <summary>
    /// Target directory hash where file will be uploaded.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// The uploaded file data (handled via IFormFile in controller).
    /// </summary>
    public Stream? FileStream { get; set; }

    /// <summary>
    /// Original filename of uploaded file.
    /// </summary>
    public string? Filename { get; set; }

    public string Command => "upload";
    public string? VolumeId { get; set; }
}
