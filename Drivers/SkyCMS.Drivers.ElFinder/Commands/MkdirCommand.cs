using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "mkdir" command: creates a new directory (or multiple directories in batch).
/// </summary>
public class MkdirCommand : IElFinderRequest
{
    /// <summary>
    /// Parent directory hash where new folder(s) will be created.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Name for the new directory (single-dir mode).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Batch directory names passed as <c>dirs[]</c> in the elFinder 2.1 protocol.
    /// When non-empty, these directories are created alongside (or instead of) <see cref="Name"/>.
    /// </summary>
    public List<string>? Dirs { get; set; }

    public string Command => "mkdir";
    public string? VolumeId { get; set; }
}
