// <copyright file="PerformanceAndConcurrencyTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Performance
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Data.Logic;



    /// <summary>
    /// Performance and concurrency tests.
    /// Tests system behavior under load, concurrent operations, and with large datasets.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PerformanceAndConcurrencyTests : SkyCmsTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            InitializeTestContext(seedLayout: true);
        }

        #region Large Dataset Tests

        /// <summary>
        /// Tests creating and querying large number of articles.
        /// </summary>
        [TestMethod]
        public async Task CreateManyArticles_PerformsEfficiently()
        {
            // Arrange
            const int articleCount = 100;
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 1; i <= articleCount; i++)
            {
                await Logic.CreateArticle($"Article {i}", TestUserId);
            }
            stopwatch.Stop();

            // Assert
            var totalArticles = await Db.Articles.CountAsync();
            Assert.IsTrue(totalArticles >= articleCount, $"Should have at least {articleCount} articles");

            // Performance assertion (should complete in reasonable time)
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 30000, 
                $"Creating {articleCount} articles took {stopwatch.ElapsedMilliseconds}ms (should be < 30s)");
        }

        /// <summary>
        /// Tests pagination performance with large dataset.
        /// </summary>
        [TestMethod]
        public async Task Pagination_WithLargeDataset_PerformsEfficiently()
        {
            // Arrange - Create 50 articles
            for (int i = 1; i <= 50; i++)
            {
                var article = await Logic.CreateArticle($"Article {i}", TestUserId);
                await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            var page1 = await Db.Pages
                .OrderByDescending(p => p.Published)
                .Take(10)
                .ToListAsync();

            var page2 = await Db.Pages
                .OrderByDescending(p => p.Published)
                .Skip(10)
                .Take(10)
                .ToListAsync();
            stopwatch.Stop();

            // Assert
            Assert.AreEqual(10, page1.Count);
            Assert.AreEqual(10, page2.Count);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, 
                $"Pagination queries took {stopwatch.ElapsedMilliseconds}ms (should be < 1s)");
        }

        /// <summary>
        /// Tests that catalog queries perform well with many entries.
        /// </summary>
        [TestMethod]
        public async Task CatalogQuery_WithManyEntries_PerformsEfficiently()
        {
            // Arrange - Create 30 articles
            for (int i = 1; i <= 30; i++)
            {
                await Logic.CreateArticle($"Catalog Test {i}", TestUserId);
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            var catalog = await Db.ArticleCatalog.ToListAsync();
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(catalog.Count >= 30);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 500, 
                $"Catalog query took {stopwatch.ElapsedMilliseconds}ms (should be < 500ms)");
        }

        #endregion

        #region Concurrent Operation Tests

        /// <summary>
        /// Tests that concurrent article creation works correctly.
        /// NOTE: DbContext is not thread-safe, so we use a semaphore to serialize database access
        /// while still testing the article numbering logic works correctly under rapid succession.
        /// In production, each HTTP request would have its own scoped DbContext.
        /// </summary>
        [TestMethod]
        public async Task ConcurrentArticleCreation_AllSucceed()
        {
            // Arrange
            const int concurrentCount = 10;
            var tasks = new List<Task<ArticleViewModel>>();
            var semaphore = new System.Threading.SemaphoreSlim(1, 1); // Serialize DbContext access

            // Act - Create articles concurrently (but serialize DbContext access)
            for (int i = 1; i <= concurrentCount; i++)
            {
                var articleTitle = $"Concurrent Article {i}";
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        return await Logic.CreateArticle(articleTitle, TestUserId);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            var articles = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(concurrentCount, articles.Length);
            Assert.IsTrue(articles.All(a => a != null), "All articles should be created");

            // Verify all have unique article numbers
            var articleNumbers = articles.Select(a => a.ArticleNumber).ToList();
            Assert.AreEqual(concurrentCount, articleNumbers.Distinct().Count(), 
                "All articles should have unique article numbers");
        }

        /// <summary>
        /// Tests concurrent publishing of different articles.
        /// NOTE: DbContext is not thread-safe, so we use a semaphore to serialize database access.
        /// In production, each HTTP request would have its own scoped DbContext.
        /// </summary>
        [TestMethod]
        public async Task ConcurrentPublishing_DifferentArticles_AllSucceed()
        {
            // Arrange - Create multiple articles
            var articles = new List<ArticleViewModel>();
            for (int i = 1; i <= 5; i++)
            {
                articles.Add(await Logic.CreateArticle($"Publish Test {i}", TestUserId));
            }

            var semaphore = new System.Threading.SemaphoreSlim(1, 1); // Serialize DbContext access

            // Act - Publish concurrently
            var publishTasks = articles.Select(a => 
                Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        return await Logic.PublishArticle(a.Id, DateTimeOffset.UtcNow);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                })
            ).ToList();

            await Task.WhenAll(publishTasks);

            // Assert - Verify all published
            var publishedCount = await Db.Articles
                .Where(a => articles.Select(x => x.Id).Contains(a.Id) && a.Published != null)
                .CountAsync();

            Assert.AreEqual(articles.Count, publishedCount, "All articles should be published");

            // Verify pages created
            var pageCount = await Db.Pages
                .Where(p => articles.Select(x => x.ArticleNumber).Contains(p.ArticleNumber))
                .CountAsync();

            Assert.AreEqual(articles.Count, pageCount, "All articles should have pages");
        }

        /// <summary>
        /// Tests concurrent updates to same article (last write wins).
        /// NOTE: DbContext is not thread-safe, so we use a semaphore to serialize database access.
        /// In production, each HTTP request would have its own scoped DbContext.
        /// </summary>
        [TestMethod]
        public async Task ConcurrentUpdates_SameArticle_LastWriteWins()
        {
            // Arrange
            var article = await Logic.CreateArticle("Concurrent Update Test", TestUserId);
            const int updateCount = 5;
            var semaphore = new System.Threading.SemaphoreSlim(1, 1); // Serialize DbContext access

            // Act - Update concurrently
            var updateTasks = new List<Task>();
            for (int i = 1; i <= updateCount; i++)
            {
                var updateNumber = i;
                updateTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var localArticle = await Logic.GetArticleById(article.Id, TestUserId);
                        localArticle.Content = $"<p>Update {updateNumber}</p>";
                        await Logic.SaveArticle(localArticle, TestUserId);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(updateTasks);

            // Assert - Article should be updated (one of the updates won)
            var latest = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            Assert.IsNotNull(latest.Content);
            Assert.IsTrue(latest.Content.Contains("Update"), "Content should contain one of the updates");
        }

        #endregion

        #region Version Management Performance Tests

        /// <summary>
        /// Tests creating many versions of same article.
        /// </summary>
        [TestMethod]
        public async Task CreateManyVersions_PerformsEfficiently()
        {
            // Arrange
            var article = await Logic.CreateArticle("Version Test", TestUserId);
            const int versionCount = 20;

            // Act
            var stopwatch = Stopwatch.StartNew();
            var currentArticle = await Db.Articles.FindAsync(article.Id);
            
            for (int i = 2; i <= versionCount; i++)
            {
                currentArticle = await Logic.NewVersion(currentArticle);
                currentArticle.Content = $"<p>Version {i}</p>";
                await Db.SaveChangesAsync();
            }
            stopwatch.Stop();

            // Assert
            var versions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .ToListAsync();

            Assert.AreEqual(versionCount, versions.Count);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, 
                $"Creating {versionCount} versions took {stopwatch.ElapsedMilliseconds}ms (should be < 5s)");
        }

        /// <summary>
        /// Tests querying article with many versions.
        /// </summary>
        [TestMethod]
        public async Task QueryArticle_WithManyVersions_PerformsEfficiently()
        {
            // Arrange - Create article with 15 versions
            var article = await Logic.CreateArticle("Multi-Version Test", TestUserId);
            var currentArticle = await Db.Articles.FindAsync(article.Id);
            
            for (int i = 2; i <= 15; i++)
            {
                currentArticle = await Logic.NewVersion(currentArticle);
            }

            // Act - Query latest version
            var stopwatch = Stopwatch.StartNew();
            var latest = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();
            stopwatch.Stop();

            // Assert
            Assert.AreEqual(15, latest.VersionNumber);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 100, 
                $"Query took {stopwatch.ElapsedMilliseconds}ms (should be < 100ms)");
        }

        #endregion

        #region Blog Performance Tests

        /// <summary>
        /// Tests querying blog posts with filtering and pagination.
        /// </summary>
        [TestMethod]
        public async Task BlogPostQuery_WithFilteringAndPagination_PerformsEfficiently()
        {
            // Arrange - Create home page
            await Logic.CreateArticle("Home", TestUserId);

            // Create 30 blog posts across categories
            var categories = new[] { "Technology", "Science", "Sports" };
            for (int i = 1; i <= 30; i++)
            {
                var post = await Logic.CreateArticle($"Blog Post {i}", TestUserId, null, "default", ArticleType.BlogPost);
                post.Category = categories[i % 3];
                await Logic.SaveArticle(post, TestUserId);
                await Logic.PublishArticle(post.Id, DateTimeOffset.UtcNow);
            }

            // Act - Query with filtering and pagination
            var stopwatch = Stopwatch.StartNew();
            var techPosts = await Db.Pages
                .Where(p => p.ArticleType == (int)ArticleType.BlogPost && p.Category == "Technology")
                .OrderByDescending(p => p.Published)
                .Take(10)
                .ToListAsync();
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(techPosts.Count >= 5, "Should have multiple technology posts");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 500, 
                $"Filtered query took {stopwatch.ElapsedMilliseconds}ms (should be < 500ms)");
        }

        #endregion

        #region Database Connection Tests

        /// <summary>
        /// Tests that multiple database operations can be performed rapidly in sequence.
        /// NOTE: DbContext is not thread-safe. This test validates sequential throughput,
        /// not true multi-threaded concurrency. For production, each request would have
        /// its own scoped DbContext instance via dependency injection.
        /// </summary>
        [TestMethod]
        public async Task ConcurrentDatabaseOperations_AllSucceed()
        {
            // Arrange - Create some test data
            await Logic.CreateArticle("Article 1", TestUserId);
            await Logic.CreateArticle("Article 2", TestUserId);

            // Act - Perform various operations in rapid sequence (not parallel)
            // This tests the system's ability to handle multiple operations quickly
            var article3 = await Logic.CreateArticle("Article 3", TestUserId);
            var article4 = await Logic.CreateArticle("Article 4", TestUserId);
            var layouts = await Db.Layouts.ToListAsync();
            var templates = await Db.Templates.ToListAsync();
            var catalogCount = await Db.ArticleCatalog.CountAsync();

            // Assert - All operations should complete successfully
            Assert.IsNotNull(article3, "Article 3 should be created");
            Assert.IsNotNull(article4, "Article 4 should be created");
            Assert.IsTrue(layouts.Count > 0, "Layouts should be retrieved");
            Assert.IsTrue(templates.Count >= 0, "Templates query should succeed");
            Assert.IsTrue(catalogCount >= 4, "Catalog should contain all articles");
        }

        #endregion

        #region Catalog Performance Tests

        /// <summary>
        /// Tests catalog synchronization performance.
        /// </summary>
        [TestMethod]
        public async Task CatalogSynchronization_WithManyArticles_PerformsEfficiently()
        {
            // Arrange - Create 20 articles
            var stopwatch = Stopwatch.StartNew();
            for (int i = 1; i <= 20; i++)
            {
                var article = await Logic.CreateArticle($"Catalog Sync {i}", TestUserId);
                await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            }
            stopwatch.Stop();

            // Assert - Catalog should be synchronized
            var catalogCount = await Db.ArticleCatalog.CountAsync();
            Assert.IsTrue(catalogCount >= 20);

            // Performance assertion - publishing includes Azure operations, DB writes, and catalog updates
            // Threshold accounts for: article creation, publishing service operations, catalog upserts
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 20000, 
                $"Catalog sync for 20 articles took {stopwatch.ElapsedMilliseconds}ms (should be < 20s)");
        }

        #endregion

        #region Memory and Resource Tests

        /// <summary>
        /// Tests that creating many articles doesn't cause memory issues.
        /// </summary>
        [TestMethod]
        public async Task CreateManyArticles_DoesNotExhaustMemory()
        {
            // Arrange
            const int articleCount = 50;
            var startMemory = GC.GetTotalMemory(true);

            // Act
            for (int i = 1; i <= articleCount; i++)
            {
                await Logic.CreateArticle($"Memory Test {i}", TestUserId);
                
                // Periodically force garbage collection to prevent accumulation
                if (i % 10 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            var endMemory = GC.GetTotalMemory(true);
            var memoryIncrease = (endMemory - startMemory) / 1024 / 1024; // MB

            // Assert
            Assert.IsTrue(memoryIncrease < 100, 
                $"Memory increased by {memoryIncrease}MB (should be < 100MB for {articleCount} articles)");
        }

        #endregion

        #region Query Optimization Tests

        /// <summary>
        /// Tests that published page queries use efficient indexes.
        /// </summary>
        [TestMethod]
        public async Task PublishedPageQuery_UsesEfficientIndexing()
        {
            // Arrange - Create and publish 25 articles
            for (int i = 1; i <= 25; i++)
            {
                var article = await Logic.CreateArticle($"Index Test {i}", TestUserId);
                await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            }

            // Act - Query with commonly used filters
            var stopwatch = Stopwatch.StartNew();
            var recentPages = await Db.Pages
                .Where(p => p.Published != null)
                .OrderByDescending(p => p.Published)
                .Take(10)
                .ToListAsync();
            stopwatch.Stop();

            // Assert
            Assert.AreEqual(10, recentPages.Count);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 200, 
                $"Query took {stopwatch.ElapsedMilliseconds}ms (should be < 200ms with proper indexing)");
        }

        #endregion
    }
}