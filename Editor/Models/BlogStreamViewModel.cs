// <copyright file="BlogStreamViewModel.cs" company="Moonrise Software, LLC">
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
    /// These view models are lightweight representations intended for Razor forms and list displays.
    /// They mirror parts of the domain type <c>Cosmos.Common.Data.Blog</c> while omitting audit fields.
    /// </remarks>
    public class BlogStreamViewModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the blog.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the blog key used to identify the blog in URLs and lookups.
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
        /// Gets or sets the blog title.
        /// </summary>
        /// <remarks>Required with a maximum length of 128 characters.</remarks>
        [Required(AllowEmptyStrings = false)]
        [MaxLength(128)]
        [Display(Name = "Title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the blog description.
        /// </summary>
        /// <remarks>Required with a maximum length of 512 characters. Typically displayed on list and detail pages.</remarks>
        [Required(AllowEmptyStrings = false)]
        [MaxLength(512)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the hero image URL or path for the blog.
        /// </summary>
        /// <remarks>This property is optional; store a URL or application-relative path to an image.</remarks>
        [Display(Name = "Hero Image (URL / Path)")]
        public string HeroImage { get; set; } // not [Required]

        /// <summary>
        /// Gets or sets the published date/time for the blog.
        /// </summary>
        [Display(Name = "Published")]
        public DateTimeOffset? Published { get; set; } = null;

        /// <summary>
        /// Gets or sets the blog URL path.
        /// </summary>
        public string UrlPath { get; set; }
    }

    /// <summary>
    /// Preferred compatibility alias for <see cref="BlogStreamViewModel"/>.
    /// </summary>
    public class BlogViewModel : BlogStreamViewModel
    {
    }

    /// <summary>
    /// Container view model for posts within a blog.
    /// </summary>
    /// <remarks>
    /// Used to transfer blog metadata along with a list of post projections to list pages.
    /// </remarks>
    public class BlogEntriesListViewModel
    {
        /// <summary>
        /// Gets or sets the blog key.
        /// </summary>
        public string BlogKey { get; set; }

        /// <summary>
        /// Gets or sets the blog title.
        /// </summary>
        public string BlogTitle { get; set; }

        /// <summary>
        /// Gets or sets the blog description.
        /// </summary>
        public string BlogDescription { get; set; }

        /// <summary>
        /// Gets or sets the hero image URL or path for the blog.
        /// </summary>
        public string HeroImage { get; set; }

        /// <summary>
        /// Gets or sets the blog URL path.
        /// </summary>
        public string BlogUrlPath { get; set; }

        /// <summary>
        /// Gets or sets the list of posts belonging to the blog.
        /// </summary>
        public List<BlogEntryListItem> Entries { get; set; } = new();
    }

    /// <summary>
    /// Preferred compatibility alias for <see cref="BlogEntriesListViewModel"/>.
    /// </summary>
    public class BlogPostsListViewModel : BlogEntriesListViewModel
    {
    }

    /// <summary>
    /// Form model for creating or editing a blog post.
    /// </summary>
    /// <remarks>
    /// This view model is intended for use in editor forms. Validation attributes on properties
    /// communicate constraints enforced by the UI and server model binding.
    /// </remarks>
    public class BlogEntryEditViewModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the blog post.
        /// </summary>
        /// <remarks>Null when creating a new post.</remarks>
        public Guid? Id { get; set; }

        /// <summary>
        /// Gets or sets the article number for the post (if assigned).
        /// </summary>
        public int? ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the blog key this post belongs to.
        /// </summary>
        public string BlogKey { get; set; }

        /// <summary>
        /// Gets or sets the post title.
        /// </summary>
        /// <remarks>Required with a maximum length of 254 characters.</remarks>
        [Required]
        [MaxLength(254)]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the introduction (teaser) text.
        /// </summary>
        /// <remarks>Optional; maximum length of 512 characters.</remarks>
        [MaxLength(512)]
        [Display(Name = "Introduction (teaser)")]
        public string Introduction { get; set; }

        /// <summary>
        /// Gets or sets the main content of the post (HTML expected).
        /// </summary>
        [Display(Name = "Content (HTML)")]
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the banner image URL or path for the post.
        /// </summary>
        [Display(Name = "Banner Image (URL / Path)")]
        public string BannerImage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to publish the post immediately.
        /// </summary>
        [Display(Name = "Publish Now?")]
        public bool PublishNow { get; set; }

        /// <summary>
        /// Gets or sets the scheduled publishing date/time for the post.
        /// </summary>
        /// <remarks>
        /// If <see cref="PublishNow"/> is true this may be ignored by server-side logic.
        /// </remarks>
        [Display(Name = "Publishing Date/Time")]
        public DateTimeOffset? Published { get; set; }
    }

    /// <summary>
    /// Preferred compatibility alias for <see cref="BlogEntryEditViewModel"/>.
    /// </summary>
    public class BlogPostEditViewModel : BlogEntryEditViewModel
    {
    }
}
