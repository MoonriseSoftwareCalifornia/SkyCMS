// <copyright file="LuceneSearchService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Search;

using Cosmos.Common.Services.Search.Configuration;
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Lucene.Net implementation of the search service.
/// Provides full-text search with advanced features like boosting, fuzzy search, and highlighting.
/// </summary>
public class LuceneSearchService : ISearchService, IDisposable
{
    private const LuceneVersion LUCENE_VERSION = LuceneVersion.LUCENE_48;

    // Field names
    private const string FIELD_ID = "id";
    private const string FIELD_TITLE = "title";
    private const string FIELD_CONTENT = "content";
    private const string FIELD_SUMMARY = "summary";
    private const string FIELD_URL = "url";
    private const string FIELD_CONTENT_TYPE = "contentType";
    private const string FIELD_STATUS = "status";
    private const string FIELD_PUBLISHED_DATE = "publishedDate";
    private const string FIELD_MODIFIED_DATE = "modifiedDate";
    private const string FIELD_AUTHOR = "author";
    private const string FIELD_TAGS = "tags";
    private const string FIELD_CATEGORIES = "categories";
    private const string FIELD_TENANT = "tenant";
    private const string FIELD_LANGUAGE = "language";
    private const string FIELD_PRIORITY = "priority";
    private const string FIELD_VIEW_COUNT = "viewCount";

    private readonly LuceneSearchOptions options;
    private readonly ILogger<LuceneSearchService> logger;
    private readonly Analyzer analyzer;
    private readonly FSDirectory? fsDirectory;
    private readonly RAMDirectory? ramDirectory;
    private IndexWriter? indexWriter;
    private SearcherManager? searcherManager;
    private readonly object writerLock = new();
    private readonly Timer? commitTimer;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="LuceneSearchService"/> class.
    /// </summary>
    public LuceneSearchService(
        IOptions<LuceneSearchOptions> options,
        ILogger<LuceneSearchService> logger)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        this.analyzer = new StandardAnalyzer(LUCENE_VERSION);

        try
        {
            if (this.options.UseRamDirectory)
            {
                logger.LogInformation("Initializing Lucene with RAM directory");
                ramDirectory = new RAMDirectory();
                InitializeIndexWriter(ramDirectory);
            }
            else
            {
                var indexPath = Path.GetFullPath(this.options.IndexPath);
                logger.LogInformation("Initializing Lucene with file system directory at {IndexPath}", indexPath);

                if (!System.IO.Directory.Exists(indexPath))
                {
                    System.IO.Directory.CreateDirectory(indexPath);
                }

                fsDirectory = FSDirectory.Open(indexPath);
                InitializeIndexWriter(fsDirectory);
            }

            // FIXED: SearcherManager needs SearcherFactory parameter
            searcherManager = new SearcherManager(indexWriter, applyAllDeletes: true, searcherFactory: null);

            if (!this.options.AutoCommit && this.options.CommitIntervalMs > 0)
            {
                commitTimer = new Timer(
                    _ => CommitChanges(),
                    null,
                    TimeSpan.FromMilliseconds(this.options.CommitIntervalMs),
                    TimeSpan.FromMilliseconds(this.options.CommitIntervalMs));
            }

            logger.LogInformation("Lucene search service initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Lucene search service");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SearchResults> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // FIXED: Use Task.Factory.StartNew with explicit type parameter
        return await Task.Factory.StartNew(() =>
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var searcher = searcherManager!.Acquire();
                try
                {
                    var userQuery = BuildQuery(request);
                    var filterQuery = BuildFilterQuery(request);
                    var combinedQuery = CombineQueries(userQuery, filterQuery);
                    var sort = BuildSort(request.SortBy);

                    var topDocs = searcher.Search(combinedQuery, options.MaxResults, sort);

                    var items = new List<SearchResultItem>();
                    var maxScore = topDocs.MaxScore;

                    var start = request.Page * request.PageSize;
                    var end = Math.Min(start + request.PageSize, topDocs.TotalHits);

                    for (int i = start; i < end; i++)
                    {
                        var scoreDoc = topDocs.ScoreDocs[i];
                        var doc = searcher.Doc(scoreDoc.Doc);

                        var item = MapDocumentToResultItem(doc, scoreDoc.Score, maxScore, request.Query);
                        items.Add(item);
                    }

                    stopwatch.Stop();

                    return new SearchResults
                    {
                        Items = items,
                        TotalCount = topDocs.TotalHits,
                        Page = request.Page,
                        PageSize = request.PageSize,
                        SearchTimeMs = stopwatch.ElapsedMilliseconds,
                        Facets = BuildFacets(searcher, combinedQuery)
                    };
                }
                finally
                {
                    searcherManager.Release(searcher);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing search query: {Query}", request.Query);
                throw;
            }
        }, cancellationToken, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
    }

    /// <inheritdoc/>
    public Task IndexContentAsync(
        SearchDocument document,
        CancellationToken cancellationToken = default)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        // FIXED: Return Task.Run directly without await since method isn't async
        return Task.Run(() =>
        {
            lock (writerLock)
            {
                try
                {
                    var doc = MapSearchDocumentToLuceneDocument(document);
                    indexWriter!.UpdateDocument(new Term(FIELD_ID, document.Id), doc);

                    if (options.AutoCommit)
                    {
                        indexWriter.Commit();
                        searcherManager?.MaybeRefresh();
                    }

                    logger.LogDebug("Indexed document: {DocumentId}", document.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error indexing document: {DocumentId}", document.Id);
                    throw;
                }
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task IndexContentBulkAsync(
        IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        if (documents == null)
            throw new ArgumentNullException(nameof(documents));

        // FIXED: Return Task.Run directly
        return Task.Run(() =>
        {
            lock (writerLock)
            {
                try
                {
                    var count = 0;
                    foreach (var document in documents)
                    {
                        var doc = MapSearchDocumentToLuceneDocument(document);
                        indexWriter!.UpdateDocument(new Term(FIELD_ID, document.Id), doc);
                        count++;
                    }

                    indexWriter!.Commit();
                    searcherManager?.MaybeRefresh();

                    logger.LogInformation("Bulk indexed {Count} documents", count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in bulk indexing");
                    throw;
                }
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteFromIndexAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));

        // FIXED: Return Task.Run directly
        return Task.Run(() =>
        {
            lock (writerLock)
            {
                try
                {
                    indexWriter!.DeleteDocuments(new Term(FIELD_ID, documentId));

                    if (options.AutoCommit)
                    {
                        indexWriter.Commit();
                        searcherManager?.MaybeRefresh();
                    }

                    logger.LogDebug("Deleted document: {DocumentId}", documentId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
                    throw;
                }
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteFromIndexBulkAsync(
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (documentIds == null)
            throw new ArgumentNullException(nameof(documentIds));

        // FIXED: Return Task.Run directly
        return Task.Run(() =>
        {
            lock (writerLock)
            {
                try
                {
                    var terms = documentIds.Select(id => new Term(FIELD_ID, id)).ToArray();
                    indexWriter!.DeleteDocuments(terms);
                    indexWriter.Commit();
                    searcherManager?.MaybeRefresh();

                    logger.LogInformation("Bulk deleted {Count} documents", terms.Length);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in bulk deletion");
                    throw;
                }
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetSuggestionsAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();

        // FIXED: Explicit return type and consistent returns
        return await Task.Run<IEnumerable<string>>(
            () =>
                {
                    try
                    {
                        var searcher = searcherManager!.Acquire();
                        try
                        {
                            var queryParser = new MultiFieldQueryParser(
                                LUCENE_VERSION,
                                new[] { FIELD_TITLE, FIELD_CONTENT },
                                analyzer);

                            var prefixQuery = queryParser.Parse($"{query}*");
                            var topDocs = searcher.Search(prefixQuery, maxResults);

                            var suggestions = new HashSet<string>();
                            foreach (var scoreDoc in topDocs.ScoreDocs)
                            {
                                var doc = searcher.Doc(scoreDoc.Doc);
                                var title = doc.Get(FIELD_TITLE);
                                if (!string.IsNullOrWhiteSpace(title))
                                {
                                    suggestions.Add(title);
                                }
                            }

                            return (IEnumerable<string>)suggestions.Take(maxResults).ToList();
                        }
                        finally
                        {
                            searcherManager.Release(searcher);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error getting suggestions for: {Query}", query);
                        return (IEnumerable<string>)Array.Empty<string>();
                    }
                }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task ClearIndexAsync(CancellationToken cancellationToken = default)
    {
        // FIXED: Return Task.Run directly
        return Task.Run(
            () =>
                {
                    lock (writerLock)
                    {
                        try
                        {
                            indexWriter!.DeleteAll();
                            indexWriter.Commit();
                            searcherManager?.MaybeRefresh();

                            logger.LogWarning("Search index cleared");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error clearing index");
                            throw;
                        }
                    }
                }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        await ClearIndexAsync(cancellationToken);
        logger.LogInformation("Index rebuild triggered - external indexing pipeline should repopulate");
    }

    /// <inheritdoc/>
    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var searcher = searcherManager?.Acquire();
            if (searcher != null)
            {
                searcherManager!.Release(searcher);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    #region Private Helper Methods

    private void InitializeIndexWriter(Lucene.Net.Store.Directory directory)
    {
        var config = new IndexWriterConfig(LUCENE_VERSION, analyzer)
        {
            OpenMode = OpenMode.CREATE_OR_APPEND
        };

        indexWriter = new IndexWriter(directory, config);
    }

    private Query BuildQuery(SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new MatchAllDocsQuery();
        }

        try
        {
            var queryParser = new MultiFieldQueryParser(
                LUCENE_VERSION,
                new[]
                {
                    FIELD_TITLE,
                    FIELD_CONTENT,
                    FIELD_SUMMARY,
                    FIELD_TAGS,
                    FIELD_AUTHOR,
                    FIELD_CATEGORIES
                },
                analyzer,
                new Dictionary<string, float>
                {
                    { FIELD_TITLE, options.Boosts.Title },
                    { FIELD_CONTENT, options.Boosts.Content },
                    { FIELD_SUMMARY, options.Boosts.Summary },
                    { FIELD_TAGS, options.Boosts.Tags },
                    { FIELD_AUTHOR, options.Boosts.Author },
                    { FIELD_CATEGORIES, options.Boosts.Keywords }
                });

            queryParser.DefaultOperator = Operator.AND;
            queryParser.FuzzyMinSim = 0.7f;

            return queryParser.Parse(QueryParserBase.Escape(request.Query));
        }
        catch (ParseException ex)
        {
            logger.LogWarning(ex, "Query parse error, falling back to simple query: {Query}", request.Query);
            return new TermQuery(new Term(FIELD_CONTENT, request.Query.ToLowerInvariant()));
        }
    }

    // FIXED: Changed from Filter to Query-based filtering
    private BooleanQuery BuildFilterQuery(SearchRequest request)
    {
        var booleanQuery = new BooleanQuery();

        // Tenant filter (always applied for multi-tenancy)
        if (!string.IsNullOrWhiteSpace(request.TenantDomain))
        {
            var tenantQuery = new TermQuery(new Term(FIELD_TENANT, request.TenantDomain.ToLowerInvariant()));
            booleanQuery.Add(tenantQuery, Occur.MUST);
        }

        // Content type filter
        if (request.ContentTypes?.Any() == true)
        {
            var contentTypeQuery = new BooleanQuery();
            foreach (var contentType in request.ContentTypes)
            {
                var query = new TermQuery(new Term(FIELD_CONTENT_TYPE, contentType.ToLowerInvariant()));
                contentTypeQuery.Add(query, Occur.SHOULD);
            }
            booleanQuery.Add(contentTypeQuery, Occur.MUST);
        }

        // Date range filter
        if (request.DateFrom.HasValue || request.DateTo.HasValue)
        {
            var dateFrom = request.DateFrom?.ToString("yyyyMMdd") ?? "00000000";
            var dateTo = request.DateTo?.ToString("yyyyMMdd") ?? "99999999";

            var dateQuery = TermRangeQuery.NewStringRange(
                FIELD_PUBLISHED_DATE,
                dateFrom,
                dateTo,
                true,
                true);

            booleanQuery.Add(dateQuery, Occur.MUST);
        }

        // Status filter (only published content)
        var statusQuery = new TermQuery(new Term(FIELD_STATUS, "published"));
        booleanQuery.Add(statusQuery, Occur.MUST);

        return booleanQuery;
    }

    // FIXED: New method to combine user query with filters
    private Query CombineQueries(Query userQuery, BooleanQuery filterQuery)
    {
        // If there are no filter clauses, just return the user query
        if (filterQuery.Clauses.Count == 0)
        {
            return userQuery;
        }

        // Combine user query and filters
        var combinedQuery = new BooleanQuery
        {
            { userQuery, Occur.MUST },
            { filterQuery, Occur.MUST }
        };

        return combinedQuery;
    }

    private Sort BuildSort(string? sortBy)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "date" => new Sort(new SortField(FIELD_PUBLISHED_DATE, SortFieldType.STRING, true)),
            "modified" => new Sort(new SortField(FIELD_MODIFIED_DATE, SortFieldType.STRING, true)),
            "title" => new Sort(new SortField(FIELD_TITLE, SortFieldType.STRING)),
            "popularity" => new Sort(new SortField(FIELD_VIEW_COUNT, SortFieldType.INT64, true)),
            _ => new Sort(SortField.FIELD_SCORE)
        };
    }

    private SearchResultItem MapDocumentToResultItem(
        Document doc,
        float score,
        float maxScore,
        string? query)
    {
        var normalizedScore = maxScore > 0 ? (double)(score / maxScore) : 0.0;

        var content = doc.Get(FIELD_CONTENT) ?? string.Empty;
        var highlightedContent = string.Empty;

        if (!string.IsNullOrWhiteSpace(query))
        {
            highlightedContent = GenerateHighlights(content, query);
        }

        return new SearchResultItem
        {
            Id = doc.Get(FIELD_ID) ?? string.Empty,
            Title = doc.Get(FIELD_TITLE) ?? string.Empty,
            Content = TruncateContent(content, 300),
            HighlightedContent = highlightedContent,
            Url = doc.Get(FIELD_URL) ?? string.Empty,
            ContentType = doc.Get(FIELD_CONTENT_TYPE) ?? string.Empty,
            PublishedDate = ParseDateTimeOffset(doc.Get(FIELD_PUBLISHED_DATE)),
            ModifiedDate = ParseDateTimeOffset(doc.Get(FIELD_MODIFIED_DATE)),
            Author = doc.Get(FIELD_AUTHOR) ?? string.Empty,
            Score = normalizedScore,
            Metadata = new Dictionary<string, object>
            {
                ["Tags"] = doc.Get(FIELD_TAGS) ?? string.Empty,
                ["Categories"] = doc.Get(FIELD_CATEGORIES) ?? string.Empty,
                ["Language"] = doc.Get(FIELD_LANGUAGE) ?? "en",
                ["RawScore"] = score
            }
        };
    }

    private string GenerateHighlights(string content, string query)
    {
        try
        {
            var queryParser = new QueryParser(LUCENE_VERSION, FIELD_CONTENT, analyzer);
            var parsedQuery = queryParser.Parse(QueryParserBase.Escape(query));

            var scorer = new QueryScorer(parsedQuery);
            var highlighter = new Highlighter(
                new SimpleHTMLFormatter("<mark>", "</mark>"),
                scorer);

            var tokenStream = analyzer.GetTokenStream(FIELD_CONTENT, content);
            var fragments = highlighter.GetBestFragments(
                tokenStream,
                content,
                options.HighlightFragmentCount,
                "...");

            return string.Join(" ... ", fragments);
        }
        catch
        {
            return TruncateContent(content, options.HighlightFragmentSize);
        }
    }

    private Document MapSearchDocumentToLuceneDocument(SearchDocument searchDoc)
    {
        var doc = new Document();

        doc.Add(new StringField(FIELD_ID, searchDoc.Id, Field.Store.YES));
        doc.Add(new TextField(FIELD_TITLE, searchDoc.Title, Field.Store.YES));
        doc.Add(new TextField(FIELD_CONTENT, searchDoc.Content, Field.Store.YES));

        if (!string.IsNullOrWhiteSpace(searchDoc.Summary))
            doc.Add(new TextField(FIELD_SUMMARY, searchDoc.Summary, Field.Store.YES));

        doc.Add(new StringField(FIELD_URL, searchDoc.Url, Field.Store.YES));
        doc.Add(new StringField(FIELD_CONTENT_TYPE, searchDoc.ContentType.ToLowerInvariant(), Field.Store.YES));
        doc.Add(new StringField(FIELD_STATUS, searchDoc.Status.ToLowerInvariant(), Field.Store.YES));
        doc.Add(new StringField(FIELD_TENANT, searchDoc.TenantDomain.ToLowerInvariant(), Field.Store.YES));
        doc.Add(new StringField(FIELD_LANGUAGE, searchDoc.Language.ToLowerInvariant(), Field.Store.YES));

        if (searchDoc.PublishedDate.HasValue)
            doc.Add(new StringField(FIELD_PUBLISHED_DATE, searchDoc.PublishedDate.Value.ToString("yyyyMMdd"), Field.Store.YES));

        doc.Add(new StringField(FIELD_MODIFIED_DATE, searchDoc.ModifiedDate.ToString("yyyyMMdd"), Field.Store.YES));

        if (!string.IsNullOrWhiteSpace(searchDoc.Author))
            doc.Add(new TextField(FIELD_AUTHOR, searchDoc.Author, Field.Store.YES));

        if (searchDoc.Tags?.Any() == true)
            doc.Add(new TextField(FIELD_TAGS, string.Join(" ", searchDoc.Tags), Field.Store.YES));

        if (searchDoc.Categories?.Any() == true)
            doc.Add(new TextField(FIELD_CATEGORIES, string.Join(" ", searchDoc.Categories), Field.Store.YES));

        doc.Add(new Int32Field(FIELD_PRIORITY, searchDoc.Priority, Field.Store.YES));
        doc.Add(new Int64Field(FIELD_VIEW_COUNT, searchDoc.ViewCount, Field.Store.YES));

        return doc;
    }

    // FIXED: Simplified facet building without Filter parameter
    private Dictionary<string, List<FacetItem>> BuildFacets(
        IndexSearcher searcher,
        Query query)
    {
        var facets = new Dictionary<string, List<FacetItem>>();

        try
        {
            facets["ContentType"] = GetFacetCounts(searcher, query, FIELD_CONTENT_TYPE);
            facets["Language"] = GetFacetCounts(searcher, query, FIELD_LANGUAGE);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error building facets");
        }

        return facets;
    }

    // FIXED: Removed Filter parameter
    private List<FacetItem> GetFacetCounts(
        IndexSearcher searcher,
        Query query,
        string fieldName)
    {
        var facetCounts = new Dictionary<string, int>();
        var topDocs = searcher.Search(query, options.MaxResults);

        foreach (var scoreDoc in topDocs.ScoreDocs)
        {
            var doc = searcher.Doc(scoreDoc.Doc);
            var value = doc.Get(fieldName);

            if (!string.IsNullOrWhiteSpace(value))
            {
                if (facetCounts.ContainsKey(value))
                    facetCounts[value]++;
                else
                    facetCounts[value] = 1;
            }
        }

        return facetCounts
            .Select(kvp => new FacetItem { Value = kvp.Key, Count = kvp.Value })
            .OrderByDescending(f => f.Count)
            .ToList();
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            return content;

        var truncated = content.Substring(0, maxLength);
        var lastSpace = truncated.LastIndexOf(' ');

        return lastSpace > 0 ? truncated.Substring(0, lastSpace) + "..." : truncated + "...";
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        if (DateTimeOffset.TryParseExact(
            dateStr,
            "yyyyMMdd",
            null,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var result))
        {
            return result;
        }

        return null;
    }

    private void CommitChanges()
    {
        lock (writerLock)
        {
            try
            {
                if (indexWriter != null)
                {
                    indexWriter.Commit();
                    searcherManager?.MaybeRefresh();
                    logger.LogDebug("Index changes committed");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error committing index changes");
            }
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
        {
            commitTimer?.Dispose();

            lock (writerLock)
            {
                searcherManager?.Dispose();
                indexWriter?.Dispose();
                fsDirectory?.Dispose();
                ramDirectory?.Dispose();
                analyzer?.Dispose();
            }

            logger.LogInformation("Lucene search service disposed");
        }

        disposed = true;
    }

    #endregion
}