// <copyright file="BlogNavigationServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Shared
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for BlogNavigationService - adjacent post queries and enrichment.
    /// </summary>
    [TestClass]
    public class BlogNavigationServiceTests
    {
        private ApplicationDbContext dbContext = null!;
        private BlogNavigationService service = null!;
        private DateTimeOffset now = DateTimeOffset.UtcNow;

        [TestInitialize]
        public void TestInitialize()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"BlogNavDb_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            dbContext = new ApplicationDbContext(options);
            service = new BlogNavigationService(dbContext);
            now = DateTimeOffset.UtcNow;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            dbContext.Dispose();
        }

        #region GetAdjacentBlogPostsAsync Tests

        /// <summary>
        /// Tests retrieving both previous and next blog posts.
        /// </summary>
        [TestMethod]
        public async Task GetAdjacentBlogPostsAsync_WithBothPrevAndNext_ReturnsBoth()
        {
            // Arrange
            var prevPublished = now.AddDays(-5);
            var currentPublished = now.AddDays(-3);
            var nextPublished = now.AddDays(-1);

            await SeedBlogEntries(
                ("previous-post", prevPublished),
                ("current-post", currentPublished),
                ("next-post", nextPublished)
            );

            // Act
            var (prev, next) = await service.GetAdjacentBlogPostsAsync(currentPublished);

            // Assert
            Assert.IsNotNull(prev);
            Assert.AreEqual("previous-post", prev.UrlPath);
            Assert.IsNotNull(next);
            Assert.AreEqual("next-post", next.UrlPath);
        }

        /// <summary>
        /// Tests retrieving only previous post when there is no next post.
        /// </summary>
        [TestMethod]
        public async Task GetAdjacentBlogPostsAsync_OnlyPrevious_ReturnsOnlyPrev()
        {
            // Arrange
            var prevPublished = now.AddDays(-5);
            var currentPublished = now.AddDays(-1);  // Most recent

            await SeedBlogEntries(
                ("first-post", prevPublished),
                ("latest-post", currentPublished)
            );

            // Act
            var (prev, next) = await service.GetAdjacentBlogPostsAsync(currentPublished);

            // Assert
            Assert.IsNotNull(prev);
            Assert.AreEqual("first-post", prev.UrlPath);
            Assert.IsNull(next);
        }

        /// <summary>
        /// Tests retrieving only next post when there is no previous post.
        /// </summary>
        [TestMethod]
        public async Task GetAdjacentBlogPostsAsync_OnlyNext_ReturnsOnlyNext()
        {
            // Arrange
            var currentPublished = now.AddDays(-5);  // Oldest
            var nextPublished = now.AddDays(-1);

            await SeedBlogEntries(
                ("oldest-post", currentPublished),
                ("newer-post", nextPublished)
            );

            // Act
            var (prev, next) = await service.GetAdjacentBlogPostsAsync(currentPublished);

            // Assert
            Assert.IsNull(prev);
            Assert.IsNotNull(next);
            Assert.AreEqual("newer-post", next.UrlPath);
        }

        /// <summary>
        /// Tests when there are no adjacent posts (only post in blog).
        /// </summary>
        [TestMethod]
        public async Task GetAdjacentBlogPostsAsync_NoAdjacentPosts_ReturnsBothNull()
        {
            // Arrange
            var currentPublished = now.AddDays(-3);

            await SeedBlogEntries(
                ("only-post", currentPublished)
            );

            // Act
            var (prev, next) = await service.GetAdjacentBlogPostsAsync(currentPublished);

            // Assert
            Assert.IsNull(prev);
            Assert.IsNull(next);
        }

        /// <summary>
        /// Tests that adjacent posts only include published posts.
        /// </summary>
        [TestMethod]
        public async Task GetAdjacentBlogPostsAsync_SkipsUnpublishedPosts()
        {
            // Arrange
            var prevPublished = now.AddDays(-5);
            var currentPublished = now.AddDays(-3);
            var nextPublished = now.AddDays(-1);

            await dbContext.ArticleCatalog.AddRangeAsync(
                new CatalogEntry
                {
                    ArticleNumber = 1,
                    UrlPath = "unpublished-prev",
                    Title = "Unpublished Previous",
                    Published = null,  // Unpublished
                    Updated = now
                },
                new CatalogEntry
                {
                    ArticleNumber = 2,
                    UrlPath = "current-post",
                    Title = "Current Post",
                    Published = currentPublished,
                    Updated = now
                },
                new CatalogEntry
                {
                    ArticleNumber = 3,
                    UrlPath = "real-next",
                    Title = "Real Next",
                    Published = nextPublished,
                    Updated = now
                }
            );
            await dbContext.SaveChangesAsync();

            // Act
            var (prev, next) = await service.GetAdjacentBlogPostsAsync(currentPublished);

            // Assert
            Assert.IsNull(prev, "Unpublished post should not be returned as previous");
            Assert.IsNotNull(next);
            Assert.AreEqual("real-next", next.UrlPath);
        }

        #endregion

        #region EnrichBlogNavigationAsync Tests

        /// <summary>
        /// Tests enriching a blog post model with navigation links.
        /// </summary>
        [TestMethod]
        public async Task EnrichBlogNavigationAsync_BlogPost_EnrichesWithNavigation()
        {
            // Arrange
            var prevPublished = now.AddDays(-5);
            var currentPublished = now.AddDays(-3);
            var nextPublished = now.AddDays(-1);

            await SeedBlogEntries(
                ("previous-post", prevPublished),
                ("current-post", currentPublished),
                ("next-post", nextPublished)
            );

            var model = new ArticleViewModel
            {
                ArticleType = ArticleType.BlogPost,
                Published = currentPublished,
                Title = "Current Post"
            };

            // Act
            await service.EnrichBlogNavigationAsync(model);

            // Assert
            Assert.AreEqual("/previous-post", model.PreviousUrl);
            Assert.AreEqual("/next-post", model.NextUrl);
        }

        /// <summary>
        /// Tests that non-blog posts are not enriched (no-op).
        /// </summary>
        [TestMethod]
        public async Task EnrichBlogNavigationAsync_GeneralArticle_NoOp()
        {
            // Arrange
            var model = new ArticleViewModel
            {
                ArticleType = ArticleType.General,  // Not a blog post
                Published = now,
                Title = "General Article"
            };

            // Act
            await service.EnrichBlogNavigationAsync(model);

            // Assert
            Assert.AreEqual(string.Empty, model.PreviousUrl);
            Assert.AreEqual(string.Empty, model.NextUrl);
        }

        /// <summary>
        /// Tests that unpublished blog posts are not enriched (no-op).
        /// </summary>
        [TestMethod]
        public async Task EnrichBlogNavigationAsync_UnpublishedBlogPost_NoOp()
        {
            // Arrange
            var model = new ArticleViewModel
            {
                ArticleType = ArticleType.BlogPost,
                Published = null,  // Unpublished
                Title = "Unpublished Post"
            };

            // Act
            await service.EnrichBlogNavigationAsync(model);

            // Assert
            Assert.AreEqual(string.Empty, model.PreviousUrl);
            Assert.AreEqual(string.Empty, model.NextUrl);
        }

        /// <summary>
        /// Tests that null model is handled gracefully.
        /// </summary>
        [TestMethod]
        public async Task EnrichBlogNavigationAsync_NullModel_NoException()
        {
            // Act & Assert - Should not throw
            await service.EnrichBlogNavigationAsync(null);
        }

        /// <summary>
        /// Tests that "root" URL is normalized to "/".
        /// </summary>
        [TestMethod]
        public async Task EnrichBlogNavigationAsync_RootUrl_NormalizedToSlash()
        {
            // Arrange
            var prevPublished = now.AddDays(-5);
            var currentPublished = now.AddDays(-1);

            await dbContext.ArticleCatalog.AddRangeAsync(
                new CatalogEntry
                {
                    ArticleNumber = 1,
                    UrlPath = "root",  // Root URL
                    Title = "Home",
                    Published = prevPublished,
                    Updated = now
                },
                new CatalogEntry
                {
                    ArticleNumber = 2,
                    UrlPath = "second-post",
                    Title = "Second",
                    Published = currentPublished,
                    Updated = now
                }
            );
            await dbContext.SaveChangesAsync();

            var model = new ArticleViewModel
            {
                ArticleType = ArticleType.BlogPost,
                Published = currentPublished,
                Title = "Second"
            };

            // Act
            await service.EnrichBlogNavigationAsync(model);

            // Assert
            Assert.AreEqual("/", model.PreviousUrl, "Root URL should be normalized to '/'");
        }

        /// <summary>
        /// Tests enrichment with only previous post.
        /// </summary>
        [TestMethod]
        public async Task EnrichBlogNavigationAsync_OnlyPrevious_EnrichesOnlyPrev()
        {
            // Arrange
            var prevPublished = now.AddDays(-5);
            var currentPublished = now.AddDays(-1);  // Latest

            await SeedBlogEntries(
                ("previous-post", prevPublished),
                ("latest-post", currentPublished)
            );

            var model = new ArticleViewModel
            {
                ArticleType = ArticleType.BlogPost,
                Published = currentPublished,
                Title = "Latest Post"
            };

            // Act
            await service.EnrichBlogNavigationAsync(model);

            // Assert
            Assert.IsNotNull(model.PreviousUrl);
            Assert.AreEqual(string.Empty, model.NextUrl);
            Assert.AreEqual("/previous-post", model.PreviousUrl);
        }

        /// <summary>
        /// Tests enrichment with only next post.
        /// </summary>
        [TestMethod]
        public async Task EnrichBlogNavigationAsync_OnlyNext_EnrichesOnlyNext()
        {
            // Arrange
            var currentPublished = now.AddDays(-5);  // Oldest
            var nextPublished = now.AddDays(-1);

            await SeedBlogEntries(
                ("first-post", currentPublished),
                ("newer-post", nextPublished)
            );

            var model = new ArticleViewModel
            {
                ArticleType = ArticleType.BlogPost,
                Published = currentPublished,
                Title = "First Post"
            };

            // Act
            await service.EnrichBlogNavigationAsync(model);

            // Assert
            Assert.AreEqual(string.Empty, model.PreviousUrl);
            Assert.IsNotNull(model.NextUrl);
            Assert.AreEqual("/newer-post", model.NextUrl);
        }

        #endregion

        #region Helper Methods

        private async Task SeedBlogEntries(params (string urlPath, DateTimeOffset published)[] entries)
        {
            var items = new System.Collections.Generic.List<CatalogEntry>();
            for (int i = 0; i < entries.Length; i++)
            {
                items.Add(new CatalogEntry
                {
                    ArticleNumber = i + 1,
                    UrlPath = entries[i].urlPath,
                    Title = entries[i].urlPath.Replace("-", " "),
                    Published = entries[i].published,
                    Updated = entries[i].published
                });
            }

            await dbContext.ArticleCatalog.AddRangeAsync(items);
            await dbContext.SaveChangesAsync();
        }

        #endregion
    }
}
