using System.Collections.Generic;
using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "parents" command.
/// Returns list of parent directories up to root.
/// </summary>
public class ParentsResponse : IElFinderResponse
{
    /// <summary>
    /// List of parent entries from target up to root.
    /// </summary>
    [JsonPropertyName("tree")]
    public List<ElFinderObject> Tree { get; set; } = new();

    /// <summary>
    /// Optional volume ID for multi-volume setups.
    /// </summary>
    [JsonPropertyName("volumeId")]
    public string? VolumeId { get; set; }
}
