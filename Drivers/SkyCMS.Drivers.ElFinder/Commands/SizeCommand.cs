using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "size" command: returns total size of file or directory.
/// For directories, recursively calculates total size of contents.
/// </summary>
public class SizeCommand : IElFinderRequest
{
    /// <summary>
    /// Hash of file or folder to get size for.
    /// </summary>
    public string? Target { get; set; }

    public string Command => "size";
    public string? VolumeId { get; set; }
}
