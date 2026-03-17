// <copyright file="GetLastPublishedDateQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.EditorQueries
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Articles.EditorQueries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="GetLastPublishedDateQueryHandler"/>.
    /// Validates last published date retrieval with optional caching.
    /// </summary>
    [TestClass]
    public class GetLastPublishedDateQueryHandlerTests : CommonTestsBase
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

            var handler = new GetLastPublishedDateQueryHandler(context);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentArticleNumber_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var handler = new GetLastPublishedDateQueryHandler(context);
            var query = new GetLastPublishedDateQuery { ArticleNumber = 999999 };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithPublishedArticle_ShouldReturnPublishedDate()
        {
            using var context = GetIsolatedContext();
            var publishedDate = DateTimeOffset.UtcNow.AddDays(-5);
            var article = TestDataBuilder.CreateArticle();
            article.ArticleNumber = 12345;
            article.Published = publishedDate;
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var handler = new GetLastPublishedDateQueryHandler(context);
            var query = new GetLastPublishedDateQuery { ArticleNumber = 12345 };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(publishedDate, result.Value);
        }

        [TestMethod]
        public async Task HandleAsync_WithUnpublishedArticle_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.ArticleNumber = 12345;
            article.Published = null;
            article.StatusCode = (int)StatusCodeEnum.Inactive;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var handler = new GetLastPublishedDateQueryHandler(context);
            var query = new GetLastPublishedDateQuery { ArticleNumber = 12345 };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithMultipleVersions_ShouldReturnLatestPublishedDate()
        {
            using var context = GetIsolatedContext();
            var oldDate = DateTimeOffset.UtcNow.AddDays(-10);
            var newDate = DateTimeOffset.UtcNow.AddDays(-2);

            var article1 = TestDataBuilder.CreateArticle();
            article1.ArticleNumber = 12345;
            article1.Published = oldDate;
            article1.VersionNumber = 1;
            context.Articles.Add(article1);

            var article2 = TestDataBuilder.CreateArticle();
            article2.ArticleNumber = 12345;
            article2.Published = newDate;
            article2.VersionNumber = 2;
            context.Articles.Add(article2);

            await context.SaveChangesAsync();

            var handler = new GetLastPublishedDateQueryHandler(context);
            var query = new GetLastPublishedDateQuery { ArticleNumber = 12345 };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(newDate, result.Value);
        }

        [TestMethod]
        public async Task HandleAsync_WithCaching_ShouldCacheResult()
        {
            using var context = GetIsolatedContext();
            var publishedDate = DateTimeOffset.UtcNow.AddDays(-5);
            var article = TestDataBuilder.CreateArticle();
            article.ArticleNumber = 12345;
            article.Published = publishedDate;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetLastPublishedDateQueryHandler(context, memoryCache);
            var query = new GetLastPublishedDateQuery
            { 
                ArticleNumber = 12345,
                CacheDuration = TimeSpan.FromMinutes(5)
            };

            // First call - cache miss
            var result1 = await handler.HandleAsync(query);

            // Modify the date
            article.Published = DateTimeOffset.UtcNow.AddDays(-1);
            await context.SaveChangesAsync();

            // Second call - cache hit (should return original date)
            var result2 = await handler.HandleAsync(query);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(result1.Value, result2.Value);
            Assert.AreEqual(publishedDate, result2.Value);
        }
    }
}
