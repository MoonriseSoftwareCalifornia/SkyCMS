// <copyright file="ArticleViewModelBuilderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Shared
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Articles.Shared;
    using Cosmos.Common.Features.Layouts.Queries;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="ArticleViewModelBuilder"/>.
    /// Validates view model construction from Article and PublishedPage entities
    /// with author resolution, layout integration, and Open Graph metadata.
    /// </summary>
    [TestClass]
    public class ArticleViewModelBuilderTests : CommonTestsBase
    {
        /// <summary>
        /// Initializes the shared test infrastructure for this test class.
        /// </summary>
        /// <param name="context">Test context provided by MSTest.</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            ContextPool = new TestDbContextPool();
        }

        /// <summary>
        /// Cleans up the shared test infrastructure after all tests complete.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            ContextPool?.Dispose();
        }

        private static Mock<IMediator> CreateMockMediatorWithLayout()
        {
            var mockMediator = new Mock<IMediator>();
            var mockLayout = new LayoutViewModel
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = true,
                Head = "<head></head>",
                HtmlHeader = "<header></header>",
                FooterHtmlContent = "<footer></footer>",
                Notes = "Test layout"
            };

            mockMediator.Setup(m => m.QueryAsync(It.IsAny<GetDefaultLayoutQuery>(), default))
                .ReturnsAsync(mockLayout);

            return mockMediator;
        }

        [TestMethod]
        public void Constructor_WithValidDependencies_ShouldSucceed()
        {
            using var context = GetIsolatedContext();
            var mockMediator = new Mock<IMediator>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var publisherUrl = "https://publisher.test";

            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, memoryCache, publisherUrl);

            Assert.IsNotNull(builder);
        }

        [TestMethod]
        public void Constructor_WithNullMediator_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            try
            {
                var builder = new ArticleViewModelBuilder(null!, context, memoryCache, "https://test.com");
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("mediator", ex.ParamName);
            }
        }

        [TestMethod]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            var mockMediator = new Mock<IMediator>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            try
            {
                var builder = new ArticleViewModelBuilder(mockMediator.Object, null!, memoryCache, "https://test.com");
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public void Constructor_WithNullMemoryCache_ShouldSucceed()
        {
            using var context = GetIsolatedContext();
            var mockMediator = new Mock<IMediator>();

            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            Assert.IsNotNull(builder);
        }

        [TestMethod]
        public void Constructor_WithIsEditorTrue_ShouldSucceed()
        {
            using var context = GetIsolatedContext();
            var mockMediator = new Mock<IMediator>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, memoryCache, "https://test.com", isEditor: true);

            Assert.IsNotNull(builder);
        }

        [TestMethod]
        public async Task BuildFromArticleAsync_WithNullArticle_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var mockMediator = new Mock<IMediator>();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            try
            {
                await builder.BuildFromArticleAsync(null!, "en-US");
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("article", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task BuildFromArticleAsync_WithValidArticle_ShouldReturnViewModel()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.Title = "Test Article";
            article.ArticleNumber = 123;

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            var result = await builder.BuildFromArticleAsync(article, "en-US");

            Assert.IsNotNull(result);
            Assert.AreEqual("Test Article", result.Title);
            Assert.AreEqual(123, result.ArticleNumber);
            Assert.AreEqual("en-US", result.LanguageCode);
        }

        [TestMethod]
        public async Task BuildFromArticleAsync_WithAuthorInfo_ShouldIncludeAuthor()
        {
            using var context = GetIsolatedContext();
            var userId = Guid.NewGuid().ToString();
            var authorInfo = new AuthorInfo
            {
                Id = userId,
                AuthorName = "John Doe",
                AuthorDescription = "Test author description"
            };
            context.AuthorInfos.Add(authorInfo);
            await context.SaveChangesAsync();

            var article = TestDataBuilder.CreateArticle();
            article.UserId = userId;

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            var result = await builder.BuildFromArticleAsync(article, "en-US");

            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result.AuthorInfo));
            Assert.IsTrue(result.AuthorInfo.Contains("John Doe"));
        }

        [TestMethod]
        public async Task BuildFromArticleAsync_WithoutAuthorInfo_ShouldHaveEmptyAuthor()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.UserId = null;

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            var result = await builder.BuildFromArticleAsync(article, "en-US");

            Assert.IsNotNull(result);
            Assert.AreEqual(string.Empty, result.AuthorInfo);
        }

        [TestMethod]
        public async Task BuildFromArticleAsync_WithIncludeLayoutTrue_ShouldIncludeLayout()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            var result = await builder.BuildFromArticleAsync(article, "en-US", includeLayout: true);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Layout);
            Assert.AreEqual("Test Layout", result.Layout.LayoutName);
        }

        [TestMethod]
        public async Task BuildFromArticleAsync_WithIsEditorTrue_ShouldSetReadWriteModeTrue()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com", isEditor: true);

            var result = await builder.BuildFromArticleAsync(article, "en-US");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.ReadWriteMode);
        }

        [TestMethod]
        public async Task BuildFromArticleAsync_WithIsEditorFalse_ShouldSetReadWriteModeFalse()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com", isEditor: false);

            var result = await builder.BuildFromArticleAsync(article, "en-US");

            Assert.IsNotNull(result);
            Assert.IsFalse(result.ReadWriteMode);
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithNullPublishedPage_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var mockMediator = new Mock<IMediator>();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            try
            {
                await builder.BuildFromPublishedPageAsync(null!, "en-US");
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("publishedPage", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithValidPublishedPage_ShouldReturnViewModel()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();
            publishedPage.Title = "Published Page";
            publishedPage.ArticleNumber = 456;
            publishedPage.Content = "<p>Test content</p>";

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            var result = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US");

            Assert.IsNotNull(result);
            Assert.AreEqual("Published Page", result.Title);
            Assert.AreEqual(456, result.ArticleNumber);
            Assert.AreEqual("<p>Test content</p>", result.Content);
            Assert.AreEqual("en-US", result.LanguageCode);
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithIncludeLayoutTrue_ShouldIncludeLayout()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            var result = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US", includeLayout: true);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Layout);
            Assert.AreEqual("Test Layout", result.Layout.LayoutName);
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithIncludeLayoutFalse_ShouldNotIncludeLayout()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();

            var mockMediator = new Mock<IMediator>();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            var result = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US", includeLayout: false);

            Assert.IsNotNull(result);
            Assert.IsNull(result.Layout);
            mockMediator.Verify(m => m.QueryAsync(It.IsAny<GetDefaultLayoutQuery>(), default), Times.Never);
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithLayoutCacheDuration_ShouldCacheLayout()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();
            var cacheDuration = TimeSpan.FromMinutes(10);

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, memoryCache, "https://test.com");

            var result1 = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US", layoutCacheDuration: cacheDuration);
            var result2 = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US", layoutCacheDuration: cacheDuration);

            Assert.IsNotNull(result1.Layout);
            Assert.IsNotNull(result2.Layout);
            mockMediator.Verify(m => m.QueryAsync(It.IsAny<GetDefaultLayoutQuery>(), default), Times.Once);
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithNullMemoryCache_ShouldNotCacheLayout()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();
            var cacheDuration = TimeSpan.FromMinutes(10);

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            var result1 = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US", layoutCacheDuration: cacheDuration);
            var result2 = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US", layoutCacheDuration: cacheDuration);

            Assert.IsNotNull(result1.Layout);
            Assert.IsNotNull(result2.Layout);
            mockMediator.Verify(m => m.QueryAsync(It.IsAny<GetDefaultLayoutQuery>(), default), Times.Exactly(2));
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithNullLayoutCacheDuration_ShouldNotCacheLayout()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, memoryCache, "https://test.com");

            var result1 = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US", layoutCacheDuration: null);
            var result2 = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US", layoutCacheDuration: null);

            Assert.IsNotNull(result1.Layout);
            Assert.IsNotNull(result2.Layout);
            mockMediator.Verify(m => m.QueryAsync(It.IsAny<GetDefaultLayoutQuery>(), default), Times.Exactly(2));
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithBannerImageAbsoluteUrl_ShouldUseAsIs()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();
            publishedPage.BannerImage = "https://external.com/image.jpg";

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://publisher.test");

            var result = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US");

            Assert.IsNotNull(result);
            Assert.AreEqual("https://external.com/image.jpg", result.OGImage);
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithBannerImageRelativeUrl_ShouldPrependPublisherUrl()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();
            publishedPage.BannerImage = "/images/banner.jpg";

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://publisher.test");

            var result = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US");

            Assert.IsNotNull(result);
            Assert.AreEqual("https://publisher.test/images/banner.jpg", result.OGImage);
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithEmptyBannerImage_ShouldHaveEmptyOGImage()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();
            publishedPage.BannerImage = string.Empty;

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://publisher.test");

            var result = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US");

            Assert.IsNotNull(result);
            Assert.AreEqual(string.Empty, result.OGImage);
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithUrlPath_ShouldGenerateOGUrl()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();
            publishedPage.UrlPath = "/articles/test-article";

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://publisher.test");

            var result = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US");

            Assert.IsNotNull(result);
            Assert.AreEqual("https://publisher.test/articles/test-article", result.OGUrl);
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_WithEmptyPublisherUrl_ShouldUseUrlPathAsIs()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();
            publishedPage.UrlPath = "/articles/test-article";

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, string.Empty);

            var result = await builder.BuildFromPublishedPageAsync(publishedPage, "en-US");

            Assert.IsNotNull(result);
            Assert.AreEqual("/articles/test-article", result.OGUrl);
        }

        [TestMethod]
        public async Task BuildFromPublishedPageAsync_ShouldMapAllProperties()
        {
            using var context = GetIsolatedContext();
            var publishedPage = TestDataBuilder.CreatePublishedPage();
            publishedPage.ArticleNumber = 789;
            publishedPage.VersionNumber = 3;
            publishedPage.Title = "Full Test";
            publishedPage.Content = "<div>Content</div>";
            publishedPage.UrlPath = "/test";
            publishedPage.StatusCode = (int)StatusCodeEnum.Active;
            publishedPage.Published = DateTimeOffset.UtcNow;
            publishedPage.Updated = DateTimeOffset.UtcNow.AddDays(1);
            publishedPage.Expires = DateTimeOffset.UtcNow.AddDays(30);
            publishedPage.HeaderJavaScript = "console.log('header');";
            publishedPage.FooterJavaScript = "console.log('footer');";
            publishedPage.AuthorInfo = "Test Author";
            publishedPage.ArticleType = (int)ArticleType.General;
            publishedPage.Category = "Test Category";
            publishedPage.Introduction = "Test intro";

            var mockMediator = CreateMockMediatorWithLayout();
            var builder = new ArticleViewModelBuilder(mockMediator.Object, context, null, "https://test.com");

            var result = await builder.BuildFromPublishedPageAsync(publishedPage, "es-ES");

            Assert.IsNotNull(result);
            Assert.AreEqual(789, result.ArticleNumber);
            Assert.AreEqual(3, result.VersionNumber);
            Assert.AreEqual("Full Test", result.Title);
            Assert.AreEqual("<div>Content</div>", result.Content);
            Assert.AreEqual("/test", result.UrlPath);
            Assert.AreEqual(StatusCodeEnum.Active, result.StatusCode);
            Assert.AreEqual(publishedPage.Published, result.Published);
            Assert.AreEqual(publishedPage.Updated, result.Updated);
            Assert.AreEqual(publishedPage.Expires, result.Expires);
            Assert.AreEqual("console.log('header');", result.HeadJavaScript);
            Assert.AreEqual("console.log('footer');", result.FooterJavaScript);
            Assert.AreEqual("Test Author", result.AuthorInfo);
            Assert.AreEqual("es-ES", result.LanguageCode);
            Assert.AreEqual(ArticleType.General, result.ArticleType);
            Assert.AreEqual("Test Category", result.Category);
            Assert.AreEqual("Test intro", result.Introduction);
            Assert.AreEqual(10, result.CacheDuration);
            Assert.AreEqual(string.Empty, result.OGDescription);
        }
    }
}
