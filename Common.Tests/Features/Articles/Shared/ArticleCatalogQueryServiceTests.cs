// <copyright file="ArticleCatalogQueryServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Shared
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="ArticleCatalogQueryService"/>.
    /// Validates table of contents generation, pagination, hierarchical navigation,
    /// prefix normalization, and full-text search capabilities.
    /// </summary>
    [TestClass]
    public class ArticleCatalogQueryServiceTests : CommonTestsBase
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
            using var context = GetIsolatedContext();
            var publisherUrl = "https://publisher.test";
            var blobUrl = "https://blob.test";

            var service = new ArticleCatalogQueryService(context, publisherUrl, blobUrl);

            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            try
            {
                var service = new ArticleCatalogQueryService(null!, "https://test.com", "https://blob.test");
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public void Constructor_WithNullPublisherUrl_ShouldUseEmptyString()
        {
            using var context = GetIsolatedContext();

            var service = new ArticleCatalogQueryService(context, null!, "https://blob.test");

            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Constructor_WithNullBlobUrl_ShouldUseEmptyString()
        {
            using var context = GetIsolatedContext();

            var service = new ArticleCatalogQueryService(context, "https://publisher.test", null!);

            Assert.IsNotNull(service);
        }

        [TestMethod]
        public async Task GetTableOfContentsAsync_WithNoArticles_ShouldReturnEmptyResult()
        {
            using var context = GetIsolatedContext();
            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");

            var result = await service.GetTableOfContentsAsync(string.Empty);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.TotalCount);
            Assert.AreEqual(0, result.Items.Count);
        }

        [TestMethod]
        public async Task GetTableOfContentsAsync_WithRootPrefix_ShouldReturnAllPublishedArticles()
        {
            using var context = GetIsolatedContext();
            var catalog1 = TestDataBuilder.CreateCatalogEntry(articleNumber: 100);
            catalog1.UrlPath = "article1";
            catalog1.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(catalog1);

            var catalog2 = TestDataBuilder.CreateCatalogEntry(articleNumber: 200);
            catalog2.UrlPath = "article2";
            catalog2.Published = DateTimeOffset.UtcNow.AddDays(-2);
            context.ArticleCatalog.Add(catalog2);

            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.GetTableOfContentsAsync(string.Empty);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.TotalCount);
            Assert.AreEqual(2, result.Items.Count);
        }

        [TestMethod]
        public async Task GetTableOfContentsAsync_WithSlashPrefix_ShouldReturnAllPublishedArticles()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.GetTableOfContentsAsync("/");

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.TotalCount);
        }

        [TestMethod]
        public async Task GetTableOfContentsAsync_ShouldExcludeUnpublishedArticles()
        {
            using var context = GetIsolatedContext();
            var publishedCatalog = TestDataBuilder.CreateCatalogEntry();
            publishedCatalog.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(publishedCatalog);

            var unpublishedCatalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 999);
            unpublishedCatalog.Published = null;
            context.ArticleCatalog.Add(unpublishedCatalog);

            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.GetTableOfContentsAsync(string.Empty);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.TotalCount);
        }

        [TestMethod]
        public async Task GetTableOfContentsAsync_ShouldExcludeFuturePublishedArticles()
        {
            using var context = GetIsolatedContext();
            var pastCatalog = TestDataBuilder.CreateCatalogEntry();
            pastCatalog.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(pastCatalog);

            var futureCatalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 999);
            futureCatalog.Published = DateTimeOffset.UtcNow.AddDays(1);
            context.ArticleCatalog.Add(futureCatalog);

            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.GetTableOfContentsAsync(string.Empty);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.TotalCount);
        }

        [TestMethod]
        public async Task GetTableOfContentsAsync_WithPagination_ShouldReturnCorrectPage()
        {
            using var context = GetIsolatedContext();
            for (int i = 1; i <= 25; i++)
            {
                var catalog = TestDataBuilder.CreateCatalogEntry(articleNumber: i);
                catalog.UrlPath = $"article{i}";
                catalog.Published = DateTimeOffset.UtcNow.AddDays(-i);
                context.ArticleCatalog.Add(catalog);
            }
            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.GetTableOfContentsAsync(string.Empty, pageNo: 1, pageSize: 10);

            Assert.IsNotNull(result);
            Assert.AreEqual(10, result.TotalCount);
            Assert.AreEqual(1, result.PageNo);
            Assert.AreEqual(10, result.PageSize);
        }

        [TestMethod]
        public async Task GetTableOfContentsAsync_WithOrderByPublishedDateTrue_ShouldOrderByNewestFirst()
        {
            using var context = GetIsolatedContext();
            var older = TestDataBuilder.CreateCatalogEntry(articleNumber: 100);
            older.UrlPath = "older";
            older.Published = DateTimeOffset.UtcNow.AddDays(-10);
            context.ArticleCatalog.Add(older);

            var newer = TestDataBuilder.CreateCatalogEntry(articleNumber: 200);
            newer.UrlPath = "newer";
            newer.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(newer);

            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.GetTableOfContentsAsync(string.Empty, orderByPublishedDate: true);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.TotalCount);
            Assert.AreEqual("newer", result.Items[0].UrlPath);
            Assert.AreEqual("older", result.Items[1].UrlPath);
        }

        [TestMethod]
        public async Task GetTableOfContentsAsync_WithOrderByPublishedDateFalse_ShouldOrderByUrlPath()
        {
            using var context = GetIsolatedContext();
            var zebra = TestDataBuilder.CreateCatalogEntry(articleNumber: 100);
            zebra.UrlPath = "zebra";
            zebra.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(zebra);

            var apple = TestDataBuilder.CreateCatalogEntry(articleNumber: 200);
            apple.UrlPath = "apple";
            apple.Published = DateTimeOffset.UtcNow.AddDays(-10);
            context.ArticleCatalog.Add(apple);

            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.GetTableOfContentsAsync(string.Empty, orderByPublishedDate: false);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.TotalCount);
            Assert.AreEqual("apple", result.Items[0].UrlPath);
            Assert.AreEqual("zebra", result.Items[1].UrlPath);
        }

        [TestMethod]
        public async Task GetTableOfContentsAsync_ShouldIncludePublisherAndBlobUrls()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var publisherUrl = "https://publisher.test";
            var blobUrl = "https://blob.test";
            var service = new ArticleCatalogQueryService(context, publisherUrl, blobUrl);
            var result = await service.GetTableOfContentsAsync(string.Empty);

            Assert.IsNotNull(result);
            Assert.AreEqual(publisherUrl, result.PublisherUrl);
            Assert.AreEqual(blobUrl, result.BlobPublicUrl);
        }

        [TestMethod]
        public async Task GetTableOfContentsAsync_ShouldMapAllProperties()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.UrlPath = "/test/article";
            catalog.Title = "Test Title";
            catalog.Published = DateTimeOffset.UtcNow.AddDays(-1);
            catalog.Updated = DateTimeOffset.UtcNow;
            catalog.BannerImage = "banner.jpg";
            catalog.AuthorInfo = "Author Name";
            catalog.Introduction = "Test intro";
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.GetTableOfContentsAsync(string.Empty);

            Assert.IsNotNull(result);
            var item = result.Items.First();
            Assert.AreEqual("/test/article", item.UrlPath);
            Assert.AreEqual("Test Title", item.Title);
            Assert.AreEqual(catalog.Published.Value, item.Published);
            Assert.AreEqual(catalog.Updated, item.Updated);
            Assert.AreEqual("banner.jpg", item.BannerImage);
            Assert.AreEqual("Author Name", item.AuthorInfo);
            Assert.AreEqual("Test intro", item.Introduction);
        }

        [TestMethod]
        public async Task SearchAsync_WithNullSearchText_ShouldReturnEmptyList()
        {
            using var context = GetIsolatedContext();
            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");

            var result = await service.SearchAsync(null!);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task SearchAsync_WithEmptySearchText_ShouldReturnEmptyList()
        {
            using var context = GetIsolatedContext();
            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");

            var result = await service.SearchAsync(string.Empty);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task SearchAsync_WithMatchingTitle_ShouldReturnResults()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.Title = "Cosmos Database Tutorial";
            catalog.Introduction = "Learn about databases";
            catalog.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.SearchAsync("Cosmos");

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Cosmos Database Tutorial", result[0].Title);
        }

        [TestMethod]
        public async Task SearchAsync_WithMatchingIntroduction_ShouldReturnResults()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.Title = "Article Title";
            catalog.Introduction = "This is about Cosmos technology";
            catalog.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.SearchAsync("Cosmos");

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task SearchAsync_ShouldBeCaseInsensitive()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.Title = "COSMOS Database";
            catalog.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.SearchAsync("cosmos");

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public async Task SearchAsync_ShouldExcludeUnpublishedArticles()
        {
            using var context = GetIsolatedContext();
            var published = TestDataBuilder.CreateCatalogEntry();
            published.Title = "Published Cosmos Article";
            published.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(published);

            var unpublished = TestDataBuilder.CreateCatalogEntry(articleNumber: 999);
            unpublished.Title = "Unpublished Cosmos Article";
            unpublished.Published = null;
            context.ArticleCatalog.Add(unpublished);

            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.SearchAsync("Cosmos");

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Published Cosmos Article", result[0].Title);
        }

        [TestMethod]
        public async Task SearchAsync_ShouldExcludeFuturePublishedArticles()
        {
            using var context = GetIsolatedContext();
            var past = TestDataBuilder.CreateCatalogEntry();
            past.Title = "Past Cosmos Article";
            past.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(past);

            var future = TestDataBuilder.CreateCatalogEntry(articleNumber: 999);
            future.Title = "Future Cosmos Article";
            future.Published = DateTimeOffset.UtcNow.AddDays(1);
            context.ArticleCatalog.Add(future);

            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.SearchAsync("Cosmos");

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Past Cosmos Article", result[0].Title);
        }

        [TestMethod]
        public async Task SearchAsync_WithMultipleTerms_ShouldAndCombineTerms()
        {
            using var context = GetIsolatedContext();
            var catalog1 = TestDataBuilder.CreateCatalogEntry(articleNumber: 100);
            catalog1.Title = "Cosmos Database Tutorial";
            catalog1.Introduction = "Learn about Cosmos";
            catalog1.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(catalog1);

            var catalog2 = TestDataBuilder.CreateCatalogEntry(articleNumber: 200);
            catalog2.Title = "Database Guide";
            catalog2.Introduction = "All about databases";
            catalog2.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(catalog2);

            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.SearchAsync("Cosmos Database");

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Cosmos Database Tutorial", result[0].Title);
        }

        [TestMethod]
        public async Task SearchAsync_ShouldOrderByTitleDescending()
        {
            using var context = GetIsolatedContext();
            var catalog1 = TestDataBuilder.CreateCatalogEntry(articleNumber: 100);
            catalog1.Title = "Apple Cosmos Guide";
            catalog1.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(catalog1);

            var catalog2 = TestDataBuilder.CreateCatalogEntry(articleNumber: 200);
            catalog2.Title = "Zebra Cosmos Tutorial";
            catalog2.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(catalog2);

            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.SearchAsync("Cosmos");

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Zebra Cosmos Tutorial", result[0].Title);
            Assert.AreEqual("Apple Cosmos Guide", result[1].Title);
        }

        [TestMethod]
        public async Task SearchAsync_ShouldMapAllProperties()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.UrlPath = "/test/search-result";
            catalog.Title = "Searchable Title";
            catalog.Introduction = "Find this article";
            catalog.Published = DateTimeOffset.UtcNow.AddDays(-1);
            catalog.Updated = DateTimeOffset.UtcNow;
            catalog.BannerImage = "search-banner.jpg";
            catalog.AuthorInfo = "Search Author";
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var service = new ArticleCatalogQueryService(context, "https://test.com", "https://blob.test");
            var result = await service.SearchAsync("Searchable");

            Assert.IsNotNull(result);
            var item = result.First();
            Assert.AreEqual("/test/search-result", item.UrlPath);
            Assert.AreEqual("Searchable Title", item.Title);
            Assert.AreEqual(catalog.Published.Value, item.Published);
            Assert.AreEqual(catalog.Updated, item.Updated);
            Assert.AreEqual("search-banner.jpg", item.BannerImage);
            Assert.AreEqual("Search Author", item.AuthorInfo);
        }
    }
}
