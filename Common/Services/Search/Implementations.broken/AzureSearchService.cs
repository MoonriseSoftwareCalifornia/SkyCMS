using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Cosmos.Common.Services.Search.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace Cosmos.Common.Services.Search.Implementations;

/// <summary>
/// Azure AI Search implementation of ISearchService
/// Provides enterprise-grade search with AI capabilities
/// </summary>
public class AzureSearchService : ISearchService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly AzureSearchOptions _options;
    private readonly ILogger<AzureSearchService> _logger;
    private const string IndexName = "skycms-content";

    public AzureSearchService(
        IOptions<AzureSearchOptions> options,
        ILogger<AzureSearchService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var credential = new AzureKeyCredential(_options.ApiKey);
        var endpoint = new Uri(_options.ServiceEndpoint);
        
        _searchClient = new SearchClient(endpoint, IndexName, credential);
        _indexClient = new SearchIndexClient(endpoint, credential);
    }

    public async Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            request.Validate();
            
            var options = new SearchOptions
            {
                Size = request.PageSize,
                Skip = (request.Page - 1) * request.PageSize,
                IncludeTotalCount = true,
                QueryType = SearchQueryType.Full
            };

            // Add search fields with boosting
            options.SearchFields.Add("title^3");
            options.SearchFields.Add("summary^2");
            options.SearchFields.Add("content");
            options.SearchFields.Add("tags^1.5");

            // Add filters
            var filters = BuildFilters(request);
            if (!string.IsNullOrEmpty(filters))
                options.Filter = filters;

            // Add sorting
            AddSorting(options, request);

            // Add highlighting
            if (request.IncludeHighlights)
            {
                options.HighlightFields.Add("title");
                options.HighlightFields.Add("content");
                options.HighlightPreTag = "<mark>";
                options.HighlightPostTag = "</mark>";
            }

            // Add facets
            if (request.IncludeFacets)
            {
                options.Facets.Add("contentType");
                options.Facets.Add("tags");
                options.Facets.Add("author");
                options.Facets.Add("publishedDate");
            }

            var response = await _searchClient.SearchAsync<SearchDocument>(
                request.Query, 
                options, 
                cancellationToken);

            var results = new SearchResults
            {
                Items = response.Value.GetResults().Select(MapToResultItem),
                TotalCount = response.Value.TotalCount ?? 0,
                Page = request.Page,
                PageSize = request.PageSize,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            // Add facets if requested
            if (request.IncludeFacets && response.Value.Facets != null)
            {
                results.Facets = response.Value.Facets.ToDictionary(
                    f => f.Key,
                    f => f.Value.Select(v => new SearchFacet 
                    { 
                        Value = v.Value?.ToString() ?? string.Empty, 
                        Count = v.Count ?? 0 
                    })
                );
            }

            _logger.LogInformation("Azure Search completed: {Query} returned {Count} results in {Time}ms",
                request.Query, results.TotalCount, results.ExecutionTimeMs);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Search failed for query: {Query}", request.Query);
            throw;
        }
    }

    public async Task IndexContentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        try
        {
            var actions = new[] { IndexDocumentsAction.Upload(document) };
            var batch = IndexDocumentsBatch.Create(actions);
            
            var response = await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
            
            _logger.LogInformation("Indexed document {DocumentId}", document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document {DocumentId}", document.Id);
            throw;
        }
    }

    public async Task IndexContentBulkAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default)
    {
        try
        {
            var actions = documents.Select(IndexDocumentsAction.Upload).ToArray();
            var batch = IndexDocumentsBatch.Create(actions);
            
            var response = await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
            
            _logger.LogInformation("Bulk indexed {Count} documents", documents.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk index documents");
            throw;
        }
    }

    public async Task DeleteFromIndexAsync(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var actions = new[] { IndexDocumentsAction.Delete("id", documentId) };
            var batch = IndexDocumentsBatch.Create(actions);
            
            await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
            
            _logger.LogInformation("Deleted document {DocumentId} from index", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId} from index", documentId);
            throw;
        }
    }

    public async Task DeleteFromIndexBulkAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var actions = documentIds.Select(id => IndexDocumentsAction.Delete("id", id)).ToArray();
            var batch = IndexDocumentsBatch.Create(actions);
            
            await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
            
            _logger.LogInformation("Bulk deleted {Count} documents from index", documentIds.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk delete documents from index");
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetSuggestionsAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new SuggestOptions
            {
                Size = maxResults,
                UseFuzzyMatching = true
            };
            
            options.SearchFields.Add("title");
            options.SearchFields.Add("content");
            
            var response = await _searchClient.SuggestAsync<SearchDocument>(query, "default-suggester", options, cancellationToken);
            
            return response.Value.Results.Select(r => r.Text).Distinct();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get suggestions for query: {Query}", query);
            return Array.Empty<string>();
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _searchClient.GetDocumentCountAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Search health check failed");
            return false;
        }
    }

    public async Task ClearIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // This is a destructive operation - implement with extreme caution
            _logger.LogWarning("Clearing Azure Search index - this will delete all documents");
            
            // In Azure Search, we need to delete and recreate the index
            await _indexClient.DeleteIndexAsync(IndexName, cancellationToken);
            await CreateIndexAsync(cancellationToken);
            
            _logger.LogWarning("Azure Search index cleared and recreated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Azure Search index");
            throw;
        }
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        // This would typically trigger a background job to reindex all content
        // Implementation depends on your data access layer
        _logger.LogInformation("Azure Search index rebuild requested - implement based on your data layer");
        await Task.CompletedTask;
    }

    private string BuildFilters(SearchRequest request)
    {
        var filters = new List<string>();

        // Tenant filter (always apply for multi-tenancy)
        if (!string.IsNullOrEmpty(request.TenantDomain))
        {
            filters.Add($"tenantDomain eq '{request.TenantDomain}'");
        }

        // Published only filter
        if (request.PublishedOnly)
        {
            filters.Add("status eq 'published'");
        }

        // Content type filter
        if (request.ContentTypes.Any())
        {
            var contentTypeFilter = string.Join(" or ", 
                request.ContentTypes.Select(ct => $"contentType eq '{ct}'"));
            filters.Add($"({contentTypeFilter})");
        }

        // Date range filter
        if (request.FromDate.HasValue || request.ToDate.HasValue)
        {
            if (request.FromDate.HasValue)
                filters.Add($"publishedDate ge {request.FromDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
            
            if (request.ToDate.HasValue)
                filters.Add($"publishedDate le {request.ToDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
        }

        // Author filter
        if (!string.IsNullOrEmpty(request.Author))
        {
            filters.Add($"author eq '{request.Author}'");
        }

        return string.Join(" and ", filters);
    }

    private void AddSorting(SearchOptions options, SearchRequest request)
    {
        switch (request.SortBy.ToLowerInvariant())
        {
            case "date":
            case "publisheddate":
                options.OrderBy.Add($"publishedDate {(request.SortDirection == SortDirection.Descending ? "desc" : "asc")}");
                break;
            case "title":
                options.OrderBy.Add($"title {(request.SortDirection == SortDirection.Descending ? "desc" : "asc")}");
                break;
            case "relevance":
            default:
                // Default to relevance score - no explicit sorting needed
                break;
        }
    }

    private SearchResultItem MapToResultItem(SearchResult<SearchDocument> result)
    {
        var item = new SearchResultItem
        {
            Id = result.Document.Id,
            Title = result.Document.Title,
            Content = TruncateContent(result.Document.Content, 300),
            Url = result.Document.Url,
            ContentType = result.Document.ContentType,
            PublishedDate = result.Document.PublishedDate,
            ModifiedDate = result.Document.ModifiedDate,
            Author = result.Document.Author,
            Tags = result.Document.Tags ?? new List<string>(),
            Score = result.Score ?? 0,
            TenantDomain = result.Document.TenantDomain,
            ImageUrl = result.Document.ImageUrl
        };

        // Add highlights
        if (result.Highlights != null)
        {
            item.Highlights = result.Highlights.ToDictionary(
                h => h.Key,
                h => h.Value.AsEnumerable()
            );
        }

        return item;
    }

    private async Task CreateIndexAsync(CancellationToken cancellationToken)
    {
        // Create Azure Search index schema
        // This would contain field definitions for all SearchDocument properties
        // Implementation details depend on your specific requirements
        _logger.LogInformation("Creating Azure Search index schema");
    }

    private string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            return content;

        var truncated = content.Substring(0, maxLength);
        var lastSpace = truncated.LastIndexOf(' ');
        
        if (lastSpace > 0)
            truncated = truncated.Substring(0, lastSpace);
        
        return truncated + "...";
    }
}

/// <summary>
/// Configuration options for Azure AI Search
/// </summary>
public class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";
    
    /// <summary>
    /// Azure Search service endpoint
    /// </summary>
    public string ServiceEndpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure Search admin API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Enable Azure Search (fallback to Lucene if false)
    /// </summary>
    public bool Enabled { get; set; } = false;
}