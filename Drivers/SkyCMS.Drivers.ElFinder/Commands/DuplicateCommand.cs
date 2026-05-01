using System.Collections.Generic;
using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "duplicate" command: creates a named copy in the same parent directory.
/// See Docs/commands/duplicate.md.
/// </summary>
public class DuplicateCommand : IElFinderRequest
{
    /// <summary>
    /// Hashes of items to duplicate (comma-separated).
    /// </summary>
    public string? Targets { get; set; }

    public string Command => "duplicate";
    public string? VolumeId { get; set; }
}
