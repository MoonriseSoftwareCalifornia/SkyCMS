using MediatR;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// elFinder "tmb" command: returns thumbnail URLs keyed by target hash.
/// </summary>
public class TmbCommand : IElFinderRequest
{
    /// <summary>
    /// Comma-separated hashes or a single hash.
    /// </summary>
    public string? Targets { get; set; }

    public string Command => "tmb";
    public string? VolumeId { get; set; }
}
