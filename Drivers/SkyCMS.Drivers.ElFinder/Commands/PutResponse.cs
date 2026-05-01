using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "put" command.
/// Returns the updated file info after edit.
/// </summary>
public class PutResponse : IElFinderResponse
{
    /// <summary>
    /// Updated file object with new content metadata.
    /// </summary>
    [JsonPropertyName("changed")]
    public List<ElFinderObject> Changed { get; set; } = new();

    /// <summary>
    /// Optional volume ID for multi-volume setups.
    /// </summary>
    [JsonPropertyName("volumeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VolumeId { get; set; }
}
