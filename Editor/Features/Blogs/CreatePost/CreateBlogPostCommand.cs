// <copyright file="CreateBlogPostCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.CreatePost
{
    using System;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to create a new blog post within an existing blog stream.
    /// </summary>
    public class CreateBlogPostCommand : ICommand<CommandResult<CreateBlogPostCommandResult>>
    {
        /// <summary>
        /// Gets or sets the blog key (stream identifier) this post belongs to.
        /// Must reference an existing blog stream.
        /// Example: "cat-wash"
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title of the blog post.
        /// Required. Will be normalized to create the UrlPath.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog post content (HTML).
        /// Optional. Contains the full body of the blog post. Can be empty for drafts.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the introduction/excerpt for the blog post.
        /// Optional. Used for blog listings and previews.
        /// </summary>
        public string? Introduction { get; set; }

        /// <summary>
        /// Gets or sets the banner/hero image URL for the blog post.
        /// Optional. Used for visual presentation in listings and post header.
        /// </summary>
        public string? BannerImage { get; set; }

        /// <summary>
        /// Gets or sets the template ID to use for this blog post.
        /// Determines the layout and styling of the published post.
        /// </summary>
        public Guid TemplateId { get; set; } = Guid.Empty;

        /// <summary>
        /// Gets or sets the user ID creating this blog post.
        /// Used for audit trail and attribution.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the publication date/time (optional).
        /// If null, the post is created as a draft.
        /// If set, the post is immediately published.
        /// </summary>
        public DateTimeOffset? Published { get; set; }
    }

    /// <summary>
    /// Result of creating a blog post.
    /// </summary>
    public class CreateBlogPostCommandResult
    {
        /// <summary>
        /// Gets or sets the created blog post ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the article number (logical identifier shared across versions).
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the URL path of the created blog post.
        /// Format: "stream-key/post-slug"
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog key this post belongs to.
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;
    }
}
