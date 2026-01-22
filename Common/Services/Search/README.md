# SkyCMS Hybrid Search Implementation

This implementation provides a flexible, hybrid search solution for SkyCMS that can seamlessly switch between Azure AI Search and Lucene.NET based on configuration and availability.

## Features

- **Hybrid Architecture**: Automatically falls back between Azure AI Search and Lucene.NET
- **Multi-tenant Support**: Built-in tenant isolation for multi-tenant deployments
- **Edge Deployment Ready**: Lucene.NET for offline/edge scenarios
- **Cloud Scale**: Azure AI Search for enterprise scenarios
- **Health Monitoring**: Built-in health checks and monitoring
- **Background Indexing**: Async indexing with bulk operations
- **Search Highlighting**: Configurable search result highlighting
- **Auto-complete/Suggestions**: Intelligent search suggestions

## Architecture

```
┌─────────────────┐
│ HybridSearchService │
└─────────┬───────┘
          │
    ┌─────┴─────┐
    │           │
┌───▼────┐ ┌───▼────┐
│ Azure  │ │ Lucene │
│ AI     │ │ .NET   │
│ Search │ │ Search │
└────────┘ └────────┘
```

## Configuration

### appsettings.json

```json
{
  "HybridSearch": {
    "PreferAzureSearch": true,
    "EnableFallback": true,
    "IndexToBothServices": false
  },
  "AzureSearch": {
    "Enabled": true,
    "ServiceEndpoint": "https://your-search-service.search.windows.net",
    "ApiKey": "your-api-key-here"
  },
  "LuceneSearch": {
    "Enabled": true,
    "IndexPath": "./App_Data/SearchIndex",
    "UseMemoryIndex": false
  }
}
```

### Dependency Injection

```csharp
// In Program.cs or Startup.cs
services.AddSkyCmsSearch(configuration);

// Add health checks (optional)
services.AddSearchHealthChecks();
```

## Usage Examples

### Basic Search

```csharp
public class ArticleController : Controller
{
    private readonly ISearchService _searchService;

    public ArticleController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    public async Task<IActionResult> Search(string query)
    {
        var request = new SearchRequest
        {
            Query = query,
            TenantDomain = GetCurrentTenantDomain(),
            PublishedOnly = true,
            Page = 1,
            PageSize = 20
        };

        var results = await _searchService.SearchAsync(request);
        return View(results);
    }
}
```

### Content Indexing

```csharp
public class ContentService
{
    private readonly ISearchService _searchService;

    public ContentService(ISearchService searchService)
    {
        _searchService = searchService;
    }

    public async Task PublishArticleAsync(Article article, string tenantDomain)
    {
        // Save article to database
        await _repository.SaveAsync(article);

        // Index for search
        var searchDoc = SearchDocument.FromArticle(article, tenantDomain);
        await _searchService.IndexContentAsync(searchDoc);
    }

    public async Task BulkIndexContentAsync(IEnumerable<Article> articles, string tenantDomain)
    {
        var documents = articles.Select(a => SearchDocument.FromArticle(a, tenantDomain));
        await _searchService.IndexContentBulkAsync(documents);
    }
}
```

### Advanced Filtering

```csharp
var request = new SearchRequest
{
    Query = "artificial intelligence",
    TenantDomain = "example.com",
    ContentTypes = new[] { "article", "page" },
    Tags = new[] { "technology", "ai" },
    Author = "john.doe",
    FromDate = DateTimeOffset.UtcNow.AddMonths(-6),
    ToDate = DateTimeOffset.UtcNow,
    SortBy = "publishedDate",
    SortDirection = SortDirection.Descending,
    IncludeHighlights = true,
    IncludeFacets = true
};

var results = await _searchService.SearchAsync(request);
```

## Deployment Scenarios

### Development Environment
- Uses Lucene.NET with in-memory or file-system index
- Fast setup, no external dependencies
- Perfect for local development

### Production Cloud
- Uses Azure AI Search as primary
- Lucene.NET as fallback for availability
- Automatic failover and health monitoring

### Edge Deployment
- Uses Lucene.NET only
- Self-contained, no internet required
- Perfect for CDN edge locations

## Performance Considerations

### Azure AI Search
- **Pros**: Managed scaling, AI features, enterprise features
- **Cons**: Network latency, cost scaling with usage
- **Best for**: High-traffic, cloud-native deployments

### Lucene.NET
- **Pros**: No network latency, no external dependencies, predictable performance
- **Cons**: Manual scaling, limited AI features
- **Best for**: Edge deployments, development, predictable workloads

## Monitoring and Health Checks

The hybrid search includes built-in health checks:

```csharp
// Health check endpoint
app.MapHealthChecks("/health/search");

// Programmatic health check
var isHealthy = await _searchService.HealthCheckAsync();
```

Health check responses:
- **Healthy**: At least one search service is responding
- **Unhealthy**: No search services are available

## Security Considerations

### Azure AI Search
- API key management through Azure Key Vault
- Network security rules and private endpoints
- Managed identity support

### Lucene.NET
- File system permissions for index directory
- Secure deletion of sensitive content
- Index encryption at rest (via file system)

## Multi-Tenant Architecture

The search implementation is designed for SkyCMS's multi-tenant architecture:

```csharp
// Tenant isolation in search documents
public class SearchDocument
{
    public string TenantDomain { get; set; } // Filters by tenant
    // ... other properties
}

// Automatic tenant filtering
var request = new SearchRequest
{
    TenantDomain = HttpContext.GetTenantDomain(), // Auto-populated
    // ... other properties
};
```

## Indexing Strategy

### Real-time Indexing
Content is indexed immediately when published:
```csharp
await _searchService.IndexContentAsync(document);
```

### Background Indexing
Large operations use background processing:
```csharp
// Using Hangfire or similar
BackgroundJob.Enqueue(() => _searchService.IndexContentBulkAsync(documents));
```

### Index Rebuild
Full index rebuild for data migration or corruption recovery:
```csharp
await _searchService.RebuildIndexAsync();
```

## Error Handling and Fallback

The hybrid approach provides automatic fallback:

1. **Primary Service Fails**: Automatically switches to fallback service
2. **Network Issues**: Local Lucene.NET continues working
3. **Configuration Issues**: Graceful degradation with logging
4. **Index Corruption**: Automatic rebuild capabilities

## Extending the Implementation

### Custom Search Fields
Add new fields to `SearchDocument`:

```csharp
public class SearchDocument
{
    // Add custom field
    public string CustomField { get; set; }
    
    // Update mapping method
    public static SearchDocument FromArticle(Article article, string tenantDomain)
    {
        return new SearchDocument
        {
            // ... existing fields
            CustomField = article.CustomProperty
        };
    }
}
```

### Custom Search Implementations
Implement `ISearchService` for other search engines:

```csharp
public class ElasticsearchService : ISearchService
{
    // Implement all interface methods
}

// Register in DI
services.AddSingleton<ElasticsearchService>();
```

## Troubleshooting

### Common Issues

1. **Azure Search Connection Failed**
   - Check service endpoint and API key
   - Verify network connectivity
   - Check Azure Search service status

2. **Lucene Index Corruption**
   - Delete index directory and rebuild
   - Check file system permissions
   - Monitor disk space

3. **Search Results Empty**
   - Verify content is indexed
   - Check tenant filtering
   - Validate search query syntax

### Debug Logging

Enable detailed logging:

```json
{
  "Logging": {
    "LogLevel": {
      "Cosmos.Common.Services.Search": "Debug"
    }
  }
}
```

## Performance Tuning

### Azure AI Search
- Use appropriate pricing tier for your workload
- Configure index schema for your search patterns
- Use search analyzers for language-specific content
- Implement result caching for common queries

### Lucene.NET
- Tune JVM memory settings for large indexes
- Use SSD storage for index files
- Implement index warming for cold start scenarios
- Monitor index size and commit frequency

## Migration Guide

### From Existing Search Solutions

1. **Install packages** (already done in this implementation)
2. **Configure services** in `appsettings.json`
3. **Update DI registration** to use `AddSkyCmsSearch()`
4. **Migrate indexing code** to use `ISearchService`
5. **Update search UI** to use new search models
6. **Test fallback scenarios** thoroughly

This hybrid search implementation provides a robust, scalable foundation for SkyCMS that can adapt to various deployment scenarios while maintaining excellent search performance and reliability.