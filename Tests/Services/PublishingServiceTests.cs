// <copyright file="PublishingServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for publishing workflow.
    /// Tests article publishing via ArticleEditLogic (which uses PublishingService internally).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PublishingWorkflowTests : SkyCmsTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            InitializeTestContext(seedLayout: true);
        }

        #region Publishing Workflow Tests

        /// <summary>
        /// Tests that publishing an article creates a published page entry.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_CreatesPublishedPageEntry()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<p>Test content</p>";
            await Logic.SaveArticle(article, TestUserId);

            // Act
            var cdnResults = await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Assert
            var publishedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(publishedPage);
            Assert.AreEqual(article.Title, publishedPage.Title);
            Assert.AreEqual(article.UrlPath, publishedPage.UrlPath);
        }

        /// <summary>
        /// Tests that republishing updates existing page entry.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_UpdatesExistingPage()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Update and publish again
            article.Content = "<p>Updated content</p>";
            await Logic.SaveArticle(article, TestUserId);
            
            var latest = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            // Act
            await Logic.PublishArticle(latest.Id, DateTimeOffset.UtcNow);

            // Assert
            var publishedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(publishedPage);
            Assert.IsTrue(publishedPage.Content.Contains("Updated content"));
        }

        /// <summary>
        /// Tests that publishing sets Published timestamp.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_SetsPublishedTimestamp()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Act
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Assert
            var updated = await Db.Articles.FindAsync(article.Id);
            Assert.IsNotNull(updated.Published);
            Assert.IsTrue(updated.Published <= DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// Tests that publishing unpublishes other versions.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_UnpublishesOtherVersions()
        {
            // Arrange
            var article1 = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.PublishArticle(article1.Id, DateTimeOffset.UtcNow);

            // Create version 2
            var article1Db = await Db.Articles.FindAsync(article1.Id);
            var version2 = await Logic.NewVersion(article1Db);

            // Act - Publish version 2
            await Logic.PublishArticle(version2.Id, DateTimeOffset.UtcNow);

            // Assert
            var version1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNull(version1.Published, "Version 1 should be unpublished");

            var version2Updated = await Db.Articles.FindAsync(version2.Id);
            Assert.IsNotNull(version2Updated.Published, "Version 2 should be published");
        }

        /// <summary>
        /// Tests that only one version per article is published.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_OnlyOneVersionPublished()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var v1Db = await Db.Articles.FindAsync(article.Id);
            var v2 = await Logic.NewVersion(v1Db);
            await Logic.PublishArticle(v2.Id, DateTimeOffset.UtcNow);
            
            var v3 = await Logic.NewVersion(v2);
            await Logic.PublishArticle(v3.Id, DateTimeOffset.UtcNow);

            // Act - Count published versions
            var publishedCount = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber && a.Published != null)
                .CountAsync();

            // Assert
            Assert.AreEqual(1, publishedCount, "Only one version should be published");
        }

        #endregion

        #region Page Creation Tests

        /// <summary>
        /// Tests that published page contains article content.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_PageContainsContent()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<p>Unique test content 12345</p>";
            await Logic.SaveArticle(article, TestUserId);

            // Act
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page);
            Assert.IsTrue(page.Content.Contains("Unique test content 12345"));
        }

        /// <summary>
        /// Tests that published page has correct metadata.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_PageHasCorrectMetadata()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<p>Content</p>";
            article.BannerImage = "/images/banner.jpg";
            article.Category = "Technology";
            await Logic.SaveArticle(article, TestUserId);

            // Act
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page);
            Assert.AreEqual(article.BannerImage, page.BannerImage);
            Assert.AreEqual(article.Category, page.Category);
            Assert.AreEqual(article.VersionNumber, page.VersionNumber);
        }

        /// <summary>
        /// Tests that root page URL is handled correctly.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_RootPage_CorrectUrlPath()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            Assert.AreEqual("root", rootArticle.UrlPath);

            // Act
            await Logic.PublishArticle(rootArticle.Id, DateTimeOffset.UtcNow);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == rootArticle.ArticleNumber);
            Assert.IsNotNull(page);
            Assert.AreEqual("root", page.UrlPath);
        }

        #endregion

        #region Catalog Updates Tests

        /// <summary>
        /// Tests that publishing updates catalog entry.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_UpdatesCatalogEntry()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);

            // Act
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Assert
            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            
            Assert.IsNotNull(catalogEntry);
            Assert.IsNotNull(catalogEntry.Published);
        }

        #endregion

        #region Blog Post Publishing Tests

        /// <summary>
        /// Tests that publishing a blog post works correctly.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_BlogPost_CreatesPage()
        {
            // Arrange
            // Create home page first
            await Logic.CreateArticle("Home", TestUserId);
            
            var blogPost = await Logic.CreateArticle("Blog Post", TestUserId, null, "default", ArticleType.BlogPost);
            blogPost.Content = "<p>Blog content</p>";
            blogPost.Category = "Technology";
            await Logic.SaveArticle(blogPost, TestUserId);

            // Act
            await Logic.PublishArticle(blogPost.Id, DateTimeOffset.UtcNow);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == blogPost.ArticleNumber);
            Assert.IsNotNull(page);
            Assert.AreEqual((int)ArticleType.BlogPost, page.ArticleType);
            Assert.AreEqual("Technology", page.Category);
        }

        /// <summary>
        /// Tests that blog post has blog key.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_BlogPost_HasBlogKey()
        {
            // Arrange
            await Logic.CreateArticle("Home", TestUserId);
            
            var blogPost = await Logic.CreateArticle("Blog Post", TestUserId, null, "tech-blog", ArticleType.BlogPost);
            blogPost.Content = "<p>Content</p>";
            await Logic.SaveArticle(blogPost, TestUserId);

            // Act
            await Logic.PublishArticle(blogPost.Id, DateTimeOffset.UtcNow);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == blogPost.ArticleNumber);
            Assert.IsNotNull(page);
            Assert.AreEqual("tech-blog", page.BlogKey);
        }

        #endregion

        #region Version Management Tests

        /// <summary>
        /// Tests that page reflects correct version number.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_PageHasCorrectVersionNumber()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var v1Db = await Db.Articles.FindAsync(article.Id);
            var v2 = await Logic.NewVersion(v1Db);

            // Act
            await Logic.PublishArticle(v2.Id, DateTimeOffset.UtcNow);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page);
            Assert.AreEqual(v2.VersionNumber, page.VersionNumber);
        }

        /// <summary>
        /// Tests that old page is replaced when new version is published.
        /// </summary>
        [TestMethod]
        public async Task PublishArticle_NewVersion_ReplacesOldPage()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<p>Version 1</p>";
            await Logic.SaveArticle(article, TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var v1Db = await Db.Articles.FindAsync(article.Id);
            var v2 = await Logic.NewVersion(v1Db);
            v2.Content = "<p>Version 2</p>";
            await Db.SaveChangesAsync();

            // Act
            await Logic.PublishArticle(v2.Id, DateTimeOffset.UtcNow);

            // Assert
            var pages = await Db.Pages
                .Where(p => p.ArticleNumber == article.ArticleNumber)
                .ToListAsync();
            
            Assert.AreEqual(1, pages.Count, "Should only have one published page");
            Assert.IsTrue(pages[0].Content.Contains("Version 2"));
        }

        #endregion

        #region Published Page Query Tests

        /// <summary>
        /// Tests querying published pages.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPages_ReturnsOnlyPublished()
        {
            // Arrange
            var published = await Logic.CreateArticle("Published", TestUserId);
            await Logic.PublishArticle(published.Id, DateTimeOffset.UtcNow);
            
            var draft = await Logic.CreateArticle("Draft", TestUserId);
            // Don't publish

            // Act
            var publishedPages = await Db.Pages.ToListAsync();

            // Assert
            Assert.IsTrue(publishedPages.Any(p => p.ArticleNumber == published.ArticleNumber));
            Assert.IsFalse(publishedPages.Any(p => p.ArticleNumber == draft.ArticleNumber));
        }

        /// <summary>
        /// Tests that published pages can be queried by URL path.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedPage_ByUrlPath_FindsPage()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Act
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.UrlPath == article.UrlPath);

            // Assert
            Assert.IsNotNull(page);
            Assert.AreEqual(article.ArticleNumber, page.ArticleNumber);
        }

        #endregion
    }
}