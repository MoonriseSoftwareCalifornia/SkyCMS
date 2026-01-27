// <copyright file="SearchQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Search;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Cms.Api.Shared.Features.Search.Query;
using Sky.Cms.Api.Shared.Models.Search;
using Cosmos.Common.Data.Logic;
using Cosmos.DynamicConfig;
using Cosmos.Common.Models;
using Cosmos.Common.Data;

/// <summary>
/// Unit tests for the <see cref="SearchQueryHandler"/> class.
/// </summary>
[DoNotParallelize]
[TestClass]
public class SearchQueryHandlerTests : SkyCmsTestBase
{
    private SearchQueryHandler _searchQueryHandler = null!;
    private Mock<ILogger<SearchQueryHandler>> _mockLogger = null!;
    private ArticleLogic _articleLogic = null!;
    private Mock<IDynamicConfigurationProvider> _mockDynamicConfig = null!;

    /// <summary>
    /// Initializes the test class before each test method runs.
    /// </summary>
    [TestInitialize]
    public new async Task Setup()
    {
        base.Setup(); // Call base setup to initialize Db
        
        _mockLogger = new Mock<ILogger<SearchQueryHandler>>();
        _mockDynamicConfig = new Mock<IDynamicConfigurationProvider>();
        
        // Create real ArticleLogic (not mocked) since SearchQueryHandler now queries the DB directly
        _articleLogic = new ArticleLogic(
            Db!, 
            Cache!,
            "http://localhost:5001",  // publisherUrl
            "",                       // blobPublicUrl
            false                     // isEditor
        );

        // Setup dynamic config mock - FIXED: Use GetTenantDomainNameFromRequest() instead
        _mockDynamicConfig.Setup(x => x.GetTenantDomainNameFromRequest())
            .Returns("test.domain.com");

        // FIXED: Constructor signature changed - now requires ApplicationDbContext first
        _searchQueryHandler = new SearchQueryHandler(
            Db!,
            _mockDynamicConfig.Object,
            _articleLogic,
            _mockLogger.Object
        );

        // Add test data to the database
        await SeedTestDataAsync();
    }

    /// <summary>
    /// Seeds test data for search tests.
    /// </summary>
    private async Task SeedTestDataAsync()
    {
        var testArticles = new[]
        {
            new Article
            {
                Id = Guid.NewGuid(),
                Title = "Test Article 1",
                Content = "This is test content about programming",
                UrlPath = "test-article-1",
                StatusCode = (int)StatusCodeEnum.Active, // Published
                Published = DateTime.UtcNow.AddDays(-1),
                Updated = DateTime.UtcNow,
                ArticleNumber = 1,
                VersionNumber = 1
            },
            new Article
            {
                Id = Guid.NewGuid(),
                Title = "Test Article 2",
                Content = "Another test article with different content",
                UrlPath = "test-article-2",
                StatusCode = (int)StatusCodeEnum.Active, // Published
                Published = DateTime.UtcNow.AddDays(-2),
                Updated = DateTime.UtcNow.AddDays(-1),
                ArticleNumber = 2,
                VersionNumber = 1
            },
            new Article
            {
                Id = Guid.NewGuid(),
                Title = "Programming Guide",
                Content = "This is a comprehensive guide to programming with examples and best practices.",
                UrlPath = "programming-guide",
                StatusCode = (int)StatusCodeEnum.Active,
                Published = DateTime.UtcNow.AddDays(-3),
                Updated = DateTime.UtcNow.AddDays(-2),
                ArticleNumber = 3,
                VersionNumber = 1
            },
            new Article
            {
                Id = Guid.NewGuid(),
                Title = "Unpublished Article",
                Content = "This article is not published",
                UrlPath = "unpublished-article",
                StatusCode = (int)StatusCodeEnum.Inactive, // Unpublished
                Published = null,
                Updated = DateTime.UtcNow.AddDays(-4),
                ArticleNumber = 4,
                VersionNumber = 1
            }
        };

        Db!.Articles.AddRange(testArticles);
        await Db.SaveChangesAsync();
    }

    /// <summary>
    /// Cleans up after each test method.
    /// </summary>
    [TestCleanup]
    public async Task Cleanup()
    {
        await Db!.DisposeAsync();
    }

    #region Basic Functionality Tests

    [TestMethod]
    [TestCategory("BasicFunctionality")]
    public async Task HandleAsync_WithValidQuery_ReturnsSearchResults()
    {
        // Arrange
        var searchQuery = new SearchQuery
        {
            Query = "test",
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var result = await _searchQueryHandler.HandleAsync(searchQuery, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("test", result.Query);
        Assert.AreEqual(2, result.TotalResults); // "Test Article 1" and "Test Article 2"
        Assert.AreEqual(1, result.PageNumber);
        Assert.AreEqual(10, result.PageSize);
        Assert.AreEqual(1, result.TotalPages);
        Assert.AreEqual(2, result.Results.Count);
        
        var firstResult = result.Results.First();
        Assert.IsTrue(firstResult.Title.Contains("Test"));
        Assert.IsTrue(firstResult.Content.Contains("test"));
    }

    [TestMethod]
    [TestCategory("BasicFunctionality")]
    public async Task HandleAsync_WithEmptyQuery_ReturnsAllPublishedResults()
    {
        // Arrange
        var searchQuery = new SearchQuery
        {
            Query = "",
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var result = await _searchQueryHandler.HandleAsync(searchQuery, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("", result.Query);
        Assert.AreEqual(3, result.TotalResults); // All 3 published articles
        Assert.AreEqual(3, result.Results.Count);
    }

    [TestMethod]
    [TestCategory("BasicFunctionality")]
    public async Task HandleAsync_WithNullQuery_HandlesGracefully()
    {
        // Arrange
        var searchQuery = new SearchQuery
        {
            Query = null!,
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var result = await _searchQueryHandler.HandleAsync(searchQuery, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.TotalResults); // All published articles
        Assert.AreEqual(3, result.Results.Count);
    }

    #endregion

    #region Pagination Tests

    [TestMethod]
    [TestCategory("Pagination")]
    public async Task HandleAsync_WithPagination_CalculatesCorrectTotalPages()
    {
        // Arrange - Add more test data for pagination
        var moreArticles = Enumerable.Range(10, 20).Select(i => new Article
        {
            Id = Guid.NewGuid(),
            Title = $"Additional Article {i}",
            Content = $"Content for article {i}",
            UrlPath = $"additional-article-{i}",
            StatusCode = (int)StatusCodeEnum.Active,
            Published = DateTime.UtcNow.AddDays(-i),
            Updated = DateTime.UtcNow.AddDays(-i),
            ArticleNumber = i,
            VersionNumber = 1
        });

        Db!.Articles.AddRange(moreArticles);
        await Db.SaveChangesAsync();

        var searchQuery = new SearchQuery
        {
            Query = "Article", // Will match many articles
            PageNumber = 2,
            PageSize = 5
        };

        // Act
        var result = await _searchQueryHandler.HandleAsync(searchQuery, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.PageNumber);
        Assert.AreEqual(5, result.PageSize);
        Assert.IsTrue(result.TotalPages > 1);
        Assert.IsTrue(result.Results.Count <= 5);
    }

    [TestMethod]
    [TestCategory("Pagination")]
    public async Task HandleAsync_WithExactPageSize_CalculatesCorrectTotalPages()
    {
        // Arrange - Add exactly 30 articles for clean pagination
        var exactArticles = Enumerable.Range(100, 27).Select(i => new Article
        {
            Id = Guid.NewGuid(),
            Title = $"Exact Article {i}",
            Content = $"Exact content {i}",
            UrlPath = $"exact-article-{i}",
            StatusCode = (int)StatusCodeEnum.Active,
            Published = DateTime.UtcNow.AddDays(-i),
            Updated = DateTime.UtcNow.AddDays(-i),
            ArticleNumber = i,
            VersionNumber = 1
        });

        Db!.Articles.AddRange(exactArticles);
        await Db.SaveChangesAsync();

        var searchQuery = new SearchQuery
        {
            Query = "", // Get all articles
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var result = await _searchQueryHandler.HandleAsync(searchQuery, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(30, result.TotalResults); // 3 initial + 27 added = 30
        Assert.AreEqual(3, result.TotalPages); // Exactly 3 pages
        Assert.AreEqual(10, result.Results.Count);
    }

    #endregion

    #region Content Highlighting Tests

    [TestMethod]
    [TestCategory("ContentHighlighting")]
    public async Task HandleAsync_WithSearchTerms_HighlightsContent()
    {
        // Arrange
        var searchQuery = new SearchQuery
        {
            Query = "programming",
            PageNumber = 1,
            PageSize = 10,
            IncludeHighlights = true
        };

        // Act
        var result = await _searchQueryHandler.HandleAsync(searchQuery, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Results.Count > 0);
        
        var searchResult = result.Results.First();
        Assert.IsTrue(!string.IsNullOrEmpty(searchResult.HighlightedContent));
        Assert.IsTrue(searchResult.HighlightedContent.Contains("<mark>"));
        Assert.IsTrue(searchResult.HighlightedContent.Contains("</mark>"));
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    [TestCategory("ErrorHandling")]
    public async Task HandleAsync_WithCancellation_ThrowsOperationCanceledException()
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
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            async () => await _searchQueryHandler.HandleAsync(searchQuery, cancellationTokenSource.Token)
        );
    }

    #endregion

    #region Helper Methods

    private static List<SearchResultItem> CreateMockSearchResults(int count)
    {
        var results = new List<SearchResultItem>();
        for (int i = 1; i <= count; i++)
        {
            results.Add(new SearchResultItem
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Test Article {i}",
                Content = $"This is test content for article {i}",
                Url = $"/test-article-{i}",  // FIXED: UrlPath → Url
                PublishDate = DateTime.UtcNow.AddDays(-i),  // FIXED: DateTimeOffset → DateTime
                LastModified = DateTime.UtcNow.AddDays(-i + 1)  // FIXED: Updated → LastModified
            });
        }
        return results;
    }

    #endregion
}