// <copyright file="GetBlogPostNavigationQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Blogs.Queries
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Blogs.Queries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="GetBlogPostNavigationQueryHandler"/>.
    /// Validates blog post navigation (prev/next) with optional full post list.
    /// </summary>
    [TestClass]
    public class GetBlogPostNavigationQueryHandlerTests : CommonTestsBase
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
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            try
            {
                var handler = new GetBlogPostNavigationQueryHandler(null!, memoryCache);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public void Constructor_WithNullMemoryCache_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();

            try
            {
                var handler = new GetBlogPostNavigationQueryHandler(context, null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("memoryCache", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithNullQuery_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, memoryCache);

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
        public async Task HandleAsync_WithSinglePost_ShouldReturnNoNavigation()
        {
            using var context = GetIsolatedContext();
            var blogKey = "solo-blog";
            var post = TestDataBuilder.CreatePublishedPage();
            post.BlogKey = blogKey;
            post.UrlPath = "only-post";
            post.ArticleType = (int)ArticleType.BlogPost;
            post.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(post);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostNavigationQuery
            {
                BlogKey = blogKey,
                CurrentPostUrlPath = "only-post"
            });

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.TotalPostCount);
            Assert.IsNull(result.PreviousPost);
            Assert.IsNull(result.NextPost);
        }

        [TestMethod]
        public async Task HandleAsync_WithFirstPost_ShouldHaveOnlyNextPost()
        {
            using var context = GetIsolatedContext();
            var blogKey = "nav-blog";

            var post1 = TestDataBuilder.CreatePublishedPage();
            post1.BlogKey = blogKey;
            post1.UrlPath = "post-1-newest";
            post1.ArticleType = (int)ArticleType.BlogPost;
            post1.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(post1);

            var post2 = TestDataBuilder.CreatePublishedPage();
            post2.BlogKey = blogKey;
            post2.UrlPath = "post-2-older";
            post2.ArticleType = (int)ArticleType.BlogPost;
            post2.Published = DateTimeOffset.UtcNow.AddDays(-2);
            context.Pages.Add(post2);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostNavigationQuery
            {
                BlogKey = blogKey,
                CurrentPostUrlPath = "post-1-newest"
            });

            Assert.IsNotNull(result);
            Assert.IsNull(result.PreviousPost);
            Assert.IsNotNull(result.NextPost);
            Assert.AreEqual("post-2-older", result.NextPost.UrlPath);
        }

        [TestMethod]
        public async Task HandleAsync_WithLastPost_ShouldHaveOnlyPreviousPost()
        {
            using var context = GetIsolatedContext();
            var blogKey = "nav-blog";

            var post1 = TestDataBuilder.CreatePublishedPage();
            post1.BlogKey = blogKey;
            post1.UrlPath = "post-1-newest";
            post1.ArticleType = (int)ArticleType.BlogPost;
            post1.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(post1);

            var post2 = TestDataBuilder.CreatePublishedPage();
            post2.BlogKey = blogKey;
            post2.UrlPath = "post-2-oldest";
            post2.ArticleType = (int)ArticleType.BlogPost;
            post2.Published = DateTimeOffset.UtcNow.AddDays(-3);
            context.Pages.Add(post2);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostNavigationQuery
            {
                BlogKey = blogKey,
                CurrentPostUrlPath = "post-2-oldest"
            });

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.PreviousPost);
            Assert.IsNull(result.NextPost);
            Assert.AreEqual("post-1-newest", result.PreviousPost.UrlPath);
        }

        [TestMethod]
        public async Task HandleAsync_WithMiddlePost_ShouldHaveBothNavigation()
        {
            using var context = GetIsolatedContext();
            var blogKey = "nav-blog";

            var post1 = TestDataBuilder.CreatePublishedPage();
            post1.BlogKey = blogKey;
            post1.UrlPath = "post-1";
            post1.ArticleType = (int)ArticleType.BlogPost;
            post1.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(post1);

            var post2 = TestDataBuilder.CreatePublishedPage();
            post2.BlogKey = blogKey;
            post2.UrlPath = "post-2";
            post2.ArticleType = (int)ArticleType.BlogPost;
            post2.Published = DateTimeOffset.UtcNow.AddDays(-2);
            context.Pages.Add(post2);

            var post3 = TestDataBuilder.CreatePublishedPage();
            post3.BlogKey = blogKey;
            post3.UrlPath = "post-3";
            post3.ArticleType = (int)ArticleType.BlogPost;
            post3.Published = DateTimeOffset.UtcNow.AddDays(-3);
            context.Pages.Add(post3);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostNavigationQuery
            {
                BlogKey = blogKey,
                CurrentPostUrlPath = "post-2"
            });

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.PreviousPost);
            Assert.IsNotNull(result.NextPost);
            Assert.AreEqual("post-1", result.PreviousPost.UrlPath);
            Assert.AreEqual("post-3", result.NextPost.UrlPath);
        }

        [TestMethod]
        public async Task HandleAsync_WithIncludeAllPosts_ShouldReturnFullList()
        {
            using var context = GetIsolatedContext();
            var blogKey = "full-list-blog";

            for (int i = 0; i < 5; i++)
            {
                var post = TestDataBuilder.CreatePublishedPage();
                post.BlogKey = blogKey;
                post.UrlPath = $"post-{i}";
                post.ArticleType = (int)ArticleType.BlogPost;
                post.Published = DateTimeOffset.UtcNow.AddDays(-i - 1);
                context.Pages.Add(post);
            }

            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostNavigationQuery
            {
                BlogKey = blogKey,
                CurrentPostUrlPath = "post-2",
                IncludeAllPosts = true
            });

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.AllPosts);
            Assert.AreEqual(5, result.AllPosts.Count);
        }

        [TestMethod]
        public async Task HandleAsync_PositionNumbers_ShouldBeOneBasedAndCorrect()
        {
            using var context = GetIsolatedContext();
            var blogKey = "position-blog";

            var post1 = TestDataBuilder.CreatePublishedPage();
            post1.BlogKey = blogKey;
            post1.UrlPath = "newest";
            post1.ArticleType = (int)ArticleType.BlogPost;
            post1.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(post1);

            var post2 = TestDataBuilder.CreatePublishedPage();
            post2.BlogKey = blogKey;
            post2.UrlPath = "middle";
            post2.ArticleType = (int)ArticleType.BlogPost;
            post2.Published = DateTimeOffset.UtcNow.AddDays(-2);
            context.Pages.Add(post2);

            var post3 = TestDataBuilder.CreatePublishedPage();
            post3.BlogKey = blogKey;
            post3.UrlPath = "oldest";
            post3.ArticleType = (int)ArticleType.BlogPost;
            post3.Published = DateTimeOffset.UtcNow.AddDays(-3);
            context.Pages.Add(post3);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostNavigationQuery
            {
                BlogKey = blogKey,
                CurrentPostUrlPath = "middle"
            });

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.CurrentPostPosition);
            Assert.AreEqual(1, result.PreviousPost!.Position);
            Assert.AreEqual(3, result.NextPost!.Position);
        }

        [TestMethod]
        public async Task HandleAsync_BlogKeyNormalization_ShouldConvertUnderscoreToHyphen()
        {
            using var context = GetIsolatedContext();
            var post = TestDataBuilder.CreatePublishedPage();
            post.BlogKey = "tech-blog";
            post.UrlPath = "test-post";
            post.ArticleType = (int)ArticleType.BlogPost;
            post.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(post);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostNavigationQuery
            {
                BlogKey = "tech_blog",
                CurrentPostUrlPath = "test-post"
            });

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.TotalPostCount);
        }

        [TestMethod]
        public async Task HandleAsync_WithCacheDuration_ShouldCacheResult()
        {
            using var context = GetIsolatedContext();
            var blogKey = "cached-nav";
            var post = TestDataBuilder.CreatePublishedPage();
            post.BlogKey = blogKey;
            post.UrlPath = "cached-post";
            post.ArticleType = (int)ArticleType.BlogPost;
            post.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(post);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostNavigationQueryHandler(context, memoryCache);
            var query = new GetBlogPostNavigationQuery
            {
                BlogKey = blogKey,
                CurrentPostUrlPath = "cached-post",
                CacheDuration = TimeSpan.FromMinutes(10)
            };

            var result1 = await handler.HandleAsync(query);
            var result2 = await handler.HandleAsync(query);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(result1.TotalPostCount, result2.TotalPostCount);
        }
    }
}
