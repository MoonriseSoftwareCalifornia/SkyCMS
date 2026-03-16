// <copyright file="GetPublishedPageHeaderByUrlQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Queries
{
    using System.Threading.Tasks;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="GetPublishedPageHeaderByUrlQueryHandler"/>.
    /// Validates published page header retrieval delegation to service.
    /// </summary>
    [TestClass]
    public class GetPublishedPageHeaderByUrlQueryHandlerTests : CommonTestsBase
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
            var mockService = new Mock<IPublishedPageQueryService>();

            var handler = new GetPublishedPageHeaderByUrlQueryHandler(mockService.Object);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithUrlPath_ShouldCallServiceMethod()
        {
            var urlPath = "/test-article";
            var expectedResult = new ArticleViewModel { Title = "Test Article" };

            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageHeaderByUrlAsync(urlPath))
                .ReturnsAsync(expectedResult);

            var handler = new GetPublishedPageHeaderByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageHeaderByUrlQuery { UrlPath = urlPath };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual("Test Article", result.Title);
            mockService.Verify(s => s.GetPublishedPageHeaderByUrlAsync(urlPath), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentUrl_ShouldReturnNull()
        {
            var urlPath = "/non-existent";

            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageHeaderByUrlAsync(urlPath))
                .ReturnsAsync((ArticleViewModel?)null);

            var handler = new GetPublishedPageHeaderByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageHeaderByUrlQuery { UrlPath = urlPath };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
            mockService.Verify(s => s.GetPublishedPageHeaderByUrlAsync(urlPath), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithEmptyUrlPath_ShouldCallService()
        {
            var urlPath = string.Empty;

            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageHeaderByUrlAsync(urlPath))
                .ReturnsAsync((ArticleViewModel?)null);

            var handler = new GetPublishedPageHeaderByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageHeaderByUrlQuery { UrlPath = urlPath };

            var result = await handler.HandleAsync(query);

            mockService.Verify(s => s.GetPublishedPageHeaderByUrlAsync(urlPath), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithRootPath_ShouldCallService()
        {
            var urlPath = "/";
            var expectedResult = new ArticleViewModel { Title = "Home Page" };

            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageHeaderByUrlAsync(urlPath))
                .ReturnsAsync(expectedResult);

            var handler = new GetPublishedPageHeaderByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageHeaderByUrlQuery { UrlPath = urlPath };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual("Home Page", result.Title);
            mockService.Verify(s => s.GetPublishedPageHeaderByUrlAsync(urlPath), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_ReturnsServiceResult()
        {
            var urlPath = "/about-us";
            var expectedResult = new ArticleViewModel 
            { 
                Title = "About Us",
                UrlPath = "/about-us",
                ArticleNumber = 12345
            };

            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageHeaderByUrlAsync(urlPath))
                .ReturnsAsync(expectedResult);

            var handler = new GetPublishedPageHeaderByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageHeaderByUrlQuery { UrlPath = urlPath };

            var result = await handler.HandleAsync(query);

            Assert.AreEqual(expectedResult, result);
        }

        [TestMethod]
        public async Task HandleAsync_PassesCancellationToken()
        {
            var urlPath = "/test";
            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageHeaderByUrlAsync(It.IsAny<string>()))
                .ReturnsAsync((ArticleViewModel?)null);

            var handler = new GetPublishedPageHeaderByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageHeaderByUrlQuery { UrlPath = urlPath };

            using var cts = new System.Threading.CancellationTokenSource();
            await handler.HandleAsync(query, cts.Token);

            mockService.Verify(s => s.GetPublishedPageHeaderByUrlAsync(urlPath), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithUrlContainingSpecialCharacters_ShouldCallService()
        {
            var urlPath = "/article-with-dashes_and_underscores";
            var expectedResult = new ArticleViewModel { Title = "Special Article" };

            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageHeaderByUrlAsync(urlPath))
                .ReturnsAsync(expectedResult);

            var handler = new GetPublishedPageHeaderByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageHeaderByUrlQuery { UrlPath = urlPath };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            mockService.Verify(s => s.GetPublishedPageHeaderByUrlAsync(urlPath), Times.Once);
        }
    }
}
