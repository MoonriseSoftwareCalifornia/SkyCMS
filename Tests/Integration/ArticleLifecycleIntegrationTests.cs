// <copyright file="ArticleLifecycleIntegrationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Integration
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests for complete article lifecycle workflows.
    /// Tests end-to-end scenarios from article creation through publishing, editing, and deletion.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ArticleLifecycleIntegrationTests : SkyCmsTestBase
    {
        [TestInitialize]
        public void Setup()
        {
            InitializeTestContext(seedLayout: true);
        }

        #region Full Article Lifecycle Tests

        /// <summary>
        /// Tests complete article lifecycle: create → edit → publish → delete → restore.
        /// </summary>
        [TestMethod]
        public async Task ArticleLifecycle_CompleteWorkflow_Success()
        {
            // Step 1: Create article
            var article = await Logic.CreateArticle("Lifecycle Test Article", TestUserId);
            Assert.IsNotNull(article);
            Assert.AreEqual("Lifecycle Test Article", article.Title);

            // Step 2: Edit article
            article.Content = "<p>Initial content</p>";
            article.Category = "Technology";
            var saveResult = await Logic.SaveArticle(article, TestUserId);
            Assert.IsTrue(saveResult.ServerSideSuccess);

            // Step 3: Publish article
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            var publishedArticle = await Db.Articles.FindAsync(article.Id);
            Assert.IsNotNull(publishedArticle.Published, "Article should be published");

            // Verify page was created
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page, "Published page should exist");

            // Verify catalog was updated
            var catalog = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalog.Published, "Catalog should show published");

            // Step 4: Edit published article (creates new version)
            var dbArticle = await Db.Articles.FindAsync(article.Id);
            var newVersion = await Logic.NewVersion(dbArticle);
            newVersion.Content = "<p>Updated content</p>";
            await Db.SaveChangesAsync();

            // Publish new version
            await Logic.PublishArticle(newVersion.Id, DateTimeOffset.UtcNow);

            // Verify old version unpublished
            var oldVersion = await Db.Articles.FindAsync(article.Id);
            Assert.IsNull(oldVersion.Published, "Old version should be unpublished");

            // Step 5: Delete article (soft delete)
            // Create replacement home page and swap the root designation
            var home = await Logic.CreateArticle("Home Page Temp", TestUserId);
            var homeArticles = await Db.Articles.Where(a => a.ArticleNumber == home.ArticleNumber).ToListAsync();
            foreach (var homeArticle in homeArticles)
            {
                homeArticle.UrlPath = "root";
            }
            var originalArticles = await Db.Articles.Where(a => a.ArticleNumber == article.ArticleNumber).ToListAsync();
            foreach (var originalArticle in originalArticles)
            {
                originalArticle.UrlPath = "lifecycle-test-article";
            }
            await Db.SaveChangesAsync();
            
            await Logic.DeleteArticle(article.ArticleNumber);
            var deletedArticle = await Db.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, deletedArticle.StatusCode);

            // Verify page removed
            var deletedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNull(deletedPage, "Page should be removed after delete");

            // Step 6: Restore article
            await Logic.RestoreArticle(article.ArticleNumber, TestUserId.ToString());
            var restoredArticle = await Db.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual((int)StatusCodeEnum.Active, restoredArticle.StatusCode);
            Assert.IsNull(restoredArticle.Published, "Restored article should be unpublished");
        }

        /// <summary>
        /// Tests creating multiple articles and publishing in different orders.
        /// </summary>
        [TestMethod]
        public async Task MultipleArticles_PublishInDifferentOrders_AllWorkCorrectly()
        {
            // Create 3 articles
            var article1 = await Logic.CreateArticle("Article 1", TestUserId);
            var article2 = await Logic.CreateArticle("Article 2", TestUserId);
            var article3 = await Logic.CreateArticle("Article 3", TestUserId);

            // Publish in reverse order
            await Logic.PublishArticle(article3.Id, DateTimeOffset.UtcNow);
            await Logic.PublishArticle(article1.Id, DateTimeOffset.UtcNow);

            // Leave article 2 unpublished

            // Verify published pages
            var publishedPages = await Db.Pages.ToListAsync();
            Assert.IsTrue(publishedPages.Any(p => p.ArticleNumber == article1.ArticleNumber));
            Assert.IsTrue(publishedPages.Any(p => p.ArticleNumber == article3.ArticleNumber));
            Assert.IsFalse(publishedPages.Any(p => p.ArticleNumber == article2.ArticleNumber));

            // Publish article 2
            await Logic.PublishArticle(article2.Id, DateTimeOffset.UtcNow);
            var article2Page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article2.ArticleNumber);
            Assert.IsNotNull(article2Page);
        }

        /// <summary>
        /// Tests that editing and republishing maintains correct state.
        /// </summary>
        [TestMethod]
        public async Task EditAndRepublish_MaintainsCorrectState()
        {
            // Create and publish
            var article = await Logic.CreateArticle("Edit Test", TestUserId);
            article.Content = "<p>Version 1</p>";
            await Logic.SaveArticle(article, TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            var initialPublishedDate = (await Db.Articles.FindAsync(article.Id)).Published;

            // Wait a moment
            await Task.Delay(100);

            // Edit and republish
            article.Content = "<p>Version 2</p>";
            await Logic.SaveArticle(article, TestUserId);

            var latestVersion = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            await Logic.PublishArticle(latestVersion.Id, DateTimeOffset.UtcNow);

            // Verify
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page);
            Assert.IsTrue(page.Content.Contains("Version 2"));
            Assert.IsTrue(page.Published > initialPublishedDate);
        }

        #endregion

        #region Blog Post Integration Tests

        /// <summary>
        /// Tests complete blog post workflow with categories and publishing.
        /// </summary>
        [TestMethod]
        public async Task BlogPost_CompleteWorkflow_Success()
        {
            // Create home page first
            await Logic.CreateArticle("Home", TestUserId);

            // Create blog post
            var blogPost = await Logic.CreateArticle("My Blog Post", TestUserId, null, "default", ArticleType.BlogPost);
            blogPost.Content = "<p>Blog post content with some interesting information.</p>";
            blogPost.Category = "Technology";
            blogPost.Introduction = "This is a custom introduction";

            // Save
            var saveResult = await Logic.SaveArticle(blogPost, TestUserId);
            Assert.IsTrue(saveResult.ServerSideSuccess);

            // Publish
            await Logic.PublishArticle(blogPost.Id, DateTimeOffset.UtcNow);

            // Verify page
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == blogPost.ArticleNumber);
            Assert.IsNotNull(page);
            Assert.AreEqual((int)ArticleType.BlogPost, page.ArticleType);
            Assert.AreEqual("Technology", page.Category);
            Assert.AreEqual("default", page.BlogKey);
            Assert.AreEqual("This is a custom introduction", page.Introduction);

            // Query blog posts
            var blogPosts = await Db.Pages
                .Where(p => p.ArticleType == (int)ArticleType.BlogPost && p.Category == "Technology")
                .ToListAsync();

            Assert.IsTrue(blogPosts.Any(p => p.ArticleNumber == blogPost.ArticleNumber));
        }

        /// <summary>
        /// Tests multiple blog posts with pagination.
        /// </summary>
        [TestMethod]
        public async Task MultipleBlogPosts_WithPagination_ReturnsCorrectPages()
        {
            // Create home page
            await Logic.CreateArticle("Home", TestUserId);

            // Create 10 blog posts
            for (int i = 1; i <= 10; i++)
            {
                var post = await Logic.CreateArticle($"Post {i}", TestUserId, null, "default", ArticleType.BlogPost);
                post.Content = $"<p>Content for post {i}</p>";
                await Logic.SaveArticle(post, TestUserId);
                await Logic.PublishArticle(post.Id, DateTimeOffset.UtcNow.AddMinutes(i));
                await Task.Delay(10); // Ensure different timestamps
            }

            // Get first page (5 items)
            var page1 = await Db.Pages
                .Where(p => p.ArticleType == (int)ArticleType.BlogPost)
                .OrderByDescending(p => p.Published)
                .Take(5)
                .ToListAsync();

            Assert.AreEqual(5, page1.Count);

            // Get second page
            var page2 = await Db.Pages
                .Where(p => p.ArticleType == (int)ArticleType.BlogPost)
                .OrderByDescending(p => p.Published)
                .Skip(5)
                .Take(5)
                .ToListAsync();

            Assert.AreEqual(5, page2.Count);

            // Verify no overlap
            var page1Ids = page1.Select(p => p.ArticleNumber).ToList();
            var page2Ids = page2.Select(p => p.ArticleNumber).ToList();
            Assert.IsFalse(page1Ids.Intersect(page2Ids).Any());
        }

        #endregion

        #region Version Management Integration Tests

        /// <summary>
        /// Tests creating multiple versions and publishing specific versions.
        /// </summary>
        [TestMethod]
        public async Task MultipleVersions_PublishSpecificVersion_Success()
        {
            // Create article
            var article = await Logic.CreateArticle("Version Test", TestUserId);

            // Create version 2
            var v1 = await Db.Articles.FindAsync(article.Id);
            var v2 = await Logic.NewVersion(v1);
            v2.Content = "<p>Version 2</p>";
            await Db.SaveChangesAsync();

            // Create version 3
            var v3 = await Logic.NewVersion(v2);
            v3.Content = "<p>Version 3</p>";
            await Db.SaveChangesAsync();

            // Publish version 2 (not latest)
            await Logic.PublishArticle(v2.Id, DateTimeOffset.UtcNow);

            // Verify version 2 is published
            var publishedV2 = await Db.Articles.FindAsync(v2.Id);
            Assert.IsNotNull(publishedV2.Published);

            // Verify version 3 is not published
            var unpublishedV3 = await Db.Articles.FindAsync(v3.Id);
            Assert.IsNull(unpublishedV3.Published);

            // Verify page contains version 2 content
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsTrue(page.Content.Contains("Version 2"));
        }

        /// <summary>
        /// Tests that only one version is published at a time.
        /// </summary>
        [TestMethod]
        public async Task PublishDifferentVersions_OnlyOnePublishedAtTime()
        {
            // Create article with 3 versions
            var article = await Logic.CreateArticle("Multi-Version Test", TestUserId);

            var v1 = await Db.Articles.FindAsync(article.Id);
            var v2 = await Logic.NewVersion(v1);
            var v3 = await Logic.NewVersion(v2);

            // Publish v1
            await Logic.PublishArticle(v1.Id, DateTimeOffset.UtcNow);
            var published1 = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber && a.Published != null)
                .CountAsync();
            Assert.AreEqual(1, published1);

            // Publish v2 (should unpublish v1)
            await Logic.PublishArticle(v2.Id, DateTimeOffset.UtcNow);
            var published2 = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber && a.Published != null)
                .CountAsync();
            Assert.AreEqual(1, published2);

            // Publish v3 (should unpublish v2)
            await Logic.PublishArticle(v3.Id, DateTimeOffset.UtcNow);
            var published3 = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber && a.Published != null)
                .CountAsync();
            Assert.AreEqual(1, published3);

            // Verify v3 is the only published version
            var publishedVersion = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber && a.Published != null);
            Assert.AreEqual(v3.Id, publishedVersion.Id);
        }

        #endregion

        #region Catalog Synchronization Integration Tests

        /// <summary>
        /// Tests that catalog stays synchronized with article operations.
        /// </summary>
        [TestMethod]
        public async Task CatalogSynchronization_ThroughLifecycle_StaysConsistent()
        {
            // Create a root article first so the test article isn't auto-published
            await Logic.CreateArticle("Root", TestUserId);
            
            // Create article
            var article = await Logic.CreateArticle("Catalog Sync Test", TestUserId);

            // Verify catalog entry NOT created yet (catalog entries are created on publish/save, not on creation)
            var catalog1 = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNull(catalog1, "Catalog entry should not exist until article is published or saved");

            // Publish article
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);

            // Verify catalog created and marked as published
            var catalog2 = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalog2, "Catalog entry should be created when article is published");
            Assert.IsNotNull(catalog2.Published, "Catalog entry should be marked as published");

            // Update article
            article.Title = "Updated Title";
            await Logic.SaveArticle(article, TestUserId);

            // Verify catalog title updated
            var catalog3 = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual("Updated Title", catalog3.Title);

            // Delete article (create home first to avoid deleting root)
            var home = await Logic.CreateArticle("Temp Home", TestUserId);
            await Logic.DeleteArticle(article.ArticleNumber);

            // Verify catalog removed
            var catalog4 = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNull(catalog4);
        }

        #endregion

        #region Error Recovery Tests

        /// <summary>
        /// Tests that deleting non-existent article throws appropriate exception.
        /// </summary>
        [TestMethod]
        public async Task DeleteNonExistentArticle_ThrowsException()
        {
            // Arrange
            var nonExistentArticleNumber = 99999;

            // Act
            try
            {
                await Logic.DeleteArticle(nonExistentArticleNumber);
                Assert.Fail("Expected KeyNotFoundException was not thrown.");
            }
            catch (KeyNotFoundException ex)
            {
                // Assert
                StringAssert.Contains(ex.Message, nonExistentArticleNumber.ToString());
            }
        }

        #endregion

        #region Root Page Integration Tests

        /// <summary>
        /// Tests that first article becomes root and subsequent articles don't.
        /// </summary>
        [TestMethod]
        public async Task RootPageBehavior_FirstArticleBecomesRoot_OthersDoNot()
        {
            // First article becomes root
            var firstArticle = await Logic.CreateArticle("First Article", TestUserId);
            Assert.AreEqual("root", firstArticle.UrlPath);
            Assert.IsNotNull(firstArticle.Published, "First article should auto-publish");

            // Second article doesn't become root
            var secondArticle = await Logic.CreateArticle("Second Article", TestUserId);
            Assert.AreNotEqual("root", secondArticle.UrlPath);
            Assert.IsNull(secondArticle.Published, "Second article should not auto-publish");
        }

        /// <summary>
        /// Tests that root page cannot be deleted.
        /// </summary>
        [TestMethod]
        public async Task DeleteRootPage_ThrowsNotSupportedException()
        {
            // Get root article
            var rootArticle = await Logic.CreateArticle("Root Page", TestUserId);
            Assert.AreEqual("root", rootArticle.UrlPath);

            try
            {
                await Logic.DeleteArticle(rootArticle.ArticleNumber);
                Assert.Fail("Expected NotSupportedException was not thrown.");
            }
            catch (NotSupportedException ex)
            {
                Assert.AreEqual("Cannot trash the home page. Replace it then delete.", ex.Message);
            }
        }

        #endregion
    }
}