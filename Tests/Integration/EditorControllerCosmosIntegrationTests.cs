// <copyright file="EditorControllerCosmosIntegrationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Integration
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using System;
    using System.Collections;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Cosmos integration tests for <see cref="EditorController"/> article list APIs.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    [TestCategory("Cosmos")]
    public class EditorControllerCosmosIntegrationTests : SkyCmsTestBase
    {
        private ApplicationDbContext cosmosDb = null!;
        private EditorController controller = null!;
        private string testTitlePrefix = null!;

        [TestInitialize]
        public new async Task Setup()
        {
            InitializeTestContext(seedLayout: false);

            var configuration = new ConfigurationBuilder()
                .AddUserSecrets(typeof(SkyCmsTestBase).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();

            var cosmosConnectionString = configuration.GetConnectionString("CosmosDB");
            if (string.IsNullOrWhiteSpace(cosmosConnectionString))
            {
                Assert.Inconclusive("Cosmos integration test skipped: 'ConnectionStrings:CosmosDB' is not configured.");
            }

            cosmosDb = new ApplicationDbContext(cosmosConnectionString!);
            await cosmosDb.Database.EnsureCreatedAsync();

            controller = new EditorController(
                Logger,
                cosmosDb,
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
                new Claim(ClaimTypes.Name, "cosmos-test@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            testTitlePrefix = $"Cosmos-EditorList-{Guid.NewGuid():N}";
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (cosmosDb != null)
            {
                var testArticles = await cosmosDb.Articles
                    .Where(a => a.Title.StartsWith(testTitlePrefix))
                    .ToListAsync();

                if (testArticles.Count > 0)
                {
                    cosmosDb.Articles.RemoveRange(testArticles);
                    await cosmosDb.SaveChangesAsync();
                }

                await cosmosDb.DisposeAsync();
            }
        }

        /// <summary>
        /// Verifies Cosmos query path sets HtmlEditorEnabled from content markers.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_Cosmos_SetsHtmlEditorEnabled_FromContentMarkers()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var baseArticleNumber = Math.Abs((int)(DateTime.UtcNow.Ticks % int.MaxValue));

            cosmosDb.Articles.AddRange(
                new Article
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = baseArticleNumber,
                    VersionNumber = 1,
                    Title = $"{testTitlePrefix}-Editable",
                    UrlPath = $"{testTitlePrefix.ToLowerInvariant()}-editable",
                    Content = "<div data-ccms-ceid='abc123'>Editable</div>",
                    Published = now,
                    Updated = now,
                    StatusCode = (int)StatusCodeEnum.Active,
                    UserId = TestUserId.ToString(),
                    ArticleType = (int)ArticleType.General,
                    BannerImage = string.Empty,
                },
                new Article
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = baseArticleNumber + 1,
                    VersionNumber = 1,
                    Title = $"{testTitlePrefix}-Static",
                    UrlPath = $"{testTitlePrefix.ToLowerInvariant()}-static",
                    Content = "<div>Static content only</div>",
                    Published = now,
                    Updated = now,
                    StatusCode = (int)StatusCodeEnum.Active,
                    UserId = TestUserId.ToString(),
                    ArticleType = (int)ArticleType.General,
                    BannerImage = string.Empty,
                });

            await cosmosDb.SaveChangesAsync();

            // Act
            var result = await controller.GetArticleList(term: testTitlePrefix, publishedOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var items = ((IEnumerable)jsonResult.Value!).Cast<object>().ToList();

            var editable = items.Single(i => GetPropertyValue<string>(i, "Title").EndsWith("-Editable", StringComparison.Ordinal));
            var notEditable = items.Single(i => GetPropertyValue<string>(i, "Title").EndsWith("-Static", StringComparison.Ordinal));

            Assert.IsTrue(GetPropertyValue<bool>(editable, "HtmlEditorEnabled"));
            Assert.IsFalse(GetPropertyValue<bool>(notEditable, "HtmlEditorEnabled"));
        }

        private static T GetPropertyValue<T>(object item, string propertyName)
        {
            var property = item.GetType().GetProperty(propertyName);
            Assert.IsNotNull(property, $"Expected property '{propertyName}' was not found.");

            var value = property.GetValue(item);
            Assert.IsNotNull(value, $"Property '{propertyName}' value is null.");

            return (T)value;
        }
    }
}
