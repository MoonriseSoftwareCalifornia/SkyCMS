// <copyright file="GetBlogPostQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Blogs.Queries
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Blogs.Queries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="GetBlogPostQueryHandler"/>.
    /// Validates blog post retrieval by URL with caching and navigation support.
    /// </summary>
    [TestClass]
    public class GetBlogPostQueryHandlerTests : CommonTestsBase
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
                var handler = new GetBlogPostQueryHandler(null!, memoryCache);
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
                var handler = new GetBlogPostQueryHandler(context, null!);
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
            var handler = new GetBlogPostQueryHandler(context, memoryCache);

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
        public async Task HandleAsync_WithEmptyUrlPath_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostQuery { UrlPath = string.Empty });

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithWhitespaceUrlPath_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostQuery { UrlPath = "   " });

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentPost_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostQuery { UrlPath = "/non-existent-post" });

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithValidBlogPost_ShouldReturnResult()
        {
            using var context = GetIsolatedContext();
            var blogPost = TestDataBuilder.CreateArticle();
            blogPost.UrlPath = "my-blog-post";
            blogPost.ArticleType = (int)ArticleType.BlogPost;
            blogPost.Published = DateTimeOffset.UtcNow.AddDays(-1);
            blogPost.BlogKey = "tech-blog";
            context.Articles.Add(blogPost);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostQuery { UrlPath = "/my-blog-post" });

            Assert.IsNotNull(result);
            Assert.AreEqual(blogPost.Id, result.Id);
            Assert.AreEqual(blogPost.Title, result.Title);
        }

        [TestMethod]
        public async Task HandleAsync_UrlPathNormalization_ShouldBeCaseInsensitive()
        {
            using var context = GetIsolatedContext();
            var blogPost = TestDataBuilder.CreateArticle();
            blogPost.UrlPath = "my-blog-post";
            blogPost.ArticleType = (int)ArticleType.BlogPost;
            blogPost.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(blogPost);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostQuery { UrlPath = "/MY-BLOG-POST/" });

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithFuturePublishedDate_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var blogPost = TestDataBuilder.CreateArticle();
            blogPost.UrlPath = "future-post";
            blogPost.ArticleType = (int)ArticleType.BlogPost;
            blogPost.Published = DateTimeOffset.UtcNow.AddDays(7);
            context.Articles.Add(blogPost);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostQuery { UrlPath = "/future-post" });

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithBlogStream_ShouldIncludeStreamInfo()
        {
            using var context = GetIsolatedContext();
            var blogStream = TestDataBuilder.CreateArticle();
            blogStream.UrlPath = "tech-blog";
            blogStream.BlogKey = "tech-blog";
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.Published = DateTimeOffset.UtcNow.AddDays(-10);
            context.Articles.Add(blogStream);

            var blogPost = TestDataBuilder.CreateArticle();
            blogPost.UrlPath = "tech-post-1";
            blogPost.BlogKey = "tech-blog";
            blogPost.ArticleType = (int)ArticleType.BlogPost;
            blogPost.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(blogPost);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostQuery { UrlPath = "/tech-post-1" });

            Assert.IsNotNull(result);
            Assert.AreEqual(blogStream.Title, result.BlogStreamTitle);
            Assert.AreEqual(blogStream.UrlPath, result.BlogStreamUrl);
        }

        [TestMethod]
        public async Task HandleAsync_WithCacheDuration_ShouldCacheResult()
        {
            using var context = GetIsolatedContext();
            var blogPost = TestDataBuilder.CreateArticle();
            blogPost.UrlPath = "cached-post";
            blogPost.ArticleType = (int)ArticleType.BlogPost;
            blogPost.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(blogPost);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostQueryHandler(context, memoryCache);
            var query = new GetBlogPostQuery
            {
                UrlPath = "/cached-post",
                CacheDuration = TimeSpan.FromMinutes(10)
            };

            var result1 = await handler.HandleAsync(query);
            var result2 = await handler.HandleAsync(query);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(result1.Id, result2.Id);
        }

        [TestMethod]
        public async Task HandleAsync_WithNavigationRequested_ShouldIncludeNavigation()
        {
            using var context = GetIsolatedContext();
            var blogKey = "test-blog";

            var post1 = TestDataBuilder.CreateArticle();
            post1.UrlPath = "post-1";
            post1.BlogKey = blogKey;
            post1.ArticleType = (int)ArticleType.BlogPost;
            post1.Published = DateTimeOffset.UtcNow.AddDays(-3);
            context.Articles.Add(post1);

            var post2 = TestDataBuilder.CreateArticle();
            post2.UrlPath = "post-2";
            post2.BlogKey = blogKey;
            post2.ArticleType = (int)ArticleType.BlogPost;
            post2.Published = DateTimeOffset.UtcNow.AddDays(-2);
            context.Articles.Add(post2);

            var post3 = TestDataBuilder.CreateArticle();
            post3.UrlPath = "post-3";
            post3.BlogKey = blogKey;
            post3.ArticleType = (int)ArticleType.BlogPost;
            post3.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(post3);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogPostQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogPostQuery
            {
                UrlPath = "/post-2",
                IncludeNavigation = true
            });

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Navigation);
        }
    }
}
