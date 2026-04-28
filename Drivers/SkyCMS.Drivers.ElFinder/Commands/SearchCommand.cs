using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "search" command: searches for files/directories by name substring.
/// See Docs/commands/search.md.
/// </summary>
public class SearchCommand : IElFinderRequest
{
    /// <summary>
    /// Search string (substring match against item name).
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// Hash of directory to search within (defaults to volume root).
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Optional MIME type filters (e.g. "image/png", "image/").
    /// </summary>
    public IReadOnlyList<string>? Mimes { get; set; }

    public string Command => "search";
    public string? VolumeId { get; set; }
}
