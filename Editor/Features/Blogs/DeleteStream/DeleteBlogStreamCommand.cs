// <copyright file="DeleteBlogStreamCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.DeleteStream
{
    using System;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Command to delete a blog stream and all its associated blog entries (cascade delete).
    /// </summary>
    public class DeleteBlogStreamCommand : ICommand<CommandResult<bool>>
    {
        /// <summary>
        /// Gets or sets the blog stream article ID to delete.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID performing the deletion.
        /// Used for audit/tracking purposes.
        /// </summary>
        public Guid UserId { get; set; }
    }
}
