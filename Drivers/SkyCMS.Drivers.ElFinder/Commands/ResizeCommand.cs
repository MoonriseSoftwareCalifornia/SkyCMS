using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "resize" command: resizes, crops, or rotates an image.
/// See Docs/commands/resize.md.
/// </summary>
public class ResizeCommand : IElFinderRequest
{
    /// <summary>Hash of the image file to resize.</summary>
    public string? Target { get; set; }

    /// <summary>Operation mode: "resize", "crop", or "rotate".</summary>
    public string? Mode { get; set; }

    /// <summary>Target width in pixels (resize/crop).</summary>
    public int Width { get; set; }

    /// <summary>Target height in pixels (resize/crop).</summary>
    public int Height { get; set; }

    /// <summary>Crop X offset in pixels.</summary>
    public int X { get; set; }

    /// <summary>Crop Y offset in pixels.</summary>
    public int Y { get; set; }

    /// <summary>Rotation degrees (rotate mode).</summary>
    public int Degree { get; set; }

    /// <summary>JPEG quality 1–100 (optional).</summary>
    public int Quality { get; set; } = 100;

    /// <summary>
    /// When set, saves result as a new file with this name instead of overwriting.
    /// </summary>
    public string? CopyName { get; set; }

    public string Command => "resize";
    public string? VolumeId { get; set; }
}
