using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "mkfile" command.
/// Returns the newly created file object.
/// </summary>
public class MkfileResponse : IElFinderResponse
{
    /// <summary>
    /// List of newly created file objects.
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
