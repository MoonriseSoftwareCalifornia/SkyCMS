using Cosmos.Common.Services.Search.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cosmos.Common.Services.Search.Extensions;

/// <summary>
/// Extension methods for configuring hybrid search services in SkyCMS
/// </summary>
public static class SearchServiceExtensions
{
    /// <summary>
    /// Add hybrid search services to the service collection
    /// Automatically configures Azure AI Search and Lucene.NET based on configuration
    /// </summary>
    public static IServiceCollection AddSkyCmsSearch(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure options
        services.Configure<AzureSearchOptions>(configuration.GetSection(AzureSearchOptions.SectionName));
        services.Configure<LuceneSearchOptions>(configuration.GetSection(LuceneSearchOptions.SectionName));
        services.Configure<HybridSearchOptions>(configuration.GetSection(HybridSearchOptions.SectionName));

        // Register concrete search implementations
        services.AddSingleton<AzureSearchService>();
        services.AddSingleton<LuceneSearchService>();
        services.AddSingleton<HybridSearchService>();

        // Register primary search interface - use hybrid by default
        services.TryAddSingleton<ISearchService>(provider => provider.GetRequiredService<HybridSearchService>());

        return services;
    }

    /// <summary>
    /// Add Azure AI Search only (no fallback)
    /// </summary>
    public static IServiceCollection AddAzureSearch(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureSearchOptions>(configuration.GetSection(AzureSearchOptions.SectionName));
        services.AddSingleton<AzureSearchService>();
        services.TryAddSingleton<ISearchService>(provider => provider.GetRequiredService<AzureSearchService>());
        
        return services;
    }

    /// <summary>
    /// Add Lucene.NET search only (for edge/development scenarios)
    /// </summary>
    public static IServiceCollection AddLuceneSearch(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LuceneSearchOptions>(configuration.GetSection(LuceneSearchOptions.SectionName));
        services.AddSingleton<LuceneSearchService>();
        services.TryAddSingleton<ISearchService>(provider => provider.GetRequiredService<LuceneSearchService>());
        
        return services;
    }

    /// <summary>
    /// Add search health checks
    /// </summary>
    public static IServiceCollection AddSearchHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<SearchServiceHealthCheck>("search-service");
            
        return services;
    }
}

/// <summary>
/// Health check for search services
/// </summary>
public class SearchServiceHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly ISearchService _searchService;

    public SearchServiceHealthCheck(ISearchService searchService)
    {
        _searchService = searchService;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _searchService.HealthCheckAsync(cancellationToken);
            
            return isHealthy 
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Search service is healthy")
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Search service is not responding");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Search service health check failed", ex);
        }
    }
}