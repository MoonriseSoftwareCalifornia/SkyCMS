// <copyright file="SearchApiControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers.Api;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Cms.Api.Shared.Controllers;
using Sky.Cms.Api.Shared.Features.Search.Query;
using Sky.Cms.Api.Shared.Features.Search.Suggest;
using Sky.Cms.Api.Shared.Models.Search;
using Cosmos.Common.Features.Shared;

/// <summary>
/// Unit tests for the <see cref="SearchApiController"/> class.
/// </summary>
[DoNotParallelize]
[TestClass]
public class SearchApiControllerTests : SkyCmsTestBase
{
    private SearchApiController _controller = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<ILogger<SearchApiController>> _mockLogger = null!;

    /// <summary>
    /// Initializes the test class before each test method runs.
    /// </summary>
    [TestInitialize]
    public new void Setup()
    {
        InitializeTestContext();
        
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<SearchApiController>>();
        
        _controller = new SearchApiController(_mockMediator.Object, _mockLogger.Object);
        
        // Setup HTTP context for controller
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    /// <summary>
    /// Cleans up after each test method.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        Db?.Dispose();
        // REMOVED: _controller?.Dispose() - Controller doesn't implement IDisposable
    }

    #region Search Tests

    [TestMethod]
    [TestCategory("Search")]
    public async Task SearchAsync_WithValidQuery_ReturnsOkResult()
    {
        // Arrange
        var request = new SearchApiRequest
        {
            Query = "test",
            PageNumber = 1,
            PageSize = 20
        };

        var expectedResponse = new SearchApiResponse
        {
            Query = "test",
            TotalResults = 2,
            PageNumber = 1,
            PageSize = 20,
            TotalPages = 1,
            SearchTimeMs = 150,
            Results = new List<SearchResultItem>
            {
                new() 
                { 
                    Id = Guid.NewGuid().ToString(),  // FIXED: Guid → string
                    Title = "Test Article 1",
                    Content = "This is test content",
                    Url = "/test-article-1",  // FIXED: UrlPath → Url
                    PublishDate = DateTime.UtcNow.AddDays(-1),  // FIXED: DateTimeOffset → DateTime
                    LastModified = DateTime.UtcNow
                },
                new() 
                { 
                    Id = Guid.NewGuid().ToString(),
                    Title = "Test Article 2",
                    Content = "Another test article",
                    Url = "/test-article-2",
                    PublishDate = DateTime.UtcNow.AddDays(-2),
                    LastModified = DateTime.UtcNow.AddDays(-1)
                }
            }
        };

        _mockMediator
            .Setup(x => x.QueryAsync(  // FIXED: Removed type arguments
                It.IsAny<SearchQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SearchAsync(request);  // FIXED: Method name and signature

        // Assert
        Assert.IsInstanceOfType<ActionResult<SearchApiResponse>>(result);
        var actionResult = result.Result as OkObjectResult;
        Assert.IsNotNull(actionResult);
        var response = (SearchApiResponse)actionResult.Value!;
        
        Assert.AreEqual("test", response.Query);
        Assert.AreEqual(2, response.TotalResults);
        Assert.AreEqual(1, response.PageNumber);
        Assert.AreEqual(20, response.PageSize);
        Assert.AreEqual(2, response.Results.Count);
    }

    [TestMethod]
    [TestCategory("Search")]
    public async Task SearchAsync_WithEmptyQuery_ReturnsResults()
    {
        // Arrange
        var request = new SearchApiRequest
        {
            Query = "",
            PageNumber = 1,
            PageSize = 20
        };

        var expectedResponse = new SearchApiResponse
        {
            Query = "",
            TotalResults = 5,  // FIXED: Empty query returns all published
            PageNumber = 1,
            PageSize = 20,
            TotalPages = 1,
            SearchTimeMs = 10,
            Results = CreateMockSearchResultItems(5)
        };

        _mockMediator
            .Setup(x => x.QueryAsync(
                It.IsAny<SearchQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SearchAsync(request);

        // Assert
        var actionResult = result.Result as OkObjectResult;
        Assert.IsNotNull(actionResult);
        var response = (SearchApiResponse)actionResult.Value!;
        
        Assert.AreEqual("", response.Query);
        Assert.AreEqual(5, response.TotalResults);
    }

    [TestMethod]
    [TestCategory("Search")]
    public async Task SearchAsync_WithPaginationParameters_PassesCorrectValues()
    {
        // Arrange
        var request = new SearchApiRequest
        {
            Query = "test",
            PageNumber = 3,
            PageSize = 10,
            ContentTypes = new[] { "article", "page" },
            DateFrom = new DateTime(2024, 1, 1),
            DateTo = new DateTime(2024, 12, 31),
            SortBy = "date"
        };

        var expectedResponse = new SearchApiResponse
        {
            Query = "test",
            TotalResults = 100,
            PageNumber = 3,
            PageSize = 10,
            TotalPages = 10,
            SearchTimeMs = 200,
            Results = CreateMockSearchResultItems(10)
        };

        SearchQuery? capturedQuery = null;
        _mockMediator
            .Setup(x => x.QueryAsync(
                It.IsAny<SearchQuery>(),
                It.IsAny<CancellationToken>()))
            .Callback<IQuery<SearchApiResponse>, CancellationToken>((query, ct) => 
                capturedQuery = query as SearchQuery)
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SearchAsync(request);

        // Assert
        Assert.IsNotNull(capturedQuery);
        Assert.AreEqual("test", capturedQuery.Query);
        Assert.AreEqual(3, capturedQuery.PageNumber);
        Assert.AreEqual(10, capturedQuery.PageSize);
        Assert.IsNotNull(capturedQuery.ContentTypes);
        Assert.AreEqual(2, capturedQuery.ContentTypes.Length);
        Assert.AreEqual("article", capturedQuery.ContentTypes[0]);
        Assert.AreEqual("page", capturedQuery.ContentTypes[1]);
        Assert.AreEqual(new DateTime(2024, 1, 1), capturedQuery.DateFrom);
        Assert.AreEqual(new DateTime(2024, 12, 31), capturedQuery.DateTo);
        Assert.AreEqual("date", capturedQuery.SortBy);
    }

    #endregion

    #region Suggestions Tests

    [TestMethod]
    [TestCategory("Suggestions")]
    public async Task GetSuggestionsAsync_WithValidTerm_ReturnsOkResult()
    {
        // Arrange
        var expectedResponse = new SearchSuggestionsApiResponse  // FIXED: Type name
        {
            Query = "test",
            Suggestions = new[] 
            {
                "test article",
                "testing guide",
                "test framework",
                "test automation",
                "test patterns"
            },
            GenerationTimeMs = 50
        };

        _mockMediator
            .Setup(x => x.QueryAsync(  // FIXED: Removed type arguments
                It.IsAny<SearchSuggestionsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetSuggestionsAsync("test", 5);  // FIXED: Method name

        // Assert
        var actionResult = result.Result as OkObjectResult;
        Assert.IsNotNull(actionResult);
        var response = (SearchSuggestionsApiResponse)actionResult.Value!;
        
        Assert.AreEqual("test", response.Query);
        Assert.AreEqual(5, response.Suggestions.Length);
        Assert.IsTrue(response.Suggestions.All(s => s.Contains("test")));
    }

    [TestMethod]
    [TestCategory("Suggestions")]
    public async Task GetSuggestionsAsync_WithShortTerm_ReturnsEmptyResults()
    {
        // Arrange - Query less than 2 characters returns empty without hitting mediator

        // Act
        var result = await _controller.GetSuggestionsAsync("t", 5);  // Only 1 character

        // Assert
        var actionResult = result.Result as OkObjectResult;
        Assert.IsNotNull(actionResult);
        var response = (SearchSuggestionsApiResponse)actionResult.Value!;
        
        Assert.AreEqual(0, response.Suggestions.Length);
    }

    #endregion

    #region Health Check Tests

    [TestMethod]
    [TestCategory("HealthCheck")]
    public async Task GetHealthAsync_WhenHealthy_ReturnsOkResult()
    {
        // Arrange
        var expectedResponse = new SearchHealthApiResponse  // FIXED: Type name
        {
            IsHealthy = true,
            StatusMessage = "Search service is healthy",
            Version = "1.0.0",
            LastChecked = DateTime.UtcNow,
            Metrics = new Dictionary<string, object>
            {
                ["TotalDocuments"] = 1250,
                ["ResponseTimeMs"] = 25
            }
        };

        _mockMediator
            .Setup(x => x.QueryAsync(  // FIXED: Removed type arguments
                It.IsAny<SearchHealthQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetHealthAsync();  // FIXED: Method name

        // Assert
        var actionResult = result.Result as OkObjectResult;
        Assert.IsNotNull(actionResult);
        var response = (SearchHealthApiResponse)actionResult.Value!;
        
        Assert.IsTrue(response.IsHealthy);
        Assert.AreEqual("Search service is healthy", response.StatusMessage);
        Assert.IsTrue(response.Metrics.Count > 0);
    }

    [TestMethod]
    [TestCategory("HealthCheck")]
    public async Task GetHealthAsync_WhenUnhealthy_ReturnsServiceUnavailableStatus()
    {
        // Arrange
        var expectedResponse = new SearchHealthApiResponse
        {
            IsHealthy = false,
            StatusMessage = "Database connection failed",
            Version = "1.0.0",
            LastChecked = DateTime.UtcNow,
            Metrics = new Dictionary<string, object>()
        };

        _mockMediator
            .Setup(x => x.QueryAsync(
                It.IsAny<SearchHealthQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetHealthAsync();

        // Assert
        var actionResult = result.Result as ObjectResult;
        Assert.IsNotNull(actionResult);
        Assert.AreEqual(503, actionResult.StatusCode);  // Service Unavailable
        var response = (SearchHealthApiResponse)actionResult.Value!;
        
        Assert.IsFalse(response.IsHealthy);
        Assert.AreEqual("Database connection failed", response.StatusMessage);
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    [TestCategory("ErrorHandling")]
    public async Task SearchAsync_WhenMediatorThrows_ReturnsInternalServerError()
    {
        // Arrange
        var request = new SearchApiRequest
        {
            Query = "test",
            PageNumber = 1,
            PageSize = 20
        };

        _mockMediator
            .Setup(x => x.QueryAsync(
                It.IsAny<SearchQuery>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.SearchAsync(request);

        // Assert
        var actionResult = result.Result as ObjectResult;
        Assert.IsNotNull(actionResult);
        Assert.AreEqual(500, actionResult.StatusCode);
    }

    [TestMethod]
    [TestCategory("ErrorHandling")]
    public async Task GetSuggestionsAsync_WhenMediatorThrows_ReturnsInternalServerError()
    {
        // Arrange
        _mockMediator
            .Setup(x => x.QueryAsync(
                It.IsAny<SearchSuggestionsQuery>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.GetSuggestionsAsync("test", 5);

        // Assert
        var actionResult = result.Result as ObjectResult;
        Assert.IsNotNull(actionResult);
        Assert.AreEqual(500, actionResult.StatusCode);
    }

    #endregion

    #region Helper Methods

    private static List<SearchResultItem> CreateMockSearchResultItems(int count)
    {
        var results = new List<SearchResultItem>();
        for (int i = 1; i <= count; i++)
        {
            results.Add(new SearchResultItem
            {
                Id = Guid.NewGuid().ToString(),  // FIXED: string, not Guid
                Title = $"Test Article {i}",
                Content = $"This is test content for article {i}",
                Url = $"/test-article-{i}",  // FIXED: UrlPath → Url
                PublishDate = DateTime.UtcNow.AddDays(-i),  // FIXED: DateTime, not DateTimeOffset
                LastModified = DateTime.UtcNow.AddDays(-i + 1),
                ContentType = "article",
                Score = 1.0f - (i * 0.1f)
            });
        }
        return results;
    }

    #endregion
}