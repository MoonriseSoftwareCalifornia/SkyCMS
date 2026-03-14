// <copyright file="EditorArticleQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Features.Articles.EditorQueries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Articles.EditorQueries;
    using Cosmos.Common.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for editor-specific article query handlers.
    /// </summary>
    [TestClass]
    public class EditorArticleQueryHandlerTests
    {
        private ApplicationDbContext dbContext = null!;
        private IMemoryCache memoryCache = null!;
        private IConfiguration configuration = null!;
        private DateTimeOffset now;

        [TestInitialize]
        public void TestInitialize()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"EditorArticleDb_{Guid.NewGuid()}")
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            dbContext = new ApplicationDbContext(options);
            memoryCache = new MemoryCache(new MemoryCacheOptions());
            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "CosmosPublisherUrl", "https://test.com" },
                    { "BlobPublicUrl", "https://cdn.test" }
                })
                .Build();
            now = DateTimeOffset.UtcNow;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            memoryCache.Dispose();
            dbContext.Dispose();
        }

        #region GetArticleByUrlQuery Tests

        [TestMethod]
        public async Task GetArticleByUrlQuery_WithValidUrl_ReturnsArticle()
        {
            // Arrange
            var article = CreateTestArticle("test-page", "Test Article");
            dbContext.Articles.Add(article);
            await dbContext.SaveChangesAsync();

            var handler = new GetArticleByUrlQueryHandler(null!, dbContext, memoryCache, configuration);

            // Act
            var result = await handler.HandleAsync(new GetArticleByUrlQuery { UrlPath = "test-page" });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Test Article", result.Title);
            Assert.AreEqual("test-page", result.UrlPath);
        }

        [TestMethod]
        public async Task GetArticleByUrlQuery_WithInvalidUrl_ReturnsNull()
        {
            // Arrange
            var handler = new GetArticleByUrlQueryHandler(null!, dbContext, memoryCache, configuration);

            // Act
            var result = await handler.HandleAsync(new GetArticleByUrlQuery { UrlPath = "nonexistent" });

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetArticleByIdQuery Tests

        [TestMethod]
        public async Task GetArticleByIdQuery_WithValidId_ReturnsArticle()
        {
            // Arrange
            var article = CreateTestArticle("article-1", "Article One");
            dbContext.Articles.Add(article);
            await dbContext.SaveChangesAsync();

            var handler = new GetArticleByIdQueryHandler(null!, dbContext, memoryCache, configuration);

            // Act
            var result = await handler.HandleAsync(new GetArticleByIdQuery { Id = article.Id });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(article.Id, result.Id);
            Assert.AreEqual("Article One", result.Title);
        }

        [TestMethod]
        public async Task GetArticleByIdQuery_WithInvalidId_ReturnsNull()
        {
            // Arrange
            var handler = new GetArticleByIdQueryHandler(null!, dbContext, memoryCache, configuration);

            // Act
            var result = await handler.HandleAsync(new GetArticleByIdQuery { Id = Guid.NewGuid() });

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetArticleByArticleNumberQuery Tests

        [TestMethod]
        public async Task GetArticleByArticleNumberQuery_WithValidNumber_ReturnsArticle()
        {
            // Arrange
            var article = CreateTestArticle("article-2", "Article Two");
            article.ArticleNumber = 42;
            dbContext.Articles.Add(article);
            await dbContext.SaveChangesAsync();

            var handler = new GetArticleByArticleNumberQueryHandler(null!, dbContext, memoryCache, configuration);

            // Act
            var result = await handler.HandleAsync(new GetArticleByArticleNumberQuery { ArticleNumber = 42 });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(42, result.ArticleNumber);
            Assert.AreEqual("Article Two", result.Title);
        }

        [TestMethod]
        public async Task GetArticleByArticleNumberQuery_WithInvalidNumber_ReturnsNull()
        {
            // Arrange
            var handler = new GetArticleByArticleNumberQueryHandler(null!, dbContext, memoryCache, configuration);

            // Act
            var result = await handler.HandleAsync(new GetArticleByArticleNumberQuery { ArticleNumber = 999 });

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetLastPublishedDateQuery Tests

        [TestMethod]
        public async Task GetLastPublishedDateQuery_WithPublishedArticle_ReturnsDate()
        {
            // Arrange
            var publishDate = DateTimeOffset.UtcNow.AddDays(-1);
            var article1 = CreateTestArticle("article-3", "Article Three");
            article1.ArticleNumber = 1;
            article1.VersionNumber = 1;
            article1.Published = publishDate;

            var article2 = CreateTestArticle("article-3", "Article Three");
            article2.ArticleNumber = 1;
            article2.VersionNumber = 2;
            article2.Published = now;

            dbContext.Articles.Add(article1);
            dbContext.Articles.Add(article2);
            await dbContext.SaveChangesAsync();

            var handler = new GetLastPublishedDateQueryHandler(dbContext);

            // Act
            var result = await handler.HandleAsync(new GetLastPublishedDateQuery { ArticleNumber = 1 });

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Value.Year == now.Year);
        }

        [TestMethod]
        public async Task GetLastPublishedDateQuery_WithNoPublishedArticle_ReturnsNull()
        {
            // Arrange
            var article = CreateTestArticle("article-4", "Article Four");
            article.ArticleNumber = 2;
            article.Published = null;
            dbContext.Articles.Add(article);
            await dbContext.SaveChangesAsync();

            var handler = new GetLastPublishedDateQueryHandler(dbContext);

            // Act
            var result = await handler.HandleAsync(new GetLastPublishedDateQuery { ArticleNumber = 2 });

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetLastPublishedDateQuery_WithNonexistentArticle_ReturnsNull()
        {
            // Arrange
            var handler = new GetLastPublishedDateQueryHandler(dbContext);

            // Act
            var result = await handler.HandleAsync(new GetLastPublishedDateQuery { ArticleNumber = 999 });

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetArticleCatalogEntryQuery Tests

        [TestMethod]
        public async Task GetArticleCatalogEntryQuery_WithValidArticleNumber_ReturnsCatalogEntry()
        {
            // Arrange
            var article = CreateTestArticle("article-5", "Article Five");
            article.ArticleNumber = 3;
            dbContext.Articles.Add(article);
            await dbContext.SaveChangesAsync();

            var catalogEntry = new CatalogEntry
            {
                ArticleNumber = 3,
                Title = "Article Five"
            };
            dbContext.ArticleCatalog.Add(catalogEntry);
            await dbContext.SaveChangesAsync();

            var handler = new GetArticleCatalogEntryQueryHandler(dbContext);

            // Act
            var result = await handler.HandleAsync(new GetArticleCatalogEntryQuery { ArticleNumber = 3 });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.ArticleNumber);
            Assert.AreEqual("Article Five", result.Title);
        }

        [TestMethod]
        public async Task GetArticleCatalogEntryQuery_WithInvalidArticleNumber_ReturnsNull()
        {
            // Arrange
            var handler = new GetArticleCatalogEntryQueryHandler(dbContext);

            // Act
            var result = await handler.HandleAsync(new GetArticleCatalogEntryQuery { ArticleNumber = 999 });

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetArticleRedirectsQuery Tests

        [TestMethod]
        public async Task GetArticleRedirectsQuery_WithRedirectArticles_ReturnsRedirects()
        {
            // Arrange
            var redirect1 = CreateTestArticle("old-page", "Old Page");
            redirect1.StatusCode = (int)StatusCodeEnum.Redirect;
            redirect1.BannerImage = "new-page";

            var redirect2 = CreateTestArticle("another-old", "Another Old");
            redirect2.StatusCode = (int)StatusCodeEnum.Redirect;
            redirect2.BannerImage = "new-location";

            var regular = CreateTestArticle("normal-page", "Normal Page");
            regular.StatusCode = (int)StatusCodeEnum.Active;

            dbContext.Articles.Add(redirect1);
            dbContext.Articles.Add(redirect2);
            dbContext.Articles.Add(regular);
            await dbContext.SaveChangesAsync();

            var handler = new GetArticleRedirectsQueryHandler(dbContext);

            // Act
            var result = await handler.HandleAsync(new GetArticleRedirectsQuery());

            // Assert
            Assert.IsNotNull(result);
            var redirectList = result.ToList();
            Assert.AreEqual(2, redirectList.Count);
            Assert.IsTrue(redirectList.Any(r => r.FromUrl == "old-page" && r.ToUrl == "new-page"));
            Assert.IsTrue(redirectList.Any(r => r.FromUrl == "another-old" && r.ToUrl == "new-location"));
        }

        [TestMethod]
        public async Task GetArticleRedirectsQuery_WithNoRedirects_ReturnsEmpty()
        {
            // Arrange
            var article = CreateTestArticle("normal-page", "Normal Page");
            article.StatusCode = (int)StatusCodeEnum.Active;
            dbContext.Articles.Add(article);
            await dbContext.SaveChangesAsync();

            var handler = new GetArticleRedirectsQueryHandler(dbContext);

            // Act
            var result = await handler.HandleAsync(new GetArticleRedirectsQuery());

            // Assert
            Assert.IsNotNull(result);
            var redirectList = result.ToList();
            Assert.AreEqual(0, redirectList.Count);
        }

        [TestMethod]
        public async Task GetArticleRedirectsQuery_WithEmptyDatabase_ReturnsEmpty()
        {
            // Arrange
            var handler = new GetArticleRedirectsQueryHandler(dbContext);

            // Act
            var result = await handler.HandleAsync(new GetArticleRedirectsQuery());

            // Assert
            Assert.IsNotNull(result);
            var redirectList = result.ToList();
            Assert.AreEqual(0, redirectList.Count);
        }

        #endregion

        #region Helper Methods

        private Article CreateTestArticle(string urlPath, string title)
        {
            return new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = new Random().Next(1, 1000),
                VersionNumber = 1,
                UrlPath = urlPath,
                Title = title,
                Content = "<p>Test content</p>",
                StatusCode = (int)StatusCodeEnum.Active,
                Published = now,
                Updated = now
            };
        }

        #endregion
    }
}
