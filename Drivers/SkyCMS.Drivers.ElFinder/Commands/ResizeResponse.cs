using System.Collections.Generic;
using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for the "resize" command.
/// See Docs/commands/resize.md.
/// </summary>
public class ResizeResponse : IElFinderResponse
{
    /// <summary>
    /// The changed file object (updated or newly created copy).
    /// </summary>
    [JsonPropertyName("changed")]
    public List<ElFinderObject> Changed { get; set; } = new();
}
