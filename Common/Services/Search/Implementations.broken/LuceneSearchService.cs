using Cosmos.Common.Services.Search.Models;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Highlight;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace Cosmos.Common.Services.Search.Implementations;

/// <summary>
/// Lucene.NET implementation of ISearchService
/// Provides embedded search for edge deployments and development
/// </summary>
public class LuceneSearchService : ISearchService, IDisposable
{
    private readonly LuceneSearchOptions _options;
    private readonly ILogger<LuceneSearchService> _logger;
    private readonly object _writerLock = new();
    
    private Lucene.Net.Store.Directory? _directory;
    private IndexWriter? _writer;
    private Analyzer? _analyzer;
    private SearcherManager? _searcherManager;
    
    private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

    public LuceneSearchService(
        IOptions<LuceneSearchOptions> options,
        ILogger<LuceneSearchService> logger)
    {
        _options = options.Value;
        _logger = logger;
        
        InitializeIndex();
    }

    public async Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            request.Validate();
            
            if (_searcherManager == null)
                throw new InvalidOperationException("Search service not properly initialized");

            var searcher = _searcherManager.Acquire();
            
            try
            {
                var query = BuildQuery(request);
                var filter = BuildFilter(request);
                var sort = BuildSort(request);
                
                var totalHits = searcher.Search(query, filter, 1);
                var totalCount = totalHits.TotalHits;
                
                var start = (request.Page - 1) * request.PageSize;
                var topDocs = sort != null 
                    ? searcher.Search(query, filter, start + request.PageSize, sort)
                    : searcher.Search(query, filter, start + request.PageSize);
                
                var items = new List<SearchResultItem>();
                
                // Create highlighter for search term highlighting
                Highlighter? highlighter = null;
                if (request.IncludeHighlights)
                {
                    var scorer = new QueryScorer(query);
                    highlighter = new Highlighter(scorer);
                    highlighter.TextFragmenter = new SimpleFragmenter(100);
                }
                
                for (int i = start; i < Math.Min(start + request.PageSize, topDocs.ScoreDocs.Length); i++)
                {
                    var scoreDoc = topDocs.ScoreDocs[i];
                    var doc = searcher.Doc(scoreDoc.Doc);
                    
                    var item = MapToResultItem(doc, scoreDoc.Score, highlighter);
                    items.Add(item);
                }
                
                var results = new SearchResults
                {
                    Items = items,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };

                _logger.LogInformation("Lucene search completed: {Query} returned {Count} results in {Time}ms",
                    request.Query, results.TotalCount, results.ExecutionTimeMs);

                return results;
            }
            finally
            {
                _searcherManager.Release(searcher);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lucene search failed for query: {Query}", request.Query);
            throw;
        }
    }

    public async Task IndexContentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            try
            {
                lock (_writerLock)
                {
                    if (_writer == null)
                        throw new InvalidOperationException("Index writer not initialized");

                    var luceneDoc = CreateLuceneDocument(document);
                    
                    // Update or add document
                    _writer.UpdateDocument(new Term("id", document.Id), luceneDoc);
                    _writer.Commit();
                    
                    // Refresh searcher
                    _searcherManager?.MaybeRefresh();
                }
                
                _logger.LogInformation("Indexed document {DocumentId}", document.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index document {DocumentId}", document.Id);
                throw;
            }
        }, cancellationToken);
    }

    public async Task IndexContentBulkAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            try
            {
                lock (_writerLock)
                {
                    if (_writer == null)
                        throw new InvalidOperationException("Index writer not initialized");

                    foreach (var document in documents)
                    {
                        var luceneDoc = CreateLuceneDocument(document);
                        _writer.UpdateDocument(new Term("id", document.Id), luceneDoc);
                    }
                    
                    _writer.Commit();
                    _searcherManager?.MaybeRefresh();
                }
                
                _logger.LogInformation("Bulk indexed {Count} documents", documents.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bulk index documents");
                throw;
            }
        }, cancellationToken);
    }

    public async Task DeleteFromIndexAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            try
            {
                lock (_writerLock)
                {
                    if (_writer == null)
                        throw new InvalidOperationException("Index writer not initialized");

                    _writer.DeleteDocuments(new Term("id", documentId));
                    _writer.Commit();
                    _searcherManager?.MaybeRefresh();
                }
                
                _logger.LogInformation("Deleted document {DocumentId} from index", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete document {DocumentId} from index", documentId);
                throw;
            }
        }, cancellationToken);
    }

    public async Task DeleteFromIndexBulkAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            try
            {
                lock (_writerLock)
                {
                    if (_writer == null)
                        throw new InvalidOperationException("Index writer not initialized");

                    foreach (var documentId in documentIds)
                    {
                        _writer.DeleteDocuments(new Term("id", documentId));
                    }
                    
                    _writer.Commit();
                    _searcherManager?.MaybeRefresh();
                }
                
                _logger.LogInformation("Bulk deleted {Count} documents from index", documentIds.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bulk delete documents from index");
                throw;
            }
        }, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetSuggestionsAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        // For simplicity, returning similar terms from existing content
        // A more sophisticated implementation could use Lucene's spell checker
        try
        {
            if (_searcherManager == null)
                return Array.Empty<string>();

            var searcher = _searcherManager.Acquire();
            
            try
            {
                var luceneQuery = new PrefixQuery(new Term("title", query.ToLowerInvariant()));
                var topDocs = searcher.Search(luceneQuery, maxResults);
                
                var suggestions = new List<string>();
                foreach (var scoreDoc in topDocs.ScoreDocs)
                {
                    var doc = searcher.Doc(scoreDoc.Doc);
                    var title = doc.Get("title");
                    if (!string.IsNullOrEmpty(title) && !suggestions.Contains(title))
                    {
                        suggestions.Add(title);
                    }
                }
                
                return suggestions;
            }
            finally
            {
                _searcherManager.Release(searcher);
            }
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
            if (_searcherManager == null || _directory == null)
                return false;

            var searcher = _searcherManager.Acquire();
            try
            {
                // Simple health check - try to get document count
                var reader = searcher.IndexReader;
                var docCount = reader.NumDocs;
                return true;
            }
            finally
            {
                _searcherManager.Release(searcher);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lucene health check failed");
            return false;
        }
    }

    public async Task ClearIndexAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            try
            {
                lock (_writerLock)
                {
                    if (_writer == null)
                        throw new InvalidOperationException("Index writer not initialized");

                    _writer.DeleteAll();
                    _writer.Commit();
                    _searcherManager?.MaybeRefresh();
                }
                
                _logger.LogWarning("Lucene index cleared - all documents deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear Lucene index");
                throw;
            }
        }, cancellationToken);
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        // This would typically trigger a background job to reindex all content
        // Implementation depends on your data access layer
        _logger.LogInformation("Lucene index rebuild requested - implement based on your data layer");
        await Task.CompletedTask;
    }

    private void InitializeIndex()
    {
        try
        {
            _analyzer = new StandardAnalyzer(AppLuceneVersion);
            
            if (_options.UseMemoryIndex)
            {
                _directory = new RAMDirectory();
            }
            else
            {
                var indexPath = _options.IndexPath ?? Path.Combine(Path.GetTempPath(), "skycms-lucene-index");
                System.IO.Directory.CreateDirectory(indexPath);
                _directory = FSDirectory.Open(indexPath);
            }

            var config = new IndexWriterConfig(AppLuceneVersion, _analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };
            
            _writer = new IndexWriter(_directory, config);
            _searcherManager = new SearcherManager(_writer, applyAllDeletes: true, null);
            
            _logger.LogInformation("Lucene search service initialized with {IndexType} index",
                _options.UseMemoryIndex ? "memory" : "file system");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Lucene search service");
            throw;
        }
    }

    private Query BuildQuery(SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new MatchAllDocsQuery();
        }

        if (_analyzer == null)
            throw new InvalidOperationException("Analyzer not initialized");

        var parser = new MultiFieldQueryParser(AppLuceneVersion,
            new[] { "title^3", "summary^2", "content", "tags^1.5" },
            _analyzer);
            
        try
        {
            return parser.Parse(request.Query);
        }
        catch (ParseException)
        {
            // Fallback to simple term query if parsing fails
            return new TermQuery(new Term("content", request.Query));
        }
    }

    private Filter? BuildFilter(SearchRequest request)
    {
        var filters = new List<Filter>();

        // Tenant filter
        if (!string.IsNullOrEmpty(request.TenantDomain))
        {
            filters.Add(new TermFilter(new Term("tenantDomain", request.TenantDomain)));
        }

        // Status filter
        if (request.PublishedOnly)
        {
            filters.Add(new TermFilter(new Term("status", "published")));
        }

        // Content type filter
        if (request.ContentTypes.Any())
        {
            var contentTypeFilter = new BooleanFilter();
            foreach (var contentType in request.ContentTypes)
            {
                contentTypeFilter.Add(new FilterClause(
                    new TermFilter(new Term("contentType", contentType)), 
                    Occur.SHOULD));
            }
            filters.Add(contentTypeFilter);
        }

        if (filters.Count == 0)
            return null;

        if (filters.Count == 1)
            return filters[0];

        var combinedFilter = new BooleanFilter();
        foreach (var filter in filters)
        {
            combinedFilter.Add(new FilterClause(filter, Occur.MUST));
        }

        return combinedFilter;
    }

    private Sort? BuildSort(SearchRequest request)
    {
        switch (request.SortBy.ToLowerInvariant())
        {
            case "date":
            case "publisheddate":
                return new Sort(new SortField("publishedDate", SortFieldType.INT64, 
                    request.SortDirection == SortDirection.Descending));
            case "title":
                return new Sort(new SortField("title", SortFieldType.STRING, 
                    request.SortDirection == SortDirection.Descending));
            case "relevance":
            default:
                return null; // Use relevance scoring
        }
    }

    private Document CreateLuceneDocument(SearchDocument document)
    {
        var doc = new Document();

        // Stored and indexed fields
        doc.Add(new StringField("id", document.Id, Field.Store.YES));
        doc.Add(new TextField("title", document.Title ?? "", Field.Store.YES));
        doc.Add(new TextField("content", document.Content ?? "", Field.Store.YES));
        doc.Add(new TextField("summary", document.Summary ?? "", Field.Store.YES));
        doc.Add(new StringField("url", document.Url ?? "", Field.Store.YES));
        doc.Add(new StringField("contentType", document.ContentType ?? "", Field.Store.YES));
        doc.Add(new StringField("status", document.Status ?? "", Field.Store.YES));
        doc.Add(new StringField("tenantDomain", document.TenantDomain ?? "", Field.Store.YES));
        doc.Add(new StringField("author", document.Author ?? "", Field.Store.YES));
        doc.Add(new StringField("language", document.Language ?? "en", Field.Store.YES));

        // Date fields
        if (document.PublishedDate.HasValue)
        {
            doc.Add(new Int64Field("publishedDate", document.PublishedDate.Value.ToUnixTimeSeconds(), Field.Store.YES));
        }
        
        doc.Add(new Int64Field("modifiedDate", document.ModifiedDate.ToUnixTimeSeconds(), Field.Store.YES));

        // Multi-value fields
        foreach (var tag in document.Tags ?? new List<string>())
        {
            doc.Add(new TextField("tags", tag, Field.Store.YES));
        }

        foreach (var category in document.Categories ?? new List<string>())
        {
            doc.Add(new StringField("categories", category, Field.Store.YES));
        }

        // Numeric fields
        doc.Add(new Int32Field("priority", document.Priority, Field.Store.YES));
        doc.Add(new Int64Field("viewCount", document.ViewCount, Field.Store.YES));

        return doc;
    }

    private SearchResultItem MapToResultItem(Document doc, float score, Highlighter? highlighter)
    {
        var item = new SearchResultItem
        {
            Id = doc.Get("id") ?? "",
            Title = doc.Get("title") ?? "",
            Content = TruncateContent(doc.Get("content") ?? "", 300),
            Url = doc.Get("url") ?? "",
            ContentType = doc.Get("contentType") ?? "",
            Author = doc.Get("author"),
            Score = score,
            TenantDomain = doc.Get("tenantDomain")
        };

        // Parse dates
        if (long.TryParse(doc.Get("publishedDate"), out var publishedTicks))
        {
            item.PublishedDate = DateTimeOffset.FromUnixTimeSeconds(publishedTicks);
        }
        
        if (long.TryParse(doc.Get("modifiedDate"), out var modifiedTicks))
        {
            item.ModifiedDate = DateTimeOffset.FromUnixTimeSeconds(modifiedTicks);
        }

        // Get tags
        item.Tags = doc.GetValues("tags") ?? new string[0];

        // Add highlights
        if (highlighter != null && _analyzer != null)
        {
            var highlights = new Dictionary<string, IEnumerable<string>>();
            
            try
            {
                var titleHighlight = highlighter.GetBestFragment(_analyzer, "title", item.Title);
                if (!string.IsNullOrEmpty(titleHighlight))
                {
                    highlights["title"] = new[] { titleHighlight };
                }

                var contentHighlight = highlighter.GetBestFragment(_analyzer, "content", item.Content);
                if (!string.IsNullOrEmpty(contentHighlight))
                {
                    highlights["content"] = new[] { contentHighlight };
                }
                
                item.Highlights = highlights;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate highlights for document {DocumentId}", item.Id);
            }
        }

        return item;
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

    public void Dispose()
    {
        _searcherManager?.Dispose();
        _writer?.Dispose();
        _analyzer?.Dispose();
        _directory?.Dispose();
    }
}

/// <summary>
/// Configuration options for Lucene.NET search
/// </summary>
public class LuceneSearchOptions
{
    public const string SectionName = "LuceneSearch";
    
    /// <summary>
    /// Path to store Lucene index files (null for temp directory)
    /// </summary>
    public string? IndexPath { get; set; }
    
    /// <summary>
    /// Use in-memory index (faster but not persistent)
    /// </summary>
    public bool UseMemoryIndex { get; set; } = false;
    
    /// <summary>
    /// Enable Lucene search (primary or fallback)
    /// </summary>
    public bool Enabled { get; set; } = true;
}