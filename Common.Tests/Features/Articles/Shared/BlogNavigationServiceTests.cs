// <copyright file="BlogNavigationServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Shared
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="BlogNavigationService"/>.
    /// </summary>
    [TestClass]
    public class BlogNavigationServiceTests : CommonTestsBase
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
            var service = new BlogNavigationService(context);
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            try
            {
                _ = new BlogNavigationService(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task GetAdjacentBlogPostsAsync_WithMiddlePost_ShouldReturnPreviousAndNext()
        {
            using var context = GetIsolatedContext();
            var now = DateTimeOffset.UtcNow;

            context.ArticleCatalog.Add(new CatalogEntry { ArticleNumber = 1, UrlPath = "prev", Title = "Prev", Published = now.AddDays(-3), Updated = now });
            context.ArticleCatalog.Add(new CatalogEntry { ArticleNumber = 2, UrlPath = "current", Title = "Current", Published = now.AddDays(-2), Updated = now });
            context.ArticleCatalog.Add(new CatalogEntry { ArticleNumber = 3, UrlPath = "next", Title = "Next", Published = now.AddDays(-1), Updated = now });
            await context.SaveChangesAsync();

            var service = new BlogNavigationService(context);
            var (previous, next) = await service.GetAdjacentBlogPostsAsync(now.AddDays(-2));

            Assert.IsNotNull(previous);
            Assert.AreEqual("prev", previous.UrlPath);
            Assert.IsNotNull(next);
            Assert.AreEqual("next", next.UrlPath);
        }

        [TestMethod]
        public async Task GetAdjacentBlogPostsAsync_WithFirstPost_ShouldReturnOnlyNext()
        {
            using var context = GetIsolatedContext();
            var now = DateTimeOffset.UtcNow;

            context.ArticleCatalog.Add(new CatalogEntry { ArticleNumber = 1, UrlPath = "first", Title = "First", Published = now.AddDays(-3), Updated = now });
            context.ArticleCatalog.Add(new CatalogEntry { ArticleNumber = 2, UrlPath = "next", Title = "Next", Published = now.AddDays(-2), Updated = now });
            await context.SaveChangesAsync();

            var service = new BlogNavigationService(context);
            var (previous, next) = await service.GetAdjacentBlogPostsAsync(now.AddDays(-3));

            Assert.IsNull(previous);
            Assert.IsNotNull(next);
            Assert.AreEqual("next", next.UrlPath);
        }

        [TestMethod]
        public async Task GetAdjacentBlogPostsAsync_WithLastPost_ShouldReturnOnlyPrevious()
        {
            using var context = GetIsolatedContext();
            var now = DateTimeOffset.UtcNow;

            context.ArticleCatalog.Add(new CatalogEntry { ArticleNumber = 1, UrlPath = "prev", Title = "Prev", Published = now.AddDays(-3), Updated = now });
            context.ArticleCatalog.Add(new CatalogEntry { ArticleNumber = 2, UrlPath = "last", Title = "Last", Published = now.AddDays(-2), Updated = now });
            await context.SaveChangesAsync();

            var service = new BlogNavigationService(context);
            var (previous, next) = await service.GetAdjacentBlogPostsAsync(now.AddDays(-2));

            Assert.IsNotNull(previous);
            Assert.AreEqual("prev", previous.UrlPath);
            Assert.IsNull(next);
        }

        [TestMethod]
        public async Task EnrichBlogNavigationAsync_WithNullModel_ShouldNoOp()
        {
            using var context = GetIsolatedContext();
            var service = new BlogNavigationService(context);
            await service.EnrichBlogNavigationAsync(null);
        }

        [TestMethod]
        public async Task EnrichBlogNavigationAsync_WithNonBlogPost_ShouldNoOp()
        {
            using var context = GetIsolatedContext();
            var service = new BlogNavigationService(context);
            var model = new ArticleViewModel { ArticleType = ArticleType.General, Published = DateTimeOffset.UtcNow };

            await service.EnrichBlogNavigationAsync(model);

            Assert.IsTrue(string.IsNullOrEmpty(model.PreviousUrl));
            Assert.IsTrue(string.IsNullOrEmpty(model.NextUrl));
        }

        [TestMethod]
        public async Task EnrichBlogNavigationAsync_WithBlogPost_ShouldSetUrls()
        {
            using var context = GetIsolatedContext();
            var now = DateTimeOffset.UtcNow;

            context.ArticleCatalog.Add(new CatalogEntry { ArticleNumber = 1, UrlPath = "root", Title = "Root", Published = now.AddDays(-3), Updated = now });
            context.ArticleCatalog.Add(new CatalogEntry { ArticleNumber = 2, UrlPath = "current", Title = "Current", Published = now.AddDays(-2), Updated = now });
            context.ArticleCatalog.Add(new CatalogEntry { ArticleNumber = 3, UrlPath = "next", Title = "Next", Published = now.AddDays(-1), Updated = now });
            await context.SaveChangesAsync();

            var service = new BlogNavigationService(context);
            var model = new ArticleViewModel { ArticleType = ArticleType.BlogPost, Published = now.AddDays(-2) };

            await service.EnrichBlogNavigationAsync(model);

            Assert.AreEqual("/", model.PreviousUrl);
            Assert.AreEqual("/next", model.NextUrl);
        }
    }
}
