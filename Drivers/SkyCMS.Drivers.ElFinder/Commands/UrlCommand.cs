using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "url" command: returns the public URL for a file hash.
/// See Docs/commands/url.md.
/// </summary>
public class UrlCommand : IElFinderRequest
{
    /// <summary>Hash of the file to resolve.</summary>
    public string? Target { get; set; }

    /// <summary>
    /// Base blob public URL (e.g. "https://cdn.example.com").
    /// Set by the controller from IEditorSettings.BlobPublicUrl.
    /// </summary>
    public string? BlobPublicUrl { get; set; }

    public string Command => "url";
    public string? VolumeId { get; set; }
}
