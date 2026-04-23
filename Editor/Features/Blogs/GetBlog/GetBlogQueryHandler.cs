namespace Sky.Editor.Features.Blogs.GetBlog
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Handler for retrieving blog articles for editing or display.
    /// </summary>
    public class GetBlogQueryHandler : IQueryHandler<GetBlogQuery, CommandResult<GetBlogQueryResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<GetBlogQueryHandler> logger;

        public GetBlogQueryHandler(
            ApplicationDbContext dbContext,
            ILogger<GetBlogQueryHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CommandResult<GetBlogQueryResult>> HandleAsync(
            GetBlogQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (query.Id == Guid.Empty)
            {
                logger.LogWarning("GetBlog called with empty ID");
                return CommandResult<GetBlogQueryResult>.Failure("Blog ID is required.");
            }

            try
            {
                var blogStreamType = (int)ArticleType.BlogStream;
                var deletedStatusCode = (int)StatusCodeEnum.Deleted;
                var article = await dbContext.Articles
                    .Where(a => a.Id == query.Id &&
                                a.ArticleType == blogStreamType &&
                                a.StatusCode != deletedStatusCode)
                    .OrderByDescending(a => a.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken);

                if (article == null)
                {
                    logger.LogWarning("Blog {Id} not found", query.Id);
                    return CommandResult<GetBlogQueryResult>.Failure($"Blog with ID '{query.Id}' not found.");
                }

                var result = new GetBlogQueryResult
                {
                    Article = article,
                    Title = article.Title,
                    BlogKey = article.BlogKey,
                    Description = article.Introduction ?? string.Empty,
                    HeroImage = article.BannerImage ?? string.Empty,
                    Published = article.Published,
                    UrlPath = article.UrlPath
                };

                logger.LogInformation(
                    "Retrieved blog {Id} (Title: {Title}, BlogKey: {BlogKey})",
                    query.Id,
                    article.Title,
                    article.BlogKey);

                return CommandResult<GetBlogQueryResult>.Success(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving blog {Id}", query.Id);
                return CommandResult<GetBlogQueryResult>.Failure($"Error retrieving blog: {ex.Message}");
            }
        }
    }
}
