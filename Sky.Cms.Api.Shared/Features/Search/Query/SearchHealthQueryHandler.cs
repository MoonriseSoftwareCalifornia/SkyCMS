// <copyright file="SearchHealthQueryHandler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Api.Shared.Features.Search.Query;

using System.Reflection;
using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services.Configurations;
using Cosmos.DynamicConfig;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sky.Cms.Api.Shared.Models.Search;

/// <summary>
/// Handler for search health queries.
/// </summary>
public class SearchHealthQueryHandler : IQueryHandler<SearchHealthQuery, SearchHealthApiResponse>
{
    private readonly ApplicationDbContext dbContext;
    private readonly IDynamicConfigurationProvider configurationProvider;
    private readonly ILogger<SearchHealthQueryHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchHealthQueryHandler"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="configurationProvider">Configuration provider for tenant resolution.</param>
    /// <param name="logger">Logger.</param>
    public SearchHealthQueryHandler(
        ApplicationDbContext dbContext,
        IDynamicConfigurationProvider configurationProvider,
        ILogger<SearchHealthQueryHandler> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handle the search health query.
    /// </summary>
    /// <param name="request">Search health query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search health status.</returns>
    public async Task<SearchHealthApiResponse> HandleAsync(SearchHealthQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantDomain = configurationProvider.GetTenantDomainNameFromRequest();
            
            // Test database connectivity
            var canConnectToDatabase = await TestDatabaseConnectionAsync(tenantDomain, cancellationToken);
            
            // Get some basic metrics
            var metrics = await GetHealthMetricsAsync(tenantDomain, cancellationToken);

            var isHealthy = canConnectToDatabase;
            var statusMessage = isHealthy ? "Search service is healthy" : "Search service has issues";

            return new SearchHealthApiResponse
            {
                IsHealthy = isHealthy,
                StatusMessage = statusMessage,
                Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
                LastChecked = DateTime.UtcNow,
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking search service health");
            
            return new SearchHealthApiResponse
            {
                IsHealthy = false,
                StatusMessage = $"Search service health check failed: {ex.Message}",
                Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
                LastChecked = DateTime.UtcNow,
                Metrics = new Dictionary<string, object>()
            };
        }
    }

    private async Task<bool> TestDatabaseConnectionAsync(string tenantDomain, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.Articles
                .Take(1)
                .ToListAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database connectivity test failed");
            return false;
        }
    }

    private async Task<Dictionary<string, object>> GetHealthMetricsAsync(string tenantDomain, CancellationToken cancellationToken)
    {
        var metrics = new Dictionary<string, object>();

        try
        {
            // Get article count
            var totalArticles = await dbContext.Articles
                .CountAsync(cancellationToken);

            var publishedArticles = await dbContext.Articles
                .Where(a => a.StatusCode == 0)
                .CountAsync(cancellationToken);

            metrics["TotalArticles"] = totalArticles;
            metrics["PublishedArticles"] = publishedArticles;
            metrics["TenantDomain"] = tenantDomain;
            metrics["DatabaseProvider"] = dbContext.Database.ProviderName ?? "Unknown";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error collecting health metrics");
            metrics["MetricsError"] = ex.Message;
        }

        return metrics;
    }
}