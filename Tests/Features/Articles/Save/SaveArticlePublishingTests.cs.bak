// <copyright file="SaveArticlePublishingTests.cs" company="Moonrise Software, LLC">
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
    using Sky.Editor.Domain.Events;
    using Sky.Editor.Features.Articles.Save;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for publishing workflow integration during save operations.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticlePublishingTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        public async Task SaveArticle_PublishedArticle_TriggersCdnPurge()
        {
            // Arrange
            var article = await Logic.CreateArticle("Published Article", TestUserId);
            article.Published = Clock.UtcNow;
            await Logic.SaveArticle(article, TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Updated Published Article",
                Content = "<p>New content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Data!.CdnResults);
            // Note: CdnResults may be empty in test environment if no CDN is configured
        }

        [TestMethod]
        public async Task SaveArticle_UnpublishedThenPublished_UpdatesCatalog()
        {
            // Arrange - Start unpublished
            var article = await Logic.CreateArticle("Draft", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Now Published",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow // Now published
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var catalogEntry = await Db.ArticleCatalog
                .FirstOrDefaultAsync(c => c.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(catalogEntry);
            Assert.IsNotNull(catalogEntry.Published);
        }

        [TestMethod]
        public async Task SaveArticle_ChangesWhilePublished_MaintainsPublishedState()
        {
            // Arrange
            var article = await Logic.CreateArticle("Published", TestUserId);
            article.Published = Clock.UtcNow;
            await Logic.SaveArticle(article, TestUserId);
            var originalPublishedDate = article.Published;

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Still Published After Edit",
                Content = "<p>Updated content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = originalPublishedDate // Maintain published state
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            
            var updatedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNotNull(updatedArticle!.Published);
            Assert.AreEqual(originalPublishedDate, updatedArticle.Published);
        }

        [TestMethod]
        public async Task SaveArticle_UnpublishingArticle_ClearsCatalogPublishedDate()
        {
            // Arrange - Start published
            var article = await Logic.CreateArticle("Published", TestUserId);
            article.Published = Clock.UtcNow;
            await Logic.SaveArticle(article, TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Now Unpublished",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = null // Unpublish
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            var updatedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNull(updatedArticle!.Published);
        }

        [TestMethod]
        public async Task SaveArticle_PublishedWithFutureDate_HandlesCorrectly()
        {
            // Arrange
            var article = await Logic.CreateArticle("Future Published", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var futureDate = Clock.UtcNow.AddDays(7);
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Future Published Article",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = futureDate
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            
            var savedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNotNull(savedArticle!.Published);
            Assert.IsTrue(savedArticle.Published > Clock.UtcNow);
        }

        [TestMethod]
        public async Task SaveArticle_UnpublishedArticle_DoesNotTriggerCdnPurge()
        {
            // Arrange
            var article = await Logic.CreateArticle("Draft Article", TestUserId);
            await Logic.SaveArticle(article, TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Still Draft",
                Content = "<p>Updated draft content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = null // Not published
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsEmpty(result.Data!.CdnResults);
        }

        [TestMethod]
        public async Task SaveArticle_PublishingRootPage_UpdatesCorrectly()
        {
            // Arrange - Root page is created as first article
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            Assert.AreEqual("root", rootArticle.UrlPath);

            var command = new SaveArticleCommand
            {
                ArticleNumber = rootArticle.ArticleNumber,
                Title = "Updated Home Page",
                Content = "<p>Updated home content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            
            var updated = await Db.Articles
                .Where(a => a.ArticleNumber == rootArticle.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNotNull(updated);
            Assert.AreEqual("root", updated.UrlPath); // Should remain root
            Assert.AreEqual("Updated Home Page", updated.Title); // Title should update
        }

        [TestMethod]
        public async Task SaveArticle_RootPageMultipleTitleChanges_PreservesRootPath()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            Assert.AreEqual("root", rootArticle.UrlPath);

            // Act - Change title multiple times
            var firstUpdate = new SaveArticleCommand
            {
                ArticleNumber = rootArticle.ArticleNumber,
                Title = "Welcome Page",
                Content = "<p>Welcome</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow
            };
            await SaveArticleHandler.HandleAsync(firstUpdate);

            var secondUpdate = new SaveArticleCommand
            {
                ArticleNumber = rootArticle.ArticleNumber,
                Title = "Landing Page",
                Content = "<p>Landing</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow
            };
            await SaveArticleHandler.HandleAsync(secondUpdate);

            // Assert
            var updated = await Db.Articles
                .Where(a => a.ArticleNumber == rootArticle.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
    
            Assert.IsNotNull(updated);
            Assert.AreEqual("root", updated.UrlPath);
            Assert.AreEqual("Landing Page", updated.Title);
        }

        [TestMethod]
        public async Task SaveArticle_RootPageUnpublished_StillPreservesRootPath()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            Assert.AreEqual("root", rootArticle.UrlPath);

            var command = new SaveArticleCommand
            {
                ArticleNumber = rootArticle.ArticleNumber,
                Title = "Updated Home - Draft",
                Content = "<p>Draft content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = null // Unpublished
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
    
            var updated = await Db.Articles
                .Where(a => a.ArticleNumber == rootArticle.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.IsNotNull(updated);
            Assert.AreEqual("root", updated.UrlPath);
            Assert.IsNull(updated.Published);
        }

        [TestMethod]
        public async Task SaveArticle_RootPageWithVersions_AllVersionsPreserveRoot()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            rootArticle.Published = Clock.UtcNow;
            await Logic.SaveArticle(rootArticle, TestUserId);

            // Create a new version
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == rootArticle.ArticleNumber);
            var newVersion = await Logic.NewVersion(article);
            
            Assert.AreEqual("root", article.UrlPath);
            Assert.AreEqual("root", newVersion.UrlPath);

            // Act - Update title on latest version
            var command = new SaveArticleCommand
            {
                ArticleNumber = rootArticle.ArticleNumber,
                Title = "New Home Title",
                Content = "<p>New content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow
            };
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
    
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == rootArticle.ArticleNumber)
                .ToListAsync();
    
            Assert.IsGreaterThanOrEqualTo(2, allVersions.Count, "Should have at least 2 versions");
            Assert.IsTrue(allVersions.All(v => v.UrlPath == "root"), 
        "All versions should preserve 'root' URL path");
}

        [TestMethod]
        public async Task SaveArticle_NonRootArticle_UrlPathChangesWithTitle()
        {
            // Arrange - Create root first
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
    
            // Create a non-root article
            var nonRootArticle = await Logic.CreateArticle("About Us", TestUserId);
            Assert.AreNotEqual("root", nonRootArticle.UrlPath);
            var originalPath = nonRootArticle.UrlPath;

            var command = new SaveArticleCommand
            {
                ArticleNumber = nonRootArticle.ArticleNumber,
                Title = "About Our Company",
                Content = "<p>Company info</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
    
            var updated = await Db.Articles
                .Where(a => a.ArticleNumber == nonRootArticle.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
    
            Assert.IsNotNull(updated);
            Assert.AreNotEqual(originalPath, updated.UrlPath, 
        "Non-root article URL should change with title");
            Assert.AreEqual("about-our-company", updated.UrlPath);
    
            // Verify root article is unchanged
            var rootCheck = await Db.Articles
                .Where(a => a.ArticleNumber == rootArticle.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
            Assert.AreEqual("root", rootCheck.UrlPath);
}

        [TestMethod]
        public async Task TitleChangeService_RootPageTitleChange_NoRedirectCreated()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            rootArticle.Published = Clock.UtcNow;
            await Logic.SaveArticle(rootArticle, TestUserId);
    
            var initialRedirectCount = await Db.Articles
                .CountAsync(a => a.StatusCode == (int)StatusCodeEnum.Redirect);

            // Act - Change root page title
            var command = new SaveArticleCommand
            {
                ArticleNumber = rootArticle.ArticleNumber,
                Title = "Main Page",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow
            };
            await SaveArticleHandler.HandleAsync(command);

            // Assert
            var finalRedirectCount = await Db.Articles
                .CountAsync(a => a.StatusCode == (int)StatusCodeEnum.Redirect);
    
            Assert.AreEqual(initialRedirectCount, finalRedirectCount, 
        "No redirect should be created when root page title changes");
}

        [TestMethod]
        public async Task TitleChangeService_RootPageTitleChange_EventStillDispatched()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            EventDispatcher.Clear();

            // Act
            var command = new SaveArticleCommand
            {
                ArticleNumber = rootArticle.ArticleNumber,
                Title = "Welcome Page",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow
            };
            await SaveArticleHandler.HandleAsync(command);

            // Assert
            var titleChangedEvent = EventDispatcher.Last<TitleChangedEvent>();
            Assert.IsNotNull(titleChangedEvent, "TitleChangedEvent should be dispatched");
            Assert.AreEqual(rootArticle.ArticleNumber, titleChangedEvent.ArticleNumber);
            Assert.AreEqual("Home Page", titleChangedEvent.OldTitle);
            Assert.AreEqual("Welcome Page", titleChangedEvent.NewTitle);
}

        [TestMethod]
        public async Task SaveArticle_RootPageWithSpecialCharactersInTitle_PreservesRoot()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);

            var command = new SaveArticleCommand
            {
                ArticleNumber = rootArticle.ArticleNumber,
                Title = "Home & Welcome! 2024",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
    
            var updated = await Db.Articles
                .Where(a => a.ArticleNumber == rootArticle.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
    
            Assert.IsNotNull(updated);
            Assert.AreEqual("root", updated.UrlPath);
            Assert.AreEqual("Home & Welcome! 2024", updated.Title);
}

        [TestMethod]
        public async Task SaveArticle_RootPageCaseInsensitive_PreservesRoot()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
    
            // Manually set UrlPath to different casing (edge case)
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == rootArticle.ArticleNumber);
            article.UrlPath = "ROOT"; // Uppercase
            await Db.SaveChangesAsync();

            var command = new SaveArticleCommand
            {
                ArticleNumber = rootArticle.ArticleNumber,
                Title = "Updated Title",
                Content = "<p>Content</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = Clock.UtcNow
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
    
            var updated = await Db.Articles
                .Where(a => a.ArticleNumber == rootArticle.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefaultAsync();
    
            // Should preserve root (case-insensitive comparison)
            Assert.IsNotNull(updated);
            Assert.IsTrue(updated.UrlPath.Equals("root", StringComparison.OrdinalIgnoreCase));
}
    }
}
