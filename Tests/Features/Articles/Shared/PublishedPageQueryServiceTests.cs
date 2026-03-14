// <copyright file="PublishedPageQueryServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Shared
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Shared;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for PublishedPageQueryService - query and caching logic.
    /// </summary>
    [TestClass]
    public class PublishedPageQueryServiceTests
    {
        private ApplicationDbContext dbContext = null!;
        private IMemoryCache memoryCache = null!;
        private IArticleViewModelBuilder viewModelBuilder = null!;
        private PublishedPageQueryService service = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"PublishedPageDb_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            dbContext = new ApplicationDbContext(options);
            memoryCache = new MemoryCache(new MemoryCacheOptions());
            viewModelBuilder = new ArticleViewModelBuilder(null!, dbContext, memoryCache, "https://publisher.test", isEditor: false);
            service = new PublishedPageQueryService(dbContext, memoryCache, viewModelBuilder);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            memoryCache.Dispose();
            dbContext.Dispose();
        }

        #region Cache Behavior Tests

        /// <summary>
        /// Tests that caching is used when cacheSpan is provided.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithCache_CachesResult()
        {
            // Arrange
            var publishedPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "test-page",
                Title = "Test Page",
                Content = "Hello world",
                Published = DateTimeOffset.UtcNow.AddMinutes(-5),
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 0,
                VersionNumber = 1
            };
            await dbContext.Pages.AddAsync(publishedPage);
            await dbContext.SaveChangesAsync();

            var cacheSpan = TimeSpan.FromMinutes(10);

            // Act - First call
            var result1 = await service.GetPublishedPageByUrlAsync("test-page", cacheSpan: cacheSpan);
            
            // Modify database
            publishedPage.Title = "Modified Title";
            dbContext.Pages.Update(publishedPage);
            await dbContext.SaveChangesAsync();

            // Act - Second call (should return cached value with original title)
            var result2 = await service.GetPublishedPageByUrlAsync("test-page", cacheSpan: cacheSpan);

            // Assert - Both should have original title (cached)
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual("Test Page", result2.Title, "Cache should return original value");
        }

        /// <summary>
        /// Tests that caching is disabled when cacheSpan is null.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_WithoutCache_FetchesFreshData()
        {
            // Arrange
            var publishedPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "test-page",
                Title = "Original Title",
                Content = "Hello world",
                Published = DateTimeOffset.UtcNow.AddMinutes(-5),
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 0,
                VersionNumber = 1
            };
            await dbContext.Pages.AddAsync(publishedPage);
            await dbContext.SaveChangesAsync();

            // Act - First call without caching
            var result1 = await service.GetPublishedPageByUrlAsync("test-page", cacheSpan: null);
            
            // Modify database
            publishedPage.Title = "Modified Title";
            dbContext.Pages.Update(publishedPage);
            await dbContext.SaveChangesAsync();

            // Act - Second call (should return fresh data)
            var result2 = await service.GetPublishedPageByUrlAsync("test-page", cacheSpan: null);

            // Assert
            Assert.IsNotNull(result1);
            Assert.AreEqual("Original Title", result1.Title);
            Assert.IsNotNull(result2);
            Assert.AreEqual("Modified Title", result2.Title, "Should fetch fresh data when not cached");
        }

        /// <summary>
        /// Tests that cache respects language and layout parameters.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_CacheKey_IncludesLanguageAndLayout()
        {
            // Arrange
            var publishedPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "test-page",
                Title = "Test Page",
                Content = "Hello world",
                Published = DateTimeOffset.UtcNow.AddMinutes(-5),
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 0,
                VersionNumber = 1
            };
            await dbContext.Pages.AddAsync(publishedPage);
            await dbContext.SaveChangesAsync();

            var cacheSpan = TimeSpan.FromMinutes(10);

            // Act - Call with different parameters
            var resultEn = await service.GetPublishedPageByUrlAsync("test-page", "en-US", cacheSpan, includeLayout: true);
            var resultEsNoLayout = await service.GetPublishedPageByUrlAsync("test-page", "es", cacheSpan, includeLayout: false);

            // Assert - Both should be cached separately
            Assert.IsNotNull(resultEn);
            Assert.IsNotNull(resultEsNoLayout);
            Assert.IsTrue(memoryCache.TryGetValue("test-page-en-US-True", out _));
            Assert.IsTrue(memoryCache.TryGetValue("test-page-es-False", out _));
        }

        #endregion

        #region Unpublished/Future Content Tests

        /// <summary>
        /// Tests that unpublished pages are not returned.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_UnpublishedPage_ReturnsNull()
        {
            // Arrange
            var publishedPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "unpublished-page",
                Title = "Unpublished Page",
                Content = "Not published",
                Published = null,  // Unpublished
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 0,
                VersionNumber = 1
            };
            await dbContext.Pages.AddAsync(publishedPage);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetPublishedPageByUrlAsync("unpublished-page");

            // Assert
            Assert.IsNull(result);
        }

        /// <summary>
        /// Tests that future-dated pages are not returned.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_FuturePage_ReturnsNull()
        {
            // Arrange
            var publishedPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "future-page",
                Title = "Future Page",
                Content = "Not yet published",
                Published = DateTimeOffset.UtcNow.AddHours(1),  // Future date
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 0,
                VersionNumber = 1
            };
            await dbContext.Pages.AddAsync(publishedPage);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetPublishedPageByUrlAsync("future-page");

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region Header Query Tests

        /// <summary>
        /// Tests that header-only query returns minimal fields.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageHeaderByUrlAsync_ReturnsHeaderOnly()
        {
            // Arrange
            var publishedPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 42,
                UrlPath = "test-page",
                Title = "Test Page",
                Content = "Very long content that should not be included in header query",
                Published = DateTimeOffset.UtcNow.AddMinutes(-5),
                Updated = DateTimeOffset.UtcNow,
                VersionNumber = 3,
                StatusCode = 0
            };
            await dbContext.Pages.AddAsync(publishedPage);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetPublishedPageHeaderByUrlAsync("test-page");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(42, result.ArticleNumber);
            Assert.AreEqual(publishedPage.Id, result.Id);
            Assert.AreEqual(3, result.VersionNumber);
            Assert.IsNull(result.Content, "Header query should not include content");
            Assert.IsNull(result.Title, "Header query should not include title");
        }

        /// <summary>
        /// Tests that header query respects publish date filter.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageHeaderByUrlAsync_UnpublishedPage_ReturnsNull()
        {
            // Arrange
            var publishedPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "unpublished-header",
                Title = "Unpublished",
                Content = "Not published",
                Published = null,  // Unpublished
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 0,
                VersionNumber = 1
            };
            await dbContext.Pages.AddAsync(publishedPage);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetPublishedPageHeaderByUrlAsync("unpublished-header");

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region URL Normalization Tests

        /// <summary>
        /// Tests that "root" and "/root" are treated as root page.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_RootPage_NormalizedCorrectly()
        {
            // Arrange
            var rootPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "root",
                Title = "Home Page",
                Content = "Welcome",
                Published = DateTimeOffset.UtcNow.AddMinutes(-5),
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 0,
                VersionNumber = 1
            };
            await dbContext.Pages.AddAsync(rootPage);
            await dbContext.SaveChangesAsync();

            // Act - Try different root variations
            var resultRoot = await service.GetPublishedPageByUrlAsync("root");
            var resultSlashRoot = await service.GetPublishedPageByUrlAsync("/root");
            var resultSlash = await service.GetPublishedPageByUrlAsync("/");
            var resultEmpty = await service.GetPublishedPageByUrlAsync("");

            // Assert
            Assert.IsNotNull(resultRoot);
            Assert.IsNotNull(resultSlashRoot);
            Assert.IsNotNull(resultSlash);
            Assert.IsNotNull(resultEmpty);
            Assert.AreEqual("Home Page", resultRoot.Title);
        }

        /// <summary>
        /// Tests that URLs are normalized to lowercase.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPageByUrlAsync_CaseInsensitive_NormalizedToLowercase()
        {
            // Arrange
            var publishedPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "about-us",
                Title = "About Us",
                Content = "About content",
                Published = DateTimeOffset.UtcNow.AddMinutes(-5),
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 0,
                VersionNumber = 1
            };
            await dbContext.Pages.AddAsync(publishedPage);
            await dbContext.SaveChangesAsync();

            // Act - Try with different cases
            var resultLower = await service.GetPublishedPageByUrlAsync("about-us");
            var resultUpper = await service.GetPublishedPageByUrlAsync("ABOUT-US");
            var resultMixed = await service.GetPublishedPageByUrlAsync("About-Us");

            // Assert
            Assert.IsNotNull(resultLower);
            Assert.IsNotNull(resultUpper);
            Assert.IsNotNull(resultMixed);
            Assert.AreEqual("About Us", resultUpper.Title);
        }

        #endregion
    }
}
