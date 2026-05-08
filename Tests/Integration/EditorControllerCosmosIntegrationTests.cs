// <copyright file="EditorControllerCosmosIntegrationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Integration
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Cosmos;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using Sky.Editor.Features.Articles.Inventory;
    using Sky.Editor.Models;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net;
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

            // Fast DNS pre-check: avoid waiting for Cosmos SDK's slow retry mechanism
            var endpointMatch = System.Text.RegularExpressions.Regex.Match(
                cosmosConnectionString!, @"AccountEndpoint=https?://([^:/]+)");
            if (endpointMatch.Success)
            {
                var host = endpointMatch.Groups[1].Value;
                try
                {
                    await Dns.GetHostEntryAsync(host);
                }
                catch (System.Net.Sockets.SocketException ex)
                {
                    Assert.Inconclusive($"Cosmos integration test skipped: DNS resolution failed for '{host}'. {ex.Message}");
                }
            }

            cosmosDb = new ApplicationDbContext(cosmosConnectionString!);
            var databaseName = GetDatabaseName(cosmosConnectionString!);

            try
            {
                await cosmosDb.Database.EnsureCreatedAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or CosmosException)
            {
                Assert.Inconclusive($"Cosmos integration test skipped: endpoint unreachable. {ex.Message}");
            }

            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                var articlesReady = await WaitForContainerAsync(cosmosDb, databaseName, "Articles");
                if (!articlesReady)
                {
                    Assert.Inconclusive("Cosmos integration test skipped: 'Articles' container was not ready after database initialization.");
                }
            }

            var mediatorServices = new ServiceCollection();
            mediatorServices.AddSingleton<IQueryHandler<GetEditorInventoryQuery, List<EditorInventoryItem>>>(
                new GetEditorInventoryQueryHandler(cosmosDb));
            var mediatorProvider = mediatorServices.BuildServiceProvider();
            var cosmosMediator = new Mediator(mediatorProvider, NullLogger<Mediator>.Instance);

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
                cosmosMediator,
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
                try
                {
                    var testArticles = await cosmosDb.Articles
                        .Where(a => a.Title.StartsWith(testTitlePrefix))
                        .ToListAsync();

                    if (testArticles.Count > 0)
                    {
                        cosmosDb.Articles.RemoveRange(testArticles);
                        await cosmosDb.SaveChangesAsync();
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or CosmosException)
                {
                    // Cosmos DB endpoint unreachable — nothing to clean up.
                }
                finally
                {
                    await cosmosDb.DisposeAsync();
                }
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

        private static string? GetDatabaseName(string connectionString)
        {
            return connectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(part => part.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=', 2)[1];
        }

        private static async Task<bool> WaitForContainerAsync(ApplicationDbContext dbContext, string databaseName, string containerName)
        {
            var client = dbContext.Database.GetCosmosClient();

            for (var attempt = 1; attempt <= 10; attempt++)
            {
                try
                {
                    var response = await client.GetContainer(databaseName, containerName).ReadContainerAsync();
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return true;
                    }
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    if (attempt == 10)
                    {
                        return false;
                    }
                }
                catch (HttpRequestException)
                {
                    if (attempt == 10)
                    {
                        return false;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            return false;
        }
    }
}
