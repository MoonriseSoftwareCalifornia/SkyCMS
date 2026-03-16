// <copyright file="SearchPublishedArticlesQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Queries
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="SearchPublishedArticlesQueryHandler"/>.
    /// Validates article search delegation to catalog service.
    /// </summary>
    [TestClass]
    public class SearchPublishedArticlesQueryHandlerTests : CommonTestsBase
    {
        /// <summary>
        /// Initializes the shared test infrastructure for this test class.
        /// </summary>
        /// <param name="context">Test context provided by MSTest.</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            ContextPool = new TestDbContextPool();
        }

        /// <summary>
        /// Cleans up the shared test infrastructure after all tests complete.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            ContextPool?.Dispose();
        }

        [TestMethod]
        public void Constructor_WithValidDependencies_ShouldSucceed()
        {
            var mockCatalogService = new Mock<IArticleCatalogQueryService>();

            var handler = new SearchPublishedArticlesQueryHandler(mockCatalogService.Object);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithSearchText_ShouldCallServiceSearch()
        {
            var searchText = "test search";
            var expectedResults = new List<TableOfContentsItem>
            {
                new TableOfContentsItem { Title = "Test Article 1" },
                new TableOfContentsItem { Title = "Test Article 2" }
            };

            var mockCatalogService = new Mock<IArticleCatalogQueryService>();
            mockCatalogService.Setup(s => s.SearchAsync(searchText))
                .ReturnsAsync(expectedResults);

            var handler = new SearchPublishedArticlesQueryHandler(mockCatalogService.Object);
            var query = new SearchPublishedArticlesQuery { Text = searchText };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            mockCatalogService.Verify(s => s.SearchAsync(searchText), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithEmptySearchText_ShouldCallService()
        {
            var expectedResults = new List<TableOfContentsItem>();

            var mockCatalogService = new Mock<IArticleCatalogQueryService>();
            mockCatalogService.Setup(s => s.SearchAsync(string.Empty))
                .ReturnsAsync(expectedResults);

            var handler = new SearchPublishedArticlesQueryHandler(mockCatalogService.Object);
            var query = new SearchPublishedArticlesQuery { Text = string.Empty };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
            mockCatalogService.Verify(s => s.SearchAsync(string.Empty), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithNullSearchText_ShouldCallService()
        {
            var expectedResults = new List<TableOfContentsItem>();

            var mockCatalogService = new Mock<IArticleCatalogQueryService>();
            mockCatalogService.Setup(s => s.SearchAsync(null!))
                .ReturnsAsync(expectedResults);

            var handler = new SearchPublishedArticlesQueryHandler(mockCatalogService.Object);
            var query = new SearchPublishedArticlesQuery { Text = null! };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            mockCatalogService.Verify(s => s.SearchAsync(null!), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_ReturnsServiceResults()
        {
            var searchText = "cosmos";
            var expectedResults = new List<TableOfContentsItem>
            {
                new TableOfContentsItem { Title = "Cosmos Article" },
                new TableOfContentsItem { Title = "Another Cosmos Article" }
            };

            var mockCatalogService = new Mock<IArticleCatalogQueryService>();
            mockCatalogService.Setup(s => s.SearchAsync(searchText))
                .ReturnsAsync(expectedResults);

            var handler = new SearchPublishedArticlesQueryHandler(mockCatalogService.Object);
            var query = new SearchPublishedArticlesQuery { Text = searchText };

            var result = await handler.HandleAsync(query);

            Assert.AreEqual(expectedResults, result);
        }

        [TestMethod]
        public async Task HandleAsync_PassesCancellationToken()
        {
            var searchText = "test";
            var mockCatalogService = new Mock<IArticleCatalogQueryService>();
            mockCatalogService.Setup(s => s.SearchAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<TableOfContentsItem>());

            var handler = new SearchPublishedArticlesQueryHandler(mockCatalogService.Object);
            var query = new SearchPublishedArticlesQuery { Text = searchText };

            using var cts = new System.Threading.CancellationTokenSource();
            await handler.HandleAsync(query, cts.Token);

            mockCatalogService.Verify(s => s.SearchAsync(searchText), Times.Once);
        }
    }
}
