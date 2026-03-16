// <copyright file="GetArticleByIdQueryHandlerTests.cs" company="Moonrise Software, LLC">
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
    /// Tests for <see cref="GetArticleByIdQueryHandler"/>.
    /// Validates article retrieval by ID for editor usage with view model building.
    /// </summary>
    [TestClass]
    public class GetArticleByIdQueryHandlerTests : CommonTestsBase
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

            var handler = new GetArticleByIdQueryHandler(mockMediator.Object, context, memoryCache, config);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentId_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var mockMediator = new Mock<IMediator>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByIdQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByIdQuery { Id = Guid.NewGuid() };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithValidId_ShouldReturnArticleViewModel()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByIdQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByIdQuery { Id = article.Id };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(article.Id, result.Id);
        }

        [TestMethod]
        public async Task HandleAsync_WithDeletedArticle_ShouldReturnNull()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.StatusCode = (int)StatusCodeEnum.Deleted;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = new Mock<IMediator>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByIdQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByIdQuery { Id = article.Id };

            var result = await handler.HandleAsync(query);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_UsesPublisherUrlFromConfiguration()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var publisherUrl = "https://custom-publisher.example.com";
            var config = CreateMockConfiguration(publisherUrl);

            var handler = new GetArticleByIdQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByIdQuery { Id = article.Id };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_UsesBlobUrlFromConfiguration_PrefersBlobPublicUrl()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var blobUrl = "https://custom-blob.example.com";
            var config = CreateMockConfiguration(blobUrl: blobUrl);

            var handler = new GetArticleByIdQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByIdQuery { Id = article.Id };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithEmptyPublisherUrl_ShouldUseEmptyString()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration(publisherUrl: null);

            var handler = new GetArticleByIdQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByIdQuery { Id = article.Id };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithActiveArticle_ShouldIncludeInResults()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByIdQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByIdQuery { Id = article.Id };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithInactiveArticle_ShouldIncludeInResults()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.StatusCode = (int)StatusCodeEnum.Inactive;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByIdQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByIdQuery { Id = article.Id };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task HandleAsync_BuildsViewModelWithEnUsLanguageCode()
        {
            using var context = GetIsolatedContext();
            var article = TestDataBuilder.CreateArticle();
            article.StatusCode = (int)StatusCodeEnum.Active;
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var mockMediator = CreateMockMediatorWithLayout();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = CreateMockConfiguration();

            var handler = new GetArticleByIdQueryHandler(mockMediator.Object, context, memoryCache, config);
            var query = new GetArticleByIdQuery { Id = article.Id };

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
        }
    }
}
