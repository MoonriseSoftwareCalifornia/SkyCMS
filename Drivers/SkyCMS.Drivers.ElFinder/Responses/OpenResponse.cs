// <copyright file="OpenResponse.cs" company="Moonrise Software, LLC">
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
    /// Response to the "open" command - returns current directory and its contents.
    /// </summary>
    public sealed class OpenResponse : IElFinderResponse
    {
        /// <summary>
        /// Gets or sets the current working directory metadata.
        /// </summary>
        [JsonPropertyName("cwd")]
        public ElFinderObject Cwd { get; set; }

        /// <summary>
        /// Gets or sets the list of files and folders in the current directory.
        /// </summary>
        [JsonPropertyName("files")]
        public List<ElFinderObject> Files { get; set; } = new();

        /// <summary>
        /// Gets or sets optional API version information.
        /// </summary>
        [JsonPropertyName("api")]
        public string Api { get; set; } = "2.1049";

        /// <summary>
        /// Gets or sets optional upload size limit.
        /// </summary>
        [JsonPropertyName("uplMaxSize")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string UplMaxSize { get; set; }

        /// <summary>
        /// Gets or sets the root volume object (provided on init).
        /// </summary>
        [JsonPropertyName("volumeid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string VolumeId { get; set; }

        /// <summary>
        /// Gets or sets the volume options block. Populated only on init requests.
        /// </summary>
        [JsonPropertyName("options")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ElFinderOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the list of network drivers. Always empty for this driver;
        /// must be present in the init response so the client initialises correctly.
        /// </summary>
        [JsonPropertyName("netDrivers")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<object> NetDrivers { get; set; }
    }

    /// <summary>
    /// elFinder object representation (file or directory).
    /// </summary>
    public sealed class ElFinderObject
    {
        /// <summary>
        /// Gets or sets the unique hash identifier.
        /// </summary>
        [JsonPropertyName("hash")]
        public string Hash { get; set; }

        /// <summary>
        /// Gets or sets the volume ID. Set only on volume root directory objects.
        /// </summary>
        [JsonPropertyName("volumeid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string VolumeId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this object is a volume root (1 = yes).
        /// Omitted (0 / default) for non-root entries.
        /// </summary>
        [JsonPropertyName("isroot")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int IsRoot { get; set; }

        /// <summary>
        /// Gets or sets the parent hash (folder path).
        /// </summary>
        [JsonPropertyName("phash")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ParentHash { get; set; }

        /// <summary>
        /// Gets or sets the parent hash alias used by handler code.
        /// </summary>
        [JsonIgnore]
        public string PHash
        {
            get => this.ParentHash;
            set => this.ParentHash = value;
        }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes (0 for directories).
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the MIME type or "directory".
        /// </summary>
        [JsonPropertyName("mime")]
        public string Mime { get; set; }

        /// <summary>
        /// Gets or sets the Unix timestamp of last modification.
        /// </summary>
        [JsonPropertyName("ts")]
        public long Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the timestamp alias used by handler code.
        /// </summary>
        [JsonIgnore]
        public long Ts
        {
            get => this.Timestamp;
            set => this.Timestamp = value;
        }

        /// <summary>
        /// Gets or sets a value indicating if the item is readable (1 = yes).
        /// </summary>
        [JsonPropertyName("read")]
        public int Read { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating if the item is writable (1 = yes).
        /// </summary>
        [JsonPropertyName("write")]
        public int Write { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating if the item is locked (1 = yes).
        /// </summary>
        [JsonPropertyName("locked")]
        public int Locked { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating if the item has subdirectories (1 = yes).
        /// </summary>
        [JsonPropertyName("dirs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Dirs { get; set; }

        /// <summary>
        /// Gets or sets the optional thumbnail URL.
        /// </summary>
        [JsonPropertyName("tmb")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Thumbnail { get; set; }

        /// <summary>
        /// Gets or sets the thumbnail alias used by handler code.
        /// </summary>
        [JsonIgnore]
        public string Tmb
        {
            get => this.Thumbnail;
            set => this.Thumbnail = value;
        }

        /// <summary>
        /// Gets or sets the optional public URL for download/preview.
        /// </summary>
        [JsonPropertyName("url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Url { get; set; }
    }
}
