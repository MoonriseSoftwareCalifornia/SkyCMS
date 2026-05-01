using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "mkdir" command.
/// Returns the newly created directory object(s).
/// </summary>
public class MkdirResponse : IElFinderResponse
{
    /// <summary>
    /// The newly created directory object(s).
    /// </summary>
    [JsonPropertyName("added")]
    public List<ElFinderObject> Added { get; set; } = new();

    /// <summary>
    /// Maps the requested batch dir name to its elFinder hash.
    /// Populated only when the request included <c>dirs[]</c>.
    /// </summary>
    [JsonPropertyName("hashes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Hashes { get; set; }

    /// <summary>
    /// Optional volume ID for multi-volume setups.
    /// </summary>
    [JsonPropertyName("volumeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VolumeId { get; set; }
}
