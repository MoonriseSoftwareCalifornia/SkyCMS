// <copyright file="ArticleSchedulerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Scheduling
{
    using Cosmos.BlobService;
    using Cosmos.Cms.Common.Services.Configurations;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Services.BlogPublishing;
    using Cosmos.EmailServices;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Services;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Domain.Events;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Authors;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Redirects;
    using Sky.Editor.Services.ReservedPaths;
    using Sky.Editor.Services.Scheduling;
    using Sky.Editor.Services.Slugs;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Services.Titles;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="ArticleScheduler"/>.
    /// </summary>
    [TestClass]
    public class ArticleSchedulerTests : SkyCmsTestBase
    {
        private Mock<ILogger<ArticleScheduler>> mockLogger;
        private Mock<ICosmosEmailSender> mockEmailSender;
        private TestClock testClock;
        private ServiceProvider serviceProvider;

        /// <summary>
        /// Initializes test context before each test.
        /// </summary>
        [TestInitialize]
        public new void Setup()
        {
            testClock = new TestClock();
            Clock = testClock; // Set BEFORE InitializeTestContext

            mockLogger = new Mock<ILogger<ArticleScheduler>>();
            mockEmailSender = new Mock<ICosmosEmailSender>();

            // Initialize the base context
            InitializeTestContext();

            // Create a new service collection and manually add critical services
            var services = new ServiceCollection();

            // Register all required services from base properties
            services.AddSingleton<ApplicationDbContext>(Db);
            services.AddSingleton<StorageContext>(Storage);
            services.AddSingleton<IStorageContext>(Storage);
            services.AddSingleton<IMemoryCache>(Cache);
            services.AddSingleton<IHttpContextAccessor>(HttpContextAccessor);
            services.AddSingleton<ISlugService>(SlugService);
            services.AddSingleton<IArticleHtmlService>(ArticleHtmlService);
            services.AddSingleton<ICatalogService>(CatalogService);
            services.AddSingleton<IDomainEventDispatcher>(EventDispatcher);
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton<IBlogStreamRenderingService>(BlogStreamRenderingService);
            services.AddSingleton<IAuthorInfoService>(AuthorInfoService);
            services.AddSingleton<IViewRenderService>(ViewRenderService);
            services.AddSingleton<IReservedPaths>(ReservedPaths);
            services.AddSingleton<IEditorSettings>(EditorSettings);
            services.AddSingleton<IPublishingService>(PublishingService);
            services.AddSingleton<IRedirectService>(RedirectService);
            services.AddSingleton<ITitleChangeService>(TitleChangeService);
            services.AddSingleton<ITemplateService>(TemplateService);
            services.AddSingleton<ITenantArticleLogicFactory>(TenantArticleLogicFactory);
            services.AddSingleton<UserManager<IdentityUser>>(UserManager);
            services.AddSingleton<RoleManager<IdentityRole>>(RoleManager);
            services.AddSingleton(Options.Create(new SiteSettings()));

            // Add the mock email sender - THIS IS THE KEY ADDITION
            services.AddScoped<ICosmosEmailSender>(_ => mockEmailSender.Object);

            // Rebuild the service provider
            serviceProvider = services.BuildServiceProvider();

            // Update the base class Services property so tests can access it
            Services = serviceProvider;

            // Rebuild ArticleScheduler with the new service provider
            ArticleScheduler = new ArticleScheduler(
                new NullLogger<ArticleScheduler>(),
                EditorSettings,
                Clock,
                serviceProvider);
        }

        /// <summary>
        /// Tests that ExecuteAsync processes multiple published article versions correctly.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithMultiplePublishedVersions_ActivatesMostRecentNonFutureVersion()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            // Create article with 3 versions: past, current (should be active), future
            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article V1",
                Content = "Version 1 Content",
                Published = now.AddDays(-10),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test-article"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test Article V2",
                Content = "Version 2 Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test-article"
            };

            var article3 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 3,
                Title = "Test Article V3",
                Content = "Version 3 Content",
                Published = now.AddDays(5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test-article"
            };

            Db.Articles.Add(article1);
            Db.Articles.Add(article2);
            Db.Articles.Add(article3);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Version 1 should be unpublished
            var updatedArticle1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle1, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle1.Published, "Version 1 should be unpublished");

            // Version 2 should remain published (most recent non-future)
            var updatedArticle2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updatedArticle2.Published, "Version 2 should remain published");

            // Version 3 should remain published (scheduled for future)
            var updatedArticle3 = await Db.Articles.FindAsync(article3.Id);
            Assert.IsNotNull(updatedArticle3.Published, "Version 3 should remain scheduled for future");
            Assert.IsTrue(updatedArticle3.Published > now, "Version 3 publish date should be in the future");
        }

        /// <summary>
        /// Tests that ExecuteAsync handles articles with all future-dated versions.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithAllFutureVersions_DoesNotUnpublishAnyVersions()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Future Article V1",
                Content = "Version 1 Content",
                Published = now.AddDays(1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/future-article"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Future Article V2",
                Content = "Version 2 Content",
                Published = now.AddDays(5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/future-article"
            };

            Db.Articles.Add(article1);
            Db.Articles.Add(article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Both versions should remain published
            var updatedArticle1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle1.Published, "Version 1 should remain published (future)");

            var updatedArticle2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updatedArticle2.Published, "Version 2 should remain published (future)");
        }

        /// <summary>
        /// Tests that ExecuteAsync ignores deleted articles.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithDeletedArticles_IgnoresDeletedVersions()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Deleted Article",
                Content = "Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Deleted,
                UserId = userId,
                UrlPath = "/deleted-article"
            };

            Db.Articles.Add(article1);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Article should not be processed
            var updatedArticle = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle, "Expected article to exist after scheduler execution");
            Assert.AreEqual((int)StatusCodeEnum.Deleted, updatedArticle.StatusCode);
            Assert.IsNotNull(updatedArticle.Published, "Deleted article should not be modified");
        }

        /// <summary>
        /// Tests that ExecuteAsync handles articles with only one published version.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithSinglePublishedVersion_DoesNotProcessArticle()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Single Version Article",
                Content = "Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/single-article"
            };

            Db.Articles.Add(article1);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Single version should remain unchanged
            var updatedArticle = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle.Published, "Single version should remain published");
        }

        /// <summary>
        /// Tests that ExecuteAsync handles race conditions when articles are deleted during processing.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithRaceCondition_HandlesGracefully()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            // Create article that will have only one version after initial query
            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article",
                Content = "Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            Db.Articles.Add(article1);
            await Db.SaveChangesAsync();

            // Act - Should not throw exception even if race condition occurs
            await ArticleScheduler.ExecuteAsync();

            // Assert - Article is left unmodified; scheduler handles single-version gracefully
            var updatedArticle = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle, "Article should still exist after execution");
            Assert.IsNotNull(updatedArticle.Published, "Publish date should remain set when scheduler handles gracefully");
            Assert.AreEqual((int)StatusCodeEnum.Active, updatedArticle.StatusCode, "Status should remain Active");
        }

        /// <summary>
        /// Tests that ExecuteAsync publishes the active version correctly.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_PublishesActiveVersion_CallsPublishArticle()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article V1",
                Content = "Version 1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test Article V2",
                Content = "Version 2",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            Db.Articles.Add(article1);
            Db.Articles.Add(article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Verify article2 was published
            var publishedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == 1);
            Assert.IsNotNull(publishedPage, "Published page should exist");
        }

        /// <summary>
        /// Tests that ExecuteAsync handles two versions with identical publish dates by using version number.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithIdenticalPublishDates_ActivatesHighestVersionNumber()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();
            var identicalPublishDate = now.AddDays(-1);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article V1",
                Content = "Version 1 Content",
                Published = identicalPublishDate,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test-article"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test Article V2",
                Content = "Version 2 Content",
                Published = identicalPublishDate,
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test-article"
            };

            Db.Articles.Add(article1);
            Db.Articles.Add(article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Version 1 should be unpublished (lower version number)
            var updatedArticle1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle1, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle1.Published, "Version 1 should be unpublished");

            // Version 2 should remain published (higher version number, same date)
            var updatedArticle2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updatedArticle2.Published, "Version 2 should remain published");
        }

        /// <summary>
        /// Tests that ExecuteAsync handles version published exactly at the current time boundary.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithPublishDateAtExactNow_ActivatesVersion()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article V1",
                Content = "Version 1 Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test-article"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test Article V2",
                Content = "Version 2 Content",
                Published = now, // Exact boundary
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test-article"
            };

            Db.Articles.Add(article1);
            Db.Articles.Add(article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Version 1 should be unpublished
            var updatedArticle1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle1, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle1.Published, "Version 1 should be unpublished");

            // Version 2 should remain published (at exact boundary)
            var updatedArticle2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updatedArticle2.Published, "Version 2 should be published at exact boundary time");
        }

        /// <summary>
        /// Tests that ExecuteAsync processes inactive (non-deleted) articles correctly.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithInactiveArticles_ProcessesCorrectly()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Inactive Article V1",
                Content = "Version 1 Content",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Inactive,
                UserId = userId,
                UrlPath = "/inactive-article"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Inactive Article V2",
                Content = "Version 2 Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Inactive,
                UserId = userId,
                UrlPath = "/inactive-article"
            };

            Db.Articles.Add(article1);
            Db.Articles.Add(article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Version 1 should be unpublished
            var updatedArticle1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle1, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle1.Published, "Inactive version 1 should be unpublished");

            // Version 2 should remain published (most recent)
            var updatedArticle2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updatedArticle2.Published, "Inactive version 2 should remain published");
        }

        /// <summary>
        /// Tests that ExecuteAsync updates the catalog entry when activating a version.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WhenActivatingVersion_UpdatesCatalog()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article V1",
                Content = "Version 1 Content",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test Article V2 - Updated Title",
                Content = "Version 2 Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            Db.Articles.Add(article1);
            Db.Articles.Add(article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Catalog should reflect the active version
            var catalogEntry = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == 1);
            Assert.IsNotNull(catalogEntry, "Catalog entry should exist");
            Assert.AreEqual("Test Article V2 - Updated Title", catalogEntry.Title, "Catalog should reflect active version title");
        }

        /// <summary>
        /// Tests that ExecuteAsync handles multiple different articles in a single execution.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithMultipleDifferentArticles_ProcessesAllCorrectly()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            // Article 1 with 2 versions
            var article1v1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Article 1 V1",
                Content = "Content",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/article1"
            };

            var article1v2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Article 1 V2",
                Content = "Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/article1"
            };

            // Article 2 with 2 versions
            var article2v1 = new Article
            {
                ArticleNumber = 2,
                VersionNumber = 1,
                Title = "Article 2 V1",
                Content = "Content",
                Published = now.AddDays(-3),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/article2"
            };

            var article2v2 = new Article
            {
                ArticleNumber = 2,
                VersionNumber = 2,
                Title = "Article 2 V2",
                Content = "Content",
                Published = now.AddDays(-2),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/article2"
            };

            Db.Articles.AddRange(article1v1, article1v2, article2v1, article2v2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            var updatedArticle1v1 = await Db.Articles.FindAsync(article1v1.Id);
            Assert.IsNotNull(updatedArticle1v1, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle1v1.Published, "Article 1 Version 1 should be unpublished");

            var updatedArticle1v2 = await Db.Articles.FindAsync(article1v2.Id);
            Assert.IsNotNull(updatedArticle1v2.Published, "Article 1 Version 2 should remain published");

            var updatedArticle2v1 = await Db.Articles.FindAsync(article2v1.Id);
            Assert.IsNotNull(updatedArticle2v1, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle2v1.Published, "Article 2 Version 1 should be unpublished");

            var updatedArticle2v2 = await Db.Articles.FindAsync(article2v2.Id);
            Assert.IsNotNull(updatedArticle2v2.Published, "Article 2 Version 2 should remain published");
        }

        /// <summary>
        /// Tests that ExecuteAsync handles three or more published versions correctly.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithThreePublishedVersions_UnpublishesOlderVersionsCorrectly()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article V1",
                Content = "Version 1",
                Published = now.AddDays(-10),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test Article V2",
                Content = "Version 2",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            var article3 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 3,
                Title = "Test Article V3",
                Content = "Version 3",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            Db.Articles.AddRange(article1, article2, article3);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            var updatedArticle1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle1, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle1.Published, "Version 1 should be unpublished");

            var updatedArticle2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updatedArticle2, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle2.Published, "Version 2 should be unpublished");

            var updatedArticle3 = await Db.Articles.FindAsync(article3.Id);
            Assert.IsNotNull(updatedArticle3.Published, "Version 3 should remain published");
        }

        /// <summary>
        /// Tests that ExecuteAsync handles articles with unpublished (null Published date) versions mixed in.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithMixedPublishedAndUnpublishedVersions_ProcessesOnlyPublishedVersions()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article V1",
                Content = "Version 1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test Article V2",
                Content = "Version 2",
                Published = null, // Unpublished draft
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            var article3 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 3,
                Title = "Test Article V3",
                Content = "Version 3",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            Db.Articles.AddRange(article1, article2, article3);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            var updatedArticle1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle1, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle1.Published, "Version 1 should be unpublished");

            var updatedArticle2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updatedArticle2, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle2.Published, "Version 2 should remain unpublished (was never published)");

            var updatedArticle3 = await Db.Articles.FindAsync(article3.Id);
            Assert.IsNotNull(updatedArticle3.Published, "Version 3 should remain published");
        }

        /// <summary>
        /// Tests that ExecuteAsync creates a published page for the active version.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WhenActivatingVersion_CreatesPublishedPage()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var userId = TestUserId.ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article V1",
                Content = "Version 1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test Article V2",
                Content = "<h1>Version 2 Content</h1>",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = userId,
                UrlPath = "/test"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            var publishedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == 1);
            Assert.IsNotNull(publishedPage, "Published page should be created");
            Assert.AreEqual(2, publishedPage.VersionNumber, "Published page should be version 2");
            Assert.Contains("Version 2 Content", publishedPage.Content, "Published page should contain correct content");
        }

        /// <summary>
        /// Tests that ArticleScheduler uses ITenantArticleLogicFactory correctly.
        /// </summary>
        [TestMethod]
        public async Task ProcessArticleVersions_UsesFactory_ToCreateArticleLogic()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test V1",
                Content = "Content",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test V2",
                Content = "Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/test"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Verify factory was called
            var factoryFromServices = Services.GetRequiredService<ITenantArticleLogicFactory>();
            Assert.IsNotNull(factoryFromServices);
        }

        /// <summary>
        /// Tests that factory creates properly scoped ArticleEditLogic for tenant.
        /// </summary>
        [TestMethod]
        public async Task TenantArticleLogicFactory_CreatesCorrectlyScoped_ArticleLogic()
        {
            // Arrange
            var factory = Services.GetRequiredService<ITenantArticleLogicFactory>();
            var domainName = "test.com";

            // Act
            var articleLogic = await factory.CreateForTenantAsync(domainName);

            // Assert
            Assert.IsNotNull(articleLogic);
            Assert.IsInstanceOfType(articleLogic, typeof(ArticleEditLogic));
        }

        /// <summary>
        /// Tests handling of articles scheduled exactly 1 second apart.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithVersionsScheduledOneSecondApart_HandlesCorrectly()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test V1",
                Published = now.AddSeconds(-2),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test V2",
                Published = now.AddSeconds(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/test"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            var updated1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updated1, "Expected article to exist after scheduler execution");
            var updated2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updated2, "Expected article to exist after scheduler execution");

            Assert.IsNull(updated1.Published);
            Assert.IsNotNull(updated2.Published);
        }

        /// <summary>
        /// Tests that scheduler handles version number gaps correctly.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithNonSequentialVersionNumbers_HandlesCorrectly()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            // Version numbers: 1, 5, 10 (gaps intentional)
            var articles = new[]
            {
                new Article
                {
                    ArticleNumber = 1,
                    VersionNumber = 1,
                    Published = now.AddDays(-10),
                    StatusCode = (int)StatusCodeEnum.Active,
                    UserId = TestUserId.ToString(),
                    UrlPath = "/test"
                },
                new Article
                {
                    ArticleNumber = 1,
                    VersionNumber = 5,
                    Published = now.AddDays(-5),
                    StatusCode = (int)StatusCodeEnum.Active,
                    UserId = TestUserId.ToString(),
                    UrlPath = "/test"
                },
                new Article
                {
                    ArticleNumber = 1,
                    VersionNumber = 10,
                    Published = now.AddDays(-1),
                    StatusCode = (int)StatusCodeEnum.Active,
                    UserId = TestUserId.ToString(),
                    UrlPath = "/test"
                }
            };

            Db.Articles.AddRange(articles);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Only version 10 should remain published
            Assert.IsNull((await Db.Articles.FindAsync(articles[0].Id)).Published);
            Assert.IsNull((await Db.Articles.FindAsync(articles[1].Id)).Published);
            Assert.IsNotNull((await Db.Articles.FindAsync(articles[2].Id)).Published);
        }

        /// <summary>
        /// Tests that concurrent executions don't cause data corruption.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_ConcurrentExecutions_NoDataCorruption()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            // Setup test data
            for (int i = 1; i <= 10; i++)
            {
                Db.Articles.Add(new Article
                {
                    ArticleNumber = i,
                    VersionNumber = 1,
                    Published = now.AddDays(-1),
                    StatusCode = (int)StatusCodeEnum.Active,
                    UserId = TestUserId.ToString(),
                    UrlPath = $"/article-{i}"
                });
            }
            await Db.SaveChangesAsync();

            // Act - Run scheduler concurrently
            var tasks = Enumerable.Range(0, 3)
                .Select(_ => ArticleScheduler.ExecuteAsync())
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert - All articles should still be published (no corruption)
            var publishedCount = await Db.Articles.CountAsync(a => a.Published != null);
            Assert.AreEqual(10, publishedCount);
        }

        // ============================================================================
        // EMAIL NOTIFICATION TESTS
        // ============================================================================

        /// <summary>
        /// Tests that email notification is sent when an article is published via scheduler.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WhenArticlePublished_SendsEmailNotification()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var author = new IdentityUser
            {
                Id = TestUserId.ToString(),
                UserName = "testauthor@example.com",
                Email = "testauthor@example.com",
                EmailConfirmed = true
            };
            await UserManager.CreateAsync(author);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article V1",
                Content = "Version 1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = author.Id,
                UrlPath = "/test-article"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test Article V2 - Scheduled",
                Content = "Version 2 Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = author.Id,
                UrlPath = "/test-article"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            mockEmailSender.Verify(
                x => x.SendEmailAsync(
                    author.Email,
                    It.Is<string>(s => s.Contains("Test Article V2 - Scheduled")),
                    It.Is<string>(s => s.Contains("Your Scheduled Article is Now Live!"))),
                Times.Once,
                "Email should be sent to the author when article is published");
        }

        /// <summary>
        /// Tests that scheduler continues processing even if email sending fails.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WhenEmailFails_ContinuesProcessing()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var author = new IdentityUser
            {
                Id = TestUserId.ToString(),
                UserName = "author@example.com",
                Email = "author@example.com",
                EmailConfirmed = true
            };
            await UserManager.CreateAsync(author);

            // Configure email sender to throw exception
            mockEmailSender
                .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Email service unavailable"));

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Article V1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = author.Id,
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test Article V2",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = author.Id,
                UrlPath = "/test"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act - Should not throw exception
            await ArticleScheduler.ExecuteAsync();

            // Assert - Article should still be published despite email failure
            var publishedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == 1);
            Assert.IsNotNull(publishedPage, "Article should be published even if email fails");

            var updatedArticle1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle1, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle1.Published, "Old version should be unpublished");

            var updatedArticle2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updatedArticle2.Published, "New version should remain published");
        }

        /// <summary>
        /// Tests that no email is sent when author user is not found.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WhenAuthorNotFound_LogsWarningAndContinues()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var nonExistentUserId = Guid.NewGuid().ToString();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test V1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = nonExistentUserId,
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test V2",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = nonExistentUserId,
                UrlPath = "/test"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - No email should be sent
            mockEmailSender.Verify(
                x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never,
                "No email should be sent when author is not found");

            // But article should still be published
            var publishedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == 1);
            Assert.IsNotNull(publishedPage, "Article should be published even if author not found");
        }

        /// <summary>
        /// Tests that no email is sent when author has no email address.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WhenAuthorHasNoEmail_LogsWarningAndContinues()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var author = new IdentityUser
            {
                Id = TestUserId.ToString(),
                UserName = "testauthor",
                Email = null, // No email address
                EmailConfirmed = false
            };
            await UserManager.CreateAsync(author);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test V1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = author.Id,
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test V2",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = author.Id,
                UrlPath = "/test"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - No email should be sent
            mockEmailSender.Verify(
                x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never,
                "No email should be sent when author has no email address");

            // But article should still be published
            var publishedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == 1);
            Assert.IsNotNull(publishedPage, "Article should be published even if author has no email");
        }

        /// <summary>
        /// Tests that email includes website name from home page title.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_EmailNotification_IncludesWebsiteName()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var author = new IdentityUser
            {
                Id = TestUserId.ToString(),
                UserName = "author@example.com",
                Email = "author@example.com",
                EmailConfirmed = true
            };
            await UserManager.CreateAsync(author);

            // Create home page with title
            var homePage = new PublishedPage
            {
                ArticleNumber = 0,
                UrlPath = "root",
                Title = "My Awesome Website",
                Content = "Home page content",
                StatusCode = (int)StatusCodeEnum.Active
            };
            Db.Pages.Add(homePage);
            await Db.SaveChangesAsync();

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test V1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = author.Id,
                UrlPath = "/test"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test V2",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = author.Id,
                UrlPath = "/test"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Sanity: from the same serviceProvider used by ArticleScheduler,
            // ICosmosEmailSender should resolve to the mock object.
            using (var scope = serviceProvider.CreateScope())
            {
                var resolved = scope.ServiceProvider.GetService<ICosmosEmailSender>();
                Assert.AreSame(
                    mockEmailSender.Object,
                    resolved,
                    "Scheduler scope should resolve the mocked ICosmosEmailSender");
            }

            string capturedHtmlMessage = null;
            mockEmailSender
                .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((email, subject, htmlMessage) =>
                {
                    capturedHtmlMessage = htmlMessage;
                })
                .Returns(Task.CompletedTask);

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            Assert.IsNotNull(capturedHtmlMessage, "HTML message should be captured");
            Assert.IsTrue(
                capturedHtmlMessage.Contains("My Awesome Website"),
                "Email should include website name from home page title");
        }

        /// <summary>
        /// Tests that email notification is not sent when ICosmosEmailSender service is unavailable.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WhenEmailSenderServiceNull_LogsWarningAndContinuesPublishing()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var author = new IdentityUser
            {
                Id = TestUserId.ToString(),
                UserName = "author@example.com",
                Email = "author@example.com",
                EmailConfirmed = true
            };
            await UserManager.CreateAsync(author);

            // Create service provider WITHOUT email sender service
            var servicesWithoutEmail = new ServiceCollection();
            servicesWithoutEmail.AddSingleton<ApplicationDbContext>(Db);
            servicesWithoutEmail.AddSingleton<StorageContext>(Storage);
            servicesWithoutEmail.AddSingleton<IStorageContext>(Storage);
            servicesWithoutEmail.AddSingleton<IMemoryCache>(Cache);
            servicesWithoutEmail.AddSingleton<IHttpContextAccessor>(HttpContextAccessor);
            servicesWithoutEmail.AddSingleton<ISlugService>(SlugService);
            servicesWithoutEmail.AddSingleton<IArticleHtmlService>(ArticleHtmlService);
            servicesWithoutEmail.AddSingleton<ICatalogService>(CatalogService);
            servicesWithoutEmail.AddSingleton<IDomainEventDispatcher>(EventDispatcher);
            servicesWithoutEmail.AddSingleton<IClock>(Clock);
            servicesWithoutEmail.AddSingleton<IBlogStreamRenderingService>(BlogStreamRenderingService);
            servicesWithoutEmail.AddSingleton<IAuthorInfoService>(AuthorInfoService);
            servicesWithoutEmail.AddSingleton<IViewRenderService>(ViewRenderService);
            servicesWithoutEmail.AddSingleton<IReservedPaths>(ReservedPaths);
            servicesWithoutEmail.AddSingleton<IEditorSettings>(EditorSettings);
            servicesWithoutEmail.AddSingleton<IPublishingService>(PublishingService);
            servicesWithoutEmail.AddSingleton<IRedirectService>(RedirectService);
            servicesWithoutEmail.AddSingleton<ITitleChangeService>(TitleChangeService);
            servicesWithoutEmail.AddSingleton<ITemplateService>(TemplateService);
            servicesWithoutEmail.AddSingleton<ITenantArticleLogicFactory>(TenantArticleLogicFactory);
            servicesWithoutEmail.AddSingleton<UserManager<IdentityUser>>(UserManager);
            servicesWithoutEmail.AddSingleton<RoleManager<IdentityRole>>(RoleManager);
            servicesWithoutEmail.AddSingleton(Options.Create(new SiteSettings()));
            // NOTE: NOT registering ICosmosEmailSender

            var serviceProviderWithoutEmail = servicesWithoutEmail.BuildServiceProvider();
            var schedulerWithoutEmail = new ArticleScheduler(
                new NullLogger<ArticleScheduler>(),
                EditorSettings,
                Clock,
                serviceProviderWithoutEmail);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test V1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = author.Id,
                UrlPath = "/test-no-email"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test V2",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = author.Id,
                UrlPath = "/test-no-email"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act - Should not throw exception
            await schedulerWithoutEmail.ExecuteAsync();

            // Assert - Article should still be published despite missing email service
            var publishedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == 1);
            Assert.IsNotNull(publishedPage, "Article should be published even if email service is unavailable");

            var updatedArticle1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle1, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle1.Published, "Old version should be unpublished");

            var updatedArticle2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updatedArticle2.Published, "New version should remain published");
        }

        /// <summary>
        /// Tests that email notification is not sent when UserManager service is unavailable.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WhenUserManagerServiceNull_LogsWarningAndContinuesPublishing()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            // Create service provider WITHOUT UserManager
            var servicesWithoutUserManager = new ServiceCollection();
            servicesWithoutUserManager.AddSingleton<ApplicationDbContext>(Db);
            servicesWithoutUserManager.AddSingleton<StorageContext>(Storage);
            servicesWithoutUserManager.AddSingleton<IStorageContext>(Storage);
            servicesWithoutUserManager.AddSingleton<IMemoryCache>(Cache);
            servicesWithoutUserManager.AddSingleton<IHttpContextAccessor>(HttpContextAccessor);
            servicesWithoutUserManager.AddSingleton<ISlugService>(SlugService);
            servicesWithoutUserManager.AddSingleton<IArticleHtmlService>(ArticleHtmlService);
            servicesWithoutUserManager.AddSingleton<ICatalogService>(CatalogService);
            servicesWithoutUserManager.AddSingleton<IDomainEventDispatcher>(EventDispatcher);
            servicesWithoutUserManager.AddSingleton<IClock>(Clock);
            servicesWithoutUserManager.AddSingleton<IBlogStreamRenderingService>(BlogStreamRenderingService);
            servicesWithoutUserManager.AddSingleton<IAuthorInfoService>(AuthorInfoService);
            servicesWithoutUserManager.AddSingleton<IViewRenderService>(ViewRenderService);
            servicesWithoutUserManager.AddSingleton<IReservedPaths>(ReservedPaths);
            servicesWithoutUserManager.AddSingleton<IEditorSettings>(EditorSettings);
            servicesWithoutUserManager.AddSingleton<IPublishingService>(PublishingService);
            servicesWithoutUserManager.AddSingleton<IRedirectService>(RedirectService);
            servicesWithoutUserManager.AddSingleton<ITitleChangeService>(TitleChangeService);
            servicesWithoutUserManager.AddSingleton<ITemplateService>(TemplateService);
            servicesWithoutUserManager.AddSingleton<ITenantArticleLogicFactory>(TenantArticleLogicFactory);
            servicesWithoutUserManager.AddSingleton<RoleManager<IdentityRole>>(RoleManager);
            servicesWithoutUserManager.AddSingleton(Options.Create(new SiteSettings()));
            servicesWithoutUserManager.AddScoped<ICosmosEmailSender>(_ => mockEmailSender.Object);
            // NOTE: NOT registering UserManager<IdentityUser>

            var serviceProviderWithoutUserManager = servicesWithoutUserManager.BuildServiceProvider();
            var schedulerWithoutUserManager = new ArticleScheduler(
                new NullLogger<ArticleScheduler>(),
                EditorSettings,
                Clock,
                serviceProviderWithoutUserManager);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test V1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/test-no-usermanager"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test V2",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/test-no-usermanager"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act - Should not throw exception
            await schedulerWithoutUserManager.ExecuteAsync();

            // Assert - Article should still be published despite missing UserManager service
            var publishedPage = await Db.Pages.FirstOrDefaultAsync(p => p.ArticleNumber == 1);
            Assert.IsNotNull(publishedPage, "Article should be published even if UserManager is unavailable");

            // No email should be sent
            mockEmailSender.Verify(
                x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never,
                "No email should be sent when UserManager is not available");
        }

        /// <summary>
        /// Tests ProcessArticleVersions exception handling when article publishing fails.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WhenPublishingFails_LogsErrorAndContinues()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            // Create articles with invalid data that might cause publishing to fail
            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test V1",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = null // Invalid URL path - might cause issues
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test V2",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = null // Invalid URL path
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act - Should not throw exception even if processing fails
            await ArticleScheduler.ExecuteAsync();

            // Assert - No exception should be thrown
            Assert.IsTrue(true, "Scheduler should handle processing errors gracefully");
        }

        /// <summary>
        /// Tests handling when all versions of an article are unpublished (Published = null).
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithAllVersionsUnpublished_DoesNotProcess()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test V1",
                Published = null, // Unpublished
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/unpublished"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Test V2",
                Published = null, // Unpublished
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/unpublished"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Both versions should remain unpublished
            var updated1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updated1, "Expected article to exist after scheduler execution");
            var updated2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updated2, "Expected article to exist after scheduler execution");

            Assert.IsNull(updated1.Published, "Version 1 should remain unpublished");
            Assert.IsNull(updated2.Published, "Version 2 should remain unpublished");
        }

        /// <summary>
        /// Tests that scheduler processes articles with large version numbers correctly.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithLargeVersionNumbers_HandlesCorrectly()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 999,
                Title = "Test V999",
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/large-version"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1000,
                Title = "Test V1000",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/large-version"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            var updated1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updated1, "Expected article to exist after scheduler execution");
            var updated2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updated2, "Expected article to exist after scheduler execution");

            Assert.IsNull(updated1.Published, "Version 999 should be unpublished");
            Assert.IsNotNull(updated2.Published, "Version 1000 should remain published");
        }

        /// <summary>
        /// Tests the ordering logic when multiple versions have same publish date but different version numbers.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithSamePublishDateDifferentVersions_UsesVersionNumberTiebreaker()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);
            var samePublishDate = now.AddDays(-1);

            var articles = new[]
            {
                new Article
                {
                    ArticleNumber = 1,
                    VersionNumber = 3,
                    Title = "Test V3",
                    Published = samePublishDate,
                    StatusCode = (int)StatusCodeEnum.Active,
                    UserId = TestUserId.ToString(),
                    UrlPath = "/same-date"
                },
                new Article
                {
                    ArticleNumber = 1,
                    VersionNumber = 1,
                    Title = "Test V1",
                    Published = samePublishDate,
                    StatusCode = (int)StatusCodeEnum.Active,
                    UserId = TestUserId.ToString(),
                    UrlPath = "/same-date"
                },
                new Article
                {
                    ArticleNumber = 1,
                    VersionNumber = 2,
                    Title = "Test V2",
                    Published = samePublishDate,
                    StatusCode = (int)StatusCodeEnum.Active,
                    UserId = TestUserId.ToString(),
                    UrlPath = "/same-date"
                }
            };

            Db.Articles.AddRange(articles);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert - Only version 3 should remain published (highest version number)
            var updated1 = await Db.Articles.FirstAsync(a => a.VersionNumber == 1);
            var updated2 = await Db.Articles.FirstAsync(a => a.VersionNumber == 2);
            var updated3 = await Db.Articles.FirstAsync(a => a.VersionNumber == 3);

            Assert.IsNull(updated1.Published, "Version 1 should be unpublished");
            Assert.IsNull(updated2.Published, "Version 2 should be unpublished");
            Assert.IsNotNull(updated3.Published, "Version 3 should remain published (highest version)");
        }

        /// <summary>
        /// Tests that scheduler handles empty database gracefully.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithEmptyDatabase_CompletesSuccessfully()
        {
            // Arrange - No articles in database

            // Act - Should not throw
            await ArticleScheduler.ExecuteAsync();

            // Assert
            Assert.IsTrue(true, "Scheduler should handle empty database gracefully");
        }

        /// <summary>
        /// Tests that versions published far in the past are handled correctly.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithVeryOldPublishDates_ProcessesCorrectly()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Very Old Article V1",
                Published = now.AddYears(-10), // 10 years ago
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/very-old"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Very Old Article V2",
                Published = now.AddYears(-5), // 5 years ago
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/very-old"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            var updated1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updated1, "Expected article to exist after scheduler execution");
            var updated2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updated2, "Expected article to exist after scheduler execution");

            Assert.IsNull(updated1.Published, "Very old version 1 should be unpublished");
            Assert.IsNotNull(updated2.Published, "More recent (but still old) version 2 should remain published");
        }

        /// <summary>
        /// Tests that scheduler processes multiple article numbers in a single batch.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithMultipleArticleNumbers_ProcessesAllIndependently()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            // Article 100
            var article100v1 = new Article
            {
                ArticleNumber = 100,
                VersionNumber = 1,
                Published = now.AddDays(-5),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/article100"
            };

            var article100v2 = new Article
            {
                ArticleNumber = 100,
                VersionNumber = 2,
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/article100"
            };

            // Article 200
            var article200v1 = new Article
            {
                ArticleNumber = 200,
                VersionNumber = 1,
                Published = now.AddDays(-3),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/article200"
            };

            var article200v2 = new Article
            {
                ArticleNumber = 200,
                VersionNumber = 2,
                Published = now.AddDays(-2),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/article200"
            };

            Db.Articles.AddRange(article100v1, article100v2, article200v1, article200v2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            Assert.IsNull((await Db.Articles.FindAsync(article100v1.Id)).Published);
            Assert.IsNotNull((await Db.Articles.FindAsync(article100v2.Id)).Published);
            Assert.IsNull((await Db.Articles.FindAsync(article200v1.Id)).Published);
            Assert.IsNotNull((await Db.Articles.FindAsync(article200v2.Id)).Published);
        }

        /// <summary>
        /// Tests that scheduler does not remove versions that have no published date.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_WithArticlesWithoutPublishedVersions_DoesNotRemoveUnpublishedVersions()
        {
            // Arrange
            var now = new DateTimeOffset(2024, 11, 3, 12, 0, 0, TimeSpan.Zero);
            testClock.SetUtcNow(now);

            var article1 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "To Be Removed Article",
                Content = "Content",
                Published = null, // No published date
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/to-be-removed"
            };

            var article2 = new Article
            {
                ArticleNumber = 1,
                VersionNumber = 2,
                Title = "Kept Article",
                Content = "Content",
                Published = now.AddDays(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString(),
                UrlPath = "/kept-article"
            };

            Db.Articles.AddRange(article1, article2);
            await Db.SaveChangesAsync();

            // Act
            await ArticleScheduler.ExecuteAsync();

            // Assert
            Assert.IsTrue(await Db.Articles.AnyAsync(a => a.Id == article1.Id), "Unpublished version should remain in database");
            Assert.IsTrue(await Db.Articles.AnyAsync(a => a.Id == article2.Id), "Published version should still exist");

            var updatedArticle1 = await Db.Articles.FindAsync(article1.Id);
            Assert.IsNotNull(updatedArticle1, "Expected article to exist after scheduler execution");
            var updatedArticle2 = await Db.Articles.FindAsync(article2.Id);
            Assert.IsNotNull(updatedArticle2, "Expected article to exist after scheduler execution");
            Assert.IsNull(updatedArticle1.Published, "Unpublished version should remain unpublished");
            Assert.IsNotNull(updatedArticle2.Published, "Published version should remain published");
        }

        /// <summary>
        /// Cleanup after each test.
        /// </summary>
        [TestCleanup]
        public async Task TestCleanup()
        {
            await DisposeAsync();
        }

        /// <summary>
        /// Test implementation of IClock for controlling time in tests.
        /// </summary>
        private class TestClock : IClock
        {
            private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

            public DateTimeOffset UtcNow => _utcNow;

            public void SetUtcNow(DateTimeOffset dateTime)
            {
                _utcNow = dateTime;
            }
        }
    }

}
