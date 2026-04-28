using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "rm" command: deletes a file or folder.
/// </summary>
public class RmCommand : IElFinderRequest
{
    /// <summary>
    /// Hash or array of hashes of items to delete.
    /// Can be a single hash or comma-separated list.
    /// </summary>
    public string? Target { get; set; }

    public string Command => "rm";
    public string? VolumeId { get; set; }
}
