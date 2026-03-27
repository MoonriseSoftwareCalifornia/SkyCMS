// <copyright file="CacheInvalidationHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Editor.Domain.Events.Handlers
{
    using Cosmos.Common.Constants;
    using Cosmos.Common.Services.Caching;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Domain.Events;
    using Sky.Editor.Domain.Events.Handlers;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="CacheInvalidationHandler"/>.
    /// Verifies that each domain event triggers exactly the right tenant-scoped cache removals.
    /// </summary>
    [TestClass]
    public class CacheInvalidationHandlerTests
    {
        private Mock<ICacheService<object>> cacheMock;
        private CacheInvalidationHandler handler;

        [TestInitialize]
        public void Setup()
        {
            cacheMock = new Mock<ICacheService<object>>(MockBehavior.Strict);
            handler = new CacheInvalidationHandler(
                cacheMock.Object,
                new NullLogger<CacheInvalidationHandler>());
        }

        #region ArticlePublishedEvent

        [TestMethod]
        public async Task HandleAsync_ArticlePublishedEvent_RemovesAllArticleRelatedKeys()
        {
            // Arrange
            const int articleNumber = 42;
            cacheMock.Setup(c => c.Remove(CacheKeys.ArticleCatalog(articleNumber)));
            cacheMock.Setup(c => c.Remove(CacheKeys.LastPublished(articleNumber)));
            cacheMock.Setup(c => c.Remove(CacheKeys.Sitemap));
            cacheMock.Setup(c => c.Remove(CacheKeys.ArticleRedirects));

            // Act
            await handler.HandleAsync(new ArticlePublishedEvent(articleNumber, Guid.NewGuid()));

            // Assert — all four keys removed, no extras
            cacheMock.Verify(c => c.Remove(CacheKeys.ArticleCatalog(articleNumber)), Times.Once);
            cacheMock.Verify(c => c.Remove(CacheKeys.LastPublished(articleNumber)), Times.Once);
            cacheMock.Verify(c => c.Remove(CacheKeys.Sitemap), Times.Once);
            cacheMock.Verify(c => c.Remove(CacheKeys.ArticleRedirects), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        #endregion

        #region ArticleUnpublishedEvent

        [TestMethod]
        public async Task HandleAsync_ArticleUnpublishedEvent_RemovesAllArticleRelatedKeys()
        {
            // Arrange
            const int articleNumber = 7;
            cacheMock.Setup(c => c.Remove(CacheKeys.ArticleCatalog(articleNumber)));
            cacheMock.Setup(c => c.Remove(CacheKeys.LastPublished(articleNumber)));
            cacheMock.Setup(c => c.Remove(CacheKeys.Sitemap));
            cacheMock.Setup(c => c.Remove(CacheKeys.ArticleRedirects));

            // Act
            await handler.HandleAsync(new ArticleUnpublishedEvent(articleNumber));

            // Assert
            cacheMock.Verify(c => c.Remove(CacheKeys.ArticleCatalog(articleNumber)), Times.Once);
            cacheMock.Verify(c => c.Remove(CacheKeys.LastPublished(articleNumber)), Times.Once);
            cacheMock.Verify(c => c.Remove(CacheKeys.Sitemap), Times.Once);
            cacheMock.Verify(c => c.Remove(CacheKeys.ArticleRedirects), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        #endregion

        #region LayoutPublishedEvent

        [TestMethod]
        public async Task HandleAsync_LayoutPublishedEvent_RemovesLayoutAndDefaultLayoutKeys()
        {
            // Arrange
            var layoutId = Guid.NewGuid();
            cacheMock.Setup(c => c.Remove(CacheKeys.Layout(layoutId)));
            cacheMock.Setup(c => c.Remove(CacheKeys.DefaultLayoutExists));
            cacheMock.Setup(c => c.Remove(CacheKeys.DefaultLayout));

            // Act
            await handler.HandleAsync(new LayoutPublishedEvent(layoutId));

            // Assert — exactly three layout keys, no article keys touched
            cacheMock.Verify(c => c.Remove(CacheKeys.Layout(layoutId)), Times.Once);
            cacheMock.Verify(c => c.Remove(CacheKeys.DefaultLayoutExists), Times.Once);
            cacheMock.Verify(c => c.Remove(CacheKeys.DefaultLayout), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task HandleAsync_LayoutPublishedEvent_DifferentLayoutIds_UseDistinctKeys()
        {
            // Arrange — two different layouts should remove different keys
            var layoutId1 = Guid.NewGuid();
            var layoutId2 = Guid.NewGuid();

            cacheMock.Setup(c => c.Remove(It.IsAny<string>()));

            // Act
            await handler.HandleAsync(new LayoutPublishedEvent(layoutId1));
            await handler.HandleAsync(new LayoutPublishedEvent(layoutId2));

            // Assert — each layout key is distinct
            cacheMock.Verify(c => c.Remove(CacheKeys.Layout(layoutId1)), Times.Once);
            cacheMock.Verify(c => c.Remove(CacheKeys.Layout(layoutId2)), Times.Once);
            Assert.AreNotEqual(CacheKeys.Layout(layoutId1), CacheKeys.Layout(layoutId2));
        }

        #endregion

        #region CatalogEntryUpdatedEvent

        [TestMethod]
        public async Task HandleAsync_CatalogEntryUpdatedEvent_RemovesOnlyCatalogKey()
        {
            // Arrange
            const int articleNumber = 99;
            cacheMock.Setup(c => c.Remove(CacheKeys.ArticleCatalog(articleNumber)));

            // Act
            await handler.HandleAsync(new CatalogEntryUpdatedEvent(articleNumber));

            // Assert — only catalog key, no sitemap or redirects
            cacheMock.Verify(c => c.Remove(CacheKeys.ArticleCatalog(articleNumber)), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        #endregion

        #region CatalogEntryDeletedEvent

        [TestMethod]
        public async Task HandleAsync_CatalogEntryDeletedEvent_RemovesOnlyCatalogKey()
        {
            // Arrange
            const int articleNumber = 12;
            cacheMock.Setup(c => c.Remove(CacheKeys.ArticleCatalog(articleNumber)));

            // Act
            await handler.HandleAsync(new CatalogEntryDeletedEvent(articleNumber));

            // Assert
            cacheMock.Verify(c => c.Remove(CacheKeys.ArticleCatalog(articleNumber)), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        #endregion

        #region Cross-article isolation

        [TestMethod]
        public async Task HandleAsync_TwoArticles_EachGetsOwnCacheKey()
        {
            // Arrange
            cacheMock.Setup(c => c.Remove(It.IsAny<string>()));

            // Act
            await handler.HandleAsync(new ArticlePublishedEvent(1, Guid.NewGuid()));
            await handler.HandleAsync(new ArticlePublishedEvent(2, Guid.NewGuid()));

            // Assert — article 1 keys and article 2 keys are independent
            cacheMock.Verify(c => c.Remove(CacheKeys.ArticleCatalog(1)), Times.Once);
            cacheMock.Verify(c => c.Remove(CacheKeys.ArticleCatalog(2)), Times.Once);
            Assert.AreNotEqual(CacheKeys.ArticleCatalog(1), CacheKeys.ArticleCatalog(2));
        }

        #endregion
    }
}
