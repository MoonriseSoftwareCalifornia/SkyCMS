using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "paste" command: copies or moves files/folders.
/// </summary>
public class PasteCommand : IElFinderRequest
{
    /// <summary>
    /// Destination directory hash.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Comma-separated list of source hashes to copy/move.
    /// </summary>
    public string? Sources { get; set; }

    /// <summary>
    /// Operation type: "copy" or "move" (cut).
    /// </summary>
    public string? Cut { get; set; }

    public string Command => "paste";
    public string? VolumeId { get; set; }
}
