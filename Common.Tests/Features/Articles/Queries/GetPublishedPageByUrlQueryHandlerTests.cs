// <copyright file="GetPublishedPageByUrlQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Queries
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="GetPublishedPageByUrlQueryHandler"/>.
    /// Validates published page retrieval by URL path with caching and layout options.
    /// </summary>
    [TestClass]
    public class GetPublishedPageByUrlQueryHandlerTests : CommonTestsBase
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
            var mockService = new Mock<IPublishedPageQueryService>();

            var handler = new GetPublishedPageByUrlQueryHandler(mockService.Object);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithValidUrlAndDefaultOptions_ShouldCallService()
        {
            var urlPath = "/test-page";
            var lang = "en-US";
            var mockViewModel = new ArticleViewModel { Title = "Test Page" };
            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageByUrlAsync(
                    urlPath, lang, null, null, true))
                .ReturnsAsync(mockViewModel);

            var handler = new GetPublishedPageByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageByUrlQuery
            {
                UrlPath = urlPath,
                Lang = lang,
                IncludeLayout = true
            };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual("Test Page", result.Title);
            mockService.Verify(s => s.GetPublishedPageByUrlAsync(
                urlPath, lang, null, null, true), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithCacheSpan_ShouldPassCacheSpanToService()
        {
            var cacheSpan = TimeSpan.FromMinutes(10);
            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageByUrlAsync(
                    "/test", "en", cacheSpan, null, true))
                .ReturnsAsync((ArticleViewModel?)null);

            var handler = new GetPublishedPageByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageByUrlQuery
            {
                UrlPath = "/test",
                Lang = "en",
                CacheSpan = cacheSpan,
                IncludeLayout = true
            };

            await handler.HandleAsync(query);

            mockService.Verify(s => s.GetPublishedPageByUrlAsync(
                "/test", "en", cacheSpan, null, true), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithLayoutCache_ShouldPassLayoutCacheToService()
        {
            var layoutCache = TimeSpan.FromMinutes(30);
            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageByUrlAsync(
                    "/page", "fr", null, layoutCache, true))
                .ReturnsAsync(new ArticleViewModel());

            var handler = new GetPublishedPageByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageByUrlQuery
            {
                UrlPath = "/page",
                Lang = "fr",
                LayoutCache = layoutCache,
                IncludeLayout = true
            };

            await handler.HandleAsync(query);

            mockService.Verify(s => s.GetPublishedPageByUrlAsync(
                "/page", "fr", null, layoutCache, true), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithBothCacheOptions_ShouldPassBothToService()
        {
            var cacheSpan = TimeSpan.FromMinutes(15);
            var layoutCache = TimeSpan.FromMinutes(45);
            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageByUrlAsync(
                    "/cached", "de", cacheSpan, layoutCache, false))
                .ReturnsAsync(new ArticleViewModel { Title = "Cached" });

            var handler = new GetPublishedPageByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageByUrlQuery
            {
                UrlPath = "/cached",
                Lang = "de",
                CacheSpan = cacheSpan,
                LayoutCache = layoutCache,
                IncludeLayout = false
            };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual("Cached", result.Title);
            mockService.Verify(s => s.GetPublishedPageByUrlAsync(
                "/cached", "de", cacheSpan, layoutCache, false), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithIncludeLayoutFalse_ShouldPassFalseToService()
        {
            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageByUrlAsync(
                    "/no-layout", "en", null, null, false))
                .ReturnsAsync(new ArticleViewModel());

            var handler = new GetPublishedPageByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageByUrlQuery
            {
                UrlPath = "/no-layout",
                Lang = "en",
                IncludeLayout = false
            };

            await handler.HandleAsync(query);

            mockService.Verify(s => s.GetPublishedPageByUrlAsync(
                "/no-layout", "en", null, null, false), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WhenServiceReturnsNull_ShouldReturnNull()
        {
            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageByUrlAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(),
                    It.IsAny<TimeSpan?>(), It.IsAny<bool>()))
                .ReturnsAsync((ArticleViewModel?)null);

            var handler = new GetPublishedPageByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageByUrlQuery { UrlPath = "/not-found", Lang = "en" };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithCancellationToken_ShouldPassTokenToService()
        {
            var cts = new CancellationTokenSource();
            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageByUrlAsync(
                    "/test", "en", null, null, true))
                .ReturnsAsync(new ArticleViewModel());

            var handler = new GetPublishedPageByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageByUrlQuery { UrlPath = "/test", Lang = "en" };

            await handler.HandleAsync(query, cts.Token);

            mockService.Verify(s => s.GetPublishedPageByUrlAsync(
                "/test", "en", null, null, true), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithEmptyUrlPath_ShouldStillCallService()
        {
            var mockService = new Mock<IPublishedPageQueryService>();
            mockService.Setup(s => s.GetPublishedPageByUrlAsync(
                    string.Empty, "en", null, null, true))
                .ReturnsAsync(new ArticleViewModel());

            var handler = new GetPublishedPageByUrlQueryHandler(mockService.Object);
            var query = new GetPublishedPageByUrlQuery { UrlPath = string.Empty, Lang = "en" };

            await handler.HandleAsync(query);

            mockService.Verify(s => s.GetPublishedPageByUrlAsync(
                string.Empty, "en", null, null, true), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithDifferentLanguages_ShouldPassCorrectLanguage()
        {
            var languages = new[] { "en-US", "fr-FR", "de-DE", "es-ES" };
            var mockService = new Mock<IPublishedPageQueryService>();

            foreach (var lang in languages)
            {
                mockService.Setup(s => s.GetPublishedPageByUrlAsync(
                        "/multilang", lang, null, null, true))
                    .ReturnsAsync(new ArticleViewModel { Title = $"Page in {lang}" });
            }

            var handler = new GetPublishedPageByUrlQueryHandler(mockService.Object);

            foreach (var lang in languages)
            {
                var query = new GetPublishedPageByUrlQuery { UrlPath = "/multilang", Lang = lang };
                var result = await handler.HandleAsync(query);

                Assert.IsNotNull(result);
                Assert.AreEqual($"Page in {lang}", result.Title);
            }

            foreach (var lang in languages)
            {
                mockService.Verify(s => s.GetPublishedPageByUrlAsync(
                    "/multilang", lang, null, null, true), Times.Once);
            }
        }
    }
}
