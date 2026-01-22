using Cosmos.Common.Services.Search.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cosmos.Common.Services.Search.Implementations;

/// <summary>
/// Hybrid search service that can use Azure AI Search or Lucene.NET
/// Automatically falls back to Lucene if Azure is unavailable
/// Perfect for SkyCMS edge deployments and development environments
/// </summary>
public class HybridSearchService : ISearchService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HybridSearchService> _logger;
    private readonly HybridSearchOptions _options;
    
    private ISearchService? _primaryService;
    private ISearchService? _fallbackService;
    private volatile bool _primaryServiceHealthy = true;
    private readonly object _serviceLock = new();

    public HybridSearchService(
        IServiceProvider serviceProvider,
        IOptions<HybridSearchOptions> options,
        ILogger<HybridSearchService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
        
        InitializeServices();
    }

    public async Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var service = await GetHealthySearchServiceAsync(cancellationToken);
        
        try
        {
            var results = await service.SearchAsync(request, cancellationToken);
            
            _logger.LogDebug("Search completed using {ServiceType} for query: {Query}",
                service.GetType().Name, request.Query);
                
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed using {ServiceType} for query: {Query}",
                service.GetType().Name, request.Query);
                
            // If primary service fails, try fallback
            if (service == _primaryService && _fallbackService != null && _options.EnableFallback)
            {
                _logger.LogWarning("Primary search service failed, trying fallback service");
                _primaryServiceHealthy = false;
                
                try
                {
                    return await _fallbackService.SearchAsync(request, cancellationToken);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback search service also failed");
                    throw;
                }
            }
            
            throw;
        }
    }

    public async Task IndexContentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        // Index to all available services
        var tasks = new List<Task>();
        
        if (_primaryService != null)
        {
            tasks.Add(IndexWithErrorHandling(_primaryService, document, "primary", cancellationToken));
        }
        
        if (_fallbackService != null && _options.IndexToBothServices)
        {
            tasks.Add(IndexWithErrorHandling(_fallbackService, document, "fallback", cancellationToken));
        }
        
        await Task.WhenAll(tasks);
    }

    public async Task IndexContentBulkAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default)
    {
        // Bulk index to all available services
        var tasks = new List<Task>();
        
        if (_primaryService != null)
        {
            tasks.Add(BulkIndexWithErrorHandling(_primaryService, documents, "primary", cancellationToken));
        }
        
        if (_fallbackService != null && _options.IndexToBothServices)
        {
            tasks.Add(BulkIndexWithErrorHandling(_fallbackService, documents, "fallback", cancellationToken));
        }
        
        await Task.WhenAll(tasks);
    }

    public async Task DeleteFromIndexAsync(string documentId, CancellationToken cancellationToken = default)
    {
        // Delete from all available services
        var tasks = new List<Task>();
        
        if (_primaryService != null)
        {
            tasks.Add(DeleteWithErrorHandling(_primaryService, documentId, "primary", cancellationToken));
        }
        
        if (_fallbackService != null && _options.IndexToBothServices)
        {
            tasks.Add(DeleteWithErrorHandling(_fallbackService, documentId, "fallback", cancellationToken));
        }
        
        await Task.WhenAll(tasks);
    }

    public async Task DeleteFromIndexBulkAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        // Bulk delete from all available services
        var tasks = new List<Task>();
        
        if (_primaryService != null)
        {
            tasks.Add(BulkDeleteWithErrorHandling(_primaryService, documentIds, "primary", cancellationToken));
        }
        
        if (_fallbackService != null && _options.IndexToBothServices)
        {
            tasks.Add(BulkDeleteWithErrorHandling(_fallbackService, documentIds, "fallback", cancellationToken));
        }
        
        await Task.WhenAll(tasks);
    }

    public async Task<IEnumerable<string>> GetSuggestionsAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        var service = await GetHealthySearchServiceAsync(cancellationToken);
        
        try
        {
            return await service.GetSuggestionsAsync(query, maxResults, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get suggestions using {ServiceType}", service.GetType().Name);
            
            // Try fallback for suggestions
            if (service == _primaryService && _fallbackService != null && _options.EnableFallback)
            {
                try
                {
                    return await _fallbackService.GetSuggestionsAsync(query, maxResults, cancellationToken);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback suggestions also failed");
                }
            }
            
            return Array.Empty<string>();
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var primaryHealthy = false;
        var fallbackHealthy = false;
        
        if (_primaryService != null)
        {
            try
            {
                primaryHealthy = await _primaryService.HealthCheckAsync(cancellationToken);
                _primaryServiceHealthy = primaryHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Primary search service health check failed");
                _primaryServiceHealthy = false;
            }
        }
        
        if (_fallbackService != null)
        {
            try
            {
                fallbackHealthy = await _fallbackService.HealthCheckAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallback search service health check failed");
            }
        }
        
        return primaryHealthy || fallbackHealthy;
    }

    public async Task ClearIndexAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Clearing search indexes - this will delete all documents");
        
        var tasks = new List<Task>();
        
        if (_primaryService != null)
        {
            tasks.Add(_primaryService.ClearIndexAsync(cancellationToken));
        }
        
        if (_fallbackService != null && _options.IndexToBothServices)
        {
            tasks.Add(_fallbackService.ClearIndexAsync(cancellationToken));
        }
        
        await Task.WhenAll(tasks);
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rebuilding search indexes");
        
        var tasks = new List<Task>();
        
        if (_primaryService != null)
        {
            tasks.Add(_primaryService.RebuildIndexAsync(cancellationToken));
        }
        
        if (_fallbackService != null && _options.IndexToBothServices)
        {
            tasks.Add(_fallbackService.RebuildIndexAsync(cancellationToken));
        }
        
        await Task.WhenAll(tasks);
    }

    private void InitializeServices()
    {
        lock (_serviceLock)
        {
            try
            {
                // Initialize primary service (Azure AI Search or Lucene based on config)
                if (_options.PreferAzureSearch)
                {
                    var azureOptions = _serviceProvider.GetService<IOptions<AzureSearchOptions>>();
                    if (azureOptions?.Value?.Enabled == true && !string.IsNullOrEmpty(azureOptions.Value.ServiceEndpoint))
                    {
                        _primaryService = _serviceProvider.GetRequiredService<AzureSearchService>();
                        _logger.LogInformation("Initialized Azure AI Search as primary search service");
                    }
                    else
                    {
                        _logger.LogWarning("Azure AI Search configured but not available, falling back to Lucene");
                        _primaryService = _serviceProvider.GetRequiredService<LuceneSearchService>();
                        _logger.LogInformation("Initialized Lucene.NET as primary search service");
                    }
                }
                else
                {
                    _primaryService = _serviceProvider.GetRequiredService<LuceneSearchService>();
                    _logger.LogInformation("Initialized Lucene.NET as primary search service");
                }

                // Initialize fallback service if enabled
                if (_options.EnableFallback)
                {
                    if (_primaryService is AzureSearchService)
                    {
                        _fallbackService = _serviceProvider.GetRequiredService<LuceneSearchService>();
                        _logger.LogInformation("Initialized Lucene.NET as fallback search service");
                    }
                    else if (_options.PreferAzureSearch) // Only set Azure as fallback if it was originally preferred
                    {
                        var azureOptions = _serviceProvider.GetService<IOptions<AzureSearchOptions>>();
                        if (azureOptions?.Value?.Enabled == true && !string.IsNullOrEmpty(azureOptions.Value.ServiceEndpoint))
                        {
                            _fallbackService = _serviceProvider.GetRequiredService<AzureSearchService>();
                            _logger.LogInformation("Initialized Azure AI Search as fallback search service");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize hybrid search services");
                throw;
            }
        }
    }

    private async Task<ISearchService> GetHealthySearchServiceAsync(CancellationToken cancellationToken)
    {
        // Check if primary service is healthy
        if (_primaryServiceHealthy && _primaryService != null)
        {
            return _primaryService;
        }
        
        // If primary is not healthy, perform actual health check
        if (_primaryService != null)
        {
            try
            {
                _primaryServiceHealthy = await _primaryService.HealthCheckAsync(cancellationToken);
                if (_primaryServiceHealthy)
                {
                    return _primaryService;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Primary search service health check failed");
                _primaryServiceHealthy = false;
            }
        }
        
        // Use fallback service if available
        if (_fallbackService != null && _options.EnableFallback)
        {
            _logger.LogInformation("Using fallback search service");
            return _fallbackService;
        }
        
        // If no healthy service, throw exception
        throw new InvalidOperationException("No healthy search service available");
    }

    private async Task IndexWithErrorHandling(ISearchService service, SearchDocument document, string serviceType, CancellationToken cancellationToken)
    {
        try
        {
            await service.IndexContentAsync(document, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document {DocumentId} using {ServiceType} service",
                document.Id, serviceType);
                
            if (serviceType == "primary")
            {
                _primaryServiceHealthy = false;
            }
        }
    }

    private async Task BulkIndexWithErrorHandling(ISearchService service, IEnumerable<SearchDocument> documents, string serviceType, CancellationToken cancellationToken)
    {
        try
        {
            await service.IndexContentBulkAsync(documents, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk index documents using {ServiceType} service", serviceType);
                
            if (serviceType == "primary")
            {
                _primaryServiceHealthy = false;
            }
        }
    }

    private async Task DeleteWithErrorHandling(ISearchService service, string documentId, string serviceType, CancellationToken cancellationToken)
    {
        try
        {
            await service.DeleteFromIndexAsync(documentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId} using {ServiceType} service",
                documentId, serviceType);
        }
    }

    private async Task BulkDeleteWithErrorHandling(ISearchService service, IEnumerable<string> documentIds, string serviceType, CancellationToken cancellationToken)
    {
        try
        {
            await service.DeleteFromIndexBulkAsync(documentIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk delete documents using {ServiceType} service", serviceType);
        }
    }

    public void Dispose()
    {
        if (_primaryService is IDisposable disposablePrimary)
        {
            disposablePrimary.Dispose();
        }
        
        if (_fallbackService is IDisposable disposableFallback)
        {
            disposableFallback.Dispose();
        }
    }
}

/// <summary>
/// Configuration options for hybrid search
/// </summary>
public class HybridSearchOptions
{
    public const string SectionName = "HybridSearch";
    
    /// <summary>
    /// Prefer Azure AI Search as primary (fallback to Lucene if unavailable)
    /// </summary>
    public bool PreferAzureSearch { get; set; } = true;
    
    /// <summary>
    /// Enable fallback to secondary search service
    /// </summary>
    public bool EnableFallback { get; set; } = true;
    
    /// <summary>
    /// Index content to both services (for redundancy)
    /// </summary>
    public bool IndexToBothServices { get; set; } = false;
}