using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for the "url" command.
/// See Docs/commands/url.md.
/// </summary>
public class UrlResponse : IElFinderResponse
{
    /// <summary>
    /// Public URL for the requested file.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
