// <copyright file="SaveArticleTransactionTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Save
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Save;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for transaction handling during article title changes.
    /// Verifies that operations are atomic and rollback correctly on failures.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleTransactionTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        public async Task SaveArticle_TitleChange_SlugConflict_RollsBackChanges()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create first article
            var article1 = await Logic.CreateArticle("First Article", TestUserId);
            await Logic.PublishArticle(article1.Id, DateTimeOffset.UtcNow);
            
            // Create second article
            var article2 = await Logic.CreateArticle("Second Article", TestUserId);
            await Logic.PublishArticle(article2.Id, DateTimeOffset.UtcNow);
            
            // Get the original state of article2
            var originalArticle2 = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == article2.ArticleNumber);
            var originalTitle = originalArticle2.Title;
            var originalUrlPath = originalArticle2.UrlPath;

            // Act - Try to change article2's title to conflict with article1
            var command = new SaveArticleCommand
            {
                ArticleNumber = article2.ArticleNumber,
                Title = "First Article", // This will create a slug conflict!
                Content = string.IsNullOrWhiteSpace(originalArticle2.Content) ? "<p>Content</p>" : originalArticle2.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = originalArticle2.Published
            };

            Exception caughtException = null;
            try
            {
                await SaveArticleHandler.HandleAsync(command);
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert
            Assert.IsNotNull(caughtException, "Should throw exception for slug conflict");
            Assert.IsInstanceOfType(caughtException, typeof(InvalidOperationException));
            Assert.IsTrue(caughtException.Message.Contains("already in use"), "Error message should indicate slug is in use");

            // Verify article2 was NOT modified (transaction rolled back)
            var unchangedArticle2 = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article2.ArticleNumber);
            
            Assert.IsNotNull(unchangedArticle2);
            Assert.AreEqual(originalTitle, unchangedArticle2.Title, "Title should not have changed");
            Assert.AreEqual(originalUrlPath, unchangedArticle2.UrlPath, "UrlPath should not have changed");
        }

        [TestMethod]
        public async Task SaveArticle_TitleChange_Success_CommitsAllChanges()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create parent with child
            var parent = await Logic.CreateArticle("Parent", TestUserId);
            await Logic.PublishArticle(parent.Id, DateTimeOffset.UtcNow);
            
            var child = await Logic.CreateArticle("Child", TestUserId);
            var childArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == child.ArticleNumber);
            childArticle.UrlPath = "parent/child";
            await Db.SaveChangesAsync();
            await Logic.PublishArticle(child.Id, DateTimeOffset.UtcNow);

            var savedParent = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == parent.ArticleNumber);

            // Act - Change parent title (should cascade to child)
            var command = new SaveArticleCommand
            {
                ArticleNumber = parent.ArticleNumber,
                Title = "New Parent",
                Content = string.IsNullOrWhiteSpace(savedParent.Content) ? "<p>Content</p>" : savedParent.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedParent.Published
            };

            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Verify parent was updated
            var updatedParent = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == parent.ArticleNumber);
            Assert.AreEqual("New Parent", updatedParent.Title);
            Assert.AreEqual("new-parent", updatedParent.UrlPath);

            // Verify child was updated
            var updatedChild = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == child.ArticleNumber);
            Assert.AreEqual("new-parent/child", updatedChild.UrlPath);

            // Verify redirects were created for both
            var redirects = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .ToListAsync();
            
            Assert.AreEqual(2, redirects.Count, "Should have redirects for both parent and child");
            Assert.IsTrue(redirects.Any(r => r.UrlPath == "parent"), "Should have redirect from old parent URL");
            Assert.IsTrue(redirects.Any(r => r.UrlPath == "parent/child"), "Should have redirect from old child URL");
        }

        [TestMethod]
        public async Task SaveArticle_TitleChange_MultipleVersions_AllUpdatedAtomically()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create article with multiple versions
            var article = await Logic.CreateArticle("Version 1", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            // Create a second version (draft)
            var version2 = await Logic.CreateArticle("Version 1", TestUserId); // Same title, different version
            var version2Article = await Db.Articles
                .FirstOrDefaultAsync(a => a.Id == version2.Id);
            version2Article.ArticleNumber = article.ArticleNumber; // Make it a version of the first article
            await Db.SaveChangesAsync();
            
            // Count versions before
            var versionsBefore = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .ToListAsync();
            Assert.AreEqual(2, versionsBefore.Count, "Should have 2 versions before title change");

            var savedArticle = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber && a.Published != null);

            // Act - Change title
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Version 2",
                Content = string.IsNullOrWhiteSpace(savedArticle.Content) ? "<p>Content</p>" : savedArticle.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };

            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Verify all versions were updated
            var versionsAfter = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .ToListAsync();

            Assert.AreEqual(2, versionsAfter.Count, "Should still have 2 versions");
            
            foreach (var version in versionsAfter)
            {
                Assert.AreEqual("Version 2", version.Title, "All versions should have updated title");
                Assert.AreEqual("version-2", version.UrlPath, "All versions should have updated URL path");
            }
        }

        [TestMethod]
        public async Task SaveArticle_BlogStreamTitleChange_TransactionIncludesAllPosts()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create blog stream
            var blogStream = await Logic.CreateArticle("Tech Blog", TestUserId);
            var blogStreamArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == blogStream.ArticleNumber);
            blogStreamArticle.ArticleType = (int)ArticleType.BlogStream;
            blogStreamArticle.BlogKey = "tech-blog";
            blogStreamArticle.UrlPath = "tech-blog";
            await Db.SaveChangesAsync();
            await Logic.PublishArticle(blogStream.Id, DateTimeOffset.UtcNow);
            
            // Create multiple blog posts
            for (int i = 1; i <= 3; i++)
            {
                var post = await Logic.CreateArticle($"Post {i}", TestUserId);
                var postArticle = await Db.Articles
                    .FirstOrDefaultAsync(a => a.ArticleNumber == post.ArticleNumber);
                postArticle.ArticleType = (int)ArticleType.BlogPost;
                postArticle.BlogKey = "tech-blog";
                postArticle.UrlPath = $"tech-blog/post-{i}";
                await Db.SaveChangesAsync();
                await Logic.PublishArticle(post.Id, DateTimeOffset.UtcNow);
            }

            var savedBlogStream = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == blogStream.ArticleNumber);

            // Act - Change blog stream title
            var command = new SaveArticleCommand
            {
                ArticleNumber = blogStream.ArticleNumber,
                Title = "Technology News",
                Content = string.IsNullOrWhiteSpace(savedBlogStream.Content) ? "<p>Content</p>" : savedBlogStream.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.BlogStream,
                Published = savedBlogStream.Published
            };

            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Verify blog stream was updated
            var updatedStream = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == blogStream.ArticleNumber);
            Assert.AreEqual("Technology News", updatedStream.Title);
            Assert.AreEqual("technology-news", updatedStream.UrlPath);
            Assert.AreEqual("technology-news", updatedStream.BlogKey);

            // Verify all posts were updated
            var updatedPosts = await Db.Articles
                .Where(a => a.BlogKey == "technology-news" && a.ArticleType == (int)ArticleType.BlogPost)
                .ToListAsync();
            
            Assert.AreEqual(3, updatedPosts.Count, "All posts should be updated");
            
            foreach (var post in updatedPosts)
            {
                Assert.AreEqual("technology-news", post.BlogKey, "All posts should have new blog key");
                Assert.IsTrue(post.UrlPath.StartsWith("technology-news/"), "All posts should have updated URL path");
            }

            // Verify redirects created for stream and all posts
            var redirects = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .ToListAsync();
            
            Assert.AreEqual(4, redirects.Count, "Should have redirects for stream + 3 posts");
        }
    }
}
