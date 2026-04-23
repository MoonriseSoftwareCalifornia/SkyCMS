namespace Sky.Editor.Features.Blogs.UpdateBlog
{
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Blogs.UpdateStream;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Titles;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Services.BlogPublishing;

    /// <summary>
    /// Handler for updating blog metadata and properties.
    /// </summary>
    public class UpdateBlogHandler : UpdateBlogStreamHandler, ICommandHandler<UpdateBlogCommand, CommandResult<Article>>
    {
        public UpdateBlogHandler(
            ApplicationDbContext dbContext,
            ISlugService slugService,
            ITitleChangeService titleChangeService,
            IBlogStreamRenderingService blogRenderingService,
            ArticleEditLogic articleLogic,
            ILogger<UpdateBlogStreamHandler> logger)
            : base(dbContext, slugService, titleChangeService, blogRenderingService, articleLogic, logger)
        {
        }

        public Task<CommandResult<Article>> HandleAsync(UpdateBlogCommand command, CancellationToken cancellationToken = default)
            => base.HandleAsync(command, cancellationToken);
    }
}
