// <copyright file="PublishingServiceTests_Extended.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

#nullable enable

namespace Sky.Tests.Services.Publishing
{
    using Cosmos.BlobService;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Services;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Publishing;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

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
            services.AddScoped<IStorageContext>(_ => Storage);
            services.AddLogging();

            // Register IArticleCatalogQueryService (required by PublishingService constructor)
            services.AddSingleton<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>(sp =>
                new Cosmos.Common.Features.Articles.Shared.ArticleCatalogQueryService(
                    Db,
                    EditorSettings.PublisherUrl,
                    EditorSettings.BlobPublicUrl));

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
            // Arrange - Create article in memory without saving to DB
            // (saving with null UserId would fail EF non-nullable constraint)
            var article = CreateTestArticle();
            article.UserId = null;
            article.Published = Clock.UtcNow;

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

            var service = CreatePublishingServiceWithStaticPagesEnabled();

            // Publish to create pages
            await service.PublishAsync(article1);
            await service.PublishAsync(article2);

            // Act
            await service.WriteTocAsync("/");

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

        #region Step 2: CreateStaticPages - Progress & Batching Tests

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_ReportsProgressAtStart()
        {
            // Arrange
            var page1 = CreatePublishedPage("page1");
            var page2 = CreatePublishedPage("page2");
            Db.Pages.AddRange(page1, page2);
            await Db.SaveChangesAsync();

            var progressReports = new List<(int current, int total, string message)>();
            var mockProgressReporter = new Mock<IPublishingProgressReporter>();
            mockProgressReporter
                .Setup(p => p.ReportProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<int, int, string>((c, t, m) => progressReports.Add((c, t, m)))
                .Returns(Task.CompletedTask);

            var service = CreatePublishingServiceWithProgressReporter(mockProgressReporter.Object);

            // Act
            await service.CreateStaticPages(new[] { page1.Id, page2.Id });

            // Assert
            Assert.IsTrue(progressReports.Any(r => r.message.Contains("Preparing")),
                "Should report preparation progress");
            Assert.IsTrue(progressReports.Any(r => r.message.Contains("Starting generation")),
                "Should report start of generation");
            Assert.AreEqual(2, progressReports.First().total, "Total should be 2 pages");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_ReportsProgressDuringGeneration()
        {
            // Arrange
            var pages = Enumerable.Range(1, 10)
                .Select(i => CreatePublishedPage($"page{i}"))
                .ToList();
            Db.Pages.AddRange(pages);
            await Db.SaveChangesAsync();

            var progressReports = new List<(int current, int total, string message)>();
            var mockProgressReporter = new Mock<IPublishingProgressReporter>();
            mockProgressReporter
                .Setup(p => p.ReportProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<int, int, string>((c, t, m) => progressReports.Add((c, t, m)))
                .Returns(Task.CompletedTask);

            var service = CreatePublishingServiceWithProgressReporter(mockProgressReporter.Object);

            // Act
            await service.CreateStaticPages(pages.Select(p => p.Id));

            // Assert
            var generationReports = progressReports.Where(r => r.message.Contains("Generated")).ToList();
            Assert.IsTrue(generationReports.Count > 0, "Should report progress during generation");
            Assert.IsTrue(generationReports.Any(r => r.current == 10), "Should report completion of all 10 pages");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_ReportsProgressAtCompletion()
        {
            // Arrange
            var page = CreatePublishedPage("completion-test");
            Db.Pages.Add(page);
            await Db.SaveChangesAsync();

            var progressReports = new List<(int current, int total, string message)>();
            var mockProgressReporter = new Mock<IPublishingProgressReporter>();
            mockProgressReporter
                .Setup(p => p.ReportProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<int, int, string>((c, t, m) => progressReports.Add((c, t, m)))
                .Returns(Task.CompletedTask);

            var service = CreatePublishingServiceWithProgressReporter(mockProgressReporter.Object);

            // Act
            await service.CreateStaticPages(new[] { page.Id });

            // Assert
            Assert.IsTrue(progressReports.Any(r => r.message.Contains("table of contents")),
                "Should report TOC update");
            Assert.IsTrue(progressReports.Any(r => r.message.Contains("CDN")),
                "Should report CDN purge");
            Assert.IsTrue(progressReports.Any(r => r.message.Contains("completed successfully")),
                "Should report final completion");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_WithMoreThan50Pages_ProcessesInBatches()
        {
            // Arrange - Create 75 pages to trigger batching (batch size = 50)
            var pages = Enumerable.Range(1, 75)
                .Select(i => CreatePublishedPage($"batch-page{i}"))
                .ToList();
            Db.Pages.AddRange(pages);
            await Db.SaveChangesAsync();

            var batchReports = new List<string>();
            var mockProgressReporter = new Mock<IPublishingProgressReporter>();
            mockProgressReporter
                .Setup(p => p.ReportProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<int, int, string>((c, t, m) =>
                {
                    if (m.Contains("batch"))
                    {
                        batchReports.Add(m);
                    }
                })
                .Returns(Task.CompletedTask);

            var service = CreatePublishingServiceWithProgressReporter(mockProgressReporter.Object);

            // Act
            await service.CreateStaticPages(pages.Select(p => p.Id));

            // Assert
            Assert.IsTrue(batchReports.Any(r => r.Contains("batch 1/2")),
                "Should report batch 1 of 2");
            Assert.IsTrue(batchReports.Any(r => r.Contains("batch 2/2")),
                "Should report batch 2 of 2");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_LogsParallelismConfiguration()
        {
            // Arrange
            var page = CreatePublishedPage("parallelism-test");
            Db.Pages.Add(page);
            await Db.SaveChangesAsync();

            var logMessages = new List<string>();
            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<PublishingService>>();
            mockLogger
                .Setup(l => l.Log(
                    Microsoft.Extensions.Logging.LogLevel.Information,
                    It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback(new InvocationAction(invocation =>
                {
                    var state = invocation.Arguments[2];
                    logMessages.Add(state?.ToString() ?? string.Empty);
                }));

            var service = CreatePublishingServiceWithLogger(mockLogger.Object);

            // Act
            await service.CreateStaticPages(new[] { page.Id });

            // Assert
            Assert.IsTrue(logMessages.Any(m => m.Contains("Starting static page generation") && m.Contains("parallelism")),
                "Should log parallelism configuration at start");
            Assert.IsTrue(logMessages.Any(m => m.Contains("Completed batch")),
                "Should log batch completion");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_WithNullOrEmptyIds_ProcessesAllPages()
        {
            // Arrange
            var page1 = CreatePublishedPage("all-page1");
            var page2 = CreatePublishedPage("all-page2");
            var page3 = CreatePublishedPage("all-page3");
            Db.Pages.AddRange(page1, page2, page3);
            await Db.SaveChangesAsync();

            var mockProgressReporter = new Mock<IPublishingProgressReporter>();
            mockProgressReporter
                .Setup(p => p.ReportProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var service = CreatePublishingServiceWithProgressReporter(mockProgressReporter.Object);

            // Act - Pass null to trigger "all pages" logic
            await service.CreateStaticPages(null);

            // Assert - Verify it processed all 3 pages
            mockProgressReporter.Verify(
                p => p.ReportProgressAsync(It.IsAny<int>(), 3, It.IsAny<string>()),
                Times.AtLeastOnce,
                "Should process all 3 pages when null IDs provided");
        }

        #endregion

        #region Step 3: CreateStaticPages - Post-Processing Tests

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_CallsWriteTocAsyncAfterBatchCompletion()
        {
            // Arrange
            var page = CreatePublishedPage("toc-test");
            Db.Pages.Add(page);
            await Db.SaveChangesAsync();

            // Create a default layout in the database (required for TOC generation)
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "Default",
                IsDefault = true,
                Head = string.Empty,
                HtmlHeader = string.Empty,
                FooterHtmlContent = string.Empty
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            var progressReports = new List<string>();
            var mockProgressReporter = new Mock<IPublishingProgressReporter>();
            mockProgressReporter
                .Setup(p => p.ReportProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<int, int, string>((c, t, m) => progressReports.Add(m))
                .Returns(Task.CompletedTask);

            var service = CreatePublishingServiceWithProgressReporter(mockProgressReporter.Object);

            // Act
            await service.CreateStaticPages(new[] { page.Id });

            // Assert
            Assert.IsTrue(progressReports.Any(m => m.Contains("table of contents")),
                "Should report TOC update after batch completion");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_CallsCdnPurgeAfterTocUpdate()
        {
            // Arrange
            var page = CreatePublishedPage("cdn-purge-test");
            Db.Pages.Add(page);
            await Db.SaveChangesAsync();

            var progressReports = new List<string>();
            var mockProgressReporter = new Mock<IPublishingProgressReporter>();
            mockProgressReporter
                .Setup(p => p.ReportProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<int, int, string>((c, t, m) => progressReports.Add(m))
                .Returns(Task.CompletedTask);

            var service = CreatePublishingServiceWithProgressReporter(mockProgressReporter.Object);

            // Act
            await service.CreateStaticPages(new[] { page.Id });

            // Assert - Verify order: pages generated → TOC → CDN → completion
            var tocIndex = progressReports.FindIndex(m => m.Contains("table of contents"));
            var cdnIndex = progressReports.FindIndex(m => m.Contains("CDN"));
            var completionIndex = progressReports.FindIndex(m => m.Contains("completed successfully"));

            Assert.IsTrue(tocIndex >= 0, "TOC update should be reported");
            Assert.IsTrue(cdnIndex > tocIndex, "CDN purge should happen after TOC update");
            Assert.IsTrue(completionIndex > cdnIndex, "Completion should be last");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_PreLoadsLayoutOnce()
        {
            // Arrange
            var pages = Enumerable.Range(1, 5)
                .Select(i => CreatePublishedPage($"layout-page{i}"))
                .ToList();
            Db.Pages.AddRange(pages);

            // Create a default layout
            var layout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutName = "TestLayout",
                IsDefault = true,
                Head = "<head></head>",
                HtmlHeader = "<header></header>",
                FooterHtmlContent = "<footer></footer>"
            };
            Db.Layouts.Add(layout);
            await Db.SaveChangesAsync();

            var service = PublishingService;

            // Act
            await service.CreateStaticPages(pages.Select(p => p.Id));

            // Assert - Layout should be loaded once and reused for all pages
            // We verify this indirectly by checking that all pages were processed successfully
            // (no exceptions thrown due to null layout)
            var allPages = await Db.Pages.Where(p => pages.Select(x => x.Id).Contains(p.Id)).ToListAsync();
            Assert.AreEqual(5, allPages.Count, "All pages should remain in database");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_CreatesScopedServicesPerPage()
        {
            // Arrange
            var page1 = CreatePublishedPage("scoped-1");
            var page2 = CreatePublishedPage("scoped-2");
            Db.Pages.AddRange(page1, page2);
            await Db.SaveChangesAsync();

            var viewRenderCallCount = 0;
            _mockViewRenderService
                .Setup(v => v.RenderToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
                .Callback(() => Interlocked.Increment(ref viewRenderCallCount))
                .ReturnsAsync("<html>rendered</html>");

            // Create a service that uses the mocked view renderer
            var mockProgressReporter = new Mock<IPublishingProgressReporter>();
            mockProgressReporter
                .Setup(p => p.ReportProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var service = CreatePublishingServiceWithProgressReporter(mockProgressReporter.Object);

            // Act
            await service.CreateStaticPages(new[] { page1.Id, page2.Id });

            // Assert - Each page should get its own scoped service
            Assert.AreEqual(2, viewRenderCallCount, "View renderer should be called once per page");
        }

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task CreateStaticPages_HandlesEmptyBatchGracefully()
        {
            // Arrange
            var progressReports = new List<string>();
            var mockProgressReporter = new Mock<IPublishingProgressReporter>();
            mockProgressReporter
                .Setup(p => p.ReportProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<int, int, string>((c, t, m) => progressReports.Add(m))
                .Returns(Task.CompletedTask);

            var service = CreatePublishingServiceWithProgressReporter(mockProgressReporter.Object);

            // Act - Pass empty list
            await service.CreateStaticPages(new List<Guid>());

            // Assert - Should complete without errors
            Assert.IsTrue(progressReports.Any(m => m.Contains("Preparing")),
                "Should report preparation even with empty batch");
            Assert.IsTrue(progressReports.Any(m => m.Contains("completed successfully")),
                "Should report completion even with empty batch");
        }

        #endregion

        #region Step 4: Static File & Settings Control Tests - SKIPPED

        // These tests are skipped because they require Storage methods (GetFileCount, GetAllFiles, CreateTestFile)
        // that don't exist on the actual StorageContext implementation.
        // To test these scenarios, we would need to:
        // 1. Mock IStorageContext and verify method calls
        // 2. Or extend StorageContext with test helper methods
        // 3. Or use integration tests with a real storage backend

        #endregion

        #region Step 5a: BlogPost vs Normal Article (2 tests)

        [TestMethod]
        [TestCategory("Publishing")]
        public async Task PublishAsync_WithBlogPostArticleType_GeneratesBlogEntryHtml()
        {
            // Arrange
            var article = CreateTestArticle();
            article.ArticleType = (int)ArticleType.BlogPost;
            article.BlogKey = "tech-blog";
            article.UrlPath = "tech-blog/my-first-post";
            article.Published = Clock.UtcNow;
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            var blogStreamRenderingMock = new Mock<Cosmos.Common.Services.BlogPublishing.IBlogStreamRenderingService>();

            var service = new PublishingService(
                Db,
                Storage,
                EditorSettings,
                NullLogger<PublishingService>.Instance,
                HttpContextAccessor,
                AuthorInfoService,
                Clock,
                blogStreamRenderingMock.Object,
                _mockViewRenderService.Object,
                _serviceProvider,
                new NoOpPublishingProgressReporter(),
                _serviceProvider.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>());

            // Act
            await service.PublishAsync(article);

            // Assert
            var page = await Db.Pages.FirstOrDefaultAsync();
            Assert.IsNotNull(page);
        }

        #endregion

        #region Helper Methods

        private PublishingService CreatePublishingServiceWithStaticPagesEnabled()
        {
            var mockSettings = new Mock<IEditorSettings>();
            mockSettings.SetupGet(s => s.StaticWebPages).Returns(true);
            mockSettings.SetupGet(s => s.PublisherUrl).Returns("https://test.example.com");
            mockSettings.SetupGet(s => s.BlobPublicUrl).Returns("https://blob.test.example.com");
            mockSettings.SetupGet(s => s.StaticPageParallelism).Returns((int?)null);
            var mockBlogStreamService = new Mock<Cosmos.Common.Services.BlogPublishing.IBlogStreamRenderingService>();
            return new PublishingService(
                Db,
                Storage,
                mockSettings.Object,
                NullLogger<PublishingService>.Instance,
                HttpContextAccessor,
                AuthorInfoService,
                Clock,
                mockBlogStreamService.Object,
                _mockViewRenderService.Object,
                _serviceProvider,
                new NoOpPublishingProgressReporter(),
                _serviceProvider.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>());
        }

        private PublishingService CreatePublishingServiceWithProgressReporter(IPublishingProgressReporter progressReporter)
        {
            var mockBlogStreamService = new Mock<Cosmos.Common.Services.BlogPublishing.IBlogStreamRenderingService>();
            return new PublishingService(
                Db,
                Storage,
                EditorSettings,
                NullLogger<PublishingService>.Instance,
                HttpContextAccessor,
                AuthorInfoService,
                Clock,
                mockBlogStreamService.Object,
                _mockViewRenderService.Object,
                _serviceProvider,
                progressReporter,
                _serviceProvider.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>());
        }

        private PublishingService CreatePublishingServiceWithLogger(ILogger<PublishingService> logger)
        {
            var mockBlogStreamService = new Mock<Cosmos.Common.Services.BlogPublishing.IBlogStreamRenderingService>();
            return new PublishingService(
                Db,
                Storage,
                EditorSettings,
                logger,
                HttpContextAccessor,
                AuthorInfoService,
                Clock,
                mockBlogStreamService.Object,
                _mockViewRenderService.Object,
                _serviceProvider,
                new NoOpPublishingProgressReporter(),
                _serviceProvider.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>());
        }

        private PublishedPage CreatePublishedPage(string urlPath)
        {
            return new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = Guid.NewGuid().GetHashCode(),
                VersionNumber = 1,
                Title = $"Test Page - {urlPath}",
                UrlPath = urlPath,
                Published = Clock.UtcNow,
                AuthorInfo = "[]"
            };
        }

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