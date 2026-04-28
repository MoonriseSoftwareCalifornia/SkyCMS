using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "info" command.
/// </summary>
public class InfoResponse : IElFinderResponse
{
    /// <summary>
    /// Metadata objects for requested targets.
    /// </summary>
    [JsonPropertyName("files")]
    public List<ElFinderObject> Files { get; set; } = new();
}
