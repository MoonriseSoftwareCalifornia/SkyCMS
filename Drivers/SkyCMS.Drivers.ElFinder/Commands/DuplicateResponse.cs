using System.Collections.Generic;
using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response DTO for the "duplicate" command.
/// See Docs/commands/duplicate.md.
/// </summary>
public class DuplicateResponse : IElFinderResponse
{
    /// <summary>
    /// Newly created duplicate file/folder objects.
    /// </summary>
    [JsonPropertyName("added")]
    public List<ElFinderObject> Added { get; set; } = new();
}
