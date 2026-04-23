namespace Sky.Editor.Features.Blogs.DeleteBlog
{
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Blogs.DeleteStream;

    /// <summary>
    /// Handler for deleting blogs with cascade deletion of all associated blog posts.
    /// </summary>
    public class DeleteBlogHandler : DeleteBlogStreamHandler, ICommandHandler<DeleteBlogCommand, CommandResult<bool>>
    {
        public DeleteBlogHandler(
            ApplicationDbContext dbContext,
            ArticleEditLogic articleLogic,
            ILogger<DeleteBlogStreamHandler> logger)
            : base(dbContext, articleLogic, logger)
        {
        }

        public Task<CommandResult<bool>> HandleAsync(DeleteBlogCommand command, CancellationToken cancellationToken = default)
            => base.HandleAsync(command, cancellationToken);
    }
}
