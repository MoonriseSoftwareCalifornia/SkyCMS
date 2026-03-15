// <copyright file="SaveArticleIntegrationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Integration
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Articles.Save;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Integration tests for the SaveArticle feature workflow.
    /// </summary>
    [TestClass]
    public class SaveArticleIntegrationTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        /// <summary>
        /// Tests full workflow redirect behavior for unpublished and published save scenarios.
        /// </summary>
        [TestMethod]
        public async Task FullWorkflow_CreateThenSave_RedirectBehaviorMatchesPublishState()
        {
            var scenarios = new[]
            {
                new { Name = "Unpublished", PublishBeforeSave = false, ExpectRedirect = false },
                new { Name = "Published", PublishBeforeSave = true, ExpectRedirect = true },
            };

            foreach (var scenario in scenarios)
            {
                Setup();

                await CreateArticleAsync("Root Page", TestUserId);

                var created = await Mediator.SendAsync(new CreateArticleCommand
                {
                    Title = "Integration Test",
                    UserId = TestUserId
                });

                Assert.IsTrue(created.IsSuccess, scenario.Name);
                Assert.AreEqual(2, await ArticleCountAsync(), scenario.Name);

                if (scenario.PublishBeforeSave)
                {
                    await Logic.PublishArticle(created.Data!.Id, DateTimeOffset.UtcNow);
                }

                var saved = await Mediator.SendAsync(new SaveArticleCommand
                {
                    ArticleNumber = created.Data!.ArticleNumber,
                    Title = "Updated Title",
                    Content = "<p>Updated</p>",
                    UserId = TestUserId,
                    ArticleType = ArticleType.General,
                    Published = scenario.PublishBeforeSave ? DateTimeOffset.UtcNow : null,
                });

                Assert.IsTrue(saved.IsSuccess, scenario.Name);
                Assert.AreEqual("Updated Title", saved.Data!.Model!.Title, scenario.Name);

                var totalArticles = await ArticleCountAsync();
                var redirectArticles = await Db.Articles.CountAsync(a => a.StatusCode == (int)StatusCodeEnum.Redirect);

                if (scenario.ExpectRedirect)
                {
                    var nonRedirectArticles = await Db.Articles.CountAsync(a => a.StatusCode != (int)StatusCodeEnum.Redirect);
                    Assert.AreEqual(3, totalArticles, scenario.Name);
                    Assert.AreEqual(2, nonRedirectArticles, scenario.Name);
                    Assert.AreEqual(1, redirectArticles, scenario.Name);

                    var redirect = await Db.Articles.FirstOrDefaultAsync(a => a.StatusCode == (int)StatusCodeEnum.Redirect);
                    Assert.IsNotNull(redirect, scenario.Name);
                    Assert.AreEqual("integration-test", redirect.UrlPath, scenario.Name);
                    StringAssert.Contains(redirect.Content, "/updated-title", scenario.Name);
                }
                else
                {
                    Assert.AreEqual(2, totalArticles, scenario.Name);
                    Assert.AreEqual(0, redirectArticles, scenario.Name);
                }
            }
        }
    }
}

