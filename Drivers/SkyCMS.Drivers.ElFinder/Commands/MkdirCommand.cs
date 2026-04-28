using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "mkdir" command: creates a new directory.
/// </summary>
public class MkdirCommand : IElFinderRequest
{
    /// <summary>
    /// Parent directory hash where new folder will be created.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Name for the new directory.
    /// </summary>
    public string? Name { get; set; }

    public string Command => "mkdir";
    public string? VolumeId { get; set; }
}
