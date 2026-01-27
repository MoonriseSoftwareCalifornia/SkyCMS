using Cosmos.Common.Services.Search.Models;
using Sky.Cms.Api.Shared.Features.Search.Query;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Sky.Tests
{
    /// <summary>
    /// Simple unit tests for search functionality to demonstrate the search lifecycle testing.
    /// These tests verify the basic search query and result processing without complex dependencies.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SimpleSearchTests
    {
        [TestMethod]
        [TestCategory("BasicFunctionality")]
        public void SearchQuery_WithValidInput_CreatesCorrectQuery()
        {
            // Arrange
            var query = "test search query";
            var pageSize = 20;
            var pageNumber = 1; // Now 1-based instead of 0-based

            // Act
            var searchQuery = new SearchQuery 
            { 
                Query = query,
                PageSize = pageSize,      // Changed: MaxResults → PageSize
                PageNumber = pageNumber   // Changed: Page → PageNumber (1-based)
            };

            // Assert
            Assert.AreEqual(query, searchQuery.Query);
            Assert.AreEqual(pageSize, searchQuery.PageSize);
            Assert.AreEqual(pageNumber, searchQuery.PageNumber);
        }

        [TestMethod]
        [TestCategory("BasicFunctionality")]
        public void SearchResults_WithDefaultValues_InitializesCorrectly()
        {
            // Arrange & Act
            var results = new SearchResults();

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.TotalCount);        // Changed: TotalResults → TotalCount
            Assert.IsNotNull(results.Items);               // Changed: Results → Items
            Assert.AreEqual(0, results.Items.Count());     // Items is IEnumerable, use Count()
            Assert.AreEqual(0, results.Page);              // Changed: CurrentPage → Page
        }

        [TestMethod]
        [TestCategory("BasicFunctionality")]
        public void SearchDocument_WithTestData_PropertiesSetCorrectly()
        {
            // Arrange
            var title = "Test Document";
            var content = "This is test content for searching";
            var url = "/test-document";

            // Act
            var document = new SearchDocument
            {
                Title = title,
                Content = content,
                Url = url,
                PublishedDate = DateTimeOffset.Now.AddDays(-1),  // Changed: DateTime → DateTimeOffset
                Priority = 5  // Changed: Score → Priority (SearchDocument doesn't have Score)
            };

            // Assert
            Assert.AreEqual(title, document.Title);
            Assert.AreEqual(content, document.Content);
            Assert.AreEqual(url, document.Url);
            Assert.IsTrue(document.PublishedDate.HasValue);
            Assert.AreEqual(5, document.Priority);
        }

        [TestMethod]
        [TestCategory("BasicFunctionality")]
        public void SearchResultItem_WithTestData_PropertiesSetCorrectly()
        {
            // NEW TEST: Test SearchResultItem separately from SearchDocument
            // Arrange
            var title = "Test Result";
            var content = "This is a search result";
            var url = "/test-result";

            // Act
            var resultItem = new SearchResultItem
            {
                Title = title,
                Content = content,
                Url = url,
                PublishedDate = DateTimeOffset.Now.AddDays(-1),  // DateTimeOffset, not DateTime
                Score = 0.95  // double, not float
            };

            // Assert
            Assert.AreEqual(title, resultItem.Title);
            Assert.AreEqual(content, resultItem.Content);
            Assert.AreEqual(url, resultItem.Url);
            Assert.IsTrue(resultItem.PublishedDate.HasValue);
            Assert.AreEqual(0.95, resultItem.Score);
        }

        [TestMethod]
        [TestCategory("Pagination")]
        public void SearchResults_WithPagination_CalculatesCorrectValues()
        {
            // Arrange
            var results = new SearchResults
            {
                TotalCount = 100,     // Changed: TotalResults → TotalCount (long, not int)
                Page = 2,             // Changed: CurrentPage → Page
                PageSize = 10
            };

            // Act & Assert
            Assert.AreEqual(100, results.TotalCount);
            Assert.AreEqual(2, results.Page);
            Assert.AreEqual(10, results.PageSize);
            
            // TotalPages is now a calculated property
            Assert.AreEqual(10, results.TotalPages);
            
            // Test new calculated properties
            Assert.IsTrue(results.HasNextPage);      // Page 2 of 10
            Assert.IsTrue(results.HasPreviousPage);  // Page > 1
        }

        [TestMethod]
        [TestCategory("ErrorHandling")]
        public void SearchQuery_WithEmptyInput_HandlesGracefully()
        {
            // Arrange & Act
            var searchQuery = new SearchQuery 
            { 
                Query = "",
                PageSize = 10,    // Changed: MaxResults → PageSize
                PageNumber = 1    // Changed: Page → PageNumber (1-based)
            };

            // Assert
            Assert.AreEqual("", searchQuery.Query);
            Assert.AreEqual(10, searchQuery.PageSize);
            Assert.AreEqual(1, searchQuery.PageNumber);
        }

        [TestMethod]
        [TestCategory("ErrorHandling")]
        public void SearchQuery_WithNullInput_HandlesGracefully()
        {
            // Arrange & Act
            var searchQuery = new SearchQuery 
            { 
                Query = null,
                PageSize = 10,    // Changed: MaxResults → PageSize
                PageNumber = 1    // Changed: Page → PageNumber (1-based)
            };

            // Assert - Query has default value of string.Empty, so won't be null
            Assert.AreEqual(string.Empty, searchQuery.Query); // Default is string.Empty
            Assert.AreEqual(10, searchQuery.PageSize);
            Assert.AreEqual(1, searchQuery.PageNumber);
        }

        [TestMethod]
        [TestCategory("SpecialCases")]
        public void SearchQuery_WithLargeValues_HandlesCorrectly()
        {
            // Arrange & Act
            var searchQuery = new SearchQuery 
            { 
                Query = new string('a', 1000), // Very long query
                PageSize = 1000,               // Changed: MaxResults → PageSize
                PageNumber = 999               // Changed: Page → PageNumber
            };

            // Assert
            Assert.AreEqual(1000, searchQuery.Query.Length);
            Assert.AreEqual(1000, searchQuery.PageSize);
            Assert.AreEqual(999, searchQuery.PageNumber);
        }

        [TestMethod]
        [TestCategory("SpecialCases")]
        public void SearchQuery_WithSpecialCharacters_PreservesInput()
        {
            // Arrange
            var specialQuery = "test@#$%^&*() with unicode: тест 测试 テスト";

            // Act
            var searchQuery = new SearchQuery 
            { 
                Query = specialQuery,
                PageSize = 10,
                PageNumber = 1
            };

            // Assert
            Assert.AreEqual(specialQuery, searchQuery.Query);
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void SearchDocument_Creation_PerformsQuickly()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act - Create many search documents quickly
            for (int i = 0; i < 1000; i++)
            {
                var document = new SearchDocument
                {
                    Title = $"Document {i}",
                    Content = $"Content for document number {i}",
                    Url = $"/document-{i}",
                    PublishedDate = DateTimeOffset.Now.AddDays(-i),  // DateTimeOffset
                    Priority = (i % 10)  // Priority instead of Score
                };
            }

            stopwatch.Stop();

            // Assert - Should create 1000 documents very quickly (under 1 second)
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, 
                $"Creating 1000 search documents took {stopwatch.ElapsedMilliseconds}ms, should be under 1000ms");
        }
    }
}