// <copyright file="LayoutFamilyInfo.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layout
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Information about a layout family (all versions of a layout).
    /// </summary>
    public class LayoutFamilyInfo
    {
        /// <summary>
        /// Gets or sets the layout number that identifies this family.
        /// </summary>
        public int LayoutNumber { get; set; }

        /// <summary>
        /// Gets or sets the family name for this layout.
        /// </summary>
        public string FamilyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of versions in this layout family.
        /// </summary>
        public int TotalVersions { get; set; }

        /// <summary>
        /// Gets or sets the latest version of this layout (may be unpublished).
        /// </summary>
        public Cosmos.Common.Data.Layout? LatestVersion { get; set; }

        /// <summary>
        /// Gets or sets the currently published version of this layout.
        /// </summary>
        public Cosmos.Common.Data.Layout? PublishedVersion { get; set; }

        /// <summary>
        /// Gets or sets all versions of this layout family.
        /// </summary>
        public List<Cosmos.Common.Data.Layout> AllVersions { get; set; } = new List<Cosmos.Common.Data.Layout>();

        /// <summary>
        /// Gets a value indicating whether this layout family has a published version.
        /// </summary>
        public bool IsActive => PublishedVersion != null;
    }

    /// <summary>
    /// Grouped layout family for UI selection (dropdowns, lists).
    /// </summary>
    public class LayoutFamilyGroup
    {
        /// <summary>
        /// Gets or sets the layout number that identifies this family.
        /// </summary>
        public int LayoutNumber { get; set; }

        /// <summary>
        /// Gets or sets the family name for this layout.
        /// </summary>
        public string FamilyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this layout family has a published version.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the list of available versions for this layout family.
        /// </summary>
        public List<LayoutVersionOption> Versions { get; set; } = new List<LayoutVersionOption>();
    }

    /// <summary>
    /// A single layout version option for UI selection.
    /// </summary>
    public class LayoutVersionOption
    {
        /// <summary>
        /// Gets or sets the unique identifier for this layout version.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the version number.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the display name for this version.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this version is currently published.
        /// </summary>
        public bool IsPublished { get; set; }

        /// <summary>
        /// Gets or sets the last modified date and time for this version.
        /// </summary>
        public DateTimeOffset LastModified { get; set; }
    }
}