// <copyright file="OpenCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder.Commands
{
    /// <summary>
    /// elFinder "open" command: navigate to a directory and return its contents.
    /// </summary>
    /// <remarks>
    /// The open command initializes or navigates to a folder, returning:
    /// - The current directory (cwd) with metadata
    /// - Immediate child items (files and folders)
    /// - API version and other metadata
    /// - Optional initial root volume info
    /// </remarks>
    public sealed class OpenCommand : IElFinderRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenCommand"/> class.
        /// </summary>
        /// <param name="target">The target hash (omit for root/init).</param>
        /// <param name="init">Whether this is an initialization request (opening root).</param>
        /// <param name="volumeId">The volume ID.</param>
        /// <param name="tree">Whether to include ancestor directories (tree panel navigation).</param>
        /// <param name="blobPublicUrl">Base CDN/blob URL used to build the options.url field.</param>
        /// <param name="tmbUrl">Thumbnail endpoint URL prefix used to build the options.tmbUrl field.</param>
        /// <param name="rootPath">Normalised path of the volume root (e.g. "/pub") for isroot detection.</param>
        public OpenCommand(
            string target = null,
            bool init = false,
            string volumeId = "l1_",
            bool tree = false,
            string blobPublicUrl = null,
            string tmbUrl = null,
            string rootPath = null)
        {
            this.Target = target;
            this.Init = init;
            this.VolumeId = volumeId;
            this.Tree = tree;
            this.BlobPublicUrl = blobPublicUrl;
            this.TmbUrl = tmbUrl;
            this.RootPath = rootPath;
        }

        /// <summary>
        /// Gets the elFinder command name.
        /// </summary>
        public string Command => "open";

        /// <summary>
        /// Gets the target hash (path to open).
        /// </summary>
        public string Target { get; }

        /// <summary>
        /// Gets a value indicating whether this is an initialization request.
        /// </summary>
        public bool Init { get; }

        /// <summary>
        /// Gets the volume ID.
        /// </summary>
        public string VolumeId { get; }

        /// <summary>
        /// Gets a value indicating whether ancestor directories should be included
        /// in the response (used when elFinder sends open with tree=1 for panel navigation).
        /// </summary>
        public bool Tree { get; }

        /// <summary>
        /// Gets the base CDN/blob public URL used to populate <c>options.url</c>.
        /// </summary>
        public string BlobPublicUrl { get; }

        /// <summary>
        /// Gets the thumbnail endpoint URL prefix used to populate <c>options.tmbUrl</c>.
        /// </summary>
        public string TmbUrl { get; }

        /// <summary>
        /// Gets the normalised path of the volume root (e.g. <c>/pub</c>) used to detect
        /// whether the current working directory is the root so that <c>isroot</c> can be set.
        /// </summary>
        public string RootPath { get; }
    }
}
