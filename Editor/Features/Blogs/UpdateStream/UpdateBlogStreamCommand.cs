// <copyright file="UpdateBlogStreamCommand.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.UpdateStream
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using System;

    /// <summary>
    /// Command to update blog stream metadata and properties.
    /// Handles title changes, URL updates, and blog stream HTML regeneration.
    /// </summary>
    public class UpdateBlogStreamCommand : ICommand<CommandResult<Article>>
    {
        /// <summary>
        /// Gets or sets the blog stream article ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the new title for the blog stream.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description/introduction for the blog stream.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hero/banner image URL.
        /// </summary>
        public string HeroImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the published date.
        /// </summary>
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Gets or sets the user ID performing the update.
        /// Used for audit/tracking purposes.
        /// </summary>
        public Guid UserId { get; set; }
    }
}
