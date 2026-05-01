using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "parents" command: returns the parent directory path chain.
/// Used to build breadcrumb navigation in the UI.
/// </summary>
public class ParentsCommand : IElFinderRequest
{
    /// <summary>
    /// Target file/folder hash to get parents for.
    /// </summary>
    public string? Target { get; set; }

    public string Command => "parents";
    public string? VolumeId { get; set; }
}
