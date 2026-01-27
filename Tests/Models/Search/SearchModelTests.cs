// <copyright file="SearchModelTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Models.Search;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sky.Cms.Api.Shared.Features.Search.Query;
using Sky.Cms.Api.Shared.Models.Search;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Unit tests for search model classes.
/// </summary>
[TestClass]
public class SearchModelTests
{
    #region SearchQuery Tests

    [TestMethod]
    [TestCategory("Models")]
    public void SearchQuery_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var query = new SearchQuery();

        // Assert
        Assert.AreEqual(string.Empty, query.Query);
        Assert.AreEqual(1, query.PageNumber);
        Assert.AreEqual(20, query.PageSize);
        Assert.IsNull(query.ContentTypes);
        Assert.IsNull(query.DateFrom);
        Assert.IsNull(query.DateTo);
        Assert.AreEqual("relevance", query.SortBy);
    }

    [TestMethod]
    [TestCategory("Models")]
    public void SearchQuery_WithValidValues_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var query = new SearchQuery
        {
            Query = "test search",
            PageNumber = 2,
            PageSize = 15,
            ContentTypes = new[] { "article", "page" },
            DateFrom = new DateTime(2024, 1, 1),
            DateTo = new DateTime(2024, 12, 31),
            SortBy = "date"
        };

        // Assert
        Assert.AreEqual("test search", query.Query);
        Assert.AreEqual(2, query.PageNumber);
        Assert.AreEqual(15, query.PageSize);
        Assert.IsNotNull(query.ContentTypes);
        Assert.AreEqual(2, query.ContentTypes.Length);
        Assert.AreEqual("article", query.ContentTypes[0]);
        Assert.AreEqual("page", query.ContentTypes[1]);
        Assert.AreEqual(new DateTime(2024, 1, 1), query.DateFrom);
        Assert.AreEqual(new DateTime(2024, 12, 31), query.DateTo);
        Assert.AreEqual("date", query.SortBy);
    }

    #endregion

    #region SearchApiResponse Tests

    [TestMethod]
    [TestCategory("Models")]
    public void SearchApiResponse_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var response = new SearchApiResponse();

        // Assert
        Assert.AreEqual(string.Empty, response.Query);
        Assert.AreEqual(0, response.TotalResults);
        Assert.AreEqual(0, response.PageNumber);
        Assert.AreEqual(0, response.PageSize);
        Assert.AreEqual(0, response.TotalPages);
        Assert.IsNotNull(response.Results);
        Assert.AreEqual(0, response.Results.Count);
        Assert.AreEqual(0, response.SearchTimeMs);
    }

    [TestMethod]
    [TestCategory("Models")]
    public void SearchApiResponse_WithValidValues_SetsPropertiesCorrectly()
    {
        // Arrange
        var results = new List<SearchResultItem>
        {
            new() 
            { 
                Id = Guid.NewGuid().ToString(),  // FIXED: Guid → string
                Title = "Test Article",
                Content = "Test content",
                Url = "/test",  // FIXED: UrlPath → Url
                PublishDate = DateTime.UtcNow,  // FIXED: DateTimeOffset → DateTime
                LastModified = DateTime.UtcNow  // FIXED: DateTimeOffset → DateTime
            }
        };

        // Act
        var response = new SearchApiResponse
        {
            Query = "test",
            TotalResults = 25,
            PageNumber = 3,
            PageSize = 10,
            TotalPages = 3,
            Results = results,
            SearchTimeMs = 150
        };

        // Assert
        Assert.AreEqual("test", response.Query);
        Assert.AreEqual(25, response.TotalResults);
        Assert.AreEqual(3, response.PageNumber);
        Assert.AreEqual(10, response.PageSize);
        Assert.AreEqual(3, response.TotalPages);
        Assert.AreEqual(1, response.Results.Count);
        Assert.AreEqual(150, response.SearchTimeMs);
    }

    #endregion

    #region SearchResultItem Tests

    [TestMethod]
    [TestCategory("Models")]
    public void SearchResultItem_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var item = new SearchResultItem();

        // Assert
        Assert.AreEqual(string.Empty, item.Id);  // FIXED: Guid.Empty → string.Empty
        Assert.AreEqual(string.Empty, item.Title);
        Assert.AreEqual(string.Empty, item.Content);
        Assert.AreEqual(string.Empty, item.Url);  // FIXED: UrlPath → Url
        Assert.IsNull(item.PublishDate);  // FIXED: DateTimeOffset → DateTime? (nullable)
        Assert.IsNull(item.LastModified);  // FIXED: DateTimeOffset → DateTime? (nullable)
        Assert.AreEqual(string.Empty, item.ContentType);
        Assert.AreEqual(0.0f, item.Score);
        Assert.AreEqual(string.Empty, item.Author);  // FIXED: Not nullable
        // REMOVED: Tags and Category don't exist in Api.Shared.SearchResultItem
    }

    [TestMethod]
    [TestCategory("Models")]
    public void SearchResultItem_WithValidValues_SetsPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();  // FIXED: string, not Guid
        var publishedDate = DateTime.UtcNow.AddDays(-1);  // FIXED: DateTime, not DateTimeOffset
        var lastModified = DateTime.UtcNow;  // FIXED: DateTime, not DateTimeOffset

        // Act
        var item = new SearchResultItem
        {
            Id = id,
            Title = "Test Article",
            Content = "This is test content",
            HighlightedContent = "This is test content with <mark>highlighted</mark> terms",  // FIXED: Use HighlightedContent
            Url = "/test-article",  // FIXED: UrlPath → Url
            PublishDate = publishedDate,  // FIXED: PublishedDate → PublishDate
            LastModified = lastModified,
            ContentType = "article",
            Score = 0.85f,
            Author = "John Doe",
            Metadata = new Dictionary<string, string>  // FIXED: Use Metadata instead of Tags/Category
            {
                ["Tags"] = "test,article,content",
                ["Category"] = "Technology"
            }
        };

        // Assert
        Assert.AreEqual(id, item.Id);
        Assert.AreEqual("Test Article", item.Title);
        Assert.AreEqual("This is test content", item.Content);
        Assert.AreEqual("/test-article", item.Url);
        Assert.AreEqual(publishedDate, item.PublishDate);
        Assert.AreEqual(lastModified, item.LastModified);
        Assert.AreEqual("article", item.ContentType);
        Assert.AreEqual(0.85f, item.Score);
        Assert.AreEqual("John Doe", item.Author);
        Assert.IsNotNull(item.HighlightedContent);
        Assert.IsTrue(item.HighlightedContent.Contains("<mark>"));
        Assert.IsNotNull(item.Metadata);
        Assert.AreEqual(2, item.Metadata.Count);
        Assert.AreEqual("Technology", item.Metadata["Category"]);
    }

    #endregion

    #region SearchSuggestionsApiResponse Tests

    [TestMethod]
    [TestCategory("Models")]
    public void SearchSuggestionsApiResponse_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var response = new SearchSuggestionsApiResponse();  // FIXED: Type name

        // Assert
        Assert.AreEqual(string.Empty, response.Query);  // FIXED: Term → Query
        Assert.IsNotNull(response.Suggestions);
        Assert.AreEqual(0, response.Suggestions.Length);  // FIXED: Array, not List
    }

    [TestMethod]
    [TestCategory("Models")]
    public void SearchSuggestionsApiResponse_WithValidValues_SetsPropertiesCorrectly()
    {
        // Arrange
        var suggestions = new[] { "test article", "testing guide", "test framework" };  // FIXED: Array

        // Act
        var response = new SearchSuggestionsApiResponse  // FIXED: Type name
        {
            Query = "test",  // FIXED: Term → Query
            Suggestions = suggestions
        };

        // Assert
        Assert.AreEqual("test", response.Query);
        Assert.AreEqual(3, response.Suggestions.Length);
        Assert.IsTrue(response.Suggestions.All(s => s.Contains("test")));
    }

    #endregion

    #region SearchHealthApiResponse Tests

    [TestMethod]
    [TestCategory("Models")]
    public void SearchHealthApiResponse_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var response = new SearchHealthApiResponse();  // FIXED: Type name

        // Assert
        Assert.IsFalse(response.IsHealthy);  // FIXED: New property structure
        Assert.AreEqual(string.Empty, response.StatusMessage);  // FIXED: Status → StatusMessage
        Assert.AreEqual(string.Empty, response.Version);
        Assert.IsNotNull(response.Metrics);
        Assert.AreEqual(0, response.Metrics.Count);
        // REMOVED: IndexStatus, TotalDocuments, LastIndexUpdate, ResponseTime, ErrorMessage
        // These are now in Metrics dictionary or don't exist
    }

    [TestMethod]
    [TestCategory("Models")]
    public void SearchHealthApiResponse_WithHealthyStatus_SetsPropertiesCorrectly()
    {
        // Arrange
        var lastChecked = DateTime.UtcNow;  // FIXED: DateTime, not DateTimeOffset

        // Act
        var response = new SearchHealthApiResponse  // FIXED: Type name
        {
            IsHealthy = true,  // FIXED: Status → IsHealthy
            StatusMessage = "All systems operational",  // FIXED: New property
            Version = "1.0.0",
            LastChecked = lastChecked,
            Metrics = new Dictionary<string, object>  // FIXED: Use Metrics
            {
                ["TotalDocuments"] = 1500,
                ["ResponseTimeMs"] = 50,
                ["IndexStatus"] = "Ready"
            }
        };

        // Assert
        Assert.IsTrue(response.IsHealthy);
        Assert.AreEqual("All systems operational", response.StatusMessage);
        Assert.AreEqual("1.0.0", response.Version);
        Assert.AreEqual(lastChecked, response.LastChecked);
        Assert.IsNotNull(response.Metrics);
        Assert.AreEqual(3, response.Metrics.Count);
        Assert.AreEqual(1500, response.Metrics["TotalDocuments"]);
        Assert.AreEqual(50, response.Metrics["ResponseTimeMs"]);
        Assert.AreEqual("Ready", response.Metrics["IndexStatus"]);
    }

    [TestMethod]
    [TestCategory("Models")]
    public void SearchHealthApiResponse_WithUnhealthyStatus_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var response = new SearchHealthApiResponse  // FIXED: Type name
        {
            IsHealthy = false,  // FIXED: Status → IsHealthy
            StatusMessage = "Database connection timeout",  // FIXED: New structure
            Version = "1.0.0",
            Metrics = new Dictionary<string, object>
            {
                ["TotalDocuments"] = 0,
                ["ResponseTimeMs"] = 5000,
                ["IndexStatus"] = "Error"
            }
        };

        // Assert
        Assert.IsFalse(response.IsHealthy);
        Assert.AreEqual("Database connection timeout", response.StatusMessage);
        Assert.AreEqual("1.0.0", response.Version);
        Assert.IsNotNull(response.Metrics);
        Assert.AreEqual(0, response.Metrics["TotalDocuments"]);
        Assert.AreEqual(5000, response.Metrics["ResponseTimeMs"]);
        Assert.AreEqual("Error", response.Metrics["IndexStatus"]);
    }

    #endregion

    #region Validation Tests

    [TestMethod]
    [TestCategory("Validation")]
    public void SearchResultItem_WithValidData_PassesValidation()
    {
        // Arrange
        var item = new SearchResultItem
        {
            Id = Guid.NewGuid().ToString(),  // FIXED: string
            Title = "Valid Article Title",
            Content = "Valid content that meets length requirements",
            Url = "/valid-path",  // FIXED: UrlPath → Url
            PublishDate = DateTime.UtcNow,  // FIXED: DateTime
            LastModified = DateTime.UtcNow,  // FIXED: DateTime
            ContentType = "article"
        };

        var validationContext = new ValidationContext(item);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(item, validationContext, validationResults, true);

        // Assert
        Assert.IsTrue(isValid);
        Assert.AreEqual(0, validationResults.Count);
    }

    [TestMethod]
    [TestCategory("Validation")]
    public void SearchApiResponse_WithValidData_PassesValidation()
    {
        // Arrange
        var response = new SearchApiResponse
        {
            Query = "test query",
            TotalResults = 10,
            PageNumber = 1,
            PageSize = 10,
            TotalPages = 1,
            Results = new List<SearchResultItem>(),
            SearchTimeMs = 100
        };

        var validationContext = new ValidationContext(response);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(response, validationContext, validationResults, true);

        // Assert
        Assert.IsTrue(isValid);
        Assert.AreEqual(0, validationResults.Count);
    }

    #endregion
}