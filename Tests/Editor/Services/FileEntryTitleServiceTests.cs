// <copyright file="FileEntryTitleServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Editor.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.Azure.Cosmos;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Services;

    /// <summary>
    /// Unit tests for <see cref="FileEntryTitleService"/>, covering
    /// title resolution and deleted-article filtering behaviour.
    /// </summary>
    [TestClass]
    public class FileEntryTitleServiceTests
    {
        private static ApplicationDbContext CreateDb(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"TitleResolver_{name}_{System.Guid.NewGuid()}")
                .Options;
            return new ApplicationDbContext(options);
        }

        private static FileEntryTitleService CreateService(ApplicationDbContext db)
        {
            var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
            var configProvider = new Moq.Mock<Cosmos.DynamicConfig.IDynamicConfigurationProvider>().Object;
            return new FileEntryTitleService(db, cache, configProvider);
        }

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

        // â”€â”€â”€â”€â”€ ExtractArticleNumbersFromEntries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        [TestCategory("FileEntryPathHelper")]
        public void ExtractArticleNumbers_NullEntries_ReturnsEmpty()
        {
            var result = FileEntryPathHelper.ExtractArticleNumbersFromEntries(null);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        [TestCategory("FileEntryPathHelper")]
        public void ExtractArticleNumbers_EmptyList_ReturnsEmpty()
        {
            var result = FileEntryPathHelper.ExtractArticleNumbersFromEntries(new List<FileManagerEntry>());
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        [TestCategory("FileEntryPathHelper")]
        public void ExtractArticleNumbers_FolderAtRoot_ReturnsNumber()
        {
            var entries = new[] { ArticleEntry(42) };
            var result = FileEntryPathHelper.ExtractArticleNumbersFromEntries(entries);
            CollectionAssert.AreEqual(new[] { 42 }, result);
        }

        [TestMethod]
        [TestCategory("FileEntryPathHelper")]
        public void ExtractArticleNumbers_FileWithinArticleFolder_ReturnsNumber()
        {
            var entries = new[] { FileEntry(99, "logo.png") };
            var result = FileEntryPathHelper.ExtractArticleNumbersFromEntries(entries);
            CollectionAssert.AreEqual(new[] { 99 }, result);
        }

        [TestMethod]
        [TestCategory("FileEntryPathHelper")]
        public void ExtractArticleNumbers_MultipleFilesUnderSameArticle_DeduplicatesAndSorts()
        {
            var entries = new[]
            {
                FileEntry(200, "a.css"),
                FileEntry(200, "b.css"),
                ArticleEntry(100),
            };
            var result = FileEntryPathHelper.ExtractArticleNumbersFromEntries(entries);
            CollectionAssert.AreEqual(new[] { 100, 200 }, result);
        }

        [TestMethod]
        [TestCategory("FileEntryPathHelper")]
        public void ExtractArticleNumbers_NonArticlePaths_AreIgnored()
        {
            var entries = new[]
            {
                new FileManagerEntry { Path = "/pub/templates/some-id", Name = "t", IsDirectory = true },
                new FileManagerEntry { Path = "/pub/articles", Name = "articles", IsDirectory = true },
                ArticleEntry(7),
            };
            var result = FileEntryPathHelper.ExtractArticleNumbersFromEntries(entries);
            CollectionAssert.AreEqual(new[] { 7 }, result);
        }

        // â”€â”€â”€â”€â”€ FilterDeletedArticleEntriesAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task FilterDeleted_NullList_DoesNotThrow()
        {
            using var db = CreateDb(nameof(FilterDeleted_NullList_DoesNotThrow));
            var resolver = CreateService(db);
            await resolver.FilterDeletedArticleEntriesAsync(null!);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task FilterDeleted_EmptyList_DoesNotThrow()
        {
            using var db = CreateDb(nameof(FilterDeleted_EmptyList_DoesNotThrow));
            var resolver = CreateService(db);
            var entries = new List<FileManagerEntry>();
            await resolver.FilterDeletedArticleEntriesAsync(entries);
            Assert.AreEqual(0, entries.Count);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
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
            var resolver = CreateService(db);
            await resolver.FilterDeletedArticleEntriesAsync(entries);

            Assert.AreEqual(1, entries.Count, "Active article folder should remain.");
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
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
            var resolver = CreateService(db);
            await resolver.FilterDeletedArticleEntriesAsync(entries);

            Assert.AreEqual(0, entries.Count, "Soft-deleted article folder should be removed.");
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
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
            var resolver = CreateService(db);
            await resolver.FilterDeletedArticleEntriesAsync(entries);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("/pub/articles/30", entries[0].Path);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task FilterDeleted_ArticleWithOneActivVersion_IsNotRemoved()
        {
            // An article is only hidden when ALL versions are deleted. If any version is Active it stays.
            using var db = CreateDb(nameof(FilterDeleted_ArticleWithOneActivVersion_IsNotRemoved));
            db.Articles.AddRange(
                new Cosmos.Common.Data.Article { ArticleNumber = 50, StatusCode = (int)StatusCodeEnum.Deleted, Title = "v1", UrlPath = "art-50", VersionNumber = 1 },
                new Cosmos.Common.Data.Article { ArticleNumber = 50, StatusCode = (int)StatusCodeEnum.Active, Title = "v2", UrlPath = "art-50", VersionNumber = 2 });
            await db.SaveChangesAsync();

            var entries = new List<FileManagerEntry> { ArticleEntry(50) };
            var resolver = CreateService(db);
            await resolver.FilterDeletedArticleEntriesAsync(entries);

            Assert.AreEqual(1, entries.Count, "Article with at least one live version must remain visible.");
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ProjectFriendlyEntries_ArticlesRoot_RewritesRootNameAndFiltersDeleted()
        {
            using var db = CreateDb(nameof(ProjectFriendlyEntries_ArticlesRoot_RewritesRootNameAndFiltersDeleted));
            db.Articles.AddRange(
                new Cosmos.Common.Data.Article { ArticleNumber = 601, StatusCode = (int)StatusCodeEnum.Active, Title = "Visible Article", UrlPath = "visible", VersionNumber = 1 },
                new Cosmos.Common.Data.Article { ArticleNumber = 602, StatusCode = (int)StatusCodeEnum.Deleted, Title = "Hidden Article", UrlPath = "hidden", VersionNumber = 1 });
            await db.SaveChangesAsync();

            var entries = new List<FileManagerEntry>
            {
                ArticleEntry(601),
                ArticleEntry(602),
            };

            var resolver = CreateService(db);
            var projected = await resolver.ProjectFriendlyEntriesAsync(entries, "/pub/articles", string.Empty);

            Assert.AreEqual(1, projected.Count, "Deleted article entries should be removed from root listing.");
            Assert.AreEqual("Visible Article", projected[0].Name);
            Assert.AreEqual("/pub/articles/Visible Article", projected[0].DisplayPath);
            Assert.AreEqual("/pub/articles/601", projected[0].Path);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ProjectFriendlyEntries_ArticlesRoot_NestedDirectoryNameIsNotRewritten()
        {
            using var db = CreateDb(nameof(ProjectFriendlyEntries_ArticlesRoot_NestedDirectoryNameIsNotRewritten));
            db.Articles.Add(new Cosmos.Common.Data.Article
            {
                ArticleNumber = 777,
                StatusCode = (int)StatusCodeEnum.Active,
                Title = "Nested Name Rule",
                UrlPath = "nested-name-rule",
                VersionNumber = 1,
            });
            await db.SaveChangesAsync();

            var entries = new List<FileManagerEntry>
            {
                new FileManagerEntry
                {
                    Path = "/pub/articles/777/assets",
                    Name = "assets",
                    IsDirectory = true,
                },
            };

            var resolver = CreateService(db);
            var projected = await resolver.ProjectFriendlyEntriesAsync(entries, "/pub/articles", string.Empty);

            Assert.AreEqual(1, projected.Count);
            Assert.AreEqual("assets", projected[0].Name, "Nested directory names must remain unchanged.");
            Assert.AreEqual("/pub/articles/Nested Name Rule/assets", projected[0].DisplayPath);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
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

            var resolver = CreateService(db);

            // First call: article 60 is active, nothing filtered.
            var firstBatch = new List<FileManagerEntry> { ArticleEntry(60) };
            await resolver.FilterDeletedArticleEntriesAsync(firstBatch);
            Assert.AreEqual(1, firstBatch.Count, "First call: active article must not be filtered.");

            // Simulate article being soft-deleted in DB after first lookup.
            var article = await db.Articles.FirstAsync(a => a.ArticleNumber == 60);
            article.StatusCode = (int)StatusCodeEnum.Deleted;
            await db.SaveChangesAsync();

            // Second call should reflect latest DB state and filter the deleted entry.
            var secondBatch = new List<FileManagerEntry> { ArticleEntry(60) };
            await resolver.FilterDeletedArticleEntriesAsync(secondBatch);
            Assert.AreEqual(0, secondBatch.Count, "Second call should reflect the latest deleted state.");
        }

        // â”€â”€â”€â”€â”€ GetArticleTitlesByNumberAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task GetArticleTitles_MultipleVersions_ReturnsLatestVersion()
        {
            // Arrange: Create multiple versions of the same article with different titles
            using var db = CreateDb(nameof(GetArticleTitles_MultipleVersions_ReturnsLatestVersion));
            db.Articles.AddRange(
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 100,
                    VersionNumber = 1,
                    Title = "Original Title",
                    UrlPath = "article-100",
                    StatusCode = (int)StatusCodeEnum.Active,
                },
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 100,
                    VersionNumber = 2,
                    Title = "Updated Title",
                    UrlPath = "article-100",
                    StatusCode = (int)StatusCodeEnum.Active,
                },
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 100,
                    VersionNumber = 3,
                    Title = "Latest Title",
                    UrlPath = "article-100",
                    StatusCode = (int)StatusCodeEnum.Active,
                });
            await db.SaveChangesAsync();

            // Act: Get title for article 100
            var entries = new List<FileManagerEntry>
            {
                new FileManagerEntry
                {
                    Path = "pub/articles/100",
                    Name = "100",
                    IsDirectory = true,
                },
            };
            var resolver = CreateService(db);
            var result = await resolver.GetArticleTitlesByNumberAsync(entries, string.Empty);

            // Assert: Should return the title from version 3 (latest)
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey(100));
            Assert.AreEqual("Latest Title", result[100]);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task GetArticleTitles_EmptyTitles_SkipsEmptyVersions()
        {
            // Arrange: Create versions where some have empty titles
            using var db = CreateDb(nameof(GetArticleTitles_EmptyTitles_SkipsEmptyVersions));
            db.Articles.AddRange(
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 200,
                    VersionNumber = 1,
                    Title = "Good Title",
                    UrlPath = "article-200",
                    StatusCode = (int)StatusCodeEnum.Active,
                },
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 200,
                    VersionNumber = 2,
                    Title = string.Empty,
                    UrlPath = "article-200",
                    StatusCode = (int)StatusCodeEnum.Active,
                },
                new Cosmos.Common.Data.Article
                {
                    ArticleNumber = 200,
                    VersionNumber = 3,
                    Title = "   ",  // Whitespace only
                    UrlPath = "article-200",
                    StatusCode = (int)StatusCodeEnum.Active,
                });
            await db.SaveChangesAsync();

            // Act
            var entries = new List<FileManagerEntry>
            {
                new FileManagerEntry
                {
                    Path = "pub/articles/200",
                    Name = "200",
                    IsDirectory = true,
                },
            };
            var resolver = CreateService(db);
            var result = await resolver.GetArticleTitlesByNumberAsync(entries, string.Empty);

            // Assert: Should return "Good Title" from version 1 (latest non-empty)
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey(200));
            Assert.AreEqual("Good Title", result[200]);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task GetArticleTitlesByNumber_NumericOverload_ReturnsCatalogTitle()
        {
            using var db = CreateDb(nameof(GetArticleTitlesByNumber_NumericOverload_ReturnsCatalogTitle));
            db.ArticleCatalog.Add(new Cosmos.Common.Data.CatalogEntry
            {
                ArticleNumber = 900,
                Title = "Numeric Title",
                UrlPath = "numeric-title",
                Status = nameof(StatusCodeEnum.Active),
            });
            await db.SaveChangesAsync();

            var resolver = CreateService(db);
            var result = await resolver.GetArticleTitlesByNumberAsync(new[] { 900 }, string.Empty);

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey(900));
            Assert.AreEqual("Numeric Title", result[900]);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task GetArticleTitleStatusByNumber_BackfillsMissingLegacyCatalogRow()
        {
            using var db = CreateDb(nameof(GetArticleTitleStatusByNumber_BackfillsMissingLegacyCatalogRow));
            db.Articles.Add(new Cosmos.Common.Data.Article
            {
                ArticleNumber = 901,
                VersionNumber = 1,
                Title = "Legacy Inactive Article",
                UrlPath = "legacy-inactive",
                StatusCode = (int)StatusCodeEnum.Inactive,
            });
            await db.SaveChangesAsync();

            var resolver = CreateService(db);
            var result = await resolver.GetArticleTitleStatusByNumberAsync(new[] { 901 }, string.Empty);

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey(901));
            Assert.AreEqual("Legacy Inactive Article", result[901].Title);
            Assert.AreEqual((int)StatusCodeEnum.Inactive, result[901].StatusCode);
            Assert.AreEqual(1, await db.ArticleCatalog.CountAsync(a => a.ArticleNumber == 901));
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ResolveCanonicalPath_AlreadyCanonical_PassesThrough()
        {
            using var db = CreateDb(nameof(ResolveCanonicalPath_AlreadyCanonical_PassesThrough));
            var resolver = CreateService(db);

            var result = await resolver.ResolveCanonicalPathAsync("/pub/articles/123/banner.jpg");

            Assert.AreEqual("/pub/articles/123/banner.jpg", result);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ResolveCanonicalPath_NonArticlePath_PassesThrough()
        {
            using var db = CreateDb(nameof(ResolveCanonicalPath_NonArticlePath_PassesThrough));
            var resolver = CreateService(db);

            var result = await resolver.ResolveCanonicalPathAsync("/pub/static/logo.png");

            Assert.AreEqual("/pub/static/logo.png", result);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ResolveCanonicalPath_TitleInCatalog_ResolvesToNumber()
        {
            using var db = CreateDb(nameof(ResolveCanonicalPath_TitleInCatalog_ResolvesToNumber));
            db.ArticleCatalog.Add(new Cosmos.Common.Data.CatalogEntry
            {
                ArticleNumber = 456,
                Title = "Getting Started Guide",
                UrlPath = "getting-started",
                Status = nameof(StatusCodeEnum.Active),
            });
            await db.SaveChangesAsync();

            var resolver = CreateService(db);
            var result = await resolver.ResolveCanonicalPathAsync("/pub/articles/Getting Started Guide/banner.jpg");

            Assert.AreEqual("/pub/articles/456/banner.jpg", result);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ResolveCanonicalPath_TitleInArticlesTable_ResolvesToNumber()
        {
            using var db = CreateDb(nameof(ResolveCanonicalPath_TitleInArticlesTable_ResolvesToNumber));
            db.Articles.Add(new Cosmos.Common.Data.Article
            {
                ArticleNumber = 789,
                VersionNumber = 1,
                Title = "Draft Article",
                UrlPath = "draft",
                StatusCode = (int)StatusCodeEnum.Active,
            });
            await db.SaveChangesAsync();

            var resolver = CreateService(db);
            var result = await resolver.ResolveCanonicalPathAsync("/pub/articles/Draft Article");

            Assert.AreEqual("/pub/articles/789", result);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ResolveCanonicalPath_NonexistentTitle_ReturnsOriginal()
        {
            using var db = CreateDb(nameof(ResolveCanonicalPath_NonexistentTitle_ReturnsOriginal));
            var resolver = CreateService(db);

            var result = await resolver.ResolveCanonicalPathAsync("/pub/articles/Nonexistent Article");

            Assert.AreEqual("/pub/articles/Nonexistent Article", result);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ResolveCanonicalPath_TitleCollision_ReturnsLowestNumber()
        {
            using var db = CreateDb(nameof(ResolveCanonicalPath_TitleCollision_ReturnsLowestNumber));
            db.ArticleCatalog.AddRange(
                new Cosmos.Common.Data.CatalogEntry
                {
                    ArticleNumber = 999,
                    Title = "Duplicate Title",
                    UrlPath = "duplicate-999",
                    Status = nameof(StatusCodeEnum.Active),
                },
                new Cosmos.Common.Data.CatalogEntry
                {
                    ArticleNumber = 111,
                    Title = "Duplicate Title",
                    UrlPath = "duplicate-111",
                    Status = nameof(StatusCodeEnum.Active),
                });
            await db.SaveChangesAsync();

            var resolver = CreateService(db);
            var result = await resolver.ResolveCanonicalPathAsync("/pub/articles/Duplicate Title/image.png");

            // Should return lowest article number (111)
            Assert.AreEqual("/pub/articles/111/image.png", result);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ResolveCanonicalPath_EmptyPath_ReturnsEmpty()
        {
            using var db = CreateDb(nameof(ResolveCanonicalPath_EmptyPath_ReturnsEmpty));
            var resolver = CreateService(db);

            var result = await resolver.ResolveCanonicalPathAsync(string.Empty);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ResolveCanonicalPath_NullPath_ReturnsEmpty()
        {
            using var db = CreateDb(nameof(ResolveCanonicalPath_NullPath_ReturnsEmpty));
            var resolver = CreateService(db);

            var result = await resolver.ResolveCanonicalPathAsync(null!);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ResolveCanonicalPath_ShortPath_PassesThrough()
        {
            using var db = CreateDb(nameof(ResolveCanonicalPath_ShortPath_PassesThrough));
            var resolver = CreateService(db);

            var result = await resolver.ResolveCanonicalPathAsync("/pub/articles");

            Assert.AreEqual("/pub/articles", result);
        }

        [TestMethod]
        [TestCategory("FileEntryTitleService")]
        public async Task ResolveCanonicalPath_DeepNestedPath_ResolvesCorrectly()
        {
            using var db = CreateDb(nameof(ResolveCanonicalPath_DeepNestedPath_ResolvesCorrectly));
            db.ArticleCatalog.Add(new Cosmos.Common.Data.CatalogEntry
            {
                ArticleNumber = 555,
                Title = "Deep Article",
                UrlPath = "deep",
                Status = nameof(StatusCodeEnum.Active),
            });
            await db.SaveChangesAsync();

            var resolver = CreateService(db);
            var result = await resolver.ResolveCanonicalPathAsync("/pub/articles/Deep Article/assets/images/photo.jpg");

            Assert.AreEqual("/pub/articles/555/assets/images/photo.jpg", result);
        }
    }
}

