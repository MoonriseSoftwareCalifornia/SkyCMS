// <copyright file="ArticleQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Features.Articles.Queries
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ArticleQueryHandlerTests
    {
        private ApplicationDbContext dbContext = null!;
        private IMemoryCache memoryCache = null!;
        private IConfiguration configuration = null!;
        private IMediator mediator = null!;
        private DateTimeOffset now;

        [TestInitialize]
        public void TestInitialize()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"ArticleQueryDb_{Guid.NewGuid()}")
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            dbContext = new ApplicationDbContext(options);
            memoryCache = new MemoryCache(new MemoryCacheOptions());
            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "CosmosPublisherUrl", "https://publisher.test" },
                    { "BlobPublicUrl", "https://cdn.test" }
                })
                .Build();

            // Create a minimal mediator for ArticleViewModelBuilder
            var services = new ServiceCollection();
            mediator = new Mediator(services.BuildServiceProvider());

            now = DateTimeOffset.UtcNow;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            memoryCache.Dispose();
            dbContext.Dispose();
        }

        [TestMethod]
        public async Task GetPublishedPageByUrl_ReturnsPublishedPage()
        {
            await SeedPublishedPageAsync("blog/test", "Test Title", "Hello world");

            var viewModelBuilder = new Cosmos.Common.Features.Articles.Shared.ArticleViewModelBuilder(mediator, dbContext, memoryCache, "https://publisher.test", isEditor: false);
            var publishedPageService = new Cosmos.Common.Features.Articles.Shared.PublishedPageQueryService(dbContext, memoryCache, viewModelBuilder);
            var handler = new GetPublishedPageByUrlQueryHandler(publishedPageService);

            var result = await handler.HandleAsync(new GetPublishedPageByUrlQuery
            {
                UrlPath = "blog/test",
                Lang = "en",
                CacheSpan = null,
                LayoutCache = null,
                IncludeLayout = false
            });

            Assert.IsNotNull(result);
            Assert.AreEqual("blog/test", result.UrlPath);
            Assert.AreEqual("Test Title", result.Title);
            Assert.IsNull(result.Layout);
        }

        [TestMethod]
        public async Task GetPublishedPageHeaderByUrl_ReturnsHeader()
        {
            var page = await SeedPublishedPageAsync("about", "About", "Header content");

            var viewModelBuilder = new Cosmos.Common.Features.Articles.Shared.ArticleViewModelBuilder(mediator, dbContext, memoryCache, "https://publisher.test", isEditor: false);
            var publishedPageService = new Cosmos.Common.Features.Articles.Shared.PublishedPageQueryService(dbContext, memoryCache, viewModelBuilder);
            var handler = new GetPublishedPageHeaderByUrlQueryHandler(publishedPageService);

            var result = await handler.HandleAsync(new GetPublishedPageHeaderByUrlQuery
            {
                UrlPath = "about"
            });

            Assert.IsNotNull(result);
            Assert.AreEqual(page.Id, result.Id);
            Assert.AreEqual(page.ArticleNumber, result.ArticleNumber);
        }

        [TestMethod]
        public async Task GetTableOfContents_ReturnsItems()
        {
            await SeedCatalogEntryAsync("root", "Home", published: now.AddMinutes(-10));

            var catalogService = new Cosmos.Common.Features.Articles.Shared.ArticleCatalogQueryService(dbContext, "https://publisher.test", "https://cdn.test");
            var handler = new GetTableOfContentsQueryHandler(catalogService);

            var result = await handler.HandleAsync(new GetTableOfContentsQuery
            {
                Page = string.Empty,
                PageNo = 0,
                PageSize = 10,
                OrderByPublishedDate = false
            });

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("root", result.Items[0].UrlPath);
        }

        [TestMethod]
        public async Task SearchPublishedArticles_ReturnsMatches()
        {
            dbContext.ArticleCatalog.Add(new CatalogEntry
            {
                ArticleNumber = 1,
                UrlPath = "search-me",
                Title = "Search Title",
                Published = now.AddMinutes(-5),
                Updated = now,
                BannerImage = string.Empty,
                AuthorInfo = string.Empty,
                Introduction = "Hello searchable content"
            });
            await dbContext.SaveChangesAsync();

            var catalogService = new Cosmos.Common.Features.Articles.Shared.ArticleCatalogQueryService(dbContext, "https://publisher.test", "https://cdn.test");
            var handler = new SearchPublishedArticlesQueryHandler(catalogService);

            var result = await handler.HandleAsync(new SearchPublishedArticlesQuery
            {
                Text = "searchable"
            });

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("search-me", result[0].UrlPath);
        }

        private async Task<PublishedPage> SeedPublishedPageAsync(string urlPath, string title, string content)
        {
            var page = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = urlPath,
                Title = title,
                Content = content,
                VersionNumber = 1,
                Published = now.AddMinutes(-5),
                Updated = now.AddMinutes(-1),
                StatusCode = 0,
                BannerImage = string.Empty,
                AuthorInfo = string.Empty,
                Category = string.Empty,
                Introduction = string.Empty,
                ArticleType = 0
            };

            dbContext.Pages.Add(page);
            await dbContext.SaveChangesAsync();
            return page;
        }

        private async Task SeedCatalogEntryAsync(string urlPath, string title, DateTimeOffset? published)
        {
            dbContext.ArticleCatalog.Add(new CatalogEntry
            {
                ArticleNumber = 1,
                UrlPath = urlPath,
                Title = title,
                Published = published,
                Updated = now,
                BannerImage = string.Empty,
                AuthorInfo = string.Empty,
                Introduction = string.Empty
            });

            await dbContext.SaveChangesAsync();
        }
    }
}
