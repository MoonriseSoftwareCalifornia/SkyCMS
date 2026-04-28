using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "tree" command.
/// Returns a list of directory entries for tree navigation.
/// </summary>
public class TreeResponse : IElFinderResponse
{
    /// <summary>
    /// List of directory entries (files/folders) in the tree.
    /// </summary>
    [JsonPropertyName("tree")]
    public List<ElFinderObject> Tree { get; set; } = new();

    /// <summary>
    /// Optional volume ID for multi-volume setups.
    /// </summary>
    [JsonPropertyName("volumeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VolumeId { get; set; }
}
