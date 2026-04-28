using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "mkdir" command.
/// Returns the newly created directory object.
/// </summary>
public class MkdirResponse : IElFinderResponse
{
    /// <summary>
    /// The newly created directory object.
    /// </summary>
    [JsonPropertyName("added")]
    public List<ElFinderObject> Added { get; set; } = new();

    /// <summary>
    /// Optional volume ID for multi-volume setups.
    /// </summary>
    [JsonPropertyName("volumeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VolumeId { get; set; }
}
