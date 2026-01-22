// <copyright file="LayoutFamilyModels.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layout
{
    using System;
    using System.Collections.Generic;
    using Cosmos.Common.Data;

    /// <summary>
    /// Information about a layout family (all versions of a layout).
    /// </summary>
    public class LayoutFamilyInfo
    {
        public int LayoutNumber { get; set; }
        public string FamilyName { get; set; } = string.Empty;
        public int TotalVersions { get; set; }
        public Cosmos.Common.Data.Layout? LatestVersion { get; set; }
        public Cosmos.Common.Data.Layout? PublishedVersion { get; set; }
        public List<Cosmos.Common.Data.Layout> AllVersions { get; set; } = new List<Cosmos.Common.Data.Layout>();
        public bool IsActive => PublishedVersion != null;
    }

    /// <summary>
    /// Grouped layout family for UI selection (dropdowns, lists).
    /// </summary>
    public class LayoutFamilyGroup
    {
        public int LayoutNumber { get; set; }
        public string FamilyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<LayoutVersionOption> Versions { get; set; } = new List<LayoutVersionOption>();
    }

    /// <summary>
    /// A single layout version option for UI selection.
    /// </summary>
    public class LayoutVersionOption
    {
        public Guid Id { get; set; }
        public int Version { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
        public DateTimeOffset LastModified { get; set; }
    }
}