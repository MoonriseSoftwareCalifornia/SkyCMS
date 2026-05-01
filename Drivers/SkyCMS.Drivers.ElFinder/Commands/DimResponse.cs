using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for the "dim" command.
/// See Docs/commands/dim.md.
/// </summary>
public class DimResponse : IElFinderResponse
{
    /// <summary>
    /// Image dimensions in "WxH" format (e.g. "800x600").
    /// </summary>
    [JsonPropertyName("dim")]
    public string Dim { get; set; } = string.Empty;
}
