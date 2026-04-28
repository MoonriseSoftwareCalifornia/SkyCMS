using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "tmb" command.
/// </summary>
public class TmbResponse : IElFinderResponse
{
    /// <summary>
    /// Keyed by hash, value is thumbnail URL.
    /// </summary>
    [JsonPropertyName("images")]
    public Dictionary<string, string> Images { get; set; } = new();
}
