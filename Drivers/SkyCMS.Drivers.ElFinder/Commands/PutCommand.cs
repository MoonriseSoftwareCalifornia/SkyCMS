using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "put" command: edits/updates file content.
/// Typically used for text file editing in the UI.
/// </summary>
public class PutCommand : IElFinderRequest
{
    /// <summary>
    /// Hash of the file to edit.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// New content for the file.
    /// </summary>
    public string? Content { get; set; }

    public string Command => "put";
    public string? VolumeId { get; set; }
}
