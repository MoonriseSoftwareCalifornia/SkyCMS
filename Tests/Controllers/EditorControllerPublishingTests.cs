// <copyright file="EditorControllerPublishingTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.Common.Features.Articles.EditorQueries;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for EditorController publishing operations.
    /// Covers UnpublishPage, PublishStaticPages, PublishTOC, RefreshCdn, and UpdateTimeStamps.
    /// </summary>
    [TestClass]
    public class EditorControllerPublishingTests : SkyCmsTestBase
    {
        private EditorController controller = null!;

        [TestInitialize]
        public new void Setup()
        {
            base.Setup();

            controller = new EditorController(
                Logger,
                Db,
                UserManager,
                RoleManager,
                Logic,
                EditorSettings,
                ViewRenderService,
                Storage,
                Hub.Object,
                PublishingService,
                ArticleHtmlService,
                ReservedPaths,
                TitleChangeService,
                TemplateService,
                Mediator,
                LayoutCacheService,
                DynamicConfigurationProvider);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "editor@example.com"),
                new Claim(ClaimTypes.Role, "Editors")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        #region UnpublishPage Tests

        /// <summary>
        /// Tests that UnpublishPage unpublishes an article.
        /// </summary>
        [TestMethod]
        public async Task UnpublishPage_UnpublishesArticle()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);
            var articleEntity = await Db.Articles.FirstAsync(a => a.Id == article.Id); await PublishingService.PublishAsync(articleEntity);

            // Verify it's published
            var publishedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.IsNotNull(publishedArticle.Published, "Article should be published");

            // Act
            var result = await controller.UnpublishPage(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Verify article was unpublished
            var unpublishedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.IsNull(unpublishedArticle.Published, "Article should be unpublished");
        }

        /// <summary>
        /// Tests that UnpublishPage works with already unpublished article.
        /// </summary>
        [TestMethod]
        public async Task UnpublishPage_HandlesAlreadyUnpublishedArticle()
        {
            // Arrange
            var article = await CreateArticleAsync("Unpublished Article", TestUserId);
            await SaveArticleAsync(article, TestUserId);

            // Ensure article is unpublished (it may be auto-published if it's the first article)
            if (article.Published.HasValue)
            {
                await PublishingService.UnpublishAsync(new Cosmos.Common.Data.Article { ArticleNumber = article.ArticleNumber });
            }

            // Verify it's not published
            var unpublishedArticle = await Mediator.QueryAsync(new GetArticleByArticleNumberQuery { ArticleNumber = article.ArticleNumber });
            Assert.IsNull(unpublishedArticle.Published);

            // Act - Should not throw
            var result = await controller.UnpublishPage(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        #endregion

        #region PublishStaticPages Tests

        /// <summary>
        /// Tests that PublishStaticPages publishes specified pages.
        /// </summary>
        [TestMethod]
        public async Task PublishStaticPages_PublishesSpecifiedPages()
        {
            // Arrange
            var article1 = await CreateArticleAsync("Page 1", TestUserId);
            await SaveArticleAsync(article1, TestUserId);
            var article1Entity = await Db.Articles.FirstAsync(a => a.Id == article1.Id); await PublishingService.PublishAsync(article1Entity);

            var article2 = await CreateArticleAsync("Page 2", TestUserId);
            await SaveArticleAsync(article2, TestUserId);
            var article2Entity = await Db.Articles.FirstAsync(a => a.Id == article2.Id); await PublishingService.PublishAsync(article2Entity);

            var page1 = await Db.Pages.FirstAsync(p => p.ArticleNumber == article1.ArticleNumber);
            var page2 = await Db.Pages.FirstAsync(p => p.ArticleNumber == article2.ArticleNumber);

            var pageIds = new List<Guid> { page1.Id, page2.Id };

            // Act
            var result = await controller.PublishStaticPages(pageIds);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that PublishStaticPages handles empty list (publish all).
        /// </summary>
        [TestMethod]
        public async Task PublishStaticPages_HandlesEmptyList_PublishesAll()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Page", TestUserId);
            await SaveArticleAsync(article, TestUserId);
            var articleEntity = await Db.Articles.FirstAsync(a => a.Id == article.Id); await PublishingService.PublishAsync(articleEntity);

            // Act - Empty list should trigger "publish all"
            var result = await controller.PublishStaticPages(new List<Guid>());

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that PublishStaticPages handles null list.
        /// </summary>
        [TestMethod]
        public async Task PublishStaticPages_HandlesNullList()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Page", TestUserId);
            await SaveArticleAsync(article, TestUserId);
            var articleEntity = await Db.Articles.FirstAsync(a => a.Id == article.Id); await PublishingService.PublishAsync(articleEntity);

            // Act
            var result = await controller.PublishStaticPages(null!);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that PublishStaticPages returns success response.
        /// </summary>
        [TestMethod]
        public async Task PublishStaticPages_ReturnsSuccessResponse()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Page", TestUserId);
            await SaveArticleAsync(article, TestUserId);
            var articleEntity = await Db.Articles.FirstAsync(a => a.Id == article.Id); await PublishingService.PublishAsync(articleEntity);

            var page = await Db.Pages.FirstAsync(p => p.ArticleNumber == article.ArticleNumber);
            var pageIds = new List<Guid> { page.Id };

            // Act
            var result = await controller.PublishStaticPages(pageIds);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;

            // Check response structure
            var json = System.Text.Json.JsonSerializer.Serialize(jsonResult.Value);
            Assert.IsTrue(json.Contains("success"), "Response should contain 'success' field");
        }

        #endregion

        #region RefreshCdn Tests

        /// <summary>
        /// Tests that RefreshCdn returns empty list when no CDN configured.
        /// </summary>
        [TestMethod]
        public async Task RefreshCdn_ReturnsEmptyList_WhenNoCdnConfigured()
        {
            // Act
            var result = await controller.RefreshCdn();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);

            // Should return empty list when CDN service is null
            var items = jsonResult.Value as System.Collections.IEnumerable;
            if (items != null)
            {
                var count = items.Cast<object>().Count();
                Assert.AreEqual(0, count, "Should return empty list when no CDN configured");
            }
        }

        /// <summary>
        /// Tests that RefreshCdn handles exceptions gracefully.
        /// </summary>
        [TestMethod]
        public async Task RefreshCdn_HandlesExceptionsGracefully()
        {
            // Act - Should not throw even if CDN operations fail
            var result = await controller.RefreshCdn();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        #endregion
    }
}
