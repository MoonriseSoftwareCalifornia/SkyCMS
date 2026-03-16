// <copyright file="GetArticleRedirectsQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.EditorQueries
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Articles.EditorQueries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="GetArticleRedirectsQueryHandler"/>.
    /// Validates article redirect retrieval with optional caching.
    /// </summary>
    [TestClass]
    public class GetArticleRedirectsQueryHandlerTests : CommonTestsBase
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
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var handler = new GetArticleRedirectsQueryHandler(context, memoryCache);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithNoRedirects_ShouldReturnEmptyCollection()
        {
            using var context = GetIsolatedContext();
            var handler = new GetArticleRedirectsQueryHandler(context);
            var query = new GetArticleRedirectsQuery();

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public async Task HandleAsync_WithRedirects_ShouldReturnRedirectItems()
        {
            using var context = GetIsolatedContext();
            var redirect1 = TestDataBuilder.CreateArticle();
            redirect1.StatusCode = (int)StatusCodeEnum.Redirect;
            redirect1.UrlPath = "/old-path";
            redirect1.BannerImage = "/new-path";
            context.Articles.Add(redirect1);

            var redirect2 = TestDataBuilder.CreateArticle();
            redirect2.StatusCode = (int)StatusCodeEnum.Redirect;
            redirect2.UrlPath = "/another-old-path";
            redirect2.BannerImage = "/another-new-path";
            context.Articles.Add(redirect2);

            await context.SaveChangesAsync();

            var handler = new GetArticleRedirectsQueryHandler(context);
            var query = new GetArticleRedirectsQuery();

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count());
            Assert.IsTrue(result.Any(r => r.FromUrl == "/old-path" && r.ToUrl == "/new-path"));
            Assert.IsTrue(result.Any(r => r.FromUrl == "/another-old-path" && r.ToUrl == "/another-new-path"));
        }

        [TestMethod]
        public async Task HandleAsync_ExcludesNonRedirectArticles()
        {
            using var context = GetIsolatedContext();
            var redirect = TestDataBuilder.CreateArticle();
            redirect.StatusCode = (int)StatusCodeEnum.Redirect;
            redirect.UrlPath = "/old-path";
            redirect.BannerImage = "/new-path";
            context.Articles.Add(redirect);

            var activeArticle = TestDataBuilder.CreateArticle();
            activeArticle.StatusCode = (int)StatusCodeEnum.Active;
            activeArticle.UrlPath = "/active-path";
            context.Articles.Add(activeArticle);

            var deletedArticle = TestDataBuilder.CreateArticle();
            deletedArticle.StatusCode = (int)StatusCodeEnum.Deleted;
            deletedArticle.UrlPath = "/deleted-path";
            context.Articles.Add(deletedArticle);

            await context.SaveChangesAsync();

            var handler = new GetArticleRedirectsQueryHandler(context);
            var query = new GetArticleRedirectsQuery();

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());
            Assert.AreEqual("/old-path", result.First().FromUrl);
        }

        [TestMethod]
        public async Task HandleAsync_WithCaching_ShouldCacheResults()
        {
            using var context = GetIsolatedContext();
            var redirect = TestDataBuilder.CreateArticle();
            redirect.StatusCode = (int)StatusCodeEnum.Redirect;
            redirect.UrlPath = "/old-path";
            redirect.BannerImage = "/new-path";
            context.Articles.Add(redirect);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetArticleRedirectsQueryHandler(context, memoryCache);
            var query = new GetArticleRedirectsQuery 
            { 
                CacheDuration = TimeSpan.FromMinutes(5)
            };

            // First call - cache miss
            var result1 = await handler.HandleAsync(query);

            // Add another redirect
            var redirect2 = TestDataBuilder.CreateArticle();
            redirect2.StatusCode = (int)StatusCodeEnum.Redirect;
            redirect2.UrlPath = "/new-old-path";
            redirect2.BannerImage = "/new-new-path";
            context.Articles.Add(redirect2);
            await context.SaveChangesAsync();

            // Second call - should return cached results (only 1 redirect)
            var result2 = await handler.HandleAsync(query);

            Assert.AreEqual(1, result1.Count());
            Assert.AreEqual(1, result2.Count()); // Still 1 from cache
        }

        [TestMethod]
        public async Task HandleAsync_WithoutCaching_ShouldAlwaysFetchFresh()
        {
            using var context = GetIsolatedContext();
            var redirect = TestDataBuilder.CreateArticle();
            redirect.StatusCode = (int)StatusCodeEnum.Redirect;
            redirect.UrlPath = "/old-path";
            redirect.BannerImage = "/new-path";
            context.Articles.Add(redirect);
            await context.SaveChangesAsync();

            var handler = new GetArticleRedirectsQueryHandler(context);
            var query = new GetArticleRedirectsQuery(); // No cache duration

            // First call
            var result1 = await handler.HandleAsync(query);

            // Add another redirect
            var redirect2 = TestDataBuilder.CreateArticle();
            redirect2.StatusCode = (int)StatusCodeEnum.Redirect;
            redirect2.UrlPath = "/new-old-path";
            redirect2.BannerImage = "/new-new-path";
            context.Articles.Add(redirect2);
            await context.SaveChangesAsync();

            // Second call - should fetch fresh data
            var result2 = await handler.HandleAsync(query);

            Assert.AreEqual(1, result1.Count());
            Assert.AreEqual(2, result2.Count()); // Fresh data includes new redirect
        }
    }
}
