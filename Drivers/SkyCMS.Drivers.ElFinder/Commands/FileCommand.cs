using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "file" command: streams a file to the browser.
/// See Docs/commands/file.md.
/// </summary>
public class FileCommand : IElFinderRequest
{
    /// <summary>
    /// Hash of the file to serve.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// When "1", forces Content-Disposition: attachment (download).
    /// </summary>
    public string? Download { get; set; }

    public string Command => "file";
    public string? VolumeId { get; set; }
}
