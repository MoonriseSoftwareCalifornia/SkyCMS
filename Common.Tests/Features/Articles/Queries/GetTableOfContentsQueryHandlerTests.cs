// <copyright file="GetTableOfContentsQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Queries
{
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="GetTableOfContentsQueryHandler"/>.
    /// Validates table of contents retrieval with pagination and ordering options.
    /// </summary>
    [TestClass]
    public class GetTableOfContentsQueryHandlerTests : CommonTestsBase
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
        public void Constructor_WithValidService_ShouldSucceed()
        {
            var mockService = new Mock<IArticleCatalogQueryService>();

            var handler = new GetTableOfContentsQueryHandler(mockService.Object);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithDefaultParameters_ShouldCallService()
        {
            var mockToc = new TableOfContents { TotalCount = 0 };
            var mockService = new Mock<IArticleCatalogQueryService>();
            mockService.Setup(s => s.GetTableOfContentsAsync(
                    string.Empty, 0, 10, false))
                .ReturnsAsync(mockToc);

            var handler = new GetTableOfContentsQueryHandler(mockService.Object);
            var query = new GetTableOfContentsQuery
            {
                Page = string.Empty,
                PageNo = 0,
                PageSize = 10,
                OrderByPublishedDate = false
            };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            mockService.Verify(s => s.GetTableOfContentsAsync(
                string.Empty, 0, 10, false), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithCustomPageSize_ShouldPassToService()
        {
            var mockService = new Mock<IArticleCatalogQueryService>();
            mockService.Setup(s => s.GetTableOfContentsAsync(
                    "/articles", 0, 50, false))
                .ReturnsAsync(new TableOfContents());

            var handler = new GetTableOfContentsQueryHandler(mockService.Object);
            var query = new GetTableOfContentsQuery
            {
                Page = "/articles",
                PageNo = 0,
                PageSize = 50,
                OrderByPublishedDate = false
            };

            await handler.HandleAsync(query);

            mockService.Verify(s => s.GetTableOfContentsAsync(
                "/articles", 0, 50, false), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithPageNumber_ShouldPassToService()
        {
            var mockService = new Mock<IArticleCatalogQueryService>();
            mockService.Setup(s => s.GetTableOfContentsAsync(
                    "/blog", 2, 20, false))
                .ReturnsAsync(new TableOfContents());

            var handler = new GetTableOfContentsQueryHandler(mockService.Object);
            var query = new GetTableOfContentsQuery
            {
                Page = "/blog",
                PageNo = 2,
                PageSize = 20,
                OrderByPublishedDate = false
            };

            await handler.HandleAsync(query);

            mockService.Verify(s => s.GetTableOfContentsAsync(
                "/blog", 2, 20, false), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithOrderByPublishedDate_ShouldPassTrueToService()
        {
            var mockService = new Mock<IArticleCatalogQueryService>();
            mockService.Setup(s => s.GetTableOfContentsAsync(
                    "/news", 0, 25, true))
                .ReturnsAsync(new TableOfContents());

            var handler = new GetTableOfContentsQueryHandler(mockService.Object);
            var query = new GetTableOfContentsQuery
            {
                Page = "/news",
                PageNo = 0,
                PageSize = 25,
                OrderByPublishedDate = true
            };

            await handler.HandleAsync(query);

            mockService.Verify(s => s.GetTableOfContentsAsync(
                "/news", 0, 25, true), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithCancellationToken_ShouldPassToService()
        {
            var cts = new CancellationTokenSource();
            var mockService = new Mock<IArticleCatalogQueryService>();
            mockService.Setup(s => s.GetTableOfContentsAsync(
                    "/docs", 1, 15, false))
                .ReturnsAsync(new TableOfContents());

            var handler = new GetTableOfContentsQueryHandler(mockService.Object);
            var query = new GetTableOfContentsQuery
            {
                Page = "/docs",
                PageNo = 1,
                PageSize = 15,
                OrderByPublishedDate = false
            };

            await handler.HandleAsync(query, cts.Token);

            mockService.Verify(s => s.GetTableOfContentsAsync(
                "/docs", 1, 15, false), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithEmptyPagePath_ShouldStillCallService()
        {
            var mockService = new Mock<IArticleCatalogQueryService>();
            mockService.Setup(s => s.GetTableOfContentsAsync(
                    string.Empty, 0, 10, false))
                .ReturnsAsync(new TableOfContents());

            var handler = new GetTableOfContentsQueryHandler(mockService.Object);
            var query = new GetTableOfContentsQuery
            {
                Page = string.Empty,
                PageNo = 0,
                PageSize = 10
            };

            await handler.HandleAsync(query);

            mockService.Verify(s => s.GetTableOfContentsAsync(
                string.Empty, 0, 10, false), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithAllParameters_ShouldPassAllToService()
        {
            var mockToc = new TableOfContents { TotalCount = 100 };
            var mockService = new Mock<IArticleCatalogQueryService>();
            mockService.Setup(s => s.GetTableOfContentsAsync(
                    "/articles/tech", 3, 30, true))
                .ReturnsAsync(mockToc);

            var handler = new GetTableOfContentsQueryHandler(mockService.Object);
            var query = new GetTableOfContentsQuery
            {
                Page = "/articles/tech",
                PageNo = 3,
                PageSize = 30,
                OrderByPublishedDate = true
            };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(100, result.TotalCount);
            mockService.Verify(s => s.GetTableOfContentsAsync(
                "/articles/tech", 3, 30, true), Times.Once);
        }
    }
}
