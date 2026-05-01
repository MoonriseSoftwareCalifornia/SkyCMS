using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "info" command: returns metadata for one or more targets.
/// </summary>
public class InfoCommand : IElFinderRequest
{
    /// <summary>
    /// Comma-separated hashes or a single hash.
    /// </summary>
    public string? Targets { get; set; }

    public string Command => "info";
    public string? VolumeId { get; set; }
}
