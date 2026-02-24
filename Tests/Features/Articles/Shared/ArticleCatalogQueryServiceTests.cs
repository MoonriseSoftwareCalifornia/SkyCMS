// <copyright file="ArticleCatalogQueryServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for ArticleCatalogQueryService - table of contents and search.
    /// </summary>
    [TestClass]
    public class ArticleCatalogQueryServiceTests
    {
        private ApplicationDbContext dbContext = null!;
        private ArticleCatalogQueryService service = null!;
        private DateTimeOffset now = DateTimeOffset.UtcNow;

        [TestInitialize]
        public void TestInitialize()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"CatalogDb_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            dbContext = new ApplicationDbContext(options);
            service = new ArticleCatalogQueryService(dbContext, "https://publisher.test", "https://cdn.test");
            now = DateTimeOffset.UtcNow;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            dbContext.Dispose();
        }

        #region Table of Contents - Root Level

        /// <summary>
        /// Tests that root-level table of contents returns items without prefix.
        /// </summary>
        [TestMethod]
        public async Task GetTableOfContentsAsync_RootLevel_ReturnsRootItems()
        {
            // Arrange
            var entries = new[]
            {
                ("root", "Home", now.AddMinutes(-10)),
                ("about", "About Us", now.AddMinutes(-5)),
                ("contact", "Contact", now.AddMinutes(-3))
            };

            var items = entries.Select((e, i) => new CatalogEntry
            {
                ArticleNumber = i + 1,
                UrlPath = e.Item1,
                Title = e.Item2,
                Published = e.Item3,
                Updated = e.Item3
            }).ToList();

            await dbContext.ArticleCatalog.AddRangeAsync(items);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetTableOfContentsAsync(string.Empty, pageNo: 0, pageSize: 10);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Items.Count);
            Assert.IsTrue(result.Items.Any(i => i.UrlPath == "root"));
            Assert.IsTrue(result.Items.Any(i => i.UrlPath == "about"));
        }

        /// <summary>
        /// Tests that unpublished entries are excluded from TOC.
        /// </summary>
        [TestMethod]
        public async Task GetTableOfContentsAsync_ExcludesUnpublished()
        {
            // Arrange
            var entries = new[]
            {
                ("root", "Home", now.AddMinutes(-10)),
                ("about", "About", (DateTimeOffset?)null)  // Unpublished
            };

            var items = entries.Select((e, i) => new CatalogEntry
            {
                ArticleNumber = i + 1,
                UrlPath = e.Item1,
                Title = e.Item2,
                Published = e.Item3,
                Updated = e.Item3 ?? now
            }).ToList();

            await dbContext.ArticleCatalog.AddRangeAsync(items);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetTableOfContentsAsync(string.Empty);

            // Assert
            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("root", result.Items[0].UrlPath);
        }

        /// <summary>
        /// Tests that future-dated entries are excluded.
        /// </summary>
        [TestMethod]
        public async Task GetTableOfContentsAsync_ExcludesFutureEntries()
        {
            // Arrange
            var entries = new[]
            {
                ("root", "Home", now.AddMinutes(-10)),
                ("future", "Future Post", now.AddHours(1))  // Future
            };

            var items = entries.Select((e, i) => new CatalogEntry
            {
                ArticleNumber = i + 1,
                UrlPath = e.Item1,
                Title = e.Item2,
                Published = e.Item3,
                Updated = e.Item3
            }).ToList();

            await dbContext.ArticleCatalog.AddRangeAsync(items);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetTableOfContentsAsync(string.Empty);

            // Assert
            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("root", result.Items[0].UrlPath);
        }

        #endregion

        #region Pagination

        /// <summary>
        /// Tests pagination for large datasets.
        /// </summary>
        [TestMethod]
        public async Task GetTableOfContentsAsync_Pagination_CorrectlyPagesResults()
        {
            // Arrange
            var entries = Enumerable.Range(1, 25)
                .Select(i => ($"page-{i:D2}", $"Page {i}", now.AddMinutes(-i)))
                .ToArray();
            await SeedCatalogEntries(entries);

            // Act
            var page1 = await service.GetTableOfContentsAsync(string.Empty, pageNo: 0, pageSize: 10);
            var page2 = await service.GetTableOfContentsAsync(string.Empty, pageNo: 1, pageSize: 10);
            var page3 = await service.GetTableOfContentsAsync(string.Empty, pageNo: 2, pageSize: 10);

            // Assert
            Assert.AreEqual(10, page1.Items.Count);
            Assert.AreEqual(10, page2.Items.Count);
            Assert.AreEqual(5, page3.Items.Count);
            Assert.AreEqual(0, page1.PageNo);
            Assert.AreEqual(1, page2.PageNo);
            Assert.AreEqual(2, page3.PageNo);
        }

        #endregion

        #region Sorting

        /// <summary>
        /// Tests sorting by published date (newest first).
        /// </summary>
        [TestMethod]
        public async Task GetTableOfContentsAsync_OrderByPublishedDate_SortsByNewest()
        {
            // Arrange
            await SeedCatalogEntries(
                ("article-1", "Article 1", now.AddDays(-5)),
                ("article-2", "Article 2", now.AddDays(-3)),
                ("article-3", "Article 3", now.AddDays(-1))
            );

            // Act
            var result = await service.GetTableOfContentsAsync(string.Empty, orderByPublishedDate: true);

            // Assert
            Assert.AreEqual(3, result.Items.Count);
            Assert.AreEqual("article-3", result.Items[0].UrlPath);
            Assert.AreEqual("article-2", result.Items[1].UrlPath);
            Assert.AreEqual("article-1", result.Items[2].UrlPath);
        }

        /// <summary>
        /// Tests sorting by title (alphabetical).
        /// </summary>
        [TestMethod]
        public async Task GetTableOfContentsAsync_OrderByTitle_SortsByAlphabetical()
        {
            // Arrange
            await SeedCatalogEntries(
                ("z-item", "Zebra", now.AddMinutes(-5)),
                ("a-item", "Apple", now.AddMinutes(-5)),
                ("m-item", "Mango", now.AddMinutes(-5))
            );

            // Act
            var result = await service.GetTableOfContentsAsync(string.Empty, orderByPublishedDate: false);

            // Assert
            Assert.AreEqual(3, result.Items.Count);
            Assert.AreEqual("a-item", result.Items[0].UrlPath);
            Assert.AreEqual("m-item", result.Items[1].UrlPath);
            Assert.AreEqual("z-item", result.Items[2].UrlPath);
        }

        #endregion

        #region Search Functionality

        /// <summary>
        /// Tests single-term search in titles and introductions.
        /// </summary>
        [TestMethod]
        public async Task SearchAsync_SingleTerm_FindsMatches()
        {
            // Arrange - Create entries with specific Introduction text
            var entries = new[]
            {
                ("article-1", "Best Practices", now.AddMinutes(-5), "This article covers best practices"),
                ("article-2", "Advanced Topics", now.AddMinutes(-3), "This covers complex topics"),
                ("article-3", "Basics", now.AddMinutes(-1), "Advanced content here")
            };

            var items = entries.Select((e, i) => new CatalogEntry
            {
                ArticleNumber = i + 1,
                UrlPath = e.Item1,
                Title = e.Item2,
                Published = e.Item3,
                Updated = e.Item3,
                Introduction = e.Item4
            }).ToList();

            await dbContext.ArticleCatalog.AddRangeAsync(items);
            await dbContext.SaveChangesAsync();

            // Act
            var results = await service.SearchAsync("advanced");

            // Assert
            Assert.AreEqual(2, results.Count);  // Matches "Advanced Topics" (title) and "Advanced content" (introduction)
            Assert.IsTrue(results.Any(r => r.Title.Contains("Advanced")));  // article-2
            Assert.IsTrue(results.Any(r => r.UrlPath.Contains("article-3")));  // article-3
        }

        /// <summary>
        /// Tests multi-term search with AND logic.
        /// </summary>
        [TestMethod]
        public async Task SearchAsync_MultipleTerms_AndCombined()
        {
            // Arrange - Create entries with specific Introduction text for searching
            var entries = new[]
            {
                ("article-1", "Best C# Practices", now.AddMinutes(-5), "This covers C# best practices"),
                ("article-2", "Java Practices", now.AddMinutes(-3), "Java best practices here"),
                ("article-3", "Best Practices Overview", now.AddMinutes(-1), "Best practices in general")
            };

            var items = entries.Select((e, i) => new CatalogEntry
            {
                ArticleNumber = i + 1,
                UrlPath = e.Item1,
                Title = e.Item2,
                Published = e.Item3,
                Updated = e.Item3,
                Introduction = e.Item4
            }).ToList();

            await dbContext.ArticleCatalog.AddRangeAsync(items);
            await dbContext.SaveChangesAsync();

            // Act
            var results = await service.SearchAsync("best c#");

            // Assert
            Assert.AreEqual(1, results.Count);  // Only matches "Best C# Practices"
            Assert.AreEqual("article-1", results[0].UrlPath);
        }

        /// <summary>
        /// Tests search with no results.
        /// </summary>
        [TestMethod]
        public async Task SearchAsync_NoMatches_ReturnsEmpty()
        {
            // Arrange
            var items = new CatalogEntry
            {
                ArticleNumber = 1,
                UrlPath = "article-1",
                Title = "Article About Cats",
                Published = now.AddMinutes(-5),
                Updated = now.AddMinutes(-5),
                Introduction = "Meow"
            };
            await dbContext.ArticleCatalog.AddAsync(items);
            await dbContext.SaveChangesAsync();

            // Act
            var results = await service.SearchAsync("dogs");

            // Assert
            Assert.AreEqual(0, results.Count);
        }

        /// <summary>
        /// Tests search with empty string.
        /// </summary>
        [TestMethod]
        public async Task SearchAsync_EmptyString_ReturnsEmpty()
        {
            // Arrange
            var items = new CatalogEntry
            {
                ArticleNumber = 1,
                UrlPath = "article-1",
                Title = "Test Article",
                Published = now.AddMinutes(-5),
                Updated = now.AddMinutes(-5),
                Introduction = "Content"
            };
            await dbContext.ArticleCatalog.AddAsync(items);
            await dbContext.SaveChangesAsync();

            // Act
            var results = await service.SearchAsync(string.Empty);

            // Assert
            Assert.AreEqual(0, results.Count);
        }

        /// <summary>
        /// Tests that unpublished items are excluded from search.
        /// </summary>
        [TestMethod]
        public async Task SearchAsync_ExcludesUnpublished()
        {
            // Arrange
            var items = new[]
            {
                new CatalogEntry
                {
                    ArticleNumber = 1,
                    UrlPath = "article-1",
                    Title = "Published Article",
                    Published = now.AddMinutes(-5),
                    Updated = now.AddMinutes(-5),
                    Introduction = "searchable content"
                },
                new CatalogEntry
                {
                    ArticleNumber = 2,
                    UrlPath = "article-2",
                    Title = "Unpublished Article",
                    Published = null,  // Unpublished
                    Updated = now,
                    Introduction = "searchable content"
                }
            };
            await dbContext.ArticleCatalog.AddRangeAsync(items);
            await dbContext.SaveChangesAsync();

            // Act
            var results = await service.SearchAsync("searchable");

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("article-1", results[0].UrlPath);
        }

        #endregion

        #region Helper Methods

        private async Task SeedCatalogEntries(params (string urlPath, string title, DateTimeOffset published)[] entries)
        {
            var items = entries.Select((e, i) => new CatalogEntry
            {
                ArticleNumber = i + 1,
                UrlPath = e.urlPath,
                Title = e.title,
                Published = e.published,
                Updated = e.published
            }).ToList();

            await dbContext.ArticleCatalog.AddRangeAsync(items);
            await dbContext.SaveChangesAsync();
        }

        #endregion
    }
}
