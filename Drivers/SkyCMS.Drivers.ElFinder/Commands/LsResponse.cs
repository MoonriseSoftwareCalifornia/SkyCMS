using System.Collections.Generic;
using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for "ls" command.
/// Returns a plain array of item names as required by the elFinder 2.1 protocol.
/// See Docs/commands/ls.md.
/// </summary>
public class LsResponse : IElFinderResponse
{
    /// <summary>
    /// Display names of items in the listed directory.
    /// When an <c>intersect[]</c> filter is supplied only matching names appear.
    /// </summary>
    [JsonPropertyName("list")]
    public List<string> List { get; set; } = new();
}
