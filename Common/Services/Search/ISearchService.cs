using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Models;
using Cosmos.Common.Services.Search.Models;

namespace Cosmos.Common.Services.Search;

/// <summary>
/// Unified search interface supporting multiple search backends
/// Designed for SkyCMS multi-tenant architecture with hybrid deployment support
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Search content with optional filters and pagination
    /// </summary>
    Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Index a single content item (article, page, etc.)
    /// </summary>
    Task IndexContentAsync(SearchDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk index multiple content items
    /// </summary>
    Task IndexContentBulkAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove content from search index
    /// </summary>
    Task DeleteFromIndexAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove multiple documents from index
    /// </summary>
    Task DeleteFromIndexBulkAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get search suggestions/autocomplete
    /// </summary>
    Task<IEnumerable<string>> GetSuggestionsAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the search service is healthy and responding
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear entire search index (use with caution)
    /// </summary>
    Task ClearIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuild entire search index from data source
    /// </summary>
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);
}