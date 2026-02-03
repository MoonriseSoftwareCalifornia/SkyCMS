// <copyright file="PublishingServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Publishing
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Services.Publishing;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using Sky.Cms.Services;
    using Cosmos.BlobService;

    /// <summary>
    /// Unit tests for the <see cref="PublishingService"/> class.
    /// </summary>
    [TestClass]
    public class PublishingServiceTests : SkyCmsTestBase
    {
        private IServiceProvider _serviceProvider;
        private Mock<IViewRenderService> _mockViewRenderService;

        /// <summary>
        /// Initializes the test context.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            InitializeTestContext();

            // Setup mock for IViewRenderService
            _mockViewRenderService = new Mock<IViewRenderService>();
            _mockViewRenderService
                .Setup(x => x.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html>Mocked HTML Content</html>");

            var services = new ServiceCollection();
            services.AddScoped<IViewRenderService>(_ => _mockViewRenderService.Object);
            services.AddScoped<StorageContext>(_ => Storage);
            services.AddLogging();
            
            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Cleans up test resources after each test method.
        /// </summary>
        [TestCleanup]
        public void Cleanup() => Db.Dispose();

        /// <summary>
        /// Tests that PublishAsync sets the publish date when it is null.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_WhenPublishedIsNull_SetsPublishDate()
        {
            // Arrange
            var article = CreateTestArticle();
            article.Published = null;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(article);

            // Assert
            Assert.IsNotNull(article.Published, "Published date should not be null after publishing");
            Assert.IsTrue(article.Published <= Clock.UtcNow, "Published date should be less than or equal to current time");
        }

        /// <summary>
        /// Tests that PublishAsync unpublishes earlier versions of the same article.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_WhenNewerVersionPublished_UnpublishesEarlierVersions()
        {
            // Arrange
            var articleNumber = 1;
            var oldVersion = CreateTestArticle(articleNumber, 1);
            oldVersion.Published = Clock.UtcNow.AddDays(-1);
            var newVersion = CreateTestArticle(articleNumber, 2);
            newVersion.Published = Clock.UtcNow;

            Db.Articles.AddRange(oldVersion, newVersion);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(newVersion);

            // Assert
            var updatedOldVersion = await Db.Articles.FindAsync(oldVersion.Id);
            Assert.IsNull(updatedOldVersion.Published, "Earlier version should be unpublished");
        }

        /// <summary>
        /// Tests that PublishAsync removes prior non-redirect pages for the same article.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_RemovesPriorNonRedirectPages()
        {
            // Arrange
            var article = CreateTestArticle();
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);

            var priorPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = article.ArticleNumber,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = article.UrlPath,
                Title = "Old Page",
                Content = "Old Content"
            };
            Db.Pages.Add(priorPage);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(article);

            // Assert
            var pageCount = await Db.Pages.CountAsync(p => p.Id == priorPage.Id);
            Assert.AreEqual(0, pageCount, "Prior non-redirect page should be removed");
        }

        /// <summary>
        /// Tests that PublishAsync preserves redirect pages.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_PreservesRedirectPages()
        {
            // Arrange
            var article = CreateTestArticle();
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);

            var redirectPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = article.ArticleNumber,
                StatusCode = (int)StatusCodeEnum.Redirect,
                UrlPath = article.UrlPath,
                Title = "Redirect",
                Content = "/new-location"
            };
            Db.Pages.Add(redirectPage);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(article);

            // Assert
            var redirectStillExists = await Db.Pages.AnyAsync(p => p.Id == redirectPage.Id);
            Assert.IsTrue(redirectStillExists, "Redirect page should be preserved");
        }

        /// <summary>
        /// Tests that PublishAsync creates a new published page with correct properties.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_CreatesNewPublishedPage()
        {
            // Arrange
            var article = CreateTestArticle();
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(article);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page, "Published page should be created");
            Assert.AreEqual(article.Title, page.Title, "Page title should match article title");
            Assert.AreEqual(article.Content, page.Content, "Page content should match article content");
            Assert.AreEqual(article.UrlPath, page.UrlPath, "Page URL path should match article URL path");
        }

        /// <summary>
        /// Tests that PublishAsync sets ParentUrlPath correctly for nested pages.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_SetsParentUrlPathForNestedPages()
        {
            // Arrange
            var article = CreateTestArticle();
            article.UrlPath = "parent/child";
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(article);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page, "Page should exist");
            Assert.AreEqual("parent", page.ParentUrlPath, "Parent URL path should be set correctly");
        }

        /// <summary>
        /// Tests that PublishAsync sets empty ParentUrlPath for root-level pages.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_SetsEmptyParentUrlPathForRootPages()
        {
            // Arrange
            var article = CreateTestArticle();
            article.UrlPath = "root";
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(article);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.IsNotNull(page, "Page should exist");
            Assert.AreEqual(string.Empty, page.ParentUrlPath, "Parent URL path should be empty for root pages");
        }

        /// <summary>
        /// Tests that UnpublishAsync marks all versions as unpublished.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task UnpublishAsync_MarksAllVersionsAsUnpublished()
        {
            // Arrange
            var articleNumber = 1;
            var version1 = CreateTestArticle(articleNumber, 1);
            version1.Published = Clock.UtcNow.AddDays(-2);
            var version2 = CreateTestArticle(articleNumber, 2);
            version2.Published = Clock.UtcNow.AddDays(-1);

            Db.Articles.AddRange(version1, version2);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.UnpublishAsync(version1);

            // Assert
            var articles = await Db.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .ToListAsync();
            foreach (var article in articles)
            {
                Assert.IsNull(article.Published, $"Version {article.VersionNumber} should be unpublished");
            }
        }

        /// <summary>
        /// Tests that UnpublishAsync removes published pages but preserves redirects.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task UnpublishAsync_RemovesPagesButPreservesRedirects()
        {
            // Arrange
            var article = CreateTestArticle();
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);

            var normalPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = article.ArticleNumber,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = article.UrlPath,
                Title = "Normal Page",
                Content = "Content"
            };

            var redirectPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = article.ArticleNumber,
                StatusCode = (int)StatusCodeEnum.Redirect,
                UrlPath = article.UrlPath + "-old",
                Title = "Redirect",
                Content = "/new-location"
            };

            Db.Pages.AddRange(normalPage, redirectPage);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.UnpublishAsync(article);

            // Assert
            Assert.IsFalse(await Db.Pages.AnyAsync(p => p.Id == normalPage.Id), "Normal page should be removed");
            Assert.IsTrue(await Db.Pages.AnyAsync(p => p.Id == redirectPage.Id), "Redirect page should be preserved");
        }

        /// <summary>
        /// Tests that UnpublishAsync does nothing when no published versions exist.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task UnpublishAsync_DoesNothingWhenNoPublishedVersions()
        {
            // Arrange
            var article = CreateTestArticle();
            article.Published = null;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.UnpublishAsync(article);

            // Assert - should complete without errors
            Assert.IsNull(article.Published, "Article should remain unpublished");
        }

        /// <summary>
        /// Tests that UnpublishEarlierVersions only unpublishes versions with earlier version numbers.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_OnlyUnpublishesEarlierVersions()
        {
            // Arrange
            var articleNumber = 1;
            var version1 = CreateTestArticle(articleNumber, 1);
            version1.Published = Clock.UtcNow.AddDays(-3);

            var version2 = CreateTestArticle(articleNumber, 2);
            version2.Published = Clock.UtcNow.AddDays(-2);

            var version3 = CreateTestArticle(articleNumber, 3);
            version3.Published = Clock.UtcNow.AddDays(-1);

            Db.Articles.AddRange(version1, version2, version3);
            await Db.SaveChangesAsync();

            // Act - Publish version 3
            await PublishingService.PublishAsync(version3);

            // Assert
            var v1 = await Db.Articles.FindAsync(version1.Id);
            var v2 = await Db.Articles.FindAsync(version2.Id);
            var v3 = await Db.Articles.FindAsync(version3.Id);

            Assert.IsNull(v1.Published, "Version 1 should be unpublished");
            Assert.IsNull(v2.Published, "Version 2 should be unpublished");
            Assert.IsNotNull(v3.Published, "Version 3 should remain published");
        }

        /// <summary>
        /// Tests that UnpublishEarlierVersions does not unpublish future-dated articles.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_DoesNotUnpublishFutureDatedArticles()
        {
            // Arrange
            var articleNumber = 1;
            var currentVersion = CreateTestArticle(articleNumber, 1);
            currentVersion.Published = Clock.UtcNow.AddDays(1); // Future dated

            Db.Articles.Add(currentVersion);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(currentVersion);

            // Assert
            var article = await Db.Articles.FindAsync(currentVersion.Id);
            Assert.IsNotNull(article.Published, "Future-dated article should not be unpublished");
        }

        /// <summary>
        /// Tests that PublishAsync calls storage service to create static file.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_CreatesStaticFile_WhenStaticPagesEnabled()
        {
            // Arrange
            var article = CreateTestArticle();
            article.UrlPath = "test-page";  // Not "root"
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(article);

            // Assert
            // Check the correct storage path (with leading slash)
            var exists = await Storage.BlobExistsAsync("/" + article.UrlPath);
            Assert.IsTrue(exists, "Static file should be created at /{UrlPath} when static pages are enabled");
        }

        /// <summary>
        /// Creates a test article with default values.
        /// </summary>
        /// <param name="articleNumber">The article number.</param>
        /// <param name="versionNumber">The version number.</param>
        /// <returns>A test article instance.</returns>
        private Article CreateTestArticle(int articleNumber = 1, int versionNumber = 1)
        {
            return new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumber,
                VersionNumber = versionNumber,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = $"test-article-{articleNumber}",
                Title = $"Test Article {articleNumber} v{versionNumber}",
                Content = "<p>Test content</p>",
                Updated = Clock.UtcNow,
                UserId = Guid.NewGuid().ToString(),
                BannerImage = string.Empty,
                Category = string.Empty,
                Introduction = string.Empty,
                BlogKey = "default"
            };
        }
    }
}
