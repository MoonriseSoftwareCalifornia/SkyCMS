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
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleIntegrationTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        public async Task FullWorkflow_CreateThenSave_UnpublishedArticle_NoRedirectCreated()
        {
            // **FIX**: Create root article first so test article doesn't become root
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create test article (won't be root now)
            var created = await Mediator.SendAsync(new CreateArticleCommand
            {
                Title = "Integration Test",
                UserId = TestUserId
            });

            Assert.IsTrue(created.IsSuccess);
            Assert.AreEqual(2, await ArticleCountAsync(), "Should have 2 articles: root + test article");

            // Save with title change (unpublished)
            var saved = await Mediator.SendAsync(new SaveArticleCommand
            {
                ArticleNumber = created.Data!.ArticleNumber,
                Title = "Updated Title",
                Content = "<p>Updated</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General
            });

            Assert.IsTrue(saved.IsSuccess);
            Assert.AreEqual("Updated Title", saved.Data!.Model!.Title);
            
            var totalArticles = await ArticleCountAsync();
            var redirectArticles = await Db.Articles.CountAsync(a => a.StatusCode == (int)StatusCodeEnum.Redirect);
            
            Assert.AreEqual(2, totalArticles, "Unpublished article: no redirect created (root + content)");
            Assert.AreEqual(0, redirectArticles, "No redirects for unpublished articles");
        }

        [TestMethod]
        public async Task FullWorkflow_CreatePublishThenSave_CreatesRedirect()
        {
            // **FIX**: Create root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Create test article
            var created = await Mediator.SendAsync(new CreateArticleCommand
            {
                Title = "Integration Test",
                UserId = TestUserId
            });

            Assert.IsTrue(created.IsSuccess);
            Assert.AreEqual(2, await ArticleCountAsync(), "Should have 2 articles: root + test article");

            // Publish the article
            await Logic.PublishArticle(created.Data!.Id, DateTimeOffset.UtcNow);

            // Save with title change (published article)
            var saved = await Mediator.SendAsync(new SaveArticleCommand
            {
                ArticleNumber = created.Data!.ArticleNumber,
                Title = "Updated Title",
                Content = "<p>Updated</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = DateTimeOffset.UtcNow
            });

            Assert.IsTrue(saved.IsSuccess);
            Assert.AreEqual("Updated Title", saved.Data!.Model!.Title);
            
            var totalArticles = await ArticleCountAsync();
            var nonRedirectArticles = await Db.Articles.CountAsync(a => a.StatusCode != (int)StatusCodeEnum.Redirect);
            var redirectArticles = await Db.Articles.CountAsync(a => a.StatusCode == (int)StatusCodeEnum.Redirect);
            
            Assert.AreEqual(3, totalArticles, "Should have 3 total articles (root + content + redirect)");
            Assert.AreEqual(2, nonRedirectArticles, "Should have 2 non-redirect articles (root + content)");
            Assert.AreEqual(1, redirectArticles, "Should have 1 redirect article");
            
            // Verify the redirect points from old slug to new slug
            var redirect = await Db.Articles.FirstOrDefaultAsync(a => a.StatusCode == (int)StatusCodeEnum.Redirect);
            Assert.IsNotNull(redirect);
            Assert.AreEqual("integration-test", redirect.UrlPath, "Redirect should be from old slug");
            StringAssert.Contains(redirect.Content, "/updated-title", "Redirect should point to new slug");
        }
    }
}
