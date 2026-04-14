// <copyright file="DeleteBlogPostCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.DeletePost
{
    using System;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to delete a blog post.
    /// Performs a soft delete by marking the article as deleted.
    /// </summary>
    public class DeleteBlogPostCommand : ICommand<CommandResult<DeleteBlogPostCommandResult>>
    {
        /// <summary>
        /// Gets or sets the blog post article number to delete.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets the blog key the post belongs to (for safety validation).
        /// </summary>
        public string BlogKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user ID deleting this blog post.
        /// Used for audit trail.
        /// </summary>
        public Guid UserId { get; set; }
    }

    /// <summary>
    /// Result of deleting a blog post.
    /// </summary>
    public class DeleteBlogPostCommandResult
    {
        /// <summary>
        /// Gets or sets the article number of the deleted post.
        /// </summary>
        public int ArticleNumber { get; set; }

        /// <summary>
        /// Gets or sets a message indicating successful deletion.
        /// </summary>
        public string Message { get; set; } = "Blog post deleted successfully.";
    }
}
