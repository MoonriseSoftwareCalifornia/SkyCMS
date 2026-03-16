// <copyright file="GetSitemapQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Sitemap.Queries
{
    using System;
    using System.Collections;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Sitemap.Queries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using X.Web.Sitemap;

    /// <summary>
    /// Tests for <see cref="GetSitemapQueryHandler"/>.
    /// </summary>
    [TestClass]
    public class GetSitemapQueryHandlerTests : CommonTestsBase
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            ContextPool = new TestDbContextPool();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ContextPool?.Dispose();
        }

        [TestMethod]
        public void Constructor_WithValidDependencies_ShouldSucceed()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var handler = new GetSitemapQueryHandler(context, memoryCache);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            try
            {
                _ = new GetSitemapQueryHandler(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithNoCatalogEntries_ShouldReturnEmptySitemap()
        {
            using var context = GetIsolatedContext();
            var handler = new GetSitemapQueryHandler(context);

            var result = await handler.HandleAsync(new GetSitemapQuery());

            Assert.IsNotNull(result);
            Assert.AreEqual(0, GetUrlCount(result));
        }

        [TestMethod]
        public async Task HandleAsync_WithRootAndOtherEntries_ShouldIncludeBoth()
        {
            using var context = GetIsolatedContext();

            var root = TestDataBuilder.CreateCatalogEntry(articleNumber: 1);
            root.UrlPath = "root";
            root.Title = "Home";
            root.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(root);

            var article = TestDataBuilder.CreateCatalogEntry(articleNumber: 2);
            article.UrlPath = "articles/one";
            article.Title = "One";
            article.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(article);

            await context.SaveChangesAsync();

            var handler = new GetSitemapQueryHandler(context);
            var result = await handler.HandleAsync(new GetSitemapQuery());

            Assert.AreEqual(2, GetUrlCount(result));
            Assert.IsNotNull(GetUrlByLocation(result, "/"));
            Assert.IsNotNull(GetUrlByLocation(result, "/articles/one"));
        }

        [TestMethod]
        public async Task HandleAsync_ShouldIncludeNearFutureButExcludeFarFutureEntries()
        {
            using var context = GetIsolatedContext();

            var nearFuture = TestDataBuilder.CreateCatalogEntry(articleNumber: 10);
            nearFuture.UrlPath = "near-future";
            nearFuture.Published = DateTimeOffset.UtcNow.AddMinutes(5);
            context.ArticleCatalog.Add(nearFuture);

            var farFuture = TestDataBuilder.CreateCatalogEntry(articleNumber: 11);
            farFuture.UrlPath = "far-future";
            farFuture.Published = DateTimeOffset.UtcNow.AddMinutes(20);
            context.ArticleCatalog.Add(farFuture);

            await context.SaveChangesAsync();

            var handler = new GetSitemapQueryHandler(context);
            var result = await handler.HandleAsync(new GetSitemapQuery());

            Assert.IsNotNull(GetUrlByLocation(result, "/near-future"));
            Assert.IsNull(GetUrlByLocation(result, "/far-future"));
        }

        [TestMethod]
        public async Task HandleAsync_WithCachingEnabled_ShouldReturnCachedResult()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var first = TestDataBuilder.CreateCatalogEntry(articleNumber: 1);
            first.UrlPath = "cached-item";
            first.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(first);
            await context.SaveChangesAsync();

            var handler = new GetSitemapQueryHandler(context, memoryCache);
            var query = new GetSitemapQuery { CacheDuration = TimeSpan.FromMinutes(10) };

            var result1 = await handler.HandleAsync(query);
            Assert.IsNotNull(GetUrlByLocation(result1, "/cached-item"));

            var second = TestDataBuilder.CreateCatalogEntry(articleNumber: 2);
            second.UrlPath = "new-item-not-in-cache";
            second.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(second);
            await context.SaveChangesAsync();

            var result2 = await handler.HandleAsync(query);

            Assert.IsNotNull(GetUrlByLocation(result2, "/cached-item"));
            Assert.IsNull(GetUrlByLocation(result2, "/new-item-not-in-cache"));
        }

        [TestMethod]
        public async Task HandleAsync_WithRelativeBannerImage_ShouldPrefixWithSlash()
        {
            using var context = GetIsolatedContext();
            var entry = TestDataBuilder.CreateCatalogEntry(articleNumber: 1);
            entry.UrlPath = "with-image";
            entry.Title = "With Image";
            entry.BannerImage = "images/banner.jpg";
            entry.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.ArticleCatalog.Add(entry);
            await context.SaveChangesAsync();

            var handler = new GetSitemapQueryHandler(context);
            var result = await handler.HandleAsync(new GetSitemapQuery());
            var url = GetUrlByLocation(result, "/with-image");

            Assert.IsNotNull(url);
            Assert.IsNotNull(url.Images);
            Assert.AreEqual(1, url.Images.Count);
            Assert.AreEqual("/images/banner.jpg", url.Images[0].Location);
            Assert.AreEqual("With Image", url.Images[0].Title);
        }

        private static int GetUrlCount(Sitemap sitemap)
        {
            if (sitemap is IEnumerable enumerable)
            {
                return enumerable.Cast<object>().Count();
            }

            return 0;
        }

        private static Url? GetUrlByLocation(Sitemap sitemap, string location)
        {
            if (sitemap is IEnumerable enumerable)
            {
                return enumerable.Cast<object>()
                    .OfType<Url>()
                    .FirstOrDefault(u => string.Equals(u.Location, location, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }
    }
}
