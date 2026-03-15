// <copyright file="BlogViewModels.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Models.Blogs
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// View models used for blog-related pages in the editor area.
    /// </summary>
    /// <remarks>
    /// These view models are lightweight representations intended for Razor Pages forms and list displays.
    /// They mirror parts of the domain type <c>Cosmos.Common.Data.Blog</c> while omitting audit fields.
    /// </remarks>
    public class BlogStreamViewModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the blog stream.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the blog key used to identify the stream in URLs and lookups.
        /// </summary>
        /// <remarks>
        /// This value must match the regular expression: <c>^[a-z0-9-_]+$</c>.
        /// It is required and has a maximum length of 64 characters.
        /// </remarks>
        [Required]
        [MaxLength(64)]
        [RegularExpression("^[a-z0-9-_]+$", ErrorMessage = "Lowercase letters, numbers, dash, underscore only.")]
        [Display(Name = "Blog Key")]
        public string BlogKey { get; set; }

        /// <summary>
        /// Gets or sets the blog stream title.
        /// </summary>
        /// <remarks>Required with a maximum length of 128 characters.</remarks>
        [Required(AllowEmptyStrings = false)]
        [MaxLength(128)]
        [Display(Name = "Title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the blog stream description.
        /// </summary>
        /// <remarks>Required with a maximum length of 512 characters. Typically displayed on list and detail pages.</remarks>
        [Required(AllowEmptyStrings = false)]
        [MaxLength(512)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the hero image URL or path for the blog stream.
        /// </summary>
        /// <remarks>This property is optional; store a URL or application-relative path to an image.</remarks>
        [Display(Name = "Hero Image (URL / Path)")]
        public string HeroImage { get; set; }  // not [Required]

        /// <summary>
        /// Gets or sets the published date/time for the blog stream.
        /// </summary>
        [Display(Name = "Published")]
        public DateTimeOffset? Published { get; set; } = null;

        /// <summary>
        /// Gets or sets the blog URL path for the stream.
        /// </summary>
        public string UrlPath { get; set; }
    }

    /// <summary>
    /// Container view model for entries within a blog stream.
    /// </summary>
    /// <remarks>
    /// Used to transfer the stream metadata along with a list of entry projections to list pages.
    /// </remarks>
    public class BlogEntriesListViewModel
    {
        /// <summary>
        /// Gets or sets the blog stream key.
        /// </summary>
        public string BlogKey { get; set; }

        /// <summary>
        /// Gets or sets the blog stream title.
        /// </summary>
        public string BlogTitle { get; set; }

        /// <summary>
        /// Gets or sets the blog stream description.
        /// </summary>
        public string BlogDescription { get; set; }

        /// <summary>
        /// Gets or sets the hero image URL or path for the blog stream.
        /// </summary>
        public string HeroImage { get; set; }

        /// <summary>
        /// Gets or sets the blog URL path for the stream.
        /// </summary>
        public string BlogUrlPath { get; set; }

        /// <summary>
        /// Gets or sets the list of entries belonging to the stream.
        /// </summary>
        public List<BlogEntryListItem> Entries { get; set; } = new();
    }

    /// <summary>
    /// Form model for creating or editing a blog entry.
    /// </summary>
    /// <remarks>
    /// This view model is intended for use in editor forms. Validation attributes on properties
    /// communicate constraints enforced by the UI and server model binding.
    /// </remarks>
    public class BlogEntryEditViewModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the blog entry.
        /// </summary>
        /// <remarks>Null when creating a new entry.</remarks>
        public Guid? Id { get; set; }

        /// <summary>
        /// Gets or sets the article number for the entry (if assigned).
        /// </summary>
        public int? ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the blog stream key this entry belongs to.
        /// </summary>
        public string BlogKey { get; set; }

        /// <summary>
        /// Gets or sets the entry title.
        /// </summary>
        /// <remarks>Required with a maximum length of 254 characters.</remarks>
        [Required, MaxLength(254)]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the introduction (teaser) text.
        /// </summary>
        /// <remarks>Optional; maximum length of 512 characters.</remarks>
        [MaxLength(512)]
        [Display(Name = "Introduction (teaser)")]
        public string Introduction { get; set; }

        /// <summary>
        /// Gets or sets the main content of the entry (HTML expected).
        /// </summary>
        [Display(Name = "Content (HTML)")]
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the banner image URL or path for the entry.
        /// </summary>
        [Display(Name = "Banner Image (URL / Path)")]
        public string BannerImage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to publish the entry immediately.
        /// </summary>
        [Display(Name = "Publish Now?")]
        public bool PublishNow { get; set; }

        /// <summary>
        /// Gets or sets the scheduled publishing date/time for the entry.
        /// </summary>
        /// <remarks>
        /// If <see cref="PublishNow"/> is true this may be ignored by server-side logic.
        /// </remarks>
        [Display(Name = "Publishing Date/Time")]
        public DateTimeOffset? Published { get; set; }
    }
}
