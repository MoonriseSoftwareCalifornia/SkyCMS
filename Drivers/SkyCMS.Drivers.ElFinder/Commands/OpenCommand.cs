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
        public OpenCommand(string target = null, bool init = false, string volumeId = "l1_")
        {
            this.Target = target;
            this.Init = init;
            this.VolumeId = volumeId;
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
    }
}
