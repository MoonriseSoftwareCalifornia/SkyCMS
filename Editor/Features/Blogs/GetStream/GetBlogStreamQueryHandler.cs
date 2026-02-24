// <copyright file="GetBlogStreamQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Blogs.GetStream
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
    /// Handler for retrieving blog stream articles for editing or display.
    /// </summary>
    public class GetBlogStreamQueryHandler : IQueryHandler<GetBlogStreamQuery, CommandResult<GetBlogStreamQueryResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<GetBlogStreamQueryHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetBlogStreamQueryHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public GetBlogStreamQueryHandler(
            ApplicationDbContext dbContext,
            ILogger<GetBlogStreamQueryHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the get blog stream query.
        /// </summary>
        /// <param name="query">Blog stream query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with blog stream data.</returns>
        public async Task<CommandResult<GetBlogStreamQueryResult>> HandleAsync(
            GetBlogStreamQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (query.Id == Guid.Empty)
            {
                logger.LogWarning("GetBlogStream called with empty ID");
                return CommandResult<GetBlogStreamQueryResult>.Failure("Blog stream ID is required.");
            }

            try
            {
                // Get the latest version of the blog stream article
                var article = await dbContext.Articles
                    .Where(a => a.Id == query.Id && 
                                a.ArticleType == (int)ArticleType.BlogStream &&
                                a.StatusCode != (int)StatusCodeEnum.Deleted)
                    .OrderByDescending(a => a.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken);

                if (article == null)
                {
                    logger.LogWarning("Blog stream {Id} not found", query.Id);
                    return CommandResult<GetBlogStreamQueryResult>.Failure($"Blog stream with ID '{query.Id}' not found.");
                }

                var result = new GetBlogStreamQueryResult
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
                    "Retrieved blog stream {Id} (Title: {Title}, BlogKey: {BlogKey})",
                    query.Id,
                    article.Title,
                    article.BlogKey);

                return CommandResult<GetBlogStreamQueryResult>.Success(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving blog stream {Id}", query.Id);
                return CommandResult<GetBlogStreamQueryResult>.Failure($"Error retrieving blog stream: {ex.Message}");
            }
        }
    }
}
