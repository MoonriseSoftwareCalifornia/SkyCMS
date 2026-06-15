// <copyright file="DeleteBlogHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.DeleteBlog
{
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Features.Articles.Delete;
    using Sky.Editor.Features.Blogs.DeleteStream;

    /// <summary>
    /// Handler for deleting blogs with cascade deletion of all associated blog posts.
    /// </summary>
    public class DeleteBlogHandler : DeleteBlogStreamHandler, ICommandHandler<DeleteBlogCommand, CommandResult<bool>>
    {
        public DeleteBlogHandler(
            ApplicationDbContext dbContext,
            ICommandHandler<DeleteArticleCommand, CommandResult<Unit>> deleteHandler,
            ILogger<DeleteBlogStreamHandler> logger)
            : base(dbContext, deleteHandler, logger)
        {
        }

        public Task<CommandResult<bool>> HandleAsync(DeleteBlogCommand command, CancellationToken cancellationToken = default)
            => base.HandleAsync(command, cancellationToken);
    }
}
