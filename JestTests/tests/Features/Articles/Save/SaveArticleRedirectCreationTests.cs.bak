// <copyright file="SaveArticleRedirectCreationTests.cs" company="Moonrise Software, LLC">
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
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for redirect creation during title changes, focusing on published status tracking
    /// for parent articles, child articles, and blog posts.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleRedirectCreationTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        [TestCategory("Redirects")]
        [TestCategory("Hierarchy")]
        public async Task SaveArticle_ParentTitleChange_OnlyPublishedChildrenGetRedirects()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create parent article
            var parent = await Logic.CreateArticle("Parent Article", TestUserId);
            await Logic.PublishArticle(parent.Id, DateTimeOffset.UtcNow);
            
            // Create published child
            var publishedChild = await Logic.CreateArticle("Published Child", TestUserId);
            var publishedChildArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == publishedChild.ArticleNumber);
            publishedChildArticle.UrlPath = "parent-article/published-child";
            await Db.SaveChangesAsync();
            await Logic.PublishArticle(publishedChild.Id, DateTimeOffset.UtcNow);
            
            // Create unpublished child
            var unpublishedChild = await Logic.CreateArticle("Unpublished Child", TestUserId);
            var unpublishedChildArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == unpublishedChild.ArticleNumber);
            unpublishedChildArticle.UrlPath = "parent-article/unpublished-child";
            await Db.SaveChangesAsync();
            // Don't publish this one

            var savedParent = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == parent.ArticleNumber);

            // Act - Change parent title
            var command = new SaveArticleCommand
            {
                ArticleNumber = parent.ArticleNumber,
                Title = "Renamed Parent Article",
                Content = string.IsNullOrWhiteSpace(savedParent.Content) ? "<p>Content</p>" : savedParent.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedParent.Published
            };
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Should have 2 redirects: one for parent, one for published child only
            var redirects = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            Assert.AreEqual(2, redirects.Count, "Should create redirects for parent and published child only");

            // Verify parent redirect exists
            var parentRedirect = redirects.FirstOrDefault(r => r.UrlPath == "parent-article");
            Assert.IsNotNull(parentRedirect, "Parent redirect should exist");
            Assert.AreEqual("renamed-parent-article", parentRedirect.RedirectTarget);

            // Verify published child redirect exists
            var childRedirect = redirects.FirstOrDefault(r => r.UrlPath == "parent-article/published-child");
            Assert.IsNotNull(childRedirect, "Published child redirect should exist");
            Assert.AreEqual("renamed-parent-article/published-child", childRedirect.RedirectTarget);

            // Verify unpublished child does NOT have a redirect
            var unpublishedRedirect = redirects.FirstOrDefault(r => r.UrlPath.Contains("unpublished-child"));
            Assert.IsNull(unpublishedRedirect, "Unpublished child should NOT have a redirect");
        }

        [TestMethod]
        [TestCategory("Redirects")]
        [TestCategory("BlogPosts")]
        public async Task SaveArticle_BlogStreamTitleChange_OnlyPublishedPostsGetRedirects()
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
            
            // Create published blog post
            var publishedPost = await Logic.CreateArticle("Published Post", TestUserId);
            var publishedPostArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == publishedPost.ArticleNumber);
            publishedPostArticle.ArticleType = (int)ArticleType.BlogPost;
            publishedPostArticle.BlogKey = "tech-blog";
            publishedPostArticle.UrlPath = "tech-blog/published-post";
            await Db.SaveChangesAsync();
            await Logic.PublishArticle(publishedPost.Id, DateTimeOffset.UtcNow);
            
            // Create unpublished blog post
            var unpublishedPost = await Logic.CreateArticle("Draft Post", TestUserId);
            var unpublishedPostArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == unpublishedPost.ArticleNumber);
            unpublishedPostArticle.ArticleType = (int)ArticleType.BlogPost;
            unpublishedPostArticle.BlogKey = "tech-blog";
            unpublishedPostArticle.UrlPath = "tech-blog/draft-post";
            await Db.SaveChangesAsync();
            // Don't publish this one

            var savedBlogStream = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == blogStream.ArticleNumber);

            // Act - Change blog stream title
            var command = new SaveArticleCommand
            {
                ArticleNumber = blogStream.ArticleNumber,
                Title = "Technology Blog",
                Content = string.IsNullOrWhiteSpace(savedBlogStream.Content) ? "<p>Content</p>" : savedBlogStream.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.BlogStream,
                Published = savedBlogStream.Published
            };
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Should have 2 redirects: one for blog stream, one for published post only
            var redirects = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            Assert.AreEqual(2, redirects.Count, "Should create redirects for blog stream and published post only");

            // Verify blog stream redirect exists
            var streamRedirect = redirects.FirstOrDefault(r => r.UrlPath == "tech-blog");
            Assert.IsNotNull(streamRedirect, "Blog stream redirect should exist");
            Assert.AreEqual("technology-blog", streamRedirect.RedirectTarget);

            // Verify published post redirect exists
            var postRedirect = redirects.FirstOrDefault(r => r.UrlPath == "tech-blog/published-post");
            Assert.IsNotNull(postRedirect, "Published post redirect should exist");
            Assert.AreEqual("technology-blog/published-post", postRedirect.RedirectTarget);

            // Verify unpublished post does NOT have a redirect
            var draftRedirect = redirects.FirstOrDefault(r => r.UrlPath.Contains("draft-post"));
            Assert.IsNull(draftRedirect, "Draft post should NOT have a redirect");
        }

        [TestMethod]
        [TestCategory("Redirects")]
        [TestCategory("Hierarchy")]
        public async Task SaveArticle_UnpublishedParentTitleChange_NoRedirectsCreated()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create unpublished parent article
            var parent = await Logic.CreateArticle("Draft Parent", TestUserId);
            // Don't publish it
            
            // Create child that is published (edge case)
            var publishedChild = await Logic.CreateArticle("Published Child", TestUserId);
            var publishedChildArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == publishedChild.ArticleNumber);
            publishedChildArticle.UrlPath = "draft-parent/published-child";
            await Db.SaveChangesAsync();
            await Logic.PublishArticle(publishedChild.Id, DateTimeOffset.UtcNow);

            var savedParent = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == parent.ArticleNumber);

            // Act - Change unpublished parent title
            var command = new SaveArticleCommand
            {
                ArticleNumber = parent.ArticleNumber,
                Title = "Renamed Draft Parent",
                Content = string.IsNullOrWhiteSpace(savedParent.Content) ? "<p>Content</p>" : savedParent.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = null // Unpublished
            };
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Should have 1 redirect for the published child only (not for unpublished parent)
            var redirects = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            Assert.AreEqual(1, redirects.Count, "Should only create redirect for published child");

            // Verify child redirect exists
            var childRedirect = redirects.FirstOrDefault();
            Assert.IsNotNull(childRedirect);
            Assert.AreEqual("draft-parent/published-child", childRedirect.UrlPath);
            Assert.AreEqual("renamed-draft-parent/published-child", childRedirect.RedirectTarget);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task SaveArticle_TitleChange_DuplicateUrlHandling()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create an article and change its title multiple times rapidly
            var article = await Logic.CreateArticle("Original", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var savedArticle = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);

            // First change
            var command1 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };
            await SaveArticleHandler.HandleAsync(command1);

            // Second change (back to similar name, different slug due to normalization)
            var command2 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated Again",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };
            var result = await SaveArticleHandler.HandleAsync(command2);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Should have 2 unique redirects
            var redirects = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            Assert.AreEqual(2, redirects.Count);

            // Verify no duplicate old URLs
            var oldUrls = redirects.Select(r => r.UrlPath).ToList();
            Assert.AreEqual(2, oldUrls.Distinct().Count(), "Should not have duplicate redirect source URLs");
        }

        [TestMethod]
        [TestCategory("Redirects")]
        [TestCategory("Hierarchy")]
        public async Task SaveArticle_NestedChildren_AllPublishedGetRedirects()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create parent
            var parent = await Logic.CreateArticle("Docs", TestUserId);
            await Logic.PublishArticle(parent.Id, DateTimeOffset.UtcNow);
            
            // Create first level child
            var child1 = await Logic.CreateArticle("API", TestUserId);
            var child1Article = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == child1.ArticleNumber);
            child1Article.UrlPath = "docs/api";
            await Db.SaveChangesAsync();
            await Logic.PublishArticle(child1.Id, DateTimeOffset.UtcNow);
            
            // Create second level child
            var child2 = await Logic.CreateArticle("Methods", TestUserId);
            var child2Article = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == child2.ArticleNumber);
            child2Article.UrlPath = "docs/api/methods";
            await Db.SaveChangesAsync();
            await Logic.PublishArticle(child2.Id, DateTimeOffset.UtcNow);

            var savedParent = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == parent.ArticleNumber);

            // Act - Change parent title
            var command = new SaveArticleCommand
            {
                ArticleNumber = parent.ArticleNumber,
                Title = "Documentation",
                Content = string.IsNullOrWhiteSpace(savedParent.Content) ? "<p>Content</p>" : savedParent.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedParent.Published
            };
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Should have 3 redirects: parent + 2 nested children
            var redirects = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .OrderBy(a => a.UrlPath)
                .ToListAsync();

            Assert.AreEqual(3, redirects.Count, "Should create redirects for all published articles in hierarchy");

            // Verify all redirects
            Assert.IsNotNull(redirects.FirstOrDefault(r => r.UrlPath == "docs" && r.RedirectTarget == "documentation"));
            Assert.IsNotNull(redirects.FirstOrDefault(r => r.UrlPath == "docs/api" && r.RedirectTarget == "documentation/api"));
            Assert.IsNotNull(redirects.FirstOrDefault(r => r.UrlPath == "docs/api/methods" && r.RedirectTarget == "documentation/api/methods"));
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task SaveArticle_RedirectChainPrevention_RedirectsToFinalDestination()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create article
            var article = await Logic.CreateArticle("First Title", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var savedArticle = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);

            // First title change (creates redirect: first-title -> second-title)
            var command1 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Second Title",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };
            await SaveArticleHandler.HandleAsync(command1);

            // Second title change (should create redirect: first-title -> third-title, NOT first-title -> second-title -> third-title)
            var command2 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Third Title",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };
            var result = await SaveArticleHandler.HandleAsync(command2);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Check redirects
            var redirects = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .ToListAsync();

            // Should have 2 redirects
            Assert.AreEqual(2, redirects.Count, "Should have 2 redirects");

            // Verify first-title redirects directly to third-title (chain resolved)
            var firstRedirect = redirects.FirstOrDefault(r => r.UrlPath == "first-title");
            Assert.IsNotNull(firstRedirect, "first-title redirect should exist");
            Assert.AreEqual("third-title", firstRedirect.RedirectTarget, "first-title should redirect directly to third-title (chain resolved)");

            // Verify second-title redirects to third-title
            var secondRedirect = redirects.FirstOrDefault(r => r.UrlPath == "second-title");
            Assert.IsNotNull(secondRedirect, "second-title redirect should exist");
            Assert.AreEqual("third-title", secondRedirect.RedirectTarget);
        }
    }
}
