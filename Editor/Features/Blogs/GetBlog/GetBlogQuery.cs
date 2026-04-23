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
