using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "get" command.
/// For GET operations, the response includes file content/stream reference.
/// Note: Actual file stream is typically returned via HttpContext, not JSON.
/// </summary>
public class GetResponse : IElFinderResponse
{
    /// <summary>
    /// File content (as base64 or raw text depending on file type).
    /// </summary>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    /// <summary>
    /// Encoding of the file content (e.g. "utf-8").
    /// </summary>
    [JsonPropertyName("encoding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Encoding { get; set; }

    /// <summary>
    /// MIME type of the file.
    /// </summary>
    [JsonPropertyName("mime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mime { get; set; }

    /// <summary>
    /// Optional volume ID for multi-volume setups.
    /// </summary>
    [JsonPropertyName("volumeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VolumeId { get; set; }
}
