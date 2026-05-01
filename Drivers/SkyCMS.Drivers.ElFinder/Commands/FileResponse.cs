using System.IO;
using System.Text.Json.Serialization;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Commands;

/// <summary>
/// Response for the "file" command.
/// This is a binary stream response — the controller must NOT serialize this as JSON.
/// Check <see cref="Stream"/> != null and return a FileStreamResult instead.
/// See Docs/commands/file.md.
/// </summary>
public class FileResponse : IElFinderResponse
{
    /// <summary>
    /// The file stream to send to the client.
    /// </summary>
    [JsonIgnore]
    public Stream? Stream { get; set; }

    /// <summary>
    /// MIME type for the Content-Type header.
    /// </summary>
    [JsonIgnore]
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// File name for Content-Disposition header.
    /// </summary>
    [JsonIgnore]
    public string FileName { get; set; } = "download";

    /// <summary>
    /// When true, set Content-Disposition: attachment (download). Otherwise inline.
    /// </summary>
    [JsonIgnore]
    public bool ForceDownload { get; set; }
}
