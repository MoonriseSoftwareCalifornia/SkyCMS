namespace Cosmos.Common.Services.Search.Models;

/// <summary>
/// Search results container with pagination and metadata
/// </summary>
public class SearchResults
{
    /// <summary>
    /// Found documents
    /// </summary>
    public IEnumerable<SearchResultItem> Items { get; set; } = new List<SearchResultItem>();

    /// <summary>
    /// Total number of matching documents
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Current page number
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Results per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Whether there are more results
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether this is not the first page
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Search execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Facets for filtering (categories, tags, etc.)
    /// </summary>
    public Dictionary<string, IEnumerable<SearchFacet>> Facets { get; set; } = new();

    /// <summary>
    /// Search suggestions for query correction
    /// </summary>
    public IEnumerable<string> Suggestions { get; set; } = new List<string>();

    /// <summary>
    /// Empty search results
    /// </summary>
    public static SearchResults Empty => new();
}

/// <summary>
/// Individual search result item
/// </summary>
public class SearchResultItem
{
    /// <summary>
    /// Unique document identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Document title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Document content/body
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Document URL/path
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Content type (article, page, etc.)
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Publication date
    /// </summary>
    public DateTimeOffset? PublishedDate { get; set; }

    /// <summary>
    /// Last modified date
    /// </summary>
    public DateTimeOffset? ModifiedDate { get; set; }

    /// <summary>
    /// Author information
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Associated tags
    /// </summary>
    public IEnumerable<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Search relevance score
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Highlighted text snippets
    /// </summary>
    public Dictionary<string, IEnumerable<string>> Highlights { get; set; } = new();

    /// <summary>
    /// Tenant domain
    /// </summary>
    public string? TenantDomain { get; set; }

    /// <summary>
    /// Document thumbnail/image URL
    /// </summary>
    public string? ImageUrl { get; set; }
}

/// <summary>
/// Search facet for filtering
/// </summary>
public class SearchFacet
{
    /// <summary>
    /// Facet value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Number of documents with this facet value
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Whether this facet is currently selected
    /// </summary>
    public bool IsSelected { get; set; }
}