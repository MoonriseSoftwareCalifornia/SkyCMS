using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "rm" (delete) command.
/// Returns list of removed hashes.
/// </summary>
public class RmResponse : IElFinderResponse
{
    /// <summary>
    /// List of hashes that were successfully deleted.
    /// </summary>
    [JsonPropertyName("removed")]
    public List<string> Removed { get; set; } = new();

    /// <summary>
    /// List of hashes that could not be found at delete time.
    /// </summary>
    [JsonPropertyName("notFound")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? NotFound { get; set; }

    /// <summary>
    /// Diagnostics for hashes that could not be found or resolved.
    /// </summary>
    [JsonPropertyName("notFoundDetails")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RmDiagnosticEntry>? NotFoundDetails { get; set; }

    /// <summary>
    /// List of hashes that were found but still accessible after delete attempt.
    /// </summary>
    [JsonPropertyName("notRemoved")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? NotRemoved { get; set; }

    /// <summary>
    /// Diagnostics for hashes that remained after a delete attempt.
    /// </summary>
    [JsonPropertyName("notRemovedDetails")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RmDiagnosticEntry>? NotRemovedDetails { get; set; }

    /// <summary>
    /// Optional volume ID for multi-volume setups.
    /// </summary>
    [JsonPropertyName("volumeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VolumeId { get; set; }
}

/// <summary>
/// Diagnostic information for an rm target.
/// </summary>
public class RmDiagnosticEntry
{
    /// <summary>
    /// Original elFinder hash from the request.
    /// </summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Decoded path used by the remove operation, when available.
    /// </summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; set; }

    /// <summary>
    /// Human-readable reason for the rm outcome.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Stable short code for programmatic handling of the rm outcome reason.
    /// </summary>
    [JsonPropertyName("reasonCode")]
    public string ReasonCode { get; set; } = string.Empty;
}
