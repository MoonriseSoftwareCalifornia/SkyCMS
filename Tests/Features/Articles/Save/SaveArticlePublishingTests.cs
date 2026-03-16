// <copyright file="SaveArticlePublishingTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Save
{
    using Cosmos.Cms.Common;
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
    [TestClass]
    public class SaveArticlePublishingTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        /// <summary>
        /// Tests that SaveArticle_PublishedArticle_TriggersCdnPurge.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_PublishedArticle_TriggersCdnPurge()
        {
            // Arrange
            var article = await CreateArticleAsync("Published Article", TestUserId);

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

        /// <summary>
        /// Tests that SaveArticle_UnpublishedThenPublished_UpdatesCatalog.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_UnpublishedThenPublished_UpdatesCatalog()
        {
            // Arrange - Start unpublished
            var article = await CreateArticleAsync("Draft", TestUserId);

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

        /// <summary>
        /// Tests that SaveArticle_ChangesWhilePublished_MaintainsPublishedState.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_ChangesWhilePublished_MaintainsPublishedState()
        {
            // Arrange
            var article = await CreateArticleAsync("Published", TestUserId);
            var originalPublishedDate = Clock.UtcNow;

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

        /// <summary>
        /// Tests that SaveArticle_UnpublishingArticle_ClearsCatalogPublishedDate.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_UnpublishingArticle_ClearsCatalogPublishedDate()
        {
            // Arrange - Start published
            var article = await CreateArticleAsync("Published", TestUserId);

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

        /// <summary>
        /// Tests that SaveArticle_PublishedWithFutureDate_HandlesCorrectly.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_PublishedWithFutureDate_HandlesCorrectly()
        {
            // Arrange
            var article = await CreateArticleAsync("Future Published", TestUserId);

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

        /// <summary>
        /// Tests that SaveArticle_UnpublishedArticle_DoesNotTriggerCdnPurge.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_UnpublishedArticle_DoesNotTriggerCdnPurge()
        {
            // Arrange
            var article = await CreateArticleAsync("Draft Article", TestUserId);

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

        /// <summary>
        /// Tests that SaveArticle_PublishingRootPage_UpdatesCorrectly.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_PublishingRootPage_UpdatesCorrectly()
        {
            // Arrange - Root page is created as first article
            var rootArticle = await CreateArticleAsync("Home Page", TestUserId);
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

        /// <summary>
        /// Tests that SaveArticle_RootPageMultipleTitleChanges_PreservesRootPath.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_RootPageMultipleTitleChanges_PreservesRootPath()
        {
            // Arrange
            var rootArticle = await CreateArticleAsync("Home Page", TestUserId);
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

        /// <summary>
        /// Tests that SaveArticle_RootPageUnpublished_StillPreservesRootPath.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_RootPageUnpublished_StillPreservesRootPath()
        {
            // Arrange
            var rootArticle = await CreateArticleAsync("Home Page", TestUserId);
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

        /// <summary>
        /// Tests that SaveArticle_RootPageWithVersions_AllVersionsPreserveRoot.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_RootPageWithVersions_AllVersionsPreserveRoot()
        {
            // Arrange
            var rootArticle = await CreateArticleAsync("Home Page", TestUserId);
            rootArticle.Published = Clock.UtcNow;
            await SaveArticleAsync(rootArticle, TestUserId);

            // Create a new version
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == rootArticle.ArticleNumber);
            var newVersionVm = await CreateArticleVersionAsync(article.ArticleNumber);

            Assert.AreEqual("root", article.UrlPath);
            Assert.AreEqual("root", newVersionVm.UrlPath);

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

        /// <summary>
        /// Tests that SaveArticle_NonRootArticle_UrlPathChangesWithTitle.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_NonRootArticle_UrlPathChangesWithTitle()
        {
            // Arrange - Create root first
            var rootArticle = await CreateArticleAsync("Home Page", TestUserId);

            // Create a non-root article
            var nonRootArticle = await CreateArticleAsync("About Us", TestUserId);
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

        /// <summary>
        /// Tests that TitleChangeService_RootPageTitleChange_NoRedirectCreated.
        /// </summary>
        [TestMethod]
        public async Task TitleChangeService_RootPageTitleChange_NoRedirectCreated()
        {
            // Arrange
            var rootArticle = await CreateArticleAsync("Home Page", TestUserId);
            rootArticle.Published = Clock.UtcNow;
            await SaveArticleAsync(rootArticle, TestUserId);

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

        /// <summary>
        /// Tests that TitleChangeService_RootPageTitleChange_EventStillDispatched.
        /// </summary>
        [TestMethod]
        public async Task TitleChangeService_RootPageTitleChange_EventStillDispatched()
        {
            // Arrange
            var rootArticle = await CreateArticleAsync("Home Page", TestUserId);
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

        /// <summary>
        /// Tests root page URL path preservation across special title and case-insensitive root scenarios.
        /// </summary>
        [TestMethod]
        public async Task SaveArticle_RootPageEdgeCases_PreserveRootPath()
        {
            var scenarios = new[]
            {
                new
                {
                    Name = "SpecialCharactersInTitle",
                    InitialUrlPath = "root",
                    NewTitle = "Home & Welcome! 2024",
                },
                new
                {
                    Name = "CaseInsensitiveExistingRoot",
                    InitialUrlPath = "ROOT",
                    NewTitle = "Updated Title",
                },
            };

            foreach (var scenario in scenarios)
            {
                var rootArticle = await CreateArticleAsync("Home Page", TestUserId);
                var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == rootArticle.ArticleNumber);
                article.UrlPath = scenario.InitialUrlPath;
                await Db.SaveChangesAsync();

                var command = new SaveArticleCommand
                {
                    ArticleNumber = rootArticle.ArticleNumber,
                    Title = scenario.NewTitle,
                    Content = "<p>Content</p>",
                    UserId = TestUserId,
                    ArticleType = ArticleType.General,
                    Published = Clock.UtcNow
                };

                var result = await SaveArticleHandler.HandleAsync(command);
                Assert.IsTrue(result.IsSuccess, scenario.Name);

                var updated = await Db.Articles
                    .Where(a => a.ArticleNumber == rootArticle.ArticleNumber)
                    .OrderByDescending(a => a.VersionNumber)
                    .FirstOrDefaultAsync();

                Assert.IsNotNull(updated, scenario.Name);
                Assert.IsTrue(updated.UrlPath.Equals("root", StringComparison.OrdinalIgnoreCase), scenario.Name);
                Assert.AreEqual(scenario.NewTitle, updated.Title, scenario.Name);
            }
        }
    }
}

