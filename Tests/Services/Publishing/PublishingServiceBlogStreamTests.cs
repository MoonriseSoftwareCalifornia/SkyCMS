using Cosmos.Cms.Common;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Models;
using Cosmos.Common.Services.BlogPublishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Editor.Services.Publishing;
using Sky.Editor.Services.StaticFiles;
using Sky.Editor.Services.TableOfContents;
using System;
using System.Threading.Tasks;

namespace Sky.Tests.Services.Publishing
{
    /// <summary>
    /// Unit tests for <see cref="PublishingService"/> blog stream functionality.
    /// Tests the new client-side orchestration model with versioned wrappers.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class PublishingServiceBlogStreamTests : SkyCmsTestBase
    {
        private Mock<IBlogStreamRenderingService> mockBlogStreamService = null!;
        private PublishingService publishingService = null!;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
            mockBlogStreamService = new Mock<IBlogStreamRenderingService>();
            
            // Configure default mock behavior
            mockBlogStreamService
                .Setup(s => s.GenerateBlogStreamWrapperAsync(It.IsAny<Article>(), It.IsAny<string>()))
                .ReturnsAsync((Article a, string b) => $"<html><body>Mock wrapper for {a.Title}</body></html>");

            publishingService = new PublishingService(
                Db,
                Storage,
                EditorSettings,
                new Mock<ILogger<PublishingService>>().Object,
                HttpContextAccessor,
                AuthorInfoService,
                Clock,
                null!, // IMediator
                mockBlogStreamService.Object,
                ViewRenderService,
                Services,
                Mock.Of<IPublishingProgressReporter>(),
                Services.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>(),
                null, // No domain event dispatcher for tests
                new Sky.Editor.Services.CDN.CdnPurgeService(
                    Db,
                    new Mock<ILogger<Sky.Editor.Services.CDN.CdnPurgeService>>().Object,
                    HttpContextAccessor,
                    EditorSettings),
                new Sky.Editor.Services.TableOfContents.TocService(
                    Storage,
                    EditorSettings,
                    Services.GetRequiredService<Cosmos.Common.Features.Articles.Shared.IArticleCatalogQueryService>(),
                    new Mock<ILogger<Sky.Editor.Services.TableOfContents.TocService>>().Object),
                new Sky.Editor.Services.StaticFiles.StaticFileService(
                    Storage,
                    EditorSettings,
                    ViewRenderService,
                    null!, // IMediator not needed for this test
                    new Mock<ILogger<Sky.Editor.Services.StaticFiles.StaticFileService>>().Object));
        }

        [TestCleanup]
        public void Cleanup() => Db.Dispose();

        #region PublishBlogStreamAsync Tests

        /// <summary>
        /// Verifies that PublishBlogStreamAsync creates new blog stream article when none exists.
        /// </summary>
        [TestMethod]
        public async Task PublishBlogStreamAsync_NewBlogStream_CreatesArticle()
        {
            // Arrange
            var blog = new Article
            {
                Title = "New Blog Stream",
                Introduction = "Welcome to my blog",
                BlogKey = "new-blog",
                Updated = DateTimeOffset.UtcNow,
                BannerImage = "https://example.com/banner.jpg"
            };

            // Act
            await publishingService.PublishBlogStreamAsync(blog);

            // Assert
            var created = await Db.Articles
                .FirstOrDefaultAsync(a => a.BlogKey == "new-blog" && a.ArticleType == (int)ArticleType.BlogStream);

            Assert.IsNotNull(created, "Blog stream article should be created");
            Assert.AreEqual("New Blog Stream", created.Title);
            Assert.AreEqual("Welcome to my blog", created.Introduction);
            Assert.AreEqual(1, created.VersionNumber, "Initial version should be 1");
            Assert.IsNotNull(created.Published, "Published timestamp should be set");
        }

        /// <summary>
        /// Verifies that PublishBlogStreamAsync updates existing blog stream and increments version.
        /// </summary>
        [TestMethod]
        public async Task PublishBlogStreamAsync_ExistingBlogStream_UpdatesAndIncrementsVersion()
        {
            // Arrange
            var existingBlog = new Article
            {
                ArticleNumber = 1,
                UrlPath = "existing-blog",
                BlogKey = "existing-blog",
                Title = "Old Title",
                Content = "Old content",
                Published = DateTimeOffset.UtcNow.AddDays(-1),
                Updated = DateTimeOffset.UtcNow.AddDays(-1),
                UserId = TestUserId.ToString(),
                StatusCode = (int)StatusCodeEnum.Active,
                ArticleType = (int)ArticleType.BlogStream,
                VersionNumber = 1,
                Category = "blog-stream"
            };
            Db.Articles.Add(existingBlog);
            await Db.SaveChangesAsync();

            var updateBlog = new Article
            {
                Title = "Updated Title",
                Introduction = "Updated introduction",
                BlogKey = "existing-blog",
                Updated = DateTimeOffset.UtcNow,
                BannerImage = "https://example.com/new-banner.jpg"
            };

            // Act
            await publishingService.PublishBlogStreamAsync(updateBlog);

            // Assert
            var updated = await Db.Articles
                .FirstOrDefaultAsync(a => a.BlogKey == "existing-blog" && a.ArticleType == (int)ArticleType.BlogStream);

            Assert.IsNotNull(updated, "Blog stream should exist");
            Assert.AreEqual("Updated Title", updated.Title, "Title should be updated");
            Assert.AreEqual(2, updated.VersionNumber, "Version should be incremented");
        }

        /// <summary>
        /// Verifies that PublishBlogStreamAsync calls rendering service with correct parameters.
        /// </summary>
        [TestMethod]
        public async Task PublishBlogStreamAsync_CallsRenderingService()
        {
            // Arrange
            var blog = new Article
            {
                Title = "Test Blog",
                BlogKey = "test-blog",
                Updated = DateTimeOffset.UtcNow
            };

            // Act
            await publishingService.PublishBlogStreamAsync(blog);

            // Assert
            mockBlogStreamService.Verify(
                s => s.GenerateBlogStreamWrapperAsync(It.IsAny<Article>(), "test-blog"),
                Times.Once,
                "Should call rendering service once");
        }

        /// <summary>
        /// Verifies that PublishBlogStreamAsync stores wrapper HTML in article content.
        /// </summary>
        [TestMethod]
        public async Task PublishBlogStreamAsync_StoresWrapperHtmlInContent()
        {
            // Arrange
            const string expectedHtml = "<html><body>Test wrapper</body></html>";
            mockBlogStreamService
                .Setup(s => s.GenerateBlogStreamWrapperAsync(It.IsAny<Article>(), It.IsAny<string>()))
                .ReturnsAsync(expectedHtml);

            var blog = new Article
            {
                Title = "Test Blog",
                BlogKey = "test-blog",
                Updated = DateTimeOffset.UtcNow
            };

            // Act
            await publishingService.PublishBlogStreamAsync(blog);

            // Assert
            var created = await Db.Articles
                .FirstOrDefaultAsync(a => a.BlogKey == "test-blog" && a.ArticleType == (int)ArticleType.BlogStream);

            Assert.IsNotNull(created, "Blog stream should be created");
            Assert.AreEqual(expectedHtml, created.Content, "Content should contain wrapper HTML");
        }

        /// <summary>
        /// Verifies that blog stream article is published (has Published timestamp).
        /// </summary>
        [TestMethod]
        public async Task PublishBlogStreamAsync_PublishesArticle()
        {
            // Arrange
            var blog = new Article
            {
                Title = "Test Blog",
                BlogKey = "test-blog",
                Updated = DateTimeOffset.UtcNow
            };

            // Act
            await publishingService.PublishBlogStreamAsync(blog);

            // Assert
            var article = await Db.Articles
                .FirstOrDefaultAsync(a => a.BlogKey == "test-blog" && a.ArticleType == (int)ArticleType.BlogStream);

            Assert.IsNotNull(article, "Blog stream should exist");
            Assert.IsNotNull(article.Published, "Blog stream should be published");
            Assert.IsTrue(article.Published <= DateTimeOffset.UtcNow, "Published timestamp should be current or past");
        }

        /// <summary>
        /// Verifies that PublishBlogStreamAsync sets StatusCode to Active.
        /// </summary>
        [TestMethod]
        public async Task PublishBlogStreamAsync_SetsStatusCodeActive()
        {
            // Arrange
            var blog = new Article
            {
                Title = "Test Blog",
                BlogKey = "test-blog",
                Updated = DateTimeOffset.UtcNow
            };

            // Act
            await publishingService.PublishBlogStreamAsync(blog);

            // Assert
            var article = await Db.Articles
                .FirstOrDefaultAsync(a => a.BlogKey == "test-blog" && a.ArticleType == (int)ArticleType.BlogStream);

            Assert.AreEqual((int)StatusCodeEnum.Active, article.StatusCode, "Status code should be Active");
        }

        /// <summary>
        /// Verifies that PublishBlogStreamAsync preserves blog metadata.
        /// </summary>
        [TestMethod]
        public async Task PublishBlogStreamAsync_PreservesBlogMetadata()
        {
            // Arrange
            var bannerUrl = "https://example.com/banner.jpg";
            var blog = new Article
            {
                Title = "Test Blog",
                Introduction = "Test introduction",
                BlogKey = "test-blog",
                BannerImage = bannerUrl,
                Updated = DateTimeOffset.UtcNow
            };

            // Act
            await publishingService.PublishBlogStreamAsync(blog);

            // Assert
            var article = await Db.Articles
                .FirstOrDefaultAsync(a => a.BlogKey == "test-blog" && a.ArticleType == (int)ArticleType.BlogStream);

            Assert.AreEqual("Test Blog", article.Title);
            Assert.AreEqual("Test introduction", article.Introduction);
            Assert.AreEqual(bannerUrl, article.BannerImage);
        }

        #endregion

        #region PublishAsync Blog Post Tests

        /// <summary>
        /// Verifies that PublishAsync creates a PublishedPage for blog posts.
        /// </summary>
        [TestMethod]
        public async Task PublishAsync_BlogPost_CreatesPublishedPage()
        {
            // Arrange
            var blogPost = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "test-blog/test-post",
                Title = "Test Post",
                Content = "<p>Test content</p>",
                Published = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString(),
                StatusCode = (int)StatusCodeEnum.Active,
                ArticleType = (int)ArticleType.BlogPost,
                BlogKey = "test-blog",
                VersionNumber = 1
            };

            // Act
            await publishingService.PublishAsync(blogPost);

            // Assert
            var publishedPage = await Db.Pages
                .FirstOrDefaultAsync(p => p.UrlPath == "test-blog/test-post");

            Assert.IsNotNull(publishedPage, "PublishedPage should be created");
            Assert.AreEqual("Test Post", publishedPage.Title);
            Assert.AreEqual((int)ArticleType.BlogPost, publishedPage.ArticleType);
        }

        /// <summary>
        /// Verifies that blog post article type preserves BlogKey.
        /// </summary>
        [TestMethod]
        public async Task PublishAsync_BlogPost_PreservesBlogKey()
        {
            // Arrange
            var blogPost = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "test-blog/test-post",
                Title = "Test Post",
                Content = "<p>Test content</p>",
                Published = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString(),
                StatusCode = (int)StatusCodeEnum.Active,
                ArticleType = (int)ArticleType.BlogPost,
                BlogKey = "test-blog",
                VersionNumber = 1
            };

            // Act
            await publishingService.PublishAsync(blogPost);

            // Assert
            var publishedPage = await Db.Pages
                .FirstOrDefaultAsync(p => p.UrlPath == "test-blog/test-post");

            Assert.IsNotNull(publishedPage, "PublishedPage should exist");
            Assert.AreEqual("test-blog", publishedPage.BlogKey, "BlogKey should be preserved");
        }

        #endregion
    }
}
