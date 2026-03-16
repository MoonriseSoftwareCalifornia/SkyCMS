// <copyright file="UpdateBlogPostCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.UpdatePost
{
    using Cosmos.Common.Features.Shared;
    using System;

    /// <summary>
    /// Command to update an existing blog post.
    /// Creates a new version of the article with updated content.
    /// </summary>
    public class UpdateBlogPostCommand : ICommand<CommandResult<UpdateBlogPostCommandResult>>
    {
        /// <summary>
        /// Gets or sets the blog post article number (logical identifier).
        /// Used to identify which post to update.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the blog post title.
        /// Required.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the blog post content (HTML).
        /// Required. Contains the full body of the blog post.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the introduction/excerpt for the blog post.
        /// Optional.
        /// </summary>
        public string? Introduction { get; set; }

        /// <summary>
        /// Gets or sets the banner/hero image URL for the blog post.
        /// Optional.
        /// </summary>
        public string? BannerImage { get; set; }

        /// <summary>
        /// Gets or sets the publication date/time.
        /// If null, the post remains unpublished or is unpublished.
        /// If set, the post is published at that date.
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the user ID updating this blog post.
        /// Used for audit trail.
        /// </summary>
        public Guid UserId { get; set; }
    }

    /// <summary>
    /// Result of updating a blog post.
    /// </summary>
    public class UpdateBlogPostCommandResult
    {
        /// <summary>
        /// Gets or sets the updated blog post ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the article number.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the version number of this update.
        /// </summary>
        public int VersionNumber { get; set; }

        /// <summary>
        /// Gets or sets the URL path of the blog post.
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;
    }
}
