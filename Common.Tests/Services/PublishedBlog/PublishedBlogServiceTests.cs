// <copyright file="PublishedBlogServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Services.PublishedBlog
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Services.PublishedBlog;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="PublishedBlogService"/>.
    /// </summary>
    [TestClass]
    public class PublishedBlogServiceTests : CommonTestsBase
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
        public void Constructor_WithValidDbContext_ShouldSucceed()
        {
            using var context = GetIsolatedContext();
            var service = new PublishedBlogService(context);
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            try
            {
                _ = new PublishedBlogService(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task GetPublishedBlogStreamAsync_WithEmptyBlogKey_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var service = new PublishedBlogService(context);
            var result = await service.GetPublishedBlogStreamAsync(string.Empty);
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPublishedBlogStreamAsync_WithValidStream_ShouldReturnLatestPublished()
        {
            using var context = GetIsolatedContext();
            var oldStream = TestDataBuilder.CreatePublishedPage();
            oldStream.BlogKey = "blog-a";
            oldStream.ArticleType = (int)ArticleType.BlogStream;
            oldStream.Published = DateTimeOffset.UtcNow.AddDays(-5);
            context.Pages.Add(oldStream);

            var newStream = TestDataBuilder.CreatePublishedPage();
            newStream.BlogKey = "blog-a";
            newStream.ArticleType = (int)ArticleType.BlogStream;
            newStream.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(newStream);

            await context.SaveChangesAsync();

            var service = new PublishedBlogService(context);
            var result = await service.GetPublishedBlogStreamAsync("blog-a");

            Assert.IsNotNull(result);
            Assert.AreEqual(newStream.Id, result.Id);
        }

        [TestMethod]
        public async Task GetPublishedBlogEntryAsync_WithValidUrl_ShouldReturnBlogPost()
        {
            using var context = GetIsolatedContext();
            var post = TestDataBuilder.CreatePublishedPage();
            post.UrlPath = "blog/post-1";
            post.ArticleType = (int)ArticleType.BlogPost;
            post.Published = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(post);
            await context.SaveChangesAsync();

            var service = new PublishedBlogService(context);
            var result = await service.GetPublishedBlogEntryAsync("blog/post-1");

            Assert.IsNotNull(result);
            Assert.AreEqual(post.Id, result.Id);
        }

        [TestMethod]
        public async Task GetPublishedBlogEntryAsync_WithExpiredPost_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var post = TestDataBuilder.CreatePublishedPage();
            post.UrlPath = "blog/expired";
            post.ArticleType = (int)ArticleType.BlogPost;
            post.Published = DateTimeOffset.UtcNow.AddDays(-3);
            post.Expires = DateTimeOffset.UtcNow.AddDays(-1);
            context.Pages.Add(post);
            await context.SaveChangesAsync();

            var service = new PublishedBlogService(context);
            var result = await service.GetPublishedBlogEntryAsync("blog/expired");

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetBlogEntriesAsync_ShouldReturnOnlyBlogPostsForKey()
        {
            using var context = GetIsolatedContext();
            context.Pages.Add(new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "blog-a/post-1",
                Title = "Post 1",
                BlogKey = "blog-a",
                ArticleType = (int)ArticleType.BlogPost,
                Published = DateTimeOffset.UtcNow.AddDays(-2),
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 1,
                VersionNumber = 1
            });
            context.Pages.Add(new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                UrlPath = "blog-a/root",
                Title = "Stream",
                BlogKey = "blog-a",
                ArticleType = (int)ArticleType.BlogStream,
                Published = DateTimeOffset.UtcNow.AddDays(-3),
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 1,
                VersionNumber = 1
            });
            context.Pages.Add(new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 3,
                UrlPath = "blog-b/post-x",
                Title = "Other Blog",
                BlogKey = "blog-b",
                ArticleType = (int)ArticleType.BlogPost,
                Published = DateTimeOffset.UtcNow.AddDays(-1),
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 1,
                VersionNumber = 1
            });
            await context.SaveChangesAsync();

            var service = new PublishedBlogService(context);
            var results = (await service.GetBlogEntriesAsync("blog-a")).ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("blog-a/post-1", results[0].UrlPath);
        }

        [TestMethod]
        public async Task GetBlogEntriesAsync_ShouldApplyPaginationBounds()
        {
            using var context = GetIsolatedContext();
            for (int i = 1; i <= 5; i++)
            {
                context.Pages.Add(new PublishedPage
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = i,
                    UrlPath = $"blog-a/post-{i}",
                    Title = $"Post {i}",
                    BlogKey = "blog-a",
                    ArticleType = (int)ArticleType.BlogPost,
                    Published = DateTimeOffset.UtcNow.AddDays(-i),
                    Updated = DateTimeOffset.UtcNow,
                    StatusCode = 1,
                    VersionNumber = 1
                });
            }

            await context.SaveChangesAsync();
            var service = new PublishedBlogService(context);

            var page1 = (await service.GetBlogEntriesAsync("blog-a", pageSize: 2, pageNumber: 1)).ToList();
            var page2 = (await service.GetBlogEntriesAsync("blog-a", pageSize: 2, pageNumber: 2)).ToList();

            Assert.AreEqual(2, page1.Count);
            Assert.AreEqual(2, page2.Count);
            Assert.AreNotEqual(page1[0].Id, page2[0].Id);
        }

        [TestMethod]
        public async Task GetBlogEntryCountAsync_ShouldCountOnlyActivePublishedPosts()
        {
            using var context = GetIsolatedContext();
            context.Pages.Add(new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "blog-a/post-1",
                Title = "Post 1",
                BlogKey = "blog-a",
                ArticleType = (int)ArticleType.BlogPost,
                Published = DateTimeOffset.UtcNow.AddDays(-1),
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 1,
                VersionNumber = 1
            });
            context.Pages.Add(new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                UrlPath = "blog-a/post-expired",
                Title = "Expired",
                BlogKey = "blog-a",
                ArticleType = (int)ArticleType.BlogPost,
                Published = DateTimeOffset.UtcNow.AddDays(-5),
                Expires = DateTimeOffset.UtcNow.AddDays(-1),
                Updated = DateTimeOffset.UtcNow,
                StatusCode = 1,
                VersionNumber = 1
            });
            await context.SaveChangesAsync();

            var service = new PublishedBlogService(context);
            var count = await service.GetBlogEntryCountAsync("blog-a");

            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public async Task GetPreviousBlogEntryAsync_ShouldReturnClosestOlderPost()
        {
            using var context = GetIsolatedContext();
            var baseDate = DateTimeOffset.UtcNow.AddDays(-3);

            context.Pages.Add(new PublishedPage { Id = Guid.NewGuid(), ArticleNumber = 1, UrlPath = "blog/p1", Title = "P1", BlogKey = "blog", ArticleType = (int)ArticleType.BlogPost, Published = baseDate.AddDays(-2), Updated = DateTimeOffset.UtcNow, StatusCode = 1, VersionNumber = 1 });
            context.Pages.Add(new PublishedPage { Id = Guid.NewGuid(), ArticleNumber = 2, UrlPath = "blog/p2", Title = "P2", BlogKey = "blog", ArticleType = (int)ArticleType.BlogPost, Published = baseDate.AddDays(-1), Updated = DateTimeOffset.UtcNow, StatusCode = 1, VersionNumber = 1 });
            context.Pages.Add(new PublishedPage { Id = Guid.NewGuid(), ArticleNumber = 3, UrlPath = "blog/p3", Title = "P3", BlogKey = "blog", ArticleType = (int)ArticleType.BlogPost, Published = baseDate, Updated = DateTimeOffset.UtcNow, StatusCode = 1, VersionNumber = 1 });
            await context.SaveChangesAsync();

            var service = new PublishedBlogService(context);
            var result = await service.GetPreviousBlogEntryAsync("blog", baseDate);

            Assert.IsNotNull(result);
            Assert.AreEqual("blog/p2", result.UrlPath);
        }

        [TestMethod]
        public async Task GetNextBlogEntryAsync_ShouldReturnClosestNewerPost()
        {
            using var context = GetIsolatedContext();
            var baseDate = DateTimeOffset.UtcNow.AddDays(-3);

            context.Pages.Add(new PublishedPage { Id = Guid.NewGuid(), ArticleNumber = 1, UrlPath = "blog/p1", Title = "P1", BlogKey = "blog", ArticleType = (int)ArticleType.BlogPost, Published = baseDate, Updated = DateTimeOffset.UtcNow, StatusCode = 1, VersionNumber = 1 });
            context.Pages.Add(new PublishedPage { Id = Guid.NewGuid(), ArticleNumber = 2, UrlPath = "blog/p2", Title = "P2", BlogKey = "blog", ArticleType = (int)ArticleType.BlogPost, Published = baseDate.AddDays(1), Updated = DateTimeOffset.UtcNow, StatusCode = 1, VersionNumber = 1 });
            context.Pages.Add(new PublishedPage { Id = Guid.NewGuid(), ArticleNumber = 3, UrlPath = "blog/p3", Title = "P3", BlogKey = "blog", ArticleType = (int)ArticleType.BlogPost, Published = baseDate.AddDays(2), Updated = DateTimeOffset.UtcNow, StatusCode = 1, VersionNumber = 1 });
            await context.SaveChangesAsync();

            var service = new PublishedBlogService(context);
            var result = await service.GetNextBlogEntryAsync("blog", baseDate);

            Assert.IsNotNull(result);
            Assert.AreEqual("blog/p2", result.UrlPath);
        }
    }
}
