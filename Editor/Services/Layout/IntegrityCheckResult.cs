// <copyright file="IntegrityCheckResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layout
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents the result of a data integrity check.
    /// </summary>
    public class IntegrityCheckResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether all checks passed.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the list of errors found during the integrity check.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the list of warnings found during the integrity check.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the list of informational messages.
        /// </summary>
        public List<string> Info { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets statistics about the checked data.
        /// </summary>
        public IntegrityStatistics Statistics { get; set; } = new IntegrityStatistics();
    }

    /// <summary>
    /// Statistics gathered during integrity checking.
    /// </summary>
    public class IntegrityStatistics
    {
        /// <summary>
        /// Gets or sets total number of layouts checked.
        /// </summary>
        public int TotalLayouts { get; set; }

        /// <summary>
        /// Gets or sets total number of layout families.
        /// </summary>
        public int TotalLayoutFamilies { get; set; }

        /// <summary>
        /// Gets or sets total number of templates checked.
        /// </summary>
        public int TotalTemplates { get; set; }

        /// <summary>
        /// Gets or sets number of layouts with LayoutNumber = 0.
        /// </summary>
        public int LayoutsWithoutNumber { get; set; }

        /// <summary>
        /// Gets or sets number of templates with LayoutNumber = 0.
        /// </summary>
        public int TemplatesWithoutNumber { get; set; }

        /// <summary>
        /// Gets or sets number of orphaned templates (referencing non-existent layouts).
        /// </summary>
        public int OrphanedTemplates { get; set; }

        /// <summary>
        /// Gets or sets number of layout families with inconsistent IsDefault flags.
        /// </summary>
        public int FamiliesWithInconsistentDefaults { get; set; }
    }
}