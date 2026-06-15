// <copyright file="ElFinderOptions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder.Responses
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Volume options block returned in the elFinder init response.
    /// The elFinder client reads these on startup to configure the file manager UI.
    /// </summary>
    public sealed class ElFinderOptions
    {
        /// <summary>
        /// Gets or sets the base URL used to build direct download/preview links for files in this volume.
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the thumbnail base URL. elFinder appends the file hash to this string
        /// to build the thumbnail request URL.
        /// </summary>
        [JsonPropertyName("tmbUrl")]
        public string TmbUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable path shown in the breadcrumb for this volume root.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path separator character.
        /// </summary>
        [JsonPropertyName("separator")]
        public string Separator { get; set; } = "/";

        /// <summary>
        /// Gets or sets the archive capabilities for this volume.
        /// </summary>
        [JsonPropertyName("archivers")]
        public ElFinderArchivers Archivers { get; set; } = new ElFinderArchivers();

        /// <summary>
        /// Gets or sets elFinder commands that are disabled for this volume.
        /// </summary>
        [JsonPropertyName("disabled")]
        public List<string> Disabled { get; set; } = new List<string>
        {
            "callback", "chmod", "editor", "netmount", "ping", "extract", "archive",
        };

        /// <summary>
        /// Gets or sets a value indicating whether pasting over an existing file is allowed (1 = yes).
        /// </summary>
        [JsonPropertyName("copyOverwrite")]
        public int CopyOverwrite { get; set; } = 1;

        /// <summary>
        /// Gets or sets the hash of the volume trash directory, or empty string if unsupported.
        /// </summary>
        [JsonPropertyName("trashHash")]
        public string TrashHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum number of simultaneous upload connections (-1 = unlimited).
        /// </summary>
        [JsonPropertyName("uploadMaxConn")]
        public int UploadMaxConn { get; set; } = -1;

        /// <summary>
        /// Gets or sets the maximum upload size in bytes, or null for no limit.
        /// </summary>
        [JsonPropertyName("uploadMaxSize")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? UploadMaxSize { get; set; }

        /// <summary>
        /// Gets or sets the UI command map. Required by the elFinder client on every open response.
        /// </summary>
        [JsonPropertyName("uiCmdMap")]
        public Dictionary<string, object> UiCmdMap { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Archive operation capabilities for a volume.
    /// </summary>
    public sealed class ElFinderArchivers
    {
        /// <summary>
        /// Gets or sets MIME types that can be created as archives.
        /// </summary>
        [JsonPropertyName("create")]
        public List<string> Create { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets MIME types that can be extracted.
        /// </summary>
        [JsonPropertyName("extract")]
        public List<string> Extract { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets map of MIME type → file extension for archive creation.
        /// </summary>
        [JsonPropertyName("createext")]
        public Dictionary<string, string> CreateExt { get; set; } = new Dictionary<string, string>();
    }
}
