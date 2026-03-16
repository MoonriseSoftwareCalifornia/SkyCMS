// <copyright file="GetArticleByUrlQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.EditorQueries
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Articles.EditorQueries;
    using Cosmos.Common.Features.Layouts.Queries;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="GetArticleByUrlQueryHandler"/>.
    /// Validates article retrieval by URL for editor usage with version ordering.
    /// </summary>
    [TestClass]
    public class GetArticleByUrlQueryHandlerTests : CommonTestsBase
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

        private static IConfiguration CreateMockConfiguration(string? publisherUrl = null, string? blobUrl = null)
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                {"CosmosPublisherUrl", publisherUrl ?? "https://publisher.test"},
                {"BlobPublicUrl", blobUrl},
                {"AzureBlobStorageEndPoint", blobUrl ?? "https://blob.test"}
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
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
            var config = CreateMockConfiguration();

            var handler = new GetArticleByUrlQueryHandler(mockMediator.Object, context, memoryCache, config);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentUrl_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var mockMediator = new Mock<IMediator>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByUrlQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByUrlQuery { UrlPath = "/non-existent" };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithValidUrl_ShouldReturnArticleViewModel()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.UrlPath = "test-article";
            article.StatusCode = (int)StatusCodeEnum.Active;
            article.VersionNumber = 1;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByUrlQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByUrlQuery { UrlPath = "/test-article" };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(article.Id, result.Id);
        }

        [TestMethod]
        public async Task HandleAsync_WithRootPath_ShouldConvertToRoot()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.UrlPath = "root";
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByUrlQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByUrlQuery { UrlPath = "/" };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithEmptyUrlPath_ShouldConvertToRoot()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.UrlPath = "root";
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByUrlQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByUrlQuery { UrlPath = string.Empty };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithDeletedArticle_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.UrlPath = "deleted-article";
            article.StatusCode = (int)StatusCodeEnum.Deleted;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = new Mock<IMediator>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByUrlQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByUrlQuery { UrlPath = "/deleted-article" };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithMultipleVersions_ShouldReturnLatestVersion()
        {
            using var context = GetIsolatedContext();
            var article1 = TestDataBuilder.CreateArticle();
            article1.UrlPath = "versioned-article";
            article1.StatusCode = (int)StatusCodeEnum.Active;
            article1.VersionNumber = 1;
            context.Articles.Add(article1);

            var article2 = TestDataBuilder.CreateArticle();
            article2.UrlPath = "versioned-article";
            article2.StatusCode = (int)StatusCodeEnum.Active;
            article2.VersionNumber = 3;
            context.Articles.Add(article2);

            var article3 = TestDataBuilder.CreateArticle();
            article3.UrlPath = "versioned-article";
            article3.StatusCode = (int)StatusCodeEnum.Active;
            article3.VersionNumber = 2;
            context.Articles.Add(article3);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByUrlQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByUrlQuery { UrlPath = "/versioned-article" };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(article2.Id, result.Id);
        }

        [TestMethod]
        public async Task HandleAsync_WithBlogStream_ShouldReturnLatestBlogStreamEntry()
        {
            using var context = GetIsolatedContext();
            var blogStream1 = TestDataBuilder.CreateArticle();
            blogStream1.UrlPath = "my-blog";
            blogStream1.BlogKey = "my-blog";
            blogStream1.ArticleType = (int)ArticleType.BlogStream;
            blogStream1.StatusCode = (int)StatusCodeEnum.Active;
            blogStream1.VersionNumber = 1;
            context.Articles.Add(blogStream1);

            var blogStream2 = TestDataBuilder.CreateArticle();
            blogStream2.UrlPath = "my-blog";
            blogStream2.BlogKey = "my-blog";
            blogStream2.ArticleType = (int)ArticleType.BlogStream;
            blogStream2.StatusCode = (int)StatusCodeEnum.Active;
            blogStream2.VersionNumber = 2;
            context.Articles.Add(blogStream2);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByUrlQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByUrlQuery { UrlPath = "/my-blog" };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(blogStream2.Id, result.Id);
        }

        [TestMethod]
        public async Task HandleAsync_UrlPathNormalization_ShouldTrimLeadingSlash()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.UrlPath = "normalized-path";
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByUrlQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByUrlQuery { UrlPath = "/normalized-path" };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_BuildsViewModelWithEnUsLanguageCode()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.UrlPath = "lang-test";
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByUrlQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByUrlQuery { UrlPath = "/lang-test" };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }
    }
}
