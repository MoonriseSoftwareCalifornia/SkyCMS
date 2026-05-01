using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "dim" command: returns the pixel dimensions of an image.
/// See Docs/commands/dim.md.
/// </summary>
public class DimCommand : IElFinderRequest
{
    /// <summary>Hash of the image file.</summary>
    public string? Target { get; set; }

    public string Command => "dim";
    public string? VolumeId { get; set; }
}
