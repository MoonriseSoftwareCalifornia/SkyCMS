// <copyright file="SearchIntegrationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Integration.Search;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sky.Cms.Api.Shared.Features.Search.Query;
using Sky.Cms.Api.Shared.Models.Search;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Models;
using System.Diagnostics;
using Cosmos.Common.Data;

/// <summary>
/// Integration tests for the complete search functionality workflow.
/// Tests the entire pipeline from query to response with real data.
/// </summary>
[DoNotParallelize]
[TestClass]
public class SearchIntegrationTests : SkyCmsTestBase
{
    private SearchQueryHandler _searchHandler = null!;
    private Cosmos.Common.Data.Logic.ArticleLogic _articleLogic = null!;

    /// <summary>
    /// Initializes the test class before each test method runs.
    /// </summary>
    [TestInitialize]
    public new async Task Setup()
    {
        InitializeTestContext();
        
        // Create ArticleLogic for search handler
        _articleLogic = new Cosmos.Common.Data.Logic.ArticleLogic(
            Db,
            Cache,
            "http://localhost:5001",  // publisherUrl
            "",                       // blobPublicUrl
            false                     // isEditor
        );

        // Setup the search handler with real dependencies
        // FIXED: Constructor signature changed - ApplicationDbContext is now first parameter
        _searchHandler = new SearchQueryHandler(
            Db,
            DynamicConfigurationProvider,
            _articleLogic,
            new LoggerFactory().CreateLogger<SearchQueryHandler>()
        );

        // Seed test data for integration tests
        await SeedTestData();
    }

    /// <summary>
    /// Cleans up after each test method.
    /// </summary>
    [TestCleanup]
    public new async Task Cleanup()
    {
        await CleanupTestData();
        await Db.DisposeAsync();
    }

    #region Integration Tests

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWorkflow_WithTestData_ReturnsExpectedResults()
    {
        // Arrange
        var searchQuery = new SearchQuery
        {
            Query = "programming",
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var response = await _searchHandler.HandleAsync(searchQuery, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("programming", response.Query);
        Assert.IsTrue(response.TotalResults > 0, "Should find articles with 'programming' in content");
        Assert.IsTrue(response.Results.Count > 0, "Should return search results");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "Search should complete within 5 seconds");
        
        // Verify result structure - FIXED property names
        var firstResult = response.Results.First();
        Assert.IsFalse(string.IsNullOrWhiteSpace(firstResult.Title));
        Assert.IsFalse(string.IsNullOrWhiteSpace(firstResult.Content));
        Assert.IsFalse(string.IsNullOrWhiteSpace(firstResult.Url));  // FIXED: UrlPath → Url
        Assert.IsTrue(firstResult.PublishDate != default);  // FIXED: PublishedDate → PublishDate
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWorkflow_WithPagination_ReturnsCorrectPage()
    {
        // Arrange - Get first page
        var firstPageQuery = new SearchQuery
        {
            Query = "test",
            PageNumber = 1,
            PageSize = 2
        };

        // Act - Get first page
        var firstPageResponse = await _searchHandler.HandleAsync(firstPageQuery, CancellationToken.None);

        // Arrange - Get second page if there are enough results
        if (firstPageResponse.TotalResults > 2)
        {
            var secondPageQuery = new SearchQuery
            {
                Query = "test",
                PageNumber = 2,
                PageSize = 2
            };

            // Act - Get second page
            var secondPageResponse = await _searchHandler.HandleAsync(secondPageQuery, CancellationToken.None);

            // Assert
            Assert.AreEqual(firstPageResponse.TotalResults, secondPageResponse.TotalResults);
            Assert.AreEqual(2, secondPageResponse.PageNumber);
            Assert.AreNotEqual(firstPageResponse.Results.First().Id, secondPageResponse.Results.First().Id);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWorkflow_WithDateFiltering_ReturnsFilteredResults()
    {
        // Arrange
        var recentDate = DateTime.UtcNow.AddDays(-30);
        var searchQuery = new SearchQuery
        {
            Query = "test",
            PageNumber = 1,
            PageSize = 10,
            DateFrom = recentDate
        };

        // Act
        var response = await _searchHandler.HandleAsync(searchQuery, CancellationToken.None);

        // Assert
        Assert.IsNotNull(response);
        foreach (var result in response.Results)
        {
            // FIXED: PublishedDate → PublishDate
            Assert.IsTrue(result.PublishDate >= recentDate, 
                "All results should be published after the filter date");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWorkflow_WithEmptyQuery_ReturnsAllPublishedResults()
    {
        // Arrange
        var searchQuery = new SearchQuery
        {
            Query = "",
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var response = await _searchHandler.HandleAsync(searchQuery, CancellationToken.None);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("", response.Query);
        // FIXED: Empty query now returns all published articles (not 0)
        Assert.IsTrue(response.TotalResults >= 5, "Should return at least the 5 seeded articles");
        Assert.IsTrue(response.Results.Count > 0);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWorkflow_WithNonExistentTerm_ReturnsNoResults()
    {
        // Arrange
        var searchQuery = new SearchQuery
        {
            Query = "nonexistentternthatshoulnotmatchanything12345",
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var response = await _searchHandler.HandleAsync(searchQuery, CancellationToken.None);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual(0, response.TotalResults);
        Assert.AreEqual(0, response.Results.Count);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SearchWorkflow_WithSpecialCharacters_HandlesGracefully()
    {
        // Arrange
        var searchQuery = new SearchQuery
        {
            Query = "test@#$%^&*()!",
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var response = await _searchHandler.HandleAsync(searchQuery, CancellationToken.None);

        // Assert - Should not throw and should return a response
        Assert.IsNotNull(response);
        Assert.AreEqual("test@#$%^&*()!", response.Query);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [ExpectedException(typeof(OperationCanceledException))]
    public async Task SearchWorkflow_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var searchQuery = new SearchQuery
        {
            Query = "test",
            PageNumber = 1,
            PageSize = 10
        };

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        await _searchHandler.HandleAsync(searchQuery, cancellationTokenSource.Token);
    }

    #endregion

    #region Performance Tests

    [TestMethod]
    [TestCategory("Performance")]
    public async Task SearchWorkflow_PerformanceTest_CompletesWithinTimeout()
    {
        // Arrange
        var searchQueries = new List<SearchQuery>
        {
            new() { Query = "test", PageNumber = 1, PageSize = 10 },
            new() { Query = "article", PageNumber = 1, PageSize = 20 },
            new() { Query = "programming", PageNumber = 2, PageSize = 5 },
            new() { Query = "guide", PageNumber = 1, PageSize = 15 }
        };

        var maxExecutionTimeMs = 2000; // 2 seconds max for all queries

        // Act
        var stopwatch = Stopwatch.StartNew();
        var tasks = searchQueries.Select(query => 
            _searchHandler.HandleAsync(query, CancellationToken.None));
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < maxExecutionTimeMs, 
            $"All search queries should complete within {maxExecutionTimeMs}ms. Actual: {stopwatch.ElapsedMilliseconds}ms");
        
        Assert.AreEqual(4, results.Length);
        Assert.IsTrue(results.All(r => r != null), "All queries should return valid responses");
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task SearchWorkflow_LargePageSize_HandlesEfficiently()
    {
        // Arrange
        var searchQuery = new SearchQuery
        {
            Query = "test",
            PageNumber = 1,
            PageSize = 100 // Large page size
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var response = await _searchHandler.HandleAsync(searchQuery, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.IsNotNull(response);
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 3000, "Large page search should complete within 3 seconds");
        Assert.IsTrue(response.Results.Count <= 100, "Should not return more results than page size");
    }

    #endregion

    #region Helper Methods

    private async Task SeedTestData()
    {
        // Create test articles for search testing
        var testArticles = new[]
        {
            new Article
            {
                Id = Guid.NewGuid(),
                Title = "Programming Guide",
                Content = "A comprehensive guide to programming with C# and .NET",
                UrlPath = "programming-guide",
                StatusCode = (int)StatusCodeEnum.Active,  // FIXED: Use StatusCodeEnum
                Published = DateTime.UtcNow.AddDays(-1),
                Updated = DateTime.UtcNow.AddDays(-1),
                ArticleNumber = 100,
                VersionNumber = 1
            },
            new Article
            {
                Id = Guid.NewGuid(),
                Title = "Web Development Tutorial",
                Content = "Learn web development with modern frameworks and tools",
                UrlPath = "web-development",
                StatusCode = (int)StatusCodeEnum.Active,
                Published = DateTime.UtcNow.AddDays(-2),
                Updated = DateTime.UtcNow.AddDays(-2),
                ArticleNumber = 101,
                VersionNumber = 1
            },
            new Article
            {
                Id = Guid.NewGuid(),
                Title = "Testing Best Practices",
                Content = "Best practices for unit testing and integration testing",
                UrlPath = "testing-practices",
                StatusCode = (int)StatusCodeEnum.Active,
                Published = DateTime.UtcNow.AddDays(-3),
                Updated = DateTime.UtcNow.AddDays(-3),
                ArticleNumber = 102,
                VersionNumber = 1
            },
            new Article
            {
                Id = Guid.NewGuid(),
                Title = "Database Design",
                Content = "How to design efficient database schemas",
                UrlPath = "database-design",
                StatusCode = (int)StatusCodeEnum.Active,
                Published = DateTime.UtcNow.AddDays(-4),
                Updated = DateTime.UtcNow.AddDays(-4),
                ArticleNumber = 103,
                VersionNumber = 1
            },
            new Article
            {
                Id = Guid.NewGuid(),
                Title = "API Development",
                Content = "Building RESTful APIs with ASP.NET Core",
                UrlPath = "api-development",
                StatusCode = (int)StatusCodeEnum.Active,
                Published = DateTime.UtcNow.AddDays(-5),
                Updated = DateTime.UtcNow.AddDays(-5),
                ArticleNumber = 104,
                VersionNumber = 1
            }
        };

        Db.Articles.AddRange(testArticles);
        await Db.SaveChangesAsync();
    }

    private async Task CleanupTestData()
    {
        // Clean up test articles
        var testArticles = Db.Articles.Where(a => 
            a.Title.Contains("Programming Guide") ||
            a.Title.Contains("Web Development Tutorial") ||
            a.Title.Contains("Testing Best Practices") ||
            a.Title.Contains("Database Design") ||
            a.Title.Contains("API Development"));

        Db.Articles.RemoveRange(testArticles);
        await Db.SaveChangesAsync();
    }

    #endregion
}