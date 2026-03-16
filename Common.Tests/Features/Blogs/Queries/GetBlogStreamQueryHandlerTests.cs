// <copyright file="GetBlogStreamQueryHandlerTests.cs" company="Moonrise Software, LLC">
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
    /// Tests for <see cref="GetBlogStreamQueryHandler"/>.
    /// Validates blog stream retrieval with normalization, caching, and latest post preview.
    /// </summary>
    [TestClass]
    public class GetBlogStreamQueryHandlerTests : CommonTestsBase
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
                var handler = new GetBlogStreamQueryHandler(null!, memoryCache);
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
                var handler = new GetBlogStreamQueryHandler(context, null!);
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
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

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
        public async Task HandleAsync_WithEmptyBlogKey_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogStreamQuery { BlogKey = string.Empty });

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithWhitespaceBlogKey_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogStreamQuery { BlogKey = "   " });

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentBlogKey_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogStreamQuery { BlogKey = "non-existent" });

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithValidBlogStream_ShouldReturnResult()
        {
            using var context = GetIsolatedContext();
            var blogStream = TestDataBuilder.CreateArticle();
            blogStream.BlogKey = "tech-blog";
            blogStream.UrlPath = "tech-blog";
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(blogStream);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogStreamQuery { BlogKey = "tech-blog" });

            Assert.IsNotNull(result);
            Assert.AreEqual(blogStream.Id, result.StreamId);
            Assert.AreEqual(blogStream.Title, result.Title);
        }

        [TestMethod]
        public async Task HandleAsync_BlogKeyNormalization_UnderscoreToHyphen()
        {
            using var context = GetIsolatedContext();
            var blogStream = TestDataBuilder.CreateArticle();
            blogStream.BlogKey = "tech-blog";
            blogStream.UrlPath = "tech-blog";
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(blogStream);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogStreamQuery { BlogKey = "tech_blog" });

            Assert.IsNotNull(result);
            Assert.AreEqual(blogStream.Id, result.StreamId);
        }

        [TestMethod]
        public async Task HandleAsync_BlogKeyNormalization_ShouldBeCaseInsensitive()
        {
            using var context = GetIsolatedContext();
            var blogStream = TestDataBuilder.CreateArticle();
            blogStream.BlogKey = "tech-blog";
            blogStream.UrlPath = "tech-blog";
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(blogStream);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogStreamQuery { BlogKey = "TECH-BLOG" });

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithLatestBlogPost_ShouldIncludePreview()
        {
            using var context = GetIsolatedContext();
            var blogStream = TestDataBuilder.CreateArticle();
            blogStream.BlogKey = "news-blog";
            blogStream.UrlPath = "news-blog";
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.Published = DateTimeOffset.UtcNow.AddDays(-10);
            context.Articles.Add(blogStream);

            var blogPost = TestDataBuilder.CreateArticle();
            blogPost.BlogKey = "news-blog";
            blogPost.ArticleType = (int)ArticleType.BlogPost;
            blogPost.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(blogPost);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogStreamQuery { BlogKey = "news-blog" });

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.LatestPost);
            Assert.AreEqual(blogPost.Id, result.LatestPost.Id);
            Assert.AreEqual(blogPost.Title, result.LatestPost.Title);
        }

        [TestMethod]
        public async Task HandleAsync_WithMultiplePosts_ShouldReturnLatest()
        {
            using var context = GetIsolatedContext();
            var blogStream = TestDataBuilder.CreateArticle();
            blogStream.BlogKey = "updates";
            blogStream.UrlPath = "updates";
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.Published = DateTimeOffset.UtcNow.AddDays(-30);
            context.Articles.Add(blogStream);

            var oldPost = TestDataBuilder.CreateArticle();
            oldPost.BlogKey = "updates";
            oldPost.ArticleType = (int)ArticleType.BlogPost;
            oldPost.Published = DateTimeOffset.UtcNow.AddDays(-10);
            context.Articles.Add(oldPost);

            var latestPost = TestDataBuilder.CreateArticle();
            latestPost.BlogKey = "updates";
            latestPost.ArticleType = (int)ArticleType.BlogPost;
            latestPost.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(latestPost);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogStreamQuery { BlogKey = "updates" });

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.LatestPost);
            Assert.AreEqual(latestPost.Id, result.LatestPost.Id);
        }

        [TestMethod]
        public async Task HandleAsync_ShouldCountPublishedPosts()
        {
            using var context = GetIsolatedContext();
            var blogStream = TestDataBuilder.CreateArticle();
            blogStream.BlogKey = "counted-blog";
            blogStream.UrlPath = "counted-blog";
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.Published = DateTimeOffset.UtcNow.AddDays(-30);
            context.Articles.Add(blogStream);

            for (int i = 0; i < 5; i++)
            {
                var post = TestDataBuilder.CreateArticle();
                post.BlogKey = "counted-blog";
                post.ArticleType = (int)ArticleType.BlogPost;
                post.Published = DateTimeOffset.UtcNow.AddDays(-i - 1);
                context.Articles.Add(post);
            }

            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogStreamQuery { BlogKey = "counted-blog" });

            Assert.IsNotNull(result);
            Assert.AreEqual(5, result.PublishedPostCount);
        }

        [TestMethod]
        public async Task HandleAsync_WithFuturePosts_ShouldNotCount()
        {
            using var context = GetIsolatedContext();
            var blogStream = TestDataBuilder.CreateArticle();
            blogStream.BlogKey = "future-blog";
            blogStream.UrlPath = "future-blog";
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.Published = DateTimeOffset.UtcNow.AddDays(-30);
            context.Articles.Add(blogStream);

            var publishedPost = TestDataBuilder.CreateArticle();
            publishedPost.BlogKey = "future-blog";
            publishedPost.ArticleType = (int)ArticleType.BlogPost;
            publishedPost.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(publishedPost);

            var futurePost = TestDataBuilder.CreateArticle();
            futurePost.BlogKey = "future-blog";
            futurePost.ArticleType = (int)ArticleType.BlogPost;
            futurePost.Published = DateTimeOffset.UtcNow.AddDays(7);
            context.Articles.Add(futurePost);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);

            var result = await handler.HandleAsync(new GetBlogStreamQuery { BlogKey = "future-blog" });

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.PublishedPostCount);
        }

        [TestMethod]
        public async Task HandleAsync_WithCacheDuration_ShouldCacheResult()
        {
            using var context = GetIsolatedContext();
            var blogStream = TestDataBuilder.CreateArticle();
            blogStream.BlogKey = "cached-stream";
            blogStream.UrlPath = "cached-stream";
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Articles.Add(blogStream);
            await context.SaveChangesAsync();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var handler = new GetBlogStreamQueryHandler(context, memoryCache);
            var query = new GetBlogStreamQuery
            {
                BlogKey = "cached-stream",
                CacheDuration = TimeSpan.FromMinutes(10)
            };

            var result1 = await handler.HandleAsync(query);
            var result2 = await handler.HandleAsync(query);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(result1.StreamId, result2.StreamId);
        }
    }
}
