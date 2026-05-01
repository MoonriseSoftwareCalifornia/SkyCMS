using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "get" command: downloads/retrieves file content.
/// In elFinder, this typically returns file stream data.
/// </summary>
public class GetCommand : IElFinderRequest
{
    /// <summary>
    /// Hash of the file to download.
    /// </summary>
    public string? Target { get; set; }

    public string Command => "get";
    public string? VolumeId { get; set; }
}
