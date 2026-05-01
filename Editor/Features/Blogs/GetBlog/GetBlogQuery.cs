// <copyright file="GetBlogQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.GetBlog
{
    using System;
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Query to retrieve a blog by ID for editing or display.
    /// </summary>
    public class GetBlogQuery : IQuery<CommandResult<GetBlogQueryResult>>
    {
        /// <summary>
        /// Gets or sets the blog ID (Article.Id).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID requesting the blog.
        /// </summary>
        public Guid? UserId { get; set; }
    }
}
