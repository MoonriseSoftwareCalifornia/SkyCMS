// <copyright file="PublicFileEntryTitleResolverTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Editor.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Services;

    /// <summary>
    /// Unit tests for <see cref="PublicFileEntryTitleResolver"/>, covering
    /// title resolution and deleted-article filtering behaviour.
    /// </summary>
    [TestClass]
    public class PublicFileEntryTitleResolverTests
    {
        // ───── helpers ──────────────────────────────────────────────────────────

        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"TitleResolver_{name}_{System.Guid.NewGuid()}")
                .Options;
            return new ApplicationDbContext(options);
        }

        private static IMemoryCache NewCache() =>
            new MemoryCache(new MemoryCacheOptions());

        private static FileManagerEntry ArticleEntry(int articleNumber, bool isDirectory = true) =>
            new FileManagerEntry
            {
                Path = $"/pub/articles/{articleNumber}",
                Name = articleNumber.ToString(),
                IsDirectory = isDirectory,
            };

        private static FileManagerEntry FileEntry(int articleNumber, string fileName) =>
            new FileManagerEntry
            {
                Path = $"/pub/articles/{articleNumber}/{fileName}",
                Name = fileName,
                IsDirectory = false,
            };

        // ───── ExtractArticleNumbersFromEntries ──────────────────────────────────

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ExtractArticleNumbers_NullEntries_ReturnsEmpty()
        {
            var result = PublicFileEntryHelper.ExtractArticleNumbersFromEntries(null);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ExtractArticleNumbers_EmptyList_ReturnsEmpty()
        {
            var result = PublicFileEntryHelper.ExtractArticleNumbersFromEntries(new List<FileManagerEntry>());
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ExtractArticleNumbers_FolderAtRoot_ReturnsNumber()
        {
            var entries = new[] { ArticleEntry(42) };
            var result = PublicFileEntryHelper.ExtractArticleNumbersFromEntries(entries);
            CollectionAssert.AreEqual(new[] { 42 }, result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ExtractArticleNumbers_FileWithinArticleFolder_ReturnsNumber()
        {
            var entries = new[] { FileEntry(99, "logo.png") };
            var result = PublicFileEntryHelper.ExtractArticleNumbersFromEntries(entries);
            CollectionAssert.AreEqual(new[] { 99 }, result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ExtractArticleNumbers_MultipleFilesUnderSameArticle_DeduplicatesAndSorts()
        {
            var entries = new[]
            {
                FileEntry(200, "a.css"),
                FileEntry(200, "b.css"),
                ArticleEntry(100),
            };
            var result = PublicFileEntryHelper.ExtractArticleNumbersFromEntries(entries);
            CollectionAssert.AreEqual(new[] { 100, 200 }, result);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryHelper")]
        public void ExtractArticleNumbers_NonArticlePaths_AreIgnored()
        {
            var entries = new[]
            {
                new FileManagerEntry { Path = "/pub/templates/some-id", Name = "t", IsDirectory = true },
                new FileManagerEntry { Path = "/pub/articles", Name = "articles", IsDirectory = true },
                ArticleEntry(7),
            };
            var result = PublicFileEntryHelper.ExtractArticleNumbersFromEntries(entries);
            CollectionAssert.AreEqual(new[] { 7 }, result);
        }

        // ───── FilterDeletedArticleEntriesAsync ──────────────────────────────────

        [TestMethod]
        [TestCategory("PublicFileEntryTitleResolver")]
        public async Task FilterDeleted_NullList_DoesNotThrow()
        {
            using var db = CreateDb(nameof(FilterDeleted_NullList_DoesNotThrow));
            var resolver = new PublicFileEntryTitleResolver(db);
            await resolver.FilterDeletedArticleEntriesAsync(null!, NewCache());
        }

        [TestMethod]
        [TestCategory("PublicFileEntryTitleResolver")]
        public async Task FilterDeleted_EmptyList_DoesNotThrow()
        {
            using var db = CreateDb(nameof(FilterDeleted_EmptyList_DoesNotThrow));
            var resolver = new PublicFileEntryTitleResolver(db);
            var entries = new List<FileManagerEntry>();
            await resolver.FilterDeletedArticleEntriesAsync(entries, NewCache());
            Assert.AreEqual(0, entries.Count);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryTitleResolver")]
        public async Task FilterDeleted_ActiveArticle_IsNotRemoved()
        {
            using var db = CreateDb(nameof(FilterDeleted_ActiveArticle_IsNotRemoved));
            db.Articles.Add(new Cosmos.Common.Data.Article
            {
                ArticleNumber = 10,
                StatusCode = (int)StatusCodeEnum.Active,
                Title = "Active Article",
                UrlPath = "active-article",
            });
            await db.SaveChangesAsync();

            var entries = new List<FileManagerEntry> { ArticleEntry(10) };
            var resolver = new PublicFileEntryTitleResolver(db);
            await resolver.FilterDeletedArticleEntriesAsync(entries, NewCache());

            Assert.AreEqual(1, entries.Count, "Active article folder should remain.");
        }

        [TestMethod]
        [TestCategory("PublicFileEntryTitleResolver")]
        public async Task FilterDeleted_DeletedArticle_IsRemoved()
        {
            using var db = CreateDb(nameof(FilterDeleted_DeletedArticle_IsRemoved));
            db.Articles.Add(new Cosmos.Common.Data.Article
            {
                ArticleNumber = 20,
                StatusCode = (int)StatusCodeEnum.Deleted,
                Title = "Trashed Article",
                UrlPath = "trashed-article",
            });
            await db.SaveChangesAsync();

            var entries = new List<FileManagerEntry> { ArticleEntry(20) };
            var resolver = new PublicFileEntryTitleResolver(db);
            await resolver.FilterDeletedArticleEntriesAsync(entries, NewCache());

            Assert.AreEqual(0, entries.Count, "Soft-deleted article folder should be removed.");
        }

        [TestMethod]
        [TestCategory("PublicFileEntryTitleResolver")]
        public async Task FilterDeleted_MixedArticles_OnlyDeletedRemoved()
        {
            using var db = CreateDb(nameof(FilterDeleted_MixedArticles_OnlyDeletedRemoved));
            db.Articles.AddRange(
                new Cosmos.Common.Data.Article { ArticleNumber = 30, StatusCode = (int)StatusCodeEnum.Active, Title = "Live", UrlPath = "live" },
                new Cosmos.Common.Data.Article { ArticleNumber = 40, StatusCode = (int)StatusCodeEnum.Deleted, Title = "Gone", UrlPath = "gone" });
            await db.SaveChangesAsync();

            var entries = new List<FileManagerEntry>
            {
                ArticleEntry(30),
                ArticleEntry(40),
                FileEntry(40, "old-logo.png"),
            };
            var resolver = new PublicFileEntryTitleResolver(db);
            await resolver.FilterDeletedArticleEntriesAsync(entries, NewCache());

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("/pub/articles/30", entries[0].Path);
        }

        [TestMethod]
        [TestCategory("PublicFileEntryTitleResolver")]
        public async Task FilterDeleted_ArticleWithOneActivVersion_IsNotRemoved()
        {
            // An article is only hidden when ALL versions are deleted. If any version is Active it stays.
            using var db = CreateDb(nameof(FilterDeleted_ArticleWithOneActivVersion_IsNotRemoved));
            db.Articles.AddRange(
                new Cosmos.Common.Data.Article { ArticleNumber = 50, StatusCode = (int)StatusCodeEnum.Deleted, Title = "v1", UrlPath = "art-50", VersionNumber = 1 },
                new Cosmos.Common.Data.Article { ArticleNumber = 50, StatusCode = (int)StatusCodeEnum.Active, Title = "v2", UrlPath = "art-50", VersionNumber = 2 });
            await db.SaveChangesAsync();

            var entries = new List<FileManagerEntry> { ArticleEntry(50) };
            var resolver = new PublicFileEntryTitleResolver(db);
            await resolver.FilterDeletedArticleEntriesAsync(entries, NewCache());

            Assert.AreEqual(1, entries.Count, "Article with at least one live version must remain visible.");
        }

        [TestMethod]
        [TestCategory("PublicFileEntryTitleResolver")]
        public async Task FilterDeleted_CachedResult_IsReused()
        {
            // Prime the cache with one call, then add a new deleted record without clearing the cache.
            // The second call should use the cached result and NOT see the newly-deleted article.
            using var db = CreateDb(nameof(FilterDeleted_CachedResult_IsReused));
            db.Articles.Add(new Cosmos.Common.Data.Article
            {
                ArticleNumber = 60,
                StatusCode = (int)StatusCodeEnum.Active,
                Title = "Will Become Deleted",
                UrlPath = "art-60",
            });
            await db.SaveChangesAsync();

            var cache = NewCache();
            var resolver = new PublicFileEntryTitleResolver(db);

            // First call — primes the cache: article 60 is active, nothing filtered.
            var firstBatch = new List<FileManagerEntry> { ArticleEntry(60) };
            await resolver.FilterDeletedArticleEntriesAsync(firstBatch, cache);
            Assert.AreEqual(1, firstBatch.Count, "First call: active article must not be filtered.");

            // Simulate article being soft-deleted in DB after cache was primed.
            var article = await db.Articles.FirstAsync(a => a.ArticleNumber == 60);
            article.StatusCode = (int)StatusCodeEnum.Deleted;
            await db.SaveChangesAsync();

            // Second call — should still use the cached empty deleted-set, so article remains visible.
            var secondBatch = new List<FileManagerEntry> { ArticleEntry(60) };
            await resolver.FilterDeletedArticleEntriesAsync(secondBatch, cache);
            Assert.AreEqual(1, secondBatch.Count, "Second call within cache window: stale cache should keep the entry visible.");
        }
    }
}
