# SkyCMS Search Index Testing Guide

## Overview

This guide covers testing the complete search functionality lifecycle in SkyCMS, from index building to searching to rebuilding.

## Current Implementation Status

⚠️ **Current State**: The search endpoints are currently using direct database queries via Entity Framework rather than a dedicated search index (Lucene.NET infrastructure exists but is disabled in `Implementations.broken/` folder).

## Testing the Current Search Implementation

### 1. Testing Search Endpoints

#### **Sky.Api** (API-only)
```bash
# Start the API project
cd C:\Users\toiya\source\repos\SkyCMS
dotnet run --project Sky.Api

# Test endpoints (default port: 5000)
curl "http://localhost:5000/_api/search?query=test&page=1&pageSize=10"
curl "http://localhost:5000/_api/search/suggestions?term=test&maxResults=5" 
curl "http://localhost:5000/_api/search/health"
```

#### **Sky.Publisher** (Public site)
```bash
# Start the Publisher project  
dotnet run --project Publisher

# Test endpoints (default port: 5001)
curl "http://localhost:5001/Search/Suggestions?term=test"
curl "http://localhost:5001/Search/Health"

# Test search page
# Navigate to: http://localhost:5001/Search?query=test&page=1
```

#### **Sky.Editor** (Admin interface)
```bash
# Start the Editor project
dotnet run --project Editor

# Test endpoints (default port: 5002) - Requires authentication
curl "http://localhost:5002/Search/Suggestions?term=test"
curl "http://localhost:5002/Search/Health"

# Test search page  
# Navigate to: http://localhost:5002/Search?query=test&page=1
```

### 2. Testing Search Functionality

#### Basic Search Tests
```bash
# Test basic search
curl "http://localhost:5000/_api/search?query=article"

# Test pagination
curl "http://localhost:5000/_api/search?query=article&page=2&pageSize=5"

# Test sorting
curl "http://localhost:5000/_api/search?query=article&sortBy=date"
curl "http://localhost:5000/_api/search?query=article&sortBy=title"

# Test date filtering
curl "http://localhost:5000/_api/search?query=article&dateFrom=2024-01-01&dateTo=2024-12-31"

# Test empty query (should return all published articles)
curl "http://localhost:5000/_api/search?query="
```

#### Rate Limiting Tests
```bash
# Test rate limits by making rapid requests
for i in {1..35}; do curl "http://localhost:5000/_api/search?query=test$i"; done
```

### 3. Testing Data Setup

#### Create Test Articles
```sql
-- Connect to your SkyCMS database and add test articles
INSERT INTO Articles (Title, Content, UrlPath, StatusCode, Published, Updated, VersionNumber)
VALUES 
('Test Article 1', 'This is test content about programming', '/test-1', 0, GETUTCDATE(), GETUTCDATE(), 1),
('Sample Post', 'Content about web development and coding', '/sample-post', 0, GETUTCDATE(), GETUTCDATE(), 1),
('Guide to Testing', 'How to test search functionality effectively', '/testing-guide', 0, GETUTCDATE(), GETUTCDATE(), 1);
```

## Setting Up Proper Search Indexing

### 1. Enable Lucene.NET Search Service

#### Fix the Search Service Registration
Create `Common/Services/Search/Extensions/SearchServiceExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Cosmos.Common.Services.Search.Implementations;

namespace Cosmos.Common.Services.Search.Extensions;

public static class SearchServiceExtensions
{
    public static IServiceCollection AddLuceneSearch(this IServiceCollection services)
    {
        services.AddSingleton<LuceneSearchService>();
        services.TryAddSingleton<ISearchService>(provider => 
            provider.GetRequiredService<LuceneSearchService>());
        
        return services;
    }
}
```

#### Move Implementation from Broken Folder
```bash
# Move LuceneSearchService from broken folder
mv "Common/Services/Search/Implementations.broken/LuceneSearchService.cs" "Common/Services/Search/Implementations/"
```

#### Register Search Service in Projects
Add to `Sky.Api/Program.cs`, `Publisher/Program.cs`, `Editor/Program.cs`:
```csharp
using Cosmos.Common.Services.Search.Extensions;

// Add this after other service registrations
builder.Services.AddLuceneSearch();
```

### 2. Create Index Management Endpoints

#### Add Index Management Controller
Create `Sky.Cms.Api.Shared/Controllers/IndexManagementApiController.cs`:

```csharp
[ApiController]
[Route("_api/index")]
public class IndexManagementApiController : ControllerBase
{
    private readonly ISearchService searchService;
    private readonly ILogger<IndexManagementApiController> logger;

    public IndexManagementApiController(
        ISearchService searchService,
        ILogger<IndexManagementApiController> logger)
    {
        this.searchService = searchService;
        this.logger = logger;
    }

    [HttpPost("rebuild")]
    public async Task<IActionResult> RebuildIndex()
    {
        try
        {
            await searchService.RebuildIndexAsync();
            return Ok(new { message = "Index rebuild started" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rebuild search index");
            return StatusCode(500, new { error = "Index rebuild failed" });
        }
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearIndex()
    {
        try
        {
            await searchService.ClearIndexAsync();
            return Ok(new { message = "Index cleared" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear search index");
            return StatusCode(500, new { error = "Index clear failed" });
        }
    }

    [HttpPost("document/{articleId}")]
    public async Task<IActionResult> IndexArticle(int articleId)
    {
        // Implementation to index a specific article
        return Ok(new { message = $"Article {articleId} indexed" });
    }
}
```

### 3. Testing Complete Index Lifecycle

#### Step 1: Clear Existing Index
```bash
curl -X DELETE "http://localhost:5000/_api/index/clear"
```

#### Step 2: Rebuild Index from Database
```bash
curl -X POST "http://localhost:5000/_api/index/rebuild"
```

#### Step 3: Test Search Against Index
```bash
# Test that search now uses the index
curl "http://localhost:5000/_api/search?query=test"
```

#### Step 4: Add New Content
```bash
# Add new article via admin interface or database
curl -X POST "http://localhost:5000/_api/index/document/123"
```

#### Step 5: Verify New Content is Searchable
```bash
curl "http://localhost:5000/_api/search?query=new-article"
```

## Performance Testing

### Load Testing Search
```bash
# Install Apache Bench for load testing
# Windows: Download from Apache website
# Test search performance
ab -n 100 -c 10 "http://localhost:5000/_api/search?query=test"
```

### Memory and Index Size Monitoring
```powershell
# Monitor application memory usage
Get-Process dotnet | Select-Object ProcessName,WorkingSet,PrivateMemorySize

# Check index file sizes (when using Lucene)
Get-ChildItem "path/to/lucene/index" -Recurse | Measure-Object Length -Sum
```

## Automated Testing

### Unit Tests
Create `Tests/Search/SearchIntegrationTests.cs`:

```csharp
[Test]
public async Task Search_WithValidQuery_ReturnsResults()
{
    // Arrange
    var query = new SearchQuery { Query = "test" };
    
    // Act
    var result = await searchHandler.HandleAsync(query);
    
    // Assert
    Assert.That(result.Results, Is.Not.Empty);
}

[Test] 
public async Task IndexRebuild_CompletesSuccessfully()
{
    // Test index rebuild process
}
```

### Integration Tests
```bash
# Run all search-related tests
dotnet test --filter "Category=Search"
```

## Monitoring and Health Checks

### Health Check Endpoints
```bash
# Check search service health
curl "http://localhost:5000/_api/search/health"

# Expected response:
{
  "status": "healthy",
  "message": "Search service is operational",
  "version": "1.0.0",
  "metrics": {
    "totalDocuments": 150,
    "indexSize": "2.5MB",
    "lastRebuild": "2024-01-15T10:30:00Z"
  }
}
```

### Logging
Monitor application logs for:
- Search query performance
- Index rebuild progress  
- Error rates
- Rate limiting triggers

## Troubleshooting

### Common Issues

1. **No Search Results**
   - Verify articles exist with `StatusCode = 0` (published)
   - Check tenant isolation is working correctly
   - Confirm database connection

2. **Index Out of Date**
   - Trigger manual rebuild: `curl -X POST "http://localhost:5000/_api/index/rebuild"`
   - Check for indexing errors in logs

3. **Performance Issues**
   - Monitor database query performance
   - Consider adding database indexes on search columns
   - Check rate limiting configuration

4. **Rate Limiting Issues**
   - Verify rate limiting policies are correctly configured
   - Check current request counts
   - Adjust limits for development vs production

## Next Steps

1. **Enable proper Lucene.NET indexing** by moving implementations from `.broken` folders
2. **Add index management endpoints** for rebuilding and monitoring
3. **Implement real-time indexing** when articles are created/updated
4. **Add search analytics** to track popular queries
5. **Consider Elasticsearch** for advanced search features in production

This comprehensive testing approach ensures your search functionality works correctly from database to index to user-facing endpoints.