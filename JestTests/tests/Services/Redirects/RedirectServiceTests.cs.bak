// <copyright file="RedirectServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Redirects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Redirects;
    using Sky.Editor.Services.Slugs;

    /// <summary>
    /// Unit tests for the <see cref="RedirectService"/> class.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class RedirectServiceTests : SkyCmsTestBase
    {
        private RedirectService redirectService;
        private Mock<ISlugService> mockSlugService;
        private Mock<IClock> mockClock;
        private Mock<IPublishingService> mockPublishingService;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext();

            // Initialize mocks
            mockSlugService = new Mock<ISlugService>();
            mockClock = new Mock<IClock>();
            mockPublishingService = new Mock<IPublishingService>();

            // Setup default mock behavior
            mockSlugService.Setup(s => s.Normalize(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string slug, string blogKey) => slug?.ToLowerInvariant().Replace(" ", "-").TrimEnd('/') ?? string.Empty);

            mockClock.Setup(c => c.UtcNow).Returns(DateTimeOffset.UtcNow);

            mockPublishingService.Setup(p => p.PublishAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CdnResult>());

            // Create service instance
            redirectService = new RedirectService(
                Db,
                mockSlugService.Object,
                mockClock.Object,
                mockPublishingService.Object);
        }

        #region CreateOrUpdateRedirectAsync Tests

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_RootSlug_ReturnsNull()
        {
            // Arrange
            mockSlugService.Setup(s => s.Normalize("root", It.IsAny<string>()))
                .Returns("root");

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync("root", "destination", TestUserId);

            // Assert
            Assert.IsNull(result, "Redirect from root should be silently ignored and return null");
            mockPublishingService.Verify(p => p.PublishAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        [TestCategory("Slugs")]
        public async Task CreateOrUpdateRedirectAsync_ValidSlugs_NormalizesSlugs()
        {
            // Arrange
            mockSlugService.Setup(s => s.Normalize("Old Page", It.IsAny<string>()))
                .Returns("old-page");
            mockSlugService.Setup(s => s.Normalize("New Page", It.IsAny<string>()))
                .Returns("new-page");

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync("Old Page", "New Page", TestUserId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("old-page", result.UrlPath);
            mockSlugService.Verify(s => s.Normalize("Old Page", It.IsAny<string>()), Times.Once);
            mockSlugService.Verify(s => s.Normalize("New Page", It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ValidInput_CreatesRedirectArticle()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual((int)StatusCodeEnum.Redirect, result.StatusCode);
            Assert.AreEqual(fromSlug, result.UrlPath);
            Assert.AreEqual(fromSlug, result.Title);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ValidInput_GeneratesRedirectJavaScript()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.IsNotNull(result.HeaderJavaScript);
            Assert.IsTrue(result.HeaderJavaScript.Contains("<script type=\"text/javascript\">"));
            Assert.IsTrue(result.HeaderJavaScript.Contains($"window.location.href = '{System.Net.WebUtility.HtmlEncode(toSlug)}';"));
            Assert.IsTrue(result.HeaderJavaScript.Contains("</script>"));
            Assert.IsTrue(result.HeaderJavaScript.Contains("<noscript>"));
            Assert.IsTrue(result.HeaderJavaScript.Contains($"<meta http-equiv=\"refresh\" content=\"0; url='{System.Net.WebUtility.HtmlEncode(toSlug)}'\" />"));
            Assert.IsTrue(result.HeaderJavaScript.Contains("</noscript>"));
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ValidInput_GeneratesRedirectPageBody()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.IsNotNull(result.Content);
            Assert.IsTrue(result.Content.Contains($"<h1>Redirecting to {System.Net.WebUtility.HtmlEncode(toSlug)}</h1>"));
            Assert.IsTrue(result.Content.Contains($"<a href=\"/{System.Net.WebUtility.HtmlEncode(toSlug)}\">here</a>"));
            Assert.IsTrue(result.Content.Contains("Redirect generated by SkyCMS."));
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_EmptyDatabase_AssignsArticleNumber2()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.AreEqual(2, result.ArticleNumber);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ExistingArticles_IncrementsArticleNumber()
        {
            // Arrange
            var existingArticle = new Article
            {
                ArticleNumber = 5,
                Title = "Existing Article",
                UrlPath = "existing",
                StatusCode = (int)StatusCodeEnum.Active,
                UserId = TestUserId.ToString()
            };

            Db.Articles.Add(existingArticle);
            await Db.SaveChangesAsync();

            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.AreEqual(6, result.ArticleNumber); // Max (5) + 1
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ValidInput_SetsTimestampsFromClock()
        {
            // Arrange
            var fixedTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            mockClock.Setup(c => c.UtcNow).Returns(fixedTime);

            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.AreEqual(fixedTime, result.Published);
            Assert.AreEqual(fixedTime, result.Updated);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ValidInput_SetsVersionNumberTo1()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.AreEqual(1, result.VersionNumber);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ValidInput_SetsUserId()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";
            var userId = Guid.NewGuid();

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, userId);

            // Assert
            Assert.AreEqual(userId.ToString(), result.UserId);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ValidInput_SetsBannerImageToEmpty()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.AreEqual(string.Empty, result.BannerImage);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ValidInput_SavesArticleToDatabase()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            var savedArticle = await Db.Articles.FirstOrDefaultAsync(a => a.Id == result.Id);
            Assert.IsNotNull(savedArticle);
            Assert.AreEqual((int)StatusCodeEnum.Redirect, savedArticle.StatusCode);
            Assert.AreEqual(fromSlug, savedArticle.UrlPath);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ValidInput_CreatesArticleNumberEntry()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            var articleNumber = await Db.ArticleNumbers.FirstOrDefaultAsync(an => an.LastNumber == result.ArticleNumber);
            Assert.IsNotNull(articleNumber);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ValidInput_PublishesArticle()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            mockPublishingService.Verify(
                p => p.PublishAsync(It.Is<Article>(a => a.Id == result.Id), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        [TestCategory("Slugs")]
        public async Task CreateOrUpdateRedirectAsync_FromSlugWithTrailingSlash_TrimsSlash()
        {
            // Arrange
            var fromSlug = "old-page/";
            var toSlug = "new-page";

            mockSlugService.Setup(s => s.Normalize(fromSlug, It.IsAny<string>()))
                .Returns("old-page");

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.AreEqual("old-page", result.UrlPath);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_ToSlugWithSpecialChars_HtmlEncodes()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page?param=value&other=123";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            var encodedSlug = System.Net.WebUtility.HtmlEncode(toSlug);
            Assert.IsTrue(result.HeaderJavaScript.Contains(encodedSlug));
            Assert.IsTrue(result.Content.Contains(encodedSlug));
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_MultipleRedirects_CreatesMultipleArticles()
        {
            // Arrange
            var redirects = new[]
            {
                ("old-page-1", "new-page-1"),
                ("old-page-2", "new-page-2"),
                ("old-page-3", "new-page-3")
            };

            // Act
            var results = new List<Article>();
            foreach (var (from, to) in redirects)
            {
                var result = await redirectService.CreateOrUpdateRedirectAsync(from, to, TestUserId);
                results.Add(result);
            }

            // Assert
            Assert.AreEqual(3, results.Count);
            var savedArticles = await Db.Articles
                .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect)
                .ToListAsync();
            Assert.AreEqual(3, savedArticles.Count);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_EmptyArticlesTable_HandlesGracefully()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Ensure database is empty
            Db.Articles.RemoveRange(Db.Articles);
            await Db.SaveChangesAsync();

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.ArticleNumber); // 1 + 1
        }

        #endregion

        #region Edge Cases and Error Handling

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_EmptyGuidUserId_SetsEmptyGuidAsString()
        {
            // Arrange
            var fromSlug = "old-page";
            var toSlug = "new-page";

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, Guid.Empty);

            // Assert
            Assert.AreEqual(Guid.Empty.ToString(), result.UserId);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_RootSlugUpperCase_ReturnsNull()
        {
            // Arrange
            mockSlugService.Setup(s => s.Normalize("ROOT", It.IsAny<string>()))
                .Returns("root");

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync("ROOT", "destination", TestUserId);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task CreateOrUpdateRedirectAsync_VeryLongSlugs_CreatesRedirect()
        {
            // Arrange
            var fromSlug = new string('a', 1000);
            var toSlug = new string('b', 1000);

            // Act
            var result = await redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId);

            // Assert
            Assert.IsNotNull(result);
            mockPublishingService.Verify(p => p.PublishAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        [TestCategory("Concurrency")]
        public async Task CreateOrUpdateRedirectAsync_ConcurrentRequests_HandlesGracefully()
        {
            // Arrange
            var tasks = new List<Task<Article>>();

            // Act - Create 5 concurrent redirects
            for (int i = 0; i < 5; i++)
            {
                var fromSlug = $"old-page-{i}";
                var toSlug = $"new-page-{i}";
                tasks.Add(redirectService.CreateOrUpdateRedirectAsync(fromSlug, toSlug, TestUserId));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(5, results.Length);
            Assert.IsTrue(results.All(r => r != null));
            var uniqueArticleNumbers = results.Select(r => r.ArticleNumber).Distinct().Count();
            Assert.AreEqual(5, uniqueArticleNumbers, "All redirects should have unique article numbers");
        }

        #endregion
    }
}
