// <copyright file="BlogStreamRenderingServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Services.BlogPublishing
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Services.BlogPublishing;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="BlogStreamRenderingService"/>.
    /// </summary>
    [TestClass]
    public class BlogStreamRenderingServiceTests : CommonTestsBase
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
            var service = new BlogStreamRenderingService(context);
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            try
            {
                _ = new BlogStreamRenderingService(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("db", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task GenerateBlogStreamWrapperAsync_WithNullArticle_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var service = new BlogStreamRenderingService(context);

            try
            {
                await service.GenerateBlogStreamWrapperAsync(null!, "blog");
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        [TestMethod]
        public async Task GenerateBlogStreamWrapperAsync_WithEmptyBlogKey_ShouldThrowArgumentException()
        {
            using var context = GetIsolatedContext();
            var service = new BlogStreamRenderingService(context);
            var article = TestDataBuilder.CreateArticle();

            try
            {
                await service.GenerateBlogStreamWrapperAsync(article, string.Empty);
                Assert.Fail("Expected ArgumentException was not thrown");
            }
            catch (ArgumentException)
            {
                // Expected
            }
        }

        [TestMethod]
        public async Task GenerateBlogStreamWrapperAsync_WithValidInput_ShouldReturnHtmlWrapper()
        {
            using var context = GetIsolatedContext();
            var service = new BlogStreamRenderingService(context);

            var article = TestDataBuilder.CreateArticle();
            article.Title = "My Blog";
            article.Introduction = "Welcome";
            article.BannerImage = "/img/banner.jpg";

            var result = await service.GenerateBlogStreamWrapperAsync(article, "blog-a");

            Assert.IsTrue(result.Contains("<!DOCTYPE html>"));
            Assert.IsTrue(result.Contains("blog-posts-meta"));
            Assert.IsTrue(result.Contains("/js/blog-stream-loader.js"));
            Assert.IsTrue(result.Contains("My Blog"));
            Assert.IsTrue(result.Contains("Welcome"));
        }

        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_WithInvalidBlogKey_ShouldThrowArgumentException()
        {
            using var context = GetIsolatedContext();
            var service = new BlogStreamRenderingService(context);

            try
            {
                await service.GenerateBlogPostMetadataJsonAsync(" ");
                Assert.Fail("Expected ArgumentException was not thrown");
            }
            catch (ArgumentException)
            {
                // Expected
            }
        }

        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_WithZeroMaxPosts_ShouldThrowArgumentOutOfRangeException()
        {
            using var context = GetIsolatedContext();
            var service = new BlogStreamRenderingService(context);

            try
            {
                await service.GenerateBlogPostMetadataJsonAsync("blog-a", 0);
                Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Assert.AreEqual("maxPosts", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_ShouldRespectMaxPostsLimit()
        {
            using var context = GetIsolatedContext();
            var now = DateTimeOffset.UtcNow;

            for (int i = 1; i <= 5; i++)
            {
                context.Pages.Add(new PublishedPage
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = 100 + i,
                    UrlPath = $"blog-limit/p{i}",
                    Title = $"Post {i}",
                    BlogKey = "blog-limit",
                    ArticleType = (int)ArticleType.BlogPost,
                    Published = now.AddDays(-i),
                    Updated = now,
                    StatusCode = 1,
                    VersionNumber = 1
                });
            }

            await context.SaveChangesAsync();

            var service = new BlogStreamRenderingService(context);
            var result = await service.GenerateBlogPostMetadataJsonAsync("blog-limit", 3);

            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<dynamic>>(result);
            Assert.IsNotNull(parsed);
            Assert.AreEqual(3, parsed.Count);
        }

        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_WithNoPosts_ShouldReturnEmptyArray()
        {
            using var context = GetIsolatedContext();
            var service = new BlogStreamRenderingService(context);

            var result = await service.GenerateBlogPostMetadataJsonAsync("blog-empty");

            Assert.AreEqual("[]", result);
        }

        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_ShouldIncludeOnlyPublishedActiveBlogPosts()
        {
            using var context = GetIsolatedContext();
            var now = DateTimeOffset.UtcNow;

            context.Pages.Add(new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "blog-a/p1",
                Title = "Included",
                BlogKey = "blog-a",
                ArticleType = (int)ArticleType.BlogPost,
                Published = now.AddDays(-1),
                Updated = now,
                StatusCode = 1,
                VersionNumber = 1
            });

            context.Pages.Add(new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                UrlPath = "blog-a/future",
                Title = "Future",
                BlogKey = "blog-a",
                ArticleType = (int)ArticleType.BlogPost,
                Published = now.AddDays(1),
                Updated = now,
                StatusCode = 1,
                VersionNumber = 1
            });

            context.Pages.Add(new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 3,
                UrlPath = "blog-a/stream",
                Title = "Stream",
                BlogKey = "blog-a",
                ArticleType = (int)ArticleType.BlogStream,
                Published = now.AddDays(-1),
                Updated = now,
                StatusCode = 1,
                VersionNumber = 1
            });

            await context.SaveChangesAsync();

            var service = new BlogStreamRenderingService(context);
            var result = await service.GenerateBlogPostMetadataJsonAsync("blog-a");

            Assert.IsTrue(result.Contains("Included"));
            Assert.IsFalse(result.Contains("Future"));
            Assert.IsFalse(result.Contains("\"stream\""));
        }

        [TestMethod]
        public async Task GenerateBlogPostSnippetAsync_WithNullArticle_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var service = new BlogStreamRenderingService(context);

            try
            {
                await service.GenerateBlogPostSnippetAsync(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        [TestMethod]
        public async Task GenerateBlogPostSnippetAsync_WithBanner_ShouldIncludeImage()
        {
            using var context = GetIsolatedContext();
            var service = new BlogStreamRenderingService(context);
            var article = TestDataBuilder.CreateArticle();
            article.Title = "Post";
            article.Content = "<p>body</p>";
            article.BannerImage = "https://example.com/img.jpg";
            article.Updated = DateTimeOffset.UtcNow;

            var result = await service.GenerateBlogPostSnippetAsync(article);

            Assert.IsTrue(result.Contains("sky-blog-stream-figure"));
            Assert.IsTrue(result.Contains("https://example.com/img.jpg"));
        }

        [TestMethod]
        public async Task GenerateBlogPostSnippetAsync_ShouldHtmlEncodeTitleAndBanner()
        {
            using var context = GetIsolatedContext();
            var service = new BlogStreamRenderingService(context);
            var article = TestDataBuilder.CreateArticle();
            article.Title = "<script>alert('x')</script>";
            article.BannerImage = "https://x.test/img?<tag>";
            article.Content = "<p>safe body html</p>";
            article.Updated = DateTimeOffset.UtcNow;

            var result = await service.GenerateBlogPostSnippetAsync(article);

            Assert.IsTrue(result.Contains("&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;"));
            Assert.IsTrue(result.Contains("img?&lt;tag&gt;"));
            Assert.IsTrue(result.Contains("<p>safe body html</p>"));
        }
    }
}
