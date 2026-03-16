// <copyright file="BuildPublishedPageViewModelQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Queries
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="BuildPublishedPageViewModelQueryHandler"/>.
    /// Validates view model building delegation to ArticleViewModelBuilder.
    /// </summary>
    [TestClass]
    public class BuildPublishedPageViewModelQueryHandlerTests : CommonTestsBase
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
            var mockBuilder = new Mock<IArticleViewModelBuilder>();

            var handler = new BuildPublishedPageViewModelQueryHandler(mockBuilder.Object);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public void Constructor_WithNullBuilder_ShouldThrowArgumentNullException()
        {
            try
            {
                var handler = new BuildPublishedPageViewModelQueryHandler(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected exception - test passes
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithNullQuery_ShouldThrowArgumentNullException()
        {
            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            var handler = new BuildPublishedPageViewModelQueryHandler(mockBuilder.Object);

            try
            {
                await handler.HandleAsync(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected exception - test passes
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithPublishedPage_ShouldCallBuilderMethod()
        {
            var publishedPage = new PublishedPage { Title = "Test Page", ArticleNumber = 123 };
            var languageCode = "en-US";
            var expectedResult = new ArticleViewModel { Title = "Test Page" };

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            mockBuilder.Setup(b => b.BuildFromPublishedPageAsync(publishedPage, languageCode, null, true))
                .ReturnsAsync(expectedResult);

            var handler = new BuildPublishedPageViewModelQueryHandler(mockBuilder.Object);
            var query = new BuildPublishedPageViewModelQuery(
                publishedPage,
                languageCode,
                null,
                true);

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual("Test Page", result.Title);
            mockBuilder.Verify(b => b.BuildFromPublishedPageAsync(publishedPage, languageCode, null, true), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithLayoutCacheDuration_ShouldPassToBuilder()
        {
            var publishedPage = new PublishedPage { Title = "Test Page" };
            var cacheDuration = TimeSpan.FromMinutes(10);
            var expectedResult = new ArticleViewModel { Title = "Test Page" };

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            mockBuilder.Setup(b => b.BuildFromPublishedPageAsync(
                    It.IsAny<PublishedPage>(),
                    It.IsAny<string>(),
                    cacheDuration,
                    It.IsAny<bool>()))
                .ReturnsAsync(expectedResult);

            var handler = new BuildPublishedPageViewModelQueryHandler(mockBuilder.Object);
            var query = new BuildPublishedPageViewModelQuery(
                publishedPage,
                "en-US",
                cacheDuration,
                true);

            var result = await handler.HandleAsync(query);

            mockBuilder.Verify(b => b.BuildFromPublishedPageAsync(
                publishedPage,
                "en-US",
                cacheDuration,
                true), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithIncludeLayoutFalse_ShouldPassToBuilder()
        {
            var publishedPage = new PublishedPage { Title = "Test Page" };
            var expectedResult = new ArticleViewModel { Title = "Test Page" };

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            mockBuilder.Setup(b => b.BuildFromPublishedPageAsync(
                    It.IsAny<PublishedPage>(),
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan?>(),
                    false))
                .ReturnsAsync(expectedResult);

            var handler = new BuildPublishedPageViewModelQueryHandler(mockBuilder.Object);
            var query = new BuildPublishedPageViewModelQuery(
                publishedPage,
                "en-US",
                null,
                false);

            var result = await handler.HandleAsync(query);

            mockBuilder.Verify(b => b.BuildFromPublishedPageAsync(
                publishedPage,
                "en-US",
                null,
                false), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_WithDifferentLanguageCodes_ShouldPassToBuilder()
        {
            var publishedPage = new PublishedPage { Title = "Test Page" };
            var languageCode = "es-ES";
            var expectedResult = new ArticleViewModel { Title = "Test Page", LanguageCode = languageCode };

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            mockBuilder.Setup(b => b.BuildFromPublishedPageAsync(
                    It.IsAny<PublishedPage>(),
                    languageCode,
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(expectedResult);

            var handler = new BuildPublishedPageViewModelQueryHandler(mockBuilder.Object);
            var query = new BuildPublishedPageViewModelQuery(
                publishedPage,
                languageCode,
                null,
                true);

            var result = await handler.HandleAsync(query);

            Assert.AreEqual(languageCode, result.LanguageCode);
            mockBuilder.Verify(b => b.BuildFromPublishedPageAsync(
                publishedPage,
                languageCode,
                null,
                true), Times.Once);
        }

        [TestMethod]
        public async Task HandleAsync_ReturnsBuilderResult()
        {
            var publishedPage = new PublishedPage { Title = "Test Page" };
            var expectedResult = new ArticleViewModel 
            { 
                Title = "Test Page",
                ArticleNumber = 12345,
                LanguageCode = "en-US"
            };

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            mockBuilder.Setup(b => b.BuildFromPublishedPageAsync(
                    It.IsAny<PublishedPage>(),
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(expectedResult);

            var handler = new BuildPublishedPageViewModelQueryHandler(mockBuilder.Object);
            var query = new BuildPublishedPageViewModelQuery(
                publishedPage,
                "en-US",
                null,
                true);

            var result = await handler.HandleAsync(query);

            Assert.AreEqual(expectedResult, result);
        }
    }
}
