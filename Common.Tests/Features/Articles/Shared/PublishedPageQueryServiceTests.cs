// <copyright file="PublishedPageQueryServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Shared
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="PublishedPageQueryService"/>.
    /// Validates published page retrieval with caching, URL normalization,
    /// blog stream detection, and header-only queries.
    /// </summary>
    [TestClass]
    public class PublishedPageQueryServiceTests : CommonTestsBase
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

        private static Mock<IArticleViewModelBuilder> CreateMockViewModelBuilder()
        {
            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            mockBuilder.Setup(b => b.BuildFromPublishedPageAsync(
                    It.IsAny<PublishedPage>(),
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>()))
                .ReturnsAsync((PublishedPage page, string lang, TimeSpan? cache, bool include) =>
                    new ArticleViewModel
                    {
                        Title = page.Title,
                        ArticleNumber = page.ArticleNumber,
                        LanguageCode = lang
                    });
            return mockBuilder;
        }

        [TestMethod]
        public void Constructor_WithValidDependencies_ShouldSucceed()
        {
            using var context = GetIsolatedContext();
            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var service = new PublishedPageQueryService(context, memoryCache, mockBuilder.Object);

            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            try
            {
                var service = new PublishedPageQueryService(null!, memoryCache, mockBuilder.Object);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public void Constructor_WithNullViewModelBuilder_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            try
            {
                var service = new PublishedPageQueryService(context, memoryCache, null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("viewModelBuilder", ex.ParamName);
            }
        }

        [TestMethod]
        public void Constructor_WithNullMemoryCache_ShouldSucceed()
        {
            using var context = GetIsolatedContext();
            var mockBuilder = new Mock<IArticleViewModelBuilder>();

            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            Assert.IsNotNull(service);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithNonExistentUrl_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var mockBuilder = CreateMockViewModelBuilder();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageByUrlAsync("nonexistent");

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithValidUrl_ShouldReturnViewModel()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "test-article";
            page.Title = "Test Article";
            page.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageByUrlAsync("test-article");

            Assert.IsNotNull(result);
            Assert.AreEqual("Test Article", result.Title);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithRootUrl_ShouldNormalizeToRoot()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "root";
            page.Title = "Home Page";
            page.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageByUrlAsync("/");

            Assert.IsNotNull(result);
            Assert.AreEqual("Home Page", result.Title);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithEmptyUrl_ShouldNormalizeToRoot()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "root";
            page.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageByUrlAsync(string.Empty);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithUrlCasing_ShouldBeCaseInsensitive()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "test-article";
            page.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageByUrlAsync("TEST-ARTICLE");

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithUnpublishedPage_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "unpublished";
            page.Published = null;
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageByUrlAsync("unpublished");

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithFuturePublished_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "future-article";
            page.Published = DateTimeOffset.UtcNow.AddDays(1);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageByUrlAsync("future-article");

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithMultipleVersions_ShouldReturnLatest()
        {
            using var context = GetIsolatedContext();
            var page1 = TestDataBuilder.CreatePublishedPage();
            page1.UrlPath = "article";
            page1.VersionNumber = 1;
            page1.Title = "Version 1";
            page1.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page1);

            var page2 = TestDataBuilder.CreatePublishedPage();
            page2.UrlPath = "article";
            page2.VersionNumber = 3;
            page2.Title = "Version 3";
            page2.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page2);

            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageByUrlAsync("article");

            Assert.IsNotNull(result);
            Assert.AreEqual("Version 3", result.Title);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithCaching_ShouldCacheResult()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "cached-article";
            page.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var service = new PublishedPageQueryService(context, memoryCache, mockBuilder.Object);

            var result1 = await service.GetPublishedPageByUrlAsync("cached-article", cacheSpan: TimeSpan.FromMinutes(10));
            var result2 = await service.GetPublishedPageByUrlAsync("cached-article", cacheSpan: TimeSpan.FromMinutes(10));

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            mockBuilder.Verify(b => b.BuildFromPublishedPageAsync(
                It.IsAny<PublishedPage>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>()), Times.Once);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithNullCache_ShouldNotCache()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "uncached-article";
            page.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result1 = await service.GetPublishedPageByUrlAsync("uncached-article", cacheSpan: TimeSpan.FromMinutes(10));
            var result2 = await service.GetPublishedPageByUrlAsync("uncached-article", cacheSpan: TimeSpan.FromMinutes(10));

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            mockBuilder.Verify(b => b.BuildFromPublishedPageAsync(
                It.IsAny<PublishedPage>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithBlogStream_ShouldFetchLatestEntry()
        {
            using var context = GetIsolatedContext();
            var blogRoot = TestDataBuilder.CreatePublishedPage();
            blogRoot.UrlPath = "blog";
            blogRoot.ArticleType = (int)ArticleType.BlogStream;
            blogRoot.BlogKey = "my-blog";
            blogRoot.Published = DateTimeOffset.UtcNow.AddDays(-10);
            context.Pages.Add(blogRoot);

            var blogEntry = TestDataBuilder.CreatePublishedPage();
            blogEntry.UrlPath = "blog/latest-post";
            blogEntry.ArticleType = (int)ArticleType.BlogPost;
            blogEntry.BlogKey = "my-blog";
            blogEntry.Title = "Latest Blog Post";
            blogEntry.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(blogEntry);

            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var service = new PublishedPageQueryService(context, memoryCache, mockBuilder.Object);

            var result = await service.GetPublishedPageByUrlAsync("blog", cacheSpan: TimeSpan.FromMinutes(1));

            Assert.IsNotNull(result);
            Assert.AreEqual("Latest Blog Post", result.Title);
        }

        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_ShouldPassLanguageToBuilder()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "test";
            page.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = CreateMockViewModelBuilder();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageByUrlAsync("test", lang: "es-ES");

            Assert.IsNotNull(result);
            Assert.AreEqual("es-ES", result.LanguageCode);
        }

        [TestMethod]
        public async Task GetPublishedPageHeaderByUrlAsync_WithValidUrl_ShouldReturnHeader()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "test-header";
            page.ArticleNumber = 12345;
            page.VersionNumber = 2;
            page.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageHeaderByUrlAsync("test-header");

            Assert.IsNotNull(result);
            Assert.AreEqual(12345, result.ArticleNumber);
            Assert.AreEqual(2, result.VersionNumber);
        }

        [TestMethod]
        public async Task GetPublishedPageHeaderByUrlAsync_WithNonExistentUrl_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageHeaderByUrlAsync("nonexistent");

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPublishedPageHeaderByUrlAsync_WithRootUrl_ShouldNormalizeToRoot()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "root";
            page.ArticleNumber = 100;
            page.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageHeaderByUrlAsync("/");

            Assert.IsNotNull(result);
            Assert.AreEqual(100, result.ArticleNumber);
        }

        [TestMethod]
        public async Task GetPublishedPageHeaderByUrlAsync_ShouldOnlyReturnHeaderFields()
        {
            using var context = GetIsolatedContext();
            var page = TestDataBuilder.CreatePublishedPage();
            page.UrlPath = "header-test";
            page.ArticleNumber = 789;
            page.VersionNumber = 5;
            page.Content = "<p>This should not be returned</p>";
            page.Title = "This should not be returned";
            page.Published = DateTimeOffset.UtcNow.AddDays(-1);
            page.Updated = DateTimeOffset.UtcNow;
            page.Expires = DateTimeOffset.UtcNow.AddDays(30);
            context.Pages.Add(page);
            await context.SaveChangesAsync();

            var mockBuilder = new Mock<IArticleViewModelBuilder>();
            var service = new PublishedPageQueryService(context, null!, mockBuilder.Object);

            var result = await service.GetPublishedPageHeaderByUrlAsync("header-test");

            Assert.IsNotNull(result);
            Assert.AreEqual(789, result.ArticleNumber);
            Assert.AreEqual(5, result.VersionNumber);
            Assert.AreEqual(page.Updated, result.Updated);
            Assert.AreEqual(page.Expires, result.Expires);
            Assert.IsNull(result.Content);
            Assert.IsNull(result.Title);
        }
    }
}
