using System.Collections.Generic;
using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "ls" command: returns child item names as a plain array.
/// Optionally filtered to only names present in <see cref="Intersect"/> (conflict check).
/// </summary>
public class LsCommand : IElFinderRequest
{
    /// <summary>Target directory hash.</summary>
    public string? Target { get; set; }

    /// <summary>
    /// Optional list of names to check for conflicts.
    /// When non-empty the response contains only the subset of names that exist.
    /// </summary>
    public IReadOnlyList<string> Intersect { get; set; } = [];

    public string Command => "ls";
    public string? VolumeId { get; set; }
}
