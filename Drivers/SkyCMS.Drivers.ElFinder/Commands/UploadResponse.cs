using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "upload" command.
/// Returns newly uploaded file objects.
/// </summary>
public class UploadResponse : IElFinderResponse
{
    /// <summary>
    /// List of newly uploaded file objects.
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
