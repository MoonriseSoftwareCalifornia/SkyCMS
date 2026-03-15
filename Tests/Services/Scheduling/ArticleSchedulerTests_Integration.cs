// <copyright file="ArticleSchedulerTests_Integration.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Scheduling
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.EmailServices;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Scheduling;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Integration tests for <see cref="ArticleScheduler"/> class.
    /// Tests scheduled publishing workflow and version management.
    /// </summary>
    [TestClass]
    public class ArticleSchedulerTests_Integration : SkyCmsTestBase
    {
        private ArticleScheduler _scheduler;
        private Mock<IClock> _mockClock;
        private Mock<IEditorSettings> _mockSettings;
        private Mock<ICosmosEmailSender> _mockEmailSender;
        private Mock<ITenantArticleLogicFactory> _mockTenantArticleLogicFactory;
        private IServiceProvider _serviceProvider;
        private ServiceCollection _serviceCollection;
        private DateTimeOffset _testNow;

        /// <summary>
        /// Initializes test fixtures.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            InitializeTestContext();

            _testNow = new DateTimeOffset(2026, 2, 3, 12, 0, 0, TimeSpan.Zero);

            // Setup mocks
            _mockClock = new Mock<IClock>();
            _mockClock.Setup(x => x.UtcNow).Returns(_testNow);

            _mockSettings = new Mock<IEditorSettings>();
            _mockSettings.Setup(x => x.IsMultiTenantEditor).Returns(false);

            _mockEmailSender = new Mock<ICosmosEmailSender>();
            _mockEmailSender
                .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Setup mock factory that returns the test's ArticleEditLogic (Logic property from base class)
            _mockTenantArticleLogicFactory = new Mock<ITenantArticleLogicFactory>();
            _mockTenantArticleLogicFactory
                .Setup(x => x.CreateForTenantAsync(It.IsAny<string>()))
                .ReturnsAsync(Logic);

            // Setup service collection
            _serviceCollection = new ServiceCollection();
            // ?? CRITICAL: Use Singleton for Db and Storage to prevent disposal by scoped services in ArticleScheduler
            // ArticleScheduler creates scoped service providers which would dispose scoped Db instances, 
            // breaking test assertions that need to query the Db after scheduler execution
            _serviceCollection.AddSingleton(_ => Db);
            _serviceCollection.AddSingleton(_ => Storage);
            _serviceCollection.AddScoped(_ => _mockSettings.Object);
            _serviceCollection.AddScoped(_ => _mockClock.Object);
            _serviceCollection.AddScoped(_ => _mockEmailSender.Object);
            _serviceCollection.AddScoped(_ => _mockTenantArticleLogicFactory.Object);
            _serviceCollection.AddScoped(_ => new Mock<IConfiguration>().Object);
            _serviceCollection.AddScoped(_ => new Mock<UserManager<IdentityUser>>(
                new Mock<IUserStore<IdentityUser>>().Object, null, null, null, null, null, null, null, null).Object);
            _serviceCollection.AddLogging();
            _serviceCollection.AddMemoryCache();

            _serviceProvider = _serviceCollection.BuildServiceProvider();
            var logger = _serviceProvider.GetRequiredService<ILogger<ArticleScheduler>>();
            _scheduler = new ArticleScheduler(logger, _mockSettings.Object, _mockClock.Object, _serviceProvider);
        }

        /// <summary>
        /// Cleanup after tests.
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            Db?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose();
        }

        #region Constructor Tests

        /// <summary>
        /// Test: ArticleScheduler constructor throws when settings is null.
        /// </summary>
        [TestMethod]
        [TestCategory("ArticleScheduler.Constructor")]
        public void Constructor_NullSettings_ThrowsArgumentNull()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new ArticleScheduler(
                    new Mock<ILogger<ArticleScheduler>>().Object,
                    null, // null settings
                    _mockClock.Object,
                    _serviceProvider));
        }

        /// <summary>
        /// Test: ArticleScheduler constructor throws when clock is null.
        /// </summary>
        [TestMethod]
        [TestCategory("ArticleScheduler.Constructor")]
        public void Constructor_NullClock_ThrowsArgumentNull()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new ArticleScheduler(
                    new Mock<ILogger<ArticleScheduler>>().Object,
                    _mockSettings.Object,
                    null, // null clock
                    _serviceProvider));
        }

        /// <summary>
        /// Test: ArticleScheduler constructor throws when service provider is null.
        /// </summary>
        [TestMethod]
        [TestCategory("ArticleScheduler.Constructor")]
        public void Constructor_NullServiceProvider_ThrowsArgumentNull()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new ArticleScheduler(
                    new Mock<ILogger<ArticleScheduler>>().Object,
                    _mockSettings.Object,
                    _mockClock.Object,
                    null)); // null service provider
        }

        #endregion

        #region ExecuteAsync - Scheduling Tests

        /// <summary>
        /// Test: ExecuteAsync should process scheduled articles correctly.
        /// </summary>
        [TestMethod]
        [TestCategory("ArticleScheduler.Execution")]
        public async Task ExecuteAsync_PublishesScheduledArticles()
        {
            // Arrange
            var home = await CreateArticleAsync("Home", TestUserId);
            var scheduledArticle = await CreateArticleAsync("Scheduled Article", TestUserId);

            // Create a past-published version (should be activated)
            // NOTE: Must modify the actual Article entity, not the ArticleViewModel
            var scheduledArticleEntity = await Db.Articles.FirstAsync(a => a.Id == scheduledArticle.Id);
            scheduledArticleEntity.Published = _testNow.AddHours(-1);
            scheduledArticleEntity.VersionNumber = 1;
            await Db.SaveChangesAsync();

            // Create a future-published version
            var futureVersion = new Article
            {
                ArticleNumber = scheduledArticle.ArticleNumber,
                Title = "Scheduled Article v2",
                VersionNumber = 2,
                Published = _testNow.AddHours(1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(futureVersion);
            await Db.SaveChangesAsync();

            // Act
            await _scheduler.ExecuteAsync();

            // Assert
            // Debug: Check all articles for this ArticleNumber
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == scheduledArticle.ArticleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();

            Assert.IsTrue(allVersions.Count >= 2, $"Should have at least 2 versions, found {allVersions.Count}");

            var activeVersion = allVersions.FirstOrDefault(a => a.Published.HasValue && a.Published <= _testNow);

            Assert.IsNotNull(activeVersion,
                $"Should have activated past-published version. Versions found: " +
                $"{string.Join(", ", allVersions.Select(v => $"v{v.VersionNumber} Published={v.Published}"))}");
        }

        /// <summary>
        /// Test: ExecuteAsync should skip articles with no multiple versions.
        /// </summary>
        [TestMethod]
        [TestCategory("ArticleScheduler.Execution")]
        public async Task ExecuteAsync_SkipsSingleVersionArticles()
        {
            // Arrange
            var home = await CreateArticleAsync("Home", TestUserId);
            var singleVersionArticle = await CreateArticleAsync("Single Version", TestUserId);
            singleVersionArticle.Published = _testNow.AddHours(-1);
            await Db.SaveChangesAsync();

            var articlesBeforeCount = await Db.Articles.CountAsync();

            // Act
            await _scheduler.ExecuteAsync();

            // Assert - Should not modify single-version articles
            var articlesAfterCount = await Db.Articles.CountAsync();
            Assert.AreEqual(articlesBeforeCount, articlesAfterCount, "Should not modify single-version articles");
        }

        /// <summary>
        /// Test: ExecuteAsync should skip deleted articles.
        /// </summary>
        [TestMethod]
        [TestCategory("ArticleScheduler.Execution")]
        public async Task ExecuteAsync_SkipsDeletedArticles()
        {
            // Arrange
            var home = await CreateArticleAsync("Home", TestUserId);
            var deletedArticle = await CreateArticleAsync("Deleted", TestUserId);
            deletedArticle.Published = _testNow.AddHours(-1);
            deletedArticle.StatusCode = StatusCodeEnum.Deleted;
            await Db.SaveChangesAsync();

            var versionCount = await Db.Articles
                .Where(a => a.ArticleNumber == deletedArticle.ArticleNumber)
                .CountAsync();

            // Act
            await _scheduler.ExecuteAsync();

            // Assert
            var versionCountAfter = await Db.Articles
                .Where(a => a.ArticleNumber == deletedArticle.ArticleNumber)
                .CountAsync();
            Assert.AreEqual(versionCount, versionCountAfter, "Should not process deleted articles");
        }

        #endregion

        #region Version Management Tests

        /// <summary>
        /// Test: Scheduler should activate the most recent published version.
        /// </summary>
        [TestMethod]
        [TestCategory("ArticleScheduler.VersionManagement")]
        public async Task ExecuteAsync_ActivatesMostRecentVersion()
        {
            // Arrange
            var home = await CreateArticleAsync("Home", TestUserId);
            var article = await CreateArticleAsync("Multi-Version", TestUserId);

            // Create v1 (oldest)
            article.Published = _testNow.AddHours(-3);
            article.VersionNumber = 1;
            await Db.SaveChangesAsync();

            // Create v2
            var v2 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Multi-Version v2",
                VersionNumber = 2,
                Published = _testNow.AddHours(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(v2);
            await Db.SaveChangesAsync();

            // Create v3 (future)
            var v3 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Multi-Version v3",
                VersionNumber = 3,
                Published = _testNow.AddHours(2),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(v3);
            await Db.SaveChangesAsync();

            // Act
            await _scheduler.ExecuteAsync();

            // Assert - v2 should be the active published version
            var versions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();

            Assert.IsTrue(versions.Any(v => v.VersionNumber == 2 && v.Published.HasValue),
                "v2 should remain published");
            Assert.IsFalse(versions.Any(v => v.VersionNumber == 1 && v.Published.HasValue),
                "v1 should be unpublished");
        }

        /// <summary>
        /// Test: Scheduler should unpublish old versions after activating new ones.
        /// </summary>
        [TestMethod]
        [TestCategory("ArticleScheduler.VersionManagement")]
        public async Task ExecuteAsync_UnpublishesOldVersions()
        {
            // Arrange
            var home = await CreateArticleAsync("Home", TestUserId);
            var article = await CreateArticleAsync("Multi-Version Article", TestUserId);

            // v1 - old published
            article.Published = _testNow.AddHours(-2);
            article.VersionNumber = 1;
            await Db.SaveChangesAsync();

            // v2 - new published (should become active)
            var v2 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "v2",
                VersionNumber = 2,
                Published = _testNow.AddHours(-1),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(v2);
            await Db.SaveChangesAsync();

            // Act
            await _scheduler.ExecuteAsync();

            // Assert
            var oldVersion = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber && a.VersionNumber == 1);

            Assert.IsNull(oldVersion.Published, "Old version should be unpublished");
        }

        #endregion

        #region Future Publication Tests

        /// <summary>
        /// Test: ExecuteAsync should not activate future-dated articles.
        /// </summary>
        [TestMethod]
        [TestCategory("ArticleScheduler.FuturePublication")]
        public async Task ExecuteAsync_SkipsFuturePublications()
        {
            // Arrange
            var home = await CreateArticleAsync("Home", TestUserId);
            var futureArticle = await CreateArticleAsync("Future Article", TestUserId);

            futureArticle.Published = _testNow.AddHours(2); // Future date
            futureArticle.VersionNumber = 1;
            await Db.SaveChangesAsync();

            var futureVersion = new Article
            {
                ArticleNumber = futureArticle.ArticleNumber,
                Title = "Future v2",
                VersionNumber = 2,
                Published = _testNow.AddHours(4),
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString()
            };
            Db.Articles.Add(futureVersion);
            await Db.SaveChangesAsync();

            var publishedCountBefore = await Db.Articles
                .Where(a => a.ArticleNumber == futureArticle.ArticleNumber && a.Published.HasValue)
                .CountAsync();

            // Act
            await _scheduler.ExecuteAsync();

            // Assert - Should not change published status
            var publishedCountAfter = await Db.Articles
                .Where(a => a.ArticleNumber == futureArticle.ArticleNumber && a.Published.HasValue)
                .CountAsync();

            Assert.AreEqual(publishedCountBefore, publishedCountAfter,
                "Should not activate future-dated publications");
        }

        #endregion

        #region Error Handling Tests

        #endregion

        #region Test Helpers

        private class MockArticleScheduler : ArticleScheduler
        {
            public MockArticleScheduler(
                ILogger<ArticleScheduler> logger,
                IEditorSettings settings,
                IClock clock,
                IServiceProvider serviceProvider)
                : base(logger, settings, clock, serviceProvider)
            {
            }
        }

        #endregion
    }
}