using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "tree" command: returns directory tree structure.
/// Used to populate folder/directory lists in the file explorer UI.
/// </summary>
public class TreeCommand : IElFinderRequest
{
    /// <summary>
    /// Target directory hash to get tree for. If not provided, returns root tree.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Filter for tree results (e.g., "type:folder" to show only folders).
    /// </summary>
    public string? Filter { get; set; }

    public string Command => "tree";
    public string? VolumeId { get; set; }
}
