namespace Cosmos.Common.Services.Search.Models;

/// <summary>
/// Search request with filtering and pagination for SkyCMS
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// Search query string
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Tenant domain for multi-tenant filtering
    /// </summary>
    public string? TenantDomain { get; set; }

    /// <summary>
    /// Content types to include (article, page, etc.)
    /// </summary>
    public IEnumerable<string> ContentTypes { get; set; } = new List<string>();

    /// <summary>
    /// Tags to filter by
    /// </summary>
    public IEnumerable<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Author filter
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Date range filter - start date
    /// </summary>
    public DateTimeOffset? FromDate { get; set; }

    /// <summary>
    /// Date range filter - end date
    /// </summary>
    public DateTimeOffset? ToDate { get; set; }

    /// <summary>
    /// Only include published content
    /// </summary>
    public bool PublishedOnly { get; set; } = true;

    /// <summary>
    /// Page number for pagination (1-based)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Results per page
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Maximum results per page (safety limit)
    /// </summary>
    public static int MaxPageSize => 100;

    /// <summary>
    /// Sort field
    /// </summary>
    public string SortBy { get; set; } = "relevance";

    /// <summary>
    /// Sort direction
    /// </summary>
    public SortDirection SortDirection { get; set; } = SortDirection.Descending;

    /// <summary>
    /// Include highlights in results
    /// </summary>
    public bool IncludeHighlights { get; set; } = true;

    /// <summary>
    /// Include facets for filtering UI
    /// </summary>
    public bool IncludeFacets { get; set; } = false;

    /// <summary>
    /// Validate and sanitize search request
    /// </summary>
    public void Validate()
    {
        if (PageSize > MaxPageSize)
            PageSize = MaxPageSize;
        
        if (Page < 1)
            Page = 1;

        // Sanitize query to prevent injection
        Query = Query?.Trim() ?? string.Empty;
    }
}

/// <summary>
/// Sort direction enumeration
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}