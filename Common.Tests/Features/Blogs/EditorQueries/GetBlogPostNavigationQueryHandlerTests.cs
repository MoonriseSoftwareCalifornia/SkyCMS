// <copyright file="GetBlogPostNavigationQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Blogs.EditorQueries
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Blogs.EditorQueries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="GetBlogPostNavigationQueryHandler"/> (EditorQueries).
    /// </summary>
    [TestClass]
    public class GetBlogPostNavigationQueryHandlerTests : CommonTestsBase
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
            var cache = new MemoryCache(new MemoryCacheOptions());

            var handler = new GetBlogPostNavigationQueryHandler(context, cache);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            try
            {
                _ = new GetBlogPostNavigationQueryHandler(null!, cache);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithNullQuery_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, cache);

            try
            {
                await handler.HandleAsync(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("query", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithMiddlePost_ShouldReturnPreviousAndNext()
        {
            using var context = GetIsolatedContext();
            var now = DateTimeOffset.UtcNow;

            context.Articles.Add(new Article { Id = Guid.NewGuid(), ArticleNumber = 1, UrlPath = "blog/newest", Title = "Newest", BlogKey = "my-blog", ArticleType = (int)ArticleType.BlogPost, Published = now.AddDays(-1), Updated = now });
            context.Articles.Add(new Article { Id = Guid.NewGuid(), ArticleNumber = 2, UrlPath = "blog/current", Title = "Current", BlogKey = "my-blog", ArticleType = (int)ArticleType.BlogPost, Published = now.AddDays(-2), Updated = now });
            context.Articles.Add(new Article { Id = Guid.NewGuid(), ArticleNumber = 3, UrlPath = "blog/older", Title = "Older", BlogKey = "my-blog", ArticleType = (int)ArticleType.BlogPost, Published = now.AddDays(-3), Updated = now });
            await context.SaveChangesAsync();

            var cache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, cache);

            var result = await handler.HandleAsync(new GetBlogPostNavigationQuery
            {
                BlogKey = "my-blog",
                CurrentPostUrlPath = "blog/current",
                IncludeAllPosts = false
            });

            Assert.AreEqual("my-blog", result.BlogKey);
            Assert.AreEqual(3, result.TotalPostCount);
            Assert.AreEqual(2, result.CurrentPostPosition);
            Assert.IsNotNull(result.PreviousPost);
            Assert.AreEqual("blog/newest", result.PreviousPost!.UrlPath);
            Assert.IsNotNull(result.NextPost);
            Assert.AreEqual("blog/older", result.NextPost!.UrlPath);
        }

        [TestMethod]
        public async Task HandleAsync_ShouldNormalizeBlogKeyAndCurrentUrl()
        {
            using var context = GetIsolatedContext();
            var now = DateTimeOffset.UtcNow;

            context.Articles.Add(new Article { Id = Guid.NewGuid(), ArticleNumber = 1, UrlPath = "blog/current", Title = "Current", BlogKey = "my-blog", ArticleType = (int)ArticleType.BlogPost, Published = now.AddDays(-1), Updated = now });
            await context.SaveChangesAsync();

            var cache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, cache);

            var result = await handler.HandleAsync(new GetBlogPostNavigationQuery
            {
                BlogKey = "MY_BLOG",
                CurrentPostUrlPath = "/BLOG/CURRENT/",
                IncludeAllPosts = true
            });

            Assert.AreEqual("my-blog", result.BlogKey);
            Assert.AreEqual(1, result.TotalPostCount);
            Assert.AreEqual(1, result.CurrentPostPosition);
            Assert.AreEqual(1, result.AllPosts.Count);
        }

        [TestMethod]
        public async Task HandleAsync_WithCacheDuration_ShouldCacheResult()
        {
            using var context = GetIsolatedContext();
            var now = DateTimeOffset.UtcNow;

            context.Articles.Add(new Article { Id = Guid.NewGuid(), ArticleNumber = 1, UrlPath = "blog/current", Title = "Current", BlogKey = "my-blog", ArticleType = (int)ArticleType.BlogPost, Published = now.AddDays(-1), Updated = now });
            await context.SaveChangesAsync();

            var cache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, cache);

            var query = new GetBlogPostNavigationQuery
            {
                BlogKey = "my-blog",
                CurrentPostUrlPath = "blog/current",
                IncludeAllPosts = true,
                CacheDuration = TimeSpan.FromMinutes(10)
            };

            var first = await handler.HandleAsync(query);

            context.Articles.Add(new Article { Id = Guid.NewGuid(), ArticleNumber = 2, UrlPath = "blog/new-post", Title = "New", BlogKey = "my-blog", ArticleType = (int)ArticleType.BlogPost, Published = now, Updated = now });
            await context.SaveChangesAsync();

            var second = await handler.HandleAsync(query);

            Assert.AreEqual(first.TotalPostCount, second.TotalPostCount);
            Assert.AreEqual(1, second.TotalPostCount);
        }
    }
}
