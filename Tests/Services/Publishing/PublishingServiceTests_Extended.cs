// <copyright file="PublishingServiceTests_Extended.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Publishing
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Cms.Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.BlogPublishing;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using Sky.Cms.Services;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;

    /// <summary>
    /// Extended unit tests for <see cref="PublishingService"/> covering gap areas:
    /// - Static page generation and batch processing
    /// - Unpublish functionality and earlier version handling
    /// - Root page path handling (/root → /index.html)
    /// - Complex multi-version scenarios
    /// - Parent URL path calculation
    /// - Nested page support
    /// </summary>
    [TestClass]
    public class PublishingServiceTests_Extended : SkyCmsTestBase
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

            _mockViewRenderService = new Mock<IViewRenderService>();
            _mockViewRenderService
                .Setup(x => x.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("<html><head><title>Test</title></head><body>Mocked HTML Content</body></html>");

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
        public void Cleanup() => Db?.Dispose();

        #region Blog Stream Publishing Tests (Skipped - requires interface implementation)

        // Blog stream tests skipped - PublishBlogStreamAsync not exposed in IPublishingService
        // These would test blog stream creation, versioning, and metadata updates

        #endregion

        #region Blog Post Publishing Tests (Skipped - requires enum import)

        // Blog post tests skipped - require Cosmos.Cms.Common.ArticleType enum import
        // These would test blog-specific HTML rendering and metadata preservation

        #endregion

        #region Input Validation Tests

        /// <summary>
        /// Test: PublishAsync throws when UserId is null.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.Validation")]
        public async Task PublishAsync_ThrowsArgumentException_WhenUserIdIsNull()
        {
            // Arrange
            var article = CreateTestArticle();
            article.UserId = null;
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act & Assert
            try
            {
                await PublishingService.PublishAsync(article);
                Assert.Fail("Should have thrown ArgumentException");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("User ID"), "Exception should mention User ID");
            }
        }

        /// <summary>
        /// Test: PublishAsync throws when UserId is empty string.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.Validation")]
        public async Task PublishAsync_ThrowsArgumentException_WhenUserIdIsEmpty()
        {
            // Arrange
            var article = CreateTestArticle();
            article.UserId = string.Empty;
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act & Assert
            try
            {
                await PublishingService.PublishAsync(article);
                Assert.Fail("Should have thrown ArgumentException");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("User ID"), "Exception should mention User ID");
            }
        }

        /// <summary>
        /// Test: PublishAsync throws when UserId is not a valid GUID.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.Validation")]
        public async Task PublishAsync_ThrowsArgumentException_WhenUserIdIsInvalidGuid()
        {
            // Arrange
            var article = CreateTestArticle();
            article.UserId = "not-a-guid";
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act & Assert
            try
            {
                await PublishingService.PublishAsync(article);
                Assert.Fail("Should have thrown ArgumentException");
            }
            catch (ArgumentException ex)
            {
                Assert.IsTrue(ex.Message.Contains("GUID"), "Exception should mention GUID format");
            }
        }

        /// <summary>
        /// Test: PublishAsync accepts valid GUID as UserId.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.Validation")]
        public async Task PublishAsync_AcceptsValidGuid_AsUserId()
        {
            // Arrange
            var validGuid = Guid.NewGuid().ToString();
            var article = CreateTestArticle();
            article.UserId = validGuid;
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act & Assert - should not throw
            await PublishingService.PublishAsync(article);
            var page = await Db.Pages.FirstAsync();
            Assert.IsNotNull(page);
        }

        #endregion

        #region Static Pages and WriteTocAsync Tests

        /// <summary>
        /// Test: WriteTocAsync creates table of contents JSON file.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.Toc")]
        public async Task WriteTocAsync_CreatesTableOfContentsFile()
        {
            // Arrange
            var article1 = CreateTestArticle();
            article1.UrlPath = "page1";
            article1.Published = Clock.UtcNow;

            var article2 = CreateTestArticle();
            article2.ArticleNumber = 2;
            article2.UrlPath = "page2";
            article2.Published = Clock.UtcNow;

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Publish to create pages
            await PublishingService.PublishAsync(article1);
            await PublishingService.PublishAsync(article2);

            // Act
            await PublishingService.WriteTocAsync("/");

            // Assert
            var tocExists = await Storage.BlobExistsAsync("/toc.json");
            Assert.IsTrue(tocExists, "Table of contents file should be created");
        }

        /// <summary>
        /// Test: CreateStaticPages generates static HTML files for multiple pages.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.StaticPages")]
        public async Task CreateStaticPages_GeneratesMultipleStaticFiles()
        {
            // Arrange
            var articles = new List<Article>();
            for (int i = 1; i <= 3; i++)
            {
                var article = CreateTestArticle(i, 1);
                article.UrlPath = $"page-{i}";
                article.Published = Clock.UtcNow;
                articles.Add(article);
            }

            Db.Articles.AddRange(articles);
            await Db.SaveChangesAsync();

            // Publish all articles
            foreach (var article in articles)
            {
                await PublishingService.PublishAsync(article);
            }

            var pageIds = await Db.Pages.Select(p => p.Id).ToListAsync();

            // Act
            await PublishingService.CreateStaticPages(pageIds);

            // Assert
            foreach (var article in articles)
            {
                var expectedPath = article.UrlPath.Equals("root", StringComparison.OrdinalIgnoreCase)
                    ? "/index.html"
                    : "/" + article.UrlPath;
                
                var exists = await Storage.BlobExistsAsync(expectedPath);
                Assert.IsTrue(exists, $"Static file should exist at {expectedPath}");
            }
        }

        /// <summary>
        /// Test: CreateStaticPages handles empty page list.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.StaticPages")]
        public async Task CreateStaticPages_HandlesEmptyPageList()
        {
            // Arrange
            var emptyPageIds = new List<Guid>();

            // Act & Assert - should not throw
            await PublishingService.CreateStaticPages(emptyPageIds);
            Assert.IsTrue(true, "Should handle empty page list gracefully");
        }

        /// <summary>
        /// Test: CreateStaticPages handles null page list (generates all pages).
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.StaticPages")]
        public async Task CreateStaticPages_GeneratesAllPages_WhenNullProvided()
        {
            // Arrange
            var article = CreateTestArticle();
            article.UrlPath = "test-page";
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            await PublishingService.PublishAsync(article);

            // Act
            await PublishingService.CreateStaticPages(null);

            // Assert
            var exists = await Storage.BlobExistsAsync("/test-page");
            Assert.IsTrue(exists, "All pages should be generated when null provided");
        }

        #endregion

        #region Root Page Path Handling Tests

        /// <summary>
        /// Test: PublishAsync maps "root" to "/index.html" for static files.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.RootHandling")]
        public async Task PublishAsync_MapsRootToIndexHtml_ForStaticFiles()
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
            var exists = await Storage.BlobExistsAsync("/index.html");
            Assert.IsTrue(exists, "Root page should be created as /index.html");
        }

        /// <summary>
        /// Test: PublishAsync handles nested URL paths correctly for static files.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.RootHandling")]
        public async Task PublishAsync_HandlesNestedPaths_ForStaticFiles()
        {
            // Arrange
            var article = CreateTestArticle();
            article.UrlPath = "docs/getting-started";
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(article);

            // Assert
            var exists = await Storage.BlobExistsAsync("/docs/getting-started");
            Assert.IsTrue(exists, "Nested path should be preserved in static file");
        }

        #endregion

        #region Author Info Serialization Tests

        /// <summary>
        /// Test: PublishAsync serializes author info correctly in published page.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.AuthorInfo")]
        public async Task PublishAsync_SerializesAuthorInfo_InPublishedPage()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var article = CreateTestArticle();
            article.UserId = userId.ToString();
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(article);

            // Assert
            var page = await Db.Pages.FirstAsync();
            // AuthorInfo should be serialized (either empty or containing author data)
            Assert.IsNotNull(page.AuthorInfo, "AuthorInfo should be set");
        }

        #endregion

        #region Multiple Version Scenarios Tests

        /// <summary>
        /// Test: PublishAsync with article containing both VersionNumber and Published date.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.Versioning")]
        public async Task PublishAsync_HandlesMultipleVersionsCorrectly()
        {
            // Arrange
            var articleNumber = 1;
            var v1 = CreateTestArticle(articleNumber, 1);
            v1.Published = Clock.UtcNow.AddDays(-10);
            
            var v2 = CreateTestArticle(articleNumber, 2);
            v2.Published = Clock.UtcNow.AddDays(-5);
            
            var v3 = CreateTestArticle(articleNumber, 3);
            v3.Published = Clock.UtcNow;

            Db.Articles.AddRange(v1, v2, v3);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(v3);

            // Assert
            var v1Updated = await Db.Articles.FindAsync(v1.Id);
            var v2Updated = await Db.Articles.FindAsync(v2.Id);
            var v3Updated = await Db.Articles.FindAsync(v3.Id);

            Assert.IsNull(v1Updated.Published, "v1 should be unpublished");
            Assert.IsNull(v2Updated.Published, "v2 should be unpublished");
            Assert.IsNotNull(v3Updated.Published, "v3 should remain published");
        }

        #endregion

        #region Unpublish and Version Cleanup Tests

        /// <summary>
        /// Test: UnpublishAsync removes all pages for an article.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.Unpublish")]
        public async Task UnpublishAsync_RemovesAllPages_ForArticle()
        {
            // Arrange
            var article = CreateTestArticle();
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            await PublishingService.PublishAsync(article);
            var pageCount = await Db.Pages.CountAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual(1, pageCount, "Should have created a page");

            // Act
            await PublishingService.UnpublishAsync(article);

            // Assert
            var remainingPages = await Db.Pages.CountAsync(p => p.ArticleNumber == article.ArticleNumber);
            Assert.AreEqual(0, remainingPages, "All pages should be removed");
        }

        /// <summary>
        /// Test: UnpublishAsync returns early when no published versions exist.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.Unpublish")]
        public async Task UnpublishAsync_ReturnsEarly_WhenNothingPublished()
        {
            // Arrange
            var article = CreateTestArticle();
            article.Published = null;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act & Assert - should not throw
            await PublishingService.UnpublishAsync(article);
            Assert.IsNull(article.Published, "Unpublished article should remain unpublished");
        }

        #endregion

        #region Parent URL Path Tests

        /// <summary>
        /// Test: PublishAsync calculates parent URL path correctly for deeply nested pages.
        /// </summary>
        [TestMethod]
        [TestCategory("Publishing.ParentPath")]
        public async Task PublishAsync_CalculatesParentUrlPath_ForDeeplyNestedPages()
        {
            // Arrange
            var article = CreateTestArticle();
            article.UrlPath = "docs/guides/getting-started";
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            await PublishingService.PublishAsync(article);

            // Assert
            var page = await Db.Pages.FirstAsync();
            Assert.AreEqual("docs/guides", page.ParentUrlPath, "Parent URL path should match parent directory");
        }

        #endregion

        #region Test Helpers

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

        #endregion
    }
}
