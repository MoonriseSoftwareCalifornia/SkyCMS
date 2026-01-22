namespace Cosmos.Common.Services.Search.Models;

/// <summary>
/// Document model for indexing in search engines
/// Optimized for SkyCMS content structure
/// </summary>
public class SearchDocument
{
    /// <summary>
    /// Unique document identifier
    /// Format: {tenantDomain}#{contentType}#{id}
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Document title (searchable, high weight)
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Document content/body (searchable)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Document summary/excerpt (searchable, medium weight)
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Document URL/path (filterable)
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Content type (article, page, file, etc.) (filterable)
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Publication status (draft, published, archived) (filterable)
    /// </summary>
    public string Status { get; set; } = "published";

    /// <summary>
    /// Publication date (filterable, sortable)
    /// </summary>
    public DateTimeOffset? PublishedDate { get; set; }

    /// <summary>
    /// Last modified date (filterable, sortable)
    /// </summary>
    public DateTimeOffset ModifiedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Content author (filterable, searchable)
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Author email
    /// </summary>
    public string? AuthorEmail { get; set; }

    /// <summary>
    /// Associated tags (filterable, searchable)
    /// </summary>
    public ICollection<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Content categories (filterable)
    /// </summary>
    public ICollection<string> Categories { get; set; } = new List<string>();

    /// <summary>
    /// Tenant domain for multi-tenancy (filterable)
    /// </summary>
    public string TenantDomain { get; set; } = string.Empty;

    /// <summary>
    /// Language code (filterable)
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// SEO meta description
    /// </summary>
    public string? MetaDescription { get; set; }

    /// <summary>
    /// SEO keywords
    /// </summary>
    public ICollection<string> Keywords { get; set; } = new List<string>();

    /// <summary>
    /// Document priority/weight for ranking
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// View count for popularity ranking
    /// </summary>
    public long ViewCount { get; set; }

    /// <summary>
    /// Document image/thumbnail URL
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Document file size (for file content)
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// Document MIME type (for file content)
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Custom metadata fields
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Generate composite document ID for multi-tenant indexing
    /// </summary>
    /// <param name="tenantDomain">Tenant domain</param>
    /// <param name="contentType">Content type</param>
    /// <param name="id">Content ID</param>
    /// <returns>Composite document ID</returns>
    public static string GenerateId(string tenantDomain, string contentType, string id)
    {
        return $"{tenantDomain}#{contentType}#{id}".ToLowerInvariant();
    }

    /// <summary>
    /// Create search document from SkyCMS article
    /// </summary>
    public static SearchDocument FromArticle(Article article, string tenantDomain)
    {
        return new SearchDocument
        {
            Id = GenerateId(tenantDomain, "article", article.Id.ToString()),
            Title = article.Title ?? string.Empty,
            Content = article.Content ?? string.Empty,
            Summary = article.BannerImage ?? string.Empty, // Using BannerImage as summary field
            Url = $"/article/{article.UrlPath}",
            ContentType = "article",
            Status = article.StatusCode?.ToLowerInvariant() ?? "published",
            PublishedDate = article.Published,
            ModifiedDate = article.Updated ?? article.Published ?? DateTimeOffset.UtcNow,
            Author = article.AuthorInfo,
            TenantDomain = tenantDomain,
            Priority = 1,
            ViewCount = 0 // Would need to be populated from analytics
        };
    }
}