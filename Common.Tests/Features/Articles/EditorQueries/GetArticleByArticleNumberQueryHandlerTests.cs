// <copyright file="GetArticleByArticleNumberQueryHandlerTests.cs" company="Moonrise Software, LLC">
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
    /// Tests for <see cref="GetArticleByArticleNumberQueryHandler"/>.
    /// Validates article retrieval by article number with optional version support.
    /// </summary>
    [TestClass]
    public class GetArticleByArticleNumberQueryHandlerTests : CommonTestsBase
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

            var handler = new GetArticleByArticleNumberQueryHandler(mockMediator.Object, context, memoryCache, config);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentArticleNumber_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var mockMediator = new Mock<IMediator>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByArticleNumberQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByArticleNumberQuery { ArticleNumber = 999999 };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithValidArticleNumber_ShouldReturnLatestVersion()
        {
            using var context = GetIsolatedContext();
            var article1 = TestDataBuilder.CreateArticle();
            article1.ArticleNumber = 12345;
            article1.VersionNumber = 1;
            article1.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article1);

            var article2 = TestDataBuilder.CreateArticle();
            article2.ArticleNumber = 12345;
            article2.VersionNumber = 3;
            article2.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article2);

            var article3 = TestDataBuilder.CreateArticle();
            article3.ArticleNumber = 12345;
            article3.VersionNumber = 2;
            article3.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article3);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByArticleNumberQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByArticleNumberQuery { ArticleNumber = 12345 };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(article2.Id, result.Id);
            Assert.AreEqual(3, result.VersionNumber);
        }

        [TestMethod]
        public async Task HandleAsync_WithSpecificVersion_ShouldReturnThatVersion()
        {
            using var context = GetIsolatedContext();
            var article1 = TestDataBuilder.CreateArticle();
            article1.ArticleNumber = 12345;
            article1.VersionNumber = 1;
            article1.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article1);

            var article2 = TestDataBuilder.CreateArticle();
            article2.ArticleNumber = 12345;
            article2.VersionNumber = 2;
            article2.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article2);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByArticleNumberQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByArticleNumberQuery 
            { 
                ArticleNumber = 12345,
                VersionNumber = 1
            };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(article1.Id, result.Id);
            Assert.AreEqual(1, result.VersionNumber);
        }

        [TestMethod]
        public async Task HandleAsync_WithDeletedArticle_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.ArticleNumber = 12345;
            article.VersionNumber = 1;
            article.StatusCode = (int)StatusCodeEnum.Deleted;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = new Mock<IMediator>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByArticleNumberQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByArticleNumberQuery { ArticleNumber = 12345 };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentVersion_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.ArticleNumber = 12345;
            article.VersionNumber = 1;
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = new Mock<IMediator>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByArticleNumberQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByArticleNumberQuery 
            { 
                ArticleNumber = 12345,
                VersionNumber = 999
            };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithInactiveArticle_ShouldReturnViewModel()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.ArticleNumber = 12345;
            article.VersionNumber = 1;
            article.StatusCode = (int)StatusCodeEnum.Inactive;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByArticleNumberQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByArticleNumberQuery { ArticleNumber = 12345 };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(article.Id, result.Id);
        }

        [TestMethod]
        public async Task HandleAsync_UsesConfigurationForPublisherUrl()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.ArticleNumber = 12345;
            article.VersionNumber = 1;
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var customPublisherUrl = "https://custom-publisher.example.com";
            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration(publisherUrl: customPublisherUrl);

            var handler = new GetArticleByArticleNumberQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByArticleNumberQuery { ArticleNumber = 12345 };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_BuildsViewModelWithEnUsLanguageCode()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.ArticleNumber = 12345;
            article.VersionNumber = 1;
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByArticleNumberQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByArticleNumberQuery { ArticleNumber = 12345 };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual("en-US", result.LanguageCode);
        }

        [TestMethod]
        public async Task HandleAsync_WithMultipleVersions_ReturnsHighestVersionNumber()
        {
            using var context = GetIsolatedContext();
            for (int i = 1; i <= 5; i++)
            {
                var article = TestDataBuilder.CreateArticle();
                article.ArticleNumber = 12345;
                article.VersionNumber = i;
                article.StatusCode = (int)StatusCodeEnum.Active;
                context.Articles.Add(article);
            }
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByArticleNumberQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByArticleNumberQuery { ArticleNumber = 12345 };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(5, result.VersionNumber);
        }
    }
}
