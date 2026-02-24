// <copyright file="GetTemplateQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.Get
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
    /// Handles template retrieval queries.
    /// Supports retrieval with optional page design versions.
    /// Database-provider agnostic: uses standard EF Core LINQ patterns compatible with
    /// Azure Cosmos DB, SQL Server, Azure SQL, MySQL, and SQLite.
    /// </summary>
    /// <remarks>
    /// Performance Notes:
    /// - Uses <see cref="AsNoTracking"/> for read-only operations to reduce memory overhead
    /// - Avoids N+1 queries by using conditional includes
    /// - Applies ordering at the database level when possible
    /// </remarks>
    public class GetTemplateQueryHandler : IQueryHandler<GetTemplateQuery, CommandResult<GetTemplateQueryResult>>
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<GetTemplateQueryHandler> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetTemplateQueryHandler"/> class.
        /// </summary>
        /// <param name="dbContext">Application database context (supports all providers).</param>
        /// <param name="logger">Logger service for diagnostics.</param>
        /// <exception cref="ArgumentNullException">Thrown if dbContext or logger is null.</exception>
        public GetTemplateQueryHandler(
            ApplicationDbContext dbContext,
            ILogger<GetTemplateQueryHandler> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles the get template query asynchronously.
        /// </summary>
        /// <param name="query">The template retrieval query.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>
        /// A <see cref="CommandResult{GetTemplateQueryResult}"/> containing:
        /// - Success: The template and optionally its versions
        /// - Failure: Error message (template not found, etc.)
        /// </returns>
        /// <remarks>
        /// This method is compatible across all supported database providers.
        /// It uses standard EF Core patterns without provider-specific extensions.
        /// </remarks>
        public async Task<CommandResult<GetTemplateQueryResult>> HandleAsync(
            GetTemplateQuery query,
            CancellationToken cancellationToken = default)
        {
            // Input validation
            if (query == null)
            {
                logger.LogWarning("GetTemplateQueryHandler: Query object is null");
                return CommandResult<GetTemplateQueryResult>.Failure("Query cannot be null");
            }

            if (query.TemplateId == Guid.Empty)
            {
                logger.LogWarning("GetTemplateQueryHandler: TemplateId is empty");
                return CommandResult<GetTemplateQueryResult>.Failure("Template ID cannot be empty");
            }

            try
            {
                // Retrieve template from database
                // AsNoTracking: Works across all providers (Cosmos, SQL Server, MySQL, SQLite)
                // Improves performance for read-only operations
                var template = await dbContext.Templates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == query.TemplateId, cancellationToken);

                if (template == null)
                {
                    logger.LogWarning(
                        "GetTemplateQueryHandler: Template {TemplateId} not found",
                        query.TemplateId);
                    return CommandResult<GetTemplateQueryResult>.Failure(
                        $"Template with ID '{query.TemplateId}' not found");
                }

                logger.LogInformation(
                    "GetTemplateQueryHandler: Successfully retrieved template {TemplateId} '{TemplateName}'",
                    query.TemplateId,
                    template.Title);

                var result = new GetTemplateQueryResult { Template = template };

                // Optionally load page design versions
                if (query.IncludeVersions)
                {
                    result.Versions = await GetVersionsAsync(query.TemplateId, query.LatestVersionOnly, cancellationToken);
                    logger.LogDebug(
                        "GetTemplateQueryHandler: Loaded {VersionCount} version(s) for template {TemplateId}",
                        result.Versions.Count(),
                        query.TemplateId);
                }

                return CommandResult<GetTemplateQueryResult>.Success(result);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation(
                    "GetTemplateQueryHandler: Operation cancelled for template {TemplateId}",
                    query.TemplateId);
                return CommandResult<GetTemplateQueryResult>.Failure("Operation was cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "GetTemplateQueryHandler: Error retrieving template {TemplateId}",
                    query.TemplateId);
                return CommandResult<GetTemplateQueryResult>.Failure(
                    $"Error retrieving template: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves page design versions for a template.
        /// </summary>
        /// <param name="templateId">The template ID.</param>
        /// <param name="latestOnly">If true, returns only the latest version.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of page design versions.</returns>
        /// <remarks>
        /// Uses standard EF Core LINQ (no provider-specific extensions).
        /// OrderByDescending is evaluated at the database level across all providers.
        /// </remarks>
        private async Task<IEnumerable<PageDesignVersion>> GetVersionsAsync(
            Guid templateId,
            bool latestOnly,
            CancellationToken cancellationToken)
        {
            var versionsQuery = dbContext.PageDesignVersions
                .AsNoTracking()
                .Where(v => v.TemplateId == templateId)
                .OrderByDescending(v => v.Version);

            if (latestOnly)
            {
                var latestVersion = await versionsQuery
                    .FirstOrDefaultAsync(cancellationToken);

                return latestVersion != null ? new[] { latestVersion } : Array.Empty<PageDesignVersion>();
            }

            return await versionsQuery.ToListAsync(cancellationToken);
        }
    }
}
