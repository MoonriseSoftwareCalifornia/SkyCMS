// <copyright file="GetArticleCatalogEntryQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.EditorQueries
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.EditorQueries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="GetArticleCatalogEntryQueryHandler"/>.
    /// Validates catalog entry retrieval with optional caching support.
    /// </summary>
    [TestClass]
    public class GetArticleCatalogEntryQueryHandlerTests : CommonTestsBase
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

            var handler = new GetArticleCatalogEntryQueryHandler(context, memoryCache);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public void Constructor_WithNullCache_ShouldSucceed()
        {
            using var context = GetIsolatedContext();

            var handler = new GetArticleCatalogEntryQueryHandler(context, null);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentArticleNumber_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var handler = new GetArticleCatalogEntryQueryHandler(context);
            var query = new GetArticleCatalogEntryQuery { ArticleNumber = 999999 };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithValidArticleNumber_ShouldReturnCatalogEntry()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 12345);
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new GetArticleCatalogEntryQueryHandler(context);
            var query = new GetArticleCatalogEntryQuery { ArticleNumber = 12345 };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(12345, result.ArticleNumber);
            Assert.AreEqual(catalog.Title, result.Title);
        }

        [TestMethod]
        public async Task HandleAsync_WithCachingEnabled_ShouldCacheResult()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 12345);
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetArticleCatalogEntryQueryHandler(context, memoryCache);
            var query = new GetArticleCatalogEntryQuery 
            { 
                ArticleNumber = 12345,
                CacheDuration = TimeSpan.FromMinutes(5)
            };

            // First call - should fetch from database
            var result1 = await handler.HandleAsync(query);

            // Modify the database entry
            catalog.Title = "Modified Title";
            await context.SaveChangesAsync();

            // Second call - should return cached version (not modified title)
            var result2 = await handler.HandleAsync(query);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(result1.Title, result2.Title);
            Assert.AreNotEqual("Modified Title", result2.Title);
        }

        [TestMethod]
        public async Task HandleAsync_WithNoCacheDuration_ShouldNotCache()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 12345);
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetArticleCatalogEntryQueryHandler(context, memoryCache);
            var query = new GetArticleCatalogEntryQuery 
            { 
                ArticleNumber = 12345,
                CacheDuration = null // No caching
            };

            // First call
            var result1 = await handler.HandleAsync(query);

            // Modify the database entry
            catalog.Title = "Modified Title";
            await context.SaveChangesAsync();

            // Second call - should fetch fresh data
            var result2 = await handler.HandleAsync(query);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreNotEqual(result1.Title, result2.Title);
            Assert.AreEqual("Modified Title", result2.Title);
        }

        [TestMethod]
        public async Task HandleAsync_WithoutMemoryCache_ShouldAlwaysFetchFresh()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 12345);
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new GetArticleCatalogEntryQueryHandler(context, null);
            var query = new GetArticleCatalogEntryQuery 
            { 
                ArticleNumber = 12345,
                CacheDuration = TimeSpan.FromMinutes(5) // Duration specified but no cache
            };

            // First call
            var result1 = await handler.HandleAsync(query);

            // Modify the database entry
            catalog.Title = "Modified Title";
            await context.SaveChangesAsync();

            // Second call - should fetch fresh data (no cache available)
            var result2 = await handler.HandleAsync(query);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreNotEqual(result1.Title, result2.Title);
            Assert.AreEqual("Modified Title", result2.Title);
        }

        [TestMethod]
        public async Task HandleAsync_CachesNullResults_ToAvoidRepeatedDbHits()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetArticleCatalogEntryQueryHandler(context, memoryCache);
            var query = new GetArticleCatalogEntryQuery 
            { 
                ArticleNumber = 999999,
                CacheDuration = TimeSpan.FromMinutes(5)
            };

            // First call - should fetch from database (returns null)
            var result1 = await handler.HandleAsync(query);

            // Add an entry with the same article number
            var catalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 999999);
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            // Second call - should return cached null (not the new entry)
            var result2 = await handler.HandleAsync(query);

            Assert.IsNull(result1);
            Assert.IsNull(result2); // Still null from cache
        }
    }
}
