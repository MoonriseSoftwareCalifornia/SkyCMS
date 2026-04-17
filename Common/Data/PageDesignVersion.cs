// <copyright file="PageDesignVersion.cs" company="Moonrise Software, LLC">
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
    public class PageDesignVersion
    {
        /// <summary>
        ///     Gets or sets identity key for this entity.
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the template ID.
        /// </summary>
        [Required]
        public Guid TemplateId { get; set; }

        /// <summary>
        /// Gets or sets the layout ID.
        /// </summary>
        [Display(Name = "Layout ID")]
        public Guid? LayoutId { get; set; }

        /// <summary>
        /// Gets or sets the community layout ID.
        /// </summary>
        [Display(Name = "Community Layout Id")]
        public string CommunityLayoutId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the version number.
        /// </summary>
        [Display(Name = "Version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// Gets or sets the row version for concurrency.
        /// </summary>
        public long RowVersion { get; set; }

        /// <summary>
        /// Gets or sets the published date.
        /// </summary>
        public DateTimeOffset? Published { get; set; } = null;

        /// <summary>
        /// Gets or sets the modified date.
        /// </summary>
        public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;

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
