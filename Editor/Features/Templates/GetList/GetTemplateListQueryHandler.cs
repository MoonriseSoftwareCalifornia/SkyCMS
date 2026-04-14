// <copyright file="GetTemplateListQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.GetList
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Handler for retrieving paginated, sorted template lists.
    /// </summary>
    public class GetTemplateListQueryHandler : IQueryHandler<GetTemplateListQuery, CommandResult<GetTemplateListQueryResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<GetTemplateListQueryHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetTemplateListQueryHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Application database context.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public GetTemplateListQueryHandler(
            ApplicationDbContext dbContext,
            ILogger<GetTemplateListQueryHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the get template list query.
        /// </summary>
        /// <param name="query">Template list query parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Command result with paginated template list.</returns>
        public async Task<CommandResult<GetTemplateListQueryResult>> HandleAsync(
            GetTemplateListQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            try
            {
                // Get templates query
                var templatesQuery = dbContext.Templates.AsQueryable();

                // Apply layout filter if specified
                if (query.LayoutId.HasValue)
                {
                    templatesQuery = templatesQuery.Where(t => t.LayoutId == query.LayoutId.Value);
                }

                // Get total count before pagination
                var totalCount = await templatesQuery.CountAsync(cancellationToken);

                // Join with layouts and project to view model
                var dataQuery = from t in templatesQuery
                                join l in dbContext.Layouts on t.LayoutId equals l.Id into layoutGroup
                                from layout in layoutGroup.DefaultIfEmpty()
                                select new TemplateListItemViewModel
                                {
                                    Id = t.Id,
                                    Title = t.Title,
                                    Description = t.Description ?? string.Empty,
                                    LayoutName = layout != null ? layout.LayoutName : "No Layout",
                                    UsesHtmlEditor = t.Content.ToLower().Contains(" contenteditable=") ||
                                                    t.Content.ToLower().Contains(" data-ccms-ceid=")
                                };

                // Apply sorting
                dataQuery = ApplySorting(dataQuery, query.SortOrder, query.CurrentSort);

                // Apply pagination
                var templates = await dataQuery
                    .Skip(query.PageNo * query.PageSize)
                    .Take(query.PageSize)
                    .ToListAsync(cancellationToken);

                var result = new GetTemplateListQueryResult
                {
                    Templates = templates,
                    TotalCount = totalCount
                };

                logger.LogInformation(
                    "Retrieved {Count} templates (page {PageNo}, total {TotalCount})",
                    templates.Count,
                    query.PageNo,
                    totalCount);

                return CommandResult<GetTemplateListQueryResult>.Success(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving template list");
                return CommandResult<GetTemplateListQueryResult>.Failure($"Error retrieving templates: {ex.Message}");
            }
        }

        private IQueryable<TemplateListItemViewModel> ApplySorting(
            IQueryable<TemplateListItemViewModel> query,
            string sortOrder,
            string currentSort)
        {
            var isDescending = sortOrder?.ToLower() == "desc";

            return currentSort?.ToLower() switch
            {
                "layoutname" => isDescending
                    ? query.OrderByDescending(t => t.LayoutName)
                    : query.OrderBy(t => t.LayoutName),
                "description" => isDescending
                    ? query.OrderByDescending(t => t.Description)
                    : query.OrderBy(t => t.Description),
                "title" or _ => isDescending
                    ? query.OrderByDescending(t => t.Title)
                    : query.OrderBy(t => t.Title),
            };
        }
    }
}
