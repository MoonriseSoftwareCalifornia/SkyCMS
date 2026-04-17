// <copyright file="Template.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     A page template.
    /// </summary>
    public class Template
    {
        /// <summary>
        ///     Gets or sets identity key for this entity.
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the layout ID for version pinning.
        /// </summary>
        /// <remarks>
        /// When set, this template will always render with the specific Layout version identified by this ID.
        /// When null, the template uses the latest published Layout from the LayoutNumber family.
        /// This allows templates to either pin to a specific version or auto-update with layout changes.
        /// </remarks>
        [Display(Name = "Layout ID")]
        public Guid? LayoutId { get; set; }

        /// <summary>
        /// Gets or sets the layout number (family identifier) this template belongs to.
        /// </summary>
        /// <remarks>
        /// This identifies which layout "family" the template uses. All templates with the same
        /// LayoutNumber share the same layout design family across different versions.
        /// A value of 0 indicates the template has not been migrated and needs LayoutNumber assignment.
        /// Templates typically use the latest published version of layouts in their family unless
        /// LayoutId is explicitly set for version pinning.
        /// </remarks>
        [Display(Name = "Layout Number")]
        public int LayoutNumber { get; set; } = 0;

        /// <summary>
        /// Gets or sets the community layout ID.
        /// </summary>
        [Display(Name = "Community Layout Id")]
        public string CommunityLayoutId { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets friendly name or title of this page template.
        /// </summary>
        [Display(Name = "Template Title")]
        [StringLength(128)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets description or notes about how to use this template.
        /// </summary>
        [Display(Name = "Description/Notes")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the HTML content of this page template.
        /// </summary>
        [Display(Name = "HTML Content")]
        [DataType(DataType.Html)]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        ///    Gets or sets the template page type.
        /// </summary>
        /// <remarks>
        /// This is either 'home' or 'content'.
        /// </remarks>
        [Display(Name = "Page Type")]
        public string PageType { get; set; } = string.Empty;
    }
}
