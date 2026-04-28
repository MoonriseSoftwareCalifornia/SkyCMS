using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "mkfile" command: creates a new empty file.
/// </summary>
public class MkfileCommand : IElFinderRequest
{
    /// <summary>
    /// Parent directory hash where new file will be created.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Name for the new file.
    /// </summary>
    public string? Name { get; set; }

    public string Command => "mkfile";
    public string? VolumeId { get; set; }
}
