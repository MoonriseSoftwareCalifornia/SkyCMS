using System.Collections.Generic;
using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for the "search" command.
/// Returns a flat list of matching file/directory objects.
/// See Docs/commands/search.md.
/// </summary>
public class SearchResponse : IElFinderResponse
{
    /// <summary>
    /// Matching file and directory objects.
    /// </summary>
    [JsonPropertyName("files")]
    public List<ElFinderObject> Files { get; set; } = new();
}
