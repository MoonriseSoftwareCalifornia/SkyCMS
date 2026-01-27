// <copyright file="LuceneSearchServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Search;

using Cosmos.Common.Services.Search;
using Cosmos.Common.Services.Search.Configuration;
using Cosmos.Common.Services.Search.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>
/// Unit tests for the Lucene.Net search service implementation.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LuceneSearchServiceTests
{
    private LuceneSearchService? searchService;
    private Mock<ILogger<LuceneSearchService>>? mockLogger;
    private const string TEST_TENANT = "test.example.com";

    [TestInitialize]
    public void Setup()
    {
        mockLogger = new Mock<ILogger<LuceneSearchService>>();
        
        // Use Testing preset which uses in-memory RAM directory
        var options = Options.Create(LuceneSearchPresets.Testing);
        searchService = new LuceneSearchService(options, mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        searchService?.Dispose();
    }

    #region Indexing Tests

    [TestMethod]
    [TestCategory("Indexing")]
    public async Task IndexContentAsync_WithValidDocument_IndexesSuccessfully()
    {
        // Arrange
        var document = new SearchDocument
        {
            Id = "test-1",
            Title = "Test Document",
            Content = "This is test content for searching",
            TenantDomain = TEST_TENANT,
            ContentType = "article",
            Status = "published",
            PublishedDate = DateTimeOffset.UtcNow.AddDays(-1),
            Author = "Test Author"
        };

        // Act
        await searchService!.IndexContentAsync(document);

        // Assert - Search should find it
        var searchRequest = new SearchRequest
        {
            Query = "test",
            TenantDomain = TEST_TENANT,
            Page = 0,
            PageSize = 10
        };

        var results = await searchService.SearchAsync(searchRequest);
        
        Assert.AreEqual(1, results.TotalCount, "Should find exactly one document");
        Assert.AreEqual("Test Document", results.Items.First().Title);
        Assert.AreEqual("test-1", results.Items.First().Id);
    }

    [TestMethod]
    [TestCategory("Indexing")]
    public async Task IndexContentBulkAsync_WithMultipleDocuments_IndexesAll()
    {
        // Arrange
        var documents = new[]
        {
            new SearchDocument
            {
                Id = "doc-1",
                Title = "First Document",
                Content = "Content about programming",
                TenantDomain = TEST_TENANT,
                Status = "published"
            },
            new SearchDocument
            {
                Id = "doc-2",
                Title = "Second Document",
                Content = "Content about testing",
                TenantDomain = TEST_TENANT,
                Status = "published"
            },
            new SearchDocument
            {
                Id = "doc-3",
                Title = "Third Document",
                Content = "Content about deployment",
                TenantDomain = TEST_TENANT,
                Status = "published"
            }
        };

        // Act
        await searchService!.IndexContentBulkAsync(documents);

        // Assert
        var results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",  // Empty query returns all
            TenantDomain = TEST_TENANT,
            PageSize = 100
        });

        Assert.AreEqual(3, results.TotalCount, "Should find all three documents");
    }

    [TestMethod]
    [TestCategory("Indexing")]
    public async Task DeleteFromIndexAsync_ExistingDocument_RemovesSuccessfully()
    {
        // Arrange
        var document = new SearchDocument
        {
            Id = "delete-test-1",
            Title = "Document to Delete",
            Content = "This will be deleted",
            TenantDomain = TEST_TENANT,
            Status = "published"
        };

        await searchService!.IndexContentAsync(document);

        // Verify it exists
        var beforeDelete = await searchService.SearchAsync(new SearchRequest
        {
            Query = "deleted",
            TenantDomain = TEST_TENANT
        });
        Assert.AreEqual(1, beforeDelete.TotalCount);

        // Act
        await searchService.DeleteFromIndexAsync("delete-test-1");

        // Assert
        var afterDelete = await searchService.SearchAsync(new SearchRequest
        {
            Query = "deleted",
            TenantDomain = TEST_TENANT
        });
        Assert.AreEqual(0, afterDelete.TotalCount, "Document should be deleted");
    }

    [TestMethod]
    [TestCategory("Indexing")]
    public async Task DeleteFromIndexBulkAsync_MultipleDocuments_RemovesAll()
    {
        // Arrange
        var documents = new[]
        {
            new SearchDocument { Id = "bulk-1", Title = "Doc 1", Content = "Content 1", TenantDomain = TEST_TENANT, Status = "published" },
            new SearchDocument { Id = "bulk-2", Title = "Doc 2", Content = "Content 2", TenantDomain = TEST_TENANT, Status = "published" },
            new SearchDocument { Id = "bulk-3", Title = "Doc 3", Content = "Content 3", TenantDomain = TEST_TENANT, Status = "published" }
        };

        await searchService!.IndexContentBulkAsync(documents);

        // Act
        await searchService.DeleteFromIndexBulkAsync(new[] { "bulk-1", "bulk-2", "bulk-3" });

        // Assert
        var results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",
            TenantDomain = TEST_TENANT
        });

        Assert.AreEqual(0, results.TotalCount, "All documents should be deleted");
    }

    #endregion

    #region Search Tests

    [TestMethod]
    [TestCategory("Search")]
    public async Task SearchAsync_WithTitleMatch_ReturnsHigherScore()
    {
        // Arrange
        var documents = new[]
        {
            new SearchDocument
            {
                Id = "title-match",
                Title = "Programming Guide",
                Content = "Learn about software",
                TenantDomain = TEST_TENANT,
                Status = "published"
            },
            new SearchDocument
            {
                Id = "content-match",
                Title = "Software Guide",
                Content = "Learn programming techniques",
                TenantDomain = TEST_TENANT,
                Status = "published"
            }
        };

        await searchService!.IndexContentBulkAsync(documents);

        // Act
        var results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "programming",
            TenantDomain = TEST_TENANT
        });

        // Assert
        Assert.AreEqual(2, results.TotalCount);
        Assert.AreEqual("title-match", results.Items.First().Id, 
            "Title match should rank higher due to boosting");
    }

    [TestMethod]
    [TestCategory("Search")]
    public async Task SearchAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange - Create 25 documents
        var documents = Enumerable.Range(1, 25).Select(i => new SearchDocument
        {
            Id = $"page-test-{i}",
            Title = $"Document {i}",
            Content = $"Content for document number {i}",
            TenantDomain = TEST_TENANT,
            Status = "published"
        });

        await searchService!.IndexContentBulkAsync(documents);

        // Act - Get page 2 (0-based, so page 1)
        var page1Results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",
            TenantDomain = TEST_TENANT,
            Page = 1,
            PageSize = 10
        });

        // Assert
        Assert.AreEqual(25, page1Results.TotalCount);
        Assert.AreEqual(10, page1Results.Items.Count());
        Assert.AreEqual(3, page1Results.TotalPages);
        Assert.IsTrue(page1Results.HasNextPage);
        Assert.IsTrue(page1Results.HasPreviousPage);
    }

    [TestMethod]
    [TestCategory("Search")]
    public async Task SearchAsync_WithDateFilter_ReturnsOnlyMatchingDates()
    {
        // Arrange
        var oldDate = new DateTime(2023, 1, 1);
        var recentDate = DateTime.UtcNow.AddDays(-5);

        var documents = new[]
        {
            new SearchDocument
            {
                Id = "old-doc",
                Title = "Old Document",
                Content = "Old content",
                TenantDomain = TEST_TENANT,
                Status = "published",
                PublishedDate = new DateTimeOffset(oldDate)
            },
            new SearchDocument
            {
                Id = "recent-doc",
                Title = "Recent Document",
                Content = "Recent content",
                TenantDomain = TEST_TENANT,
                Status = "published",
                PublishedDate = new DateTimeOffset(recentDate)
            }
        };

        await searchService!.IndexContentBulkAsync(documents);

        // Act - Search for documents from last 10 days
        var results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",
            TenantDomain = TEST_TENANT,
            DateFrom = DateTime.UtcNow.AddDays(-10)
        });

        // Assert
        Assert.AreEqual(1, results.TotalCount);
        Assert.AreEqual("recent-doc", results.Items.First().Id);
    }

    [TestMethod]
    [TestCategory("Search")]
    public async Task SearchAsync_WithContentTypeFilter_ReturnsOnlyMatchingTypes()
    {
        // Arrange
        var documents = new[]
        {
            new SearchDocument
            {
                Id = "article-1",
                Title = "Article",
                Content = "Article content",
                TenantDomain = TEST_TENANT,
                ContentType = "article",
                Status = "published"
            },
            new SearchDocument
            {
                Id = "page-1",
                Title = "Page",
                Content = "Page content",
                TenantDomain = TEST_TENANT,
                ContentType = "page",
                Status = "published"
            }
        };

        await searchService!.IndexContentBulkAsync(documents);

        // Act
        var results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",
            TenantDomain = TEST_TENANT,
            ContentTypes = new[] { "article" }
        });

        // Assert
        Assert.AreEqual(1, results.TotalCount);
        Assert.AreEqual("article-1", results.Items.First().Id);
    }

    [TestMethod]
    [TestCategory("Search")]
    public async Task SearchAsync_WithSortByDate_OrdersCorrectly()
    {
        // Arrange
        var documents = new[]
        {
            new SearchDocument
            {
                Id = "newest",
                Title = "Newest",
                Content = "Content",
                TenantDomain = TEST_TENANT,
                Status = "published",
                PublishedDate = DateTimeOffset.UtcNow
            },
            new SearchDocument
            {
                Id = "oldest",
                Title = "Oldest",
                Content = "Content",
                TenantDomain = TEST_TENANT,
                Status = "published",
                PublishedDate = DateTimeOffset.UtcNow.AddDays(-10)
            },
            new SearchDocument
            {
                Id = "middle",
                Title = "Middle",
                Content = "Content",
                TenantDomain = TEST_TENANT,
                Status = "published",
                PublishedDate = DateTimeOffset.UtcNow.AddDays(-5)
            }
        };

        await searchService!.IndexContentBulkAsync(documents);

        // Act
        var results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",
            TenantDomain = TEST_TENANT,
            SortBy = "date"
        });

        // Assert
        Assert.AreEqual("newest", results.Items.First().Id);
        Assert.AreEqual("oldest", results.Items.Last().Id);
    }

    #endregion

    #region Multi-Tenant Tests

    [TestMethod]
    [TestCategory("MultiTenant")]
    public async Task SearchAsync_WithTenantFilter_ReturnsOnlyTenantDocuments()
    {
        // Arrange
        var tenant1Docs = new[]
        {
            new SearchDocument
            {
                Id = "tenant1-doc",
                Title = "Tenant 1 Document",
                Content = "Content",
                TenantDomain = "tenant1.com",
                Status = "published"
            }
        };

        var tenant2Docs = new[]
        {
            new SearchDocument
            {
                Id = "tenant2-doc",
                Title = "Tenant 2 Document",
                Content = "Content",
                TenantDomain = "tenant2.com",
                Status = "published"
            }
        };

        await searchService!.IndexContentBulkAsync(tenant1Docs);
        await searchService.IndexContentBulkAsync(tenant2Docs);

        // Act
        var tenant1Results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",
            TenantDomain = "tenant1.com"
        });

        var tenant2Results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",
            TenantDomain = "tenant2.com"
        });

        // Assert
        Assert.AreEqual(1, tenant1Results.TotalCount);
        Assert.AreEqual("tenant1-doc", tenant1Results.Items.First().Id);

        Assert.AreEqual(1, tenant2Results.TotalCount);
        Assert.AreEqual("tenant2-doc", tenant2Results.Items.First().Id);
    }

    #endregion

    #region Suggestions Tests

    [TestMethod]
    [TestCategory("Suggestions")]
    public async Task GetSuggestionsAsync_WithPartialQuery_ReturnsSuggestions()
    {
        // Arrange
        var documents = new[]
        {
            new SearchDocument
            {
                Id = "sug-1",
                Title = "Programming Tutorial",
                Content = "Content",
                TenantDomain = TEST_TENANT,
                Status = "published"
            },
            new SearchDocument
            {
                Id = "sug-2",
                Title = "Programming Guide",
                Content = "Content",
                TenantDomain = TEST_TENANT,
                Status = "published"
            },
            new SearchDocument
            {
                Id = "sug-3",
                Title = "Testing Guide",
                Content = "Content",
                TenantDomain = TEST_TENANT,
                Status = "published"
            }
        };

        await searchService!.IndexContentBulkAsync(documents);

        // Act
        var suggestions = await searchService.GetSuggestionsAsync("prog", 5);

        // Assert
        Assert.IsTrue(suggestions.Any(), "Should return suggestions");
        Assert.IsTrue(suggestions.All(s => s.Contains("Programming")));
        Assert.AreEqual(2, suggestions.Count());
    }

    [TestMethod]
    [TestCategory("Suggestions")]
    public async Task GetSuggestionsAsync_WithEmptyQuery_ReturnsEmpty()
    {
        // Act
        var suggestions = await searchService!.GetSuggestionsAsync("", 5);

        // Assert
        Assert.AreEqual(0, suggestions.Count());
    }

    #endregion

    #region Health Check Tests

    [TestMethod]
    [TestCategory("Health")]
    public async Task HealthCheckAsync_WhenHealthy_ReturnsTrue()
    {
        // Act
        var isHealthy = await searchService!.HealthCheckAsync();

        // Assert
        Assert.IsTrue(isHealthy, "Search service should be healthy");
    }

    #endregion

    #region Clear and Rebuild Tests

    [TestMethod]
    [TestCategory("Maintenance")]
    public async Task ClearIndexAsync_RemovesAllDocuments()
    {
        // Arrange
        var documents = new[]
        {
            new SearchDocument { Id = "clear-1", Title = "Doc 1", Content = "Content", TenantDomain = TEST_TENANT, Status = "published" },
            new SearchDocument { Id = "clear-2", Title = "Doc 2", Content = "Content", TenantDomain = TEST_TENANT, Status = "published" }
        };

        await searchService!.IndexContentBulkAsync(documents);

        // Verify they exist
        var beforeClear = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",
            TenantDomain = TEST_TENANT
        });
        Assert.AreEqual(2, beforeClear.TotalCount);

        // Act
        await searchService.ClearIndexAsync();

        // Assert
        var afterClear = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",
            TenantDomain = TEST_TENANT
        });
        Assert.AreEqual(0, afterClear.TotalCount, "Index should be empty");
    }

    #endregion

    #region Highlighting Tests

    [TestMethod]
    [TestCategory("Highlighting")]
    public async Task SearchAsync_WithQuery_GeneratesHighlights()
    {
        // Arrange
        var document = new SearchDocument
        {
            Id = "highlight-test",
            Title = "Programming Guide",
            Content = "This is a comprehensive guide to programming in C# and .NET",
            TenantDomain = TEST_TENANT,
            Status = "published"
        };

        await searchService!.IndexContentAsync(document);

        // Act
        var results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "programming",
            TenantDomain = TEST_TENANT
        });

        // Assert
        var result = results.Items.First();
        Assert.IsNotNull(result.HighlightedContent);
        Assert.IsTrue(result.HighlightedContent.Contains("<mark>"), 
            "Highlighted content should contain mark tags");
    }

    #endregion

    #region Facets Tests

    [TestMethod]
    [TestCategory("Facets")]
    public async Task SearchAsync_GeneratesFacets()
    {
        // Arrange
        var documents = new[]
        {
            new SearchDocument { Id = "f1", Title = "Doc 1", Content = "Content", TenantDomain = TEST_TENANT, ContentType = "article", Status = "published" },
            new SearchDocument { Id = "f2", Title = "Doc 2", Content = "Content", TenantDomain = TEST_TENANT, ContentType = "article", Status = "published" },
            new SearchDocument { Id = "f3", Title = "Doc 3", Content = "Content", TenantDomain = TEST_TENANT, ContentType = "page", Status = "published" }
        };

        await searchService!.IndexContentBulkAsync(documents);

        // Act
        var results = await searchService.SearchAsync(new SearchRequest
        {
            Query = "",
            TenantDomain = TEST_TENANT
        });

        // Assert
        Assert.IsTrue(results.Facets.ContainsKey("ContentType"));
        var contentTypeFacet = results.Facets["ContentType"];
        Assert.AreEqual(2, contentTypeFacet.Count); // article and page
        
        var articleFacet = contentTypeFacet.First(f => f.Value == "article");
        Assert.AreEqual(2, articleFacet.Count);
    }

    #endregion
}