using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "rename" command: renames or moves a file/folder.
/// </summary>
public class RenameCommand : IElFinderRequest
{
    /// <summary>
    /// Hash of the file/folder to rename.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// New name for the file/folder.
    /// </summary>
    public string? Name { get; set; }

    public string Command => "rename";
    public string? VolumeId { get; set; }
}
