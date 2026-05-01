using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "paste" command.
/// Returns added items and removed items if cut operation.
/// </summary>
public class PasteResponse : IElFinderResponse
{
    /// <summary>
    /// List of newly added/pasted items.
    /// </summary>
    [JsonPropertyName("added")]
    public List<ElFinderObject> Added { get; set; } = new();

    /// <summary>
    /// List of removed hashes if items were moved (cut).
    /// </summary>
    [JsonPropertyName("removed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Removed { get; set; }

    /// <summary>
    /// Optional volume ID for multi-volume setups.
    /// </summary>
    [JsonPropertyName("volumeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VolumeId { get; set; }
}
