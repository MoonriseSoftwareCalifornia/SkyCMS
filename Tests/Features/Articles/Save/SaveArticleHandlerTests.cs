// <copyright file="SaveArticleHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Save
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Models;
    using Sky.Editor.Features.Articles.Save;
    using Sky.Tests.Editor.Features.Articles;

    [TestClass]
    public class SaveArticleHandlerTests : ArticleTestBase
    {
        private SaveArticleHandler _handler;

        [TestInitialize]
        public new void TestInitialize()
        {
            base.TestInitialize();

            _handler = new SaveArticleHandler(
                DbContext,
                MockHtmlService.Object,
                MockCatalogService.Object,
                MockPublishingService.Object,
                MockTitleChangeService.Object,
                MockClock.Object,
                Mock.Of<ILogger<SaveArticleHandler>>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            DbContext?.Dispose();
        }

        /// <summary>
        /// Tests that HandleAsync_WithValidCommand_SavesArticle.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithValidCommand_SavesArticle()
        {
            // Arrange
            var article = await SeedArticleAsync("Original Title", 1, published: false);
            var userId = Guid.NewGuid();

            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "Updated Title",
                Content = "<div>Updated Content</div>",
                Category = "Technology",
                Introduction = "Updated intro",
                BannerImage = "/images/updated.jpg",
                HeadJavaScript = "<script>updated head</script>",
                FooterJavaScript = "<script>updated footer</script>",
                UserId = userId,
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.IsNotNull(result.Data, "Result should contain data");
            Assert.IsTrue(result.Data.ServerSideSuccess, "Server-side save should succeed");

            // Verify database was updated
            var updatedArticle = await DbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == 1);
            
            Assert.IsNotNull(updatedArticle, "Article should exist");
            Assert.AreEqual("Updated Title", updatedArticle.Title);
            StringAssert.Contains(updatedArticle.Content, "Updated Content");
            Assert.AreEqual("Technology", updatedArticle.Category);
            Assert.AreEqual("Updated intro", updatedArticle.Introduction);
            Assert.AreEqual("/images/updated.jpg", updatedArticle.BannerImage);
            Assert.AreEqual("<script>updated head</script>", updatedArticle.HeaderJavaScript);
            Assert.AreEqual("<script>updated footer</script>", updatedArticle.FooterJavaScript);
            Assert.AreEqual(TestNow, updatedArticle.Updated, "Updated timestamp should be set");
        }

        /// <summary>
        /// Tests that HandleAsync_TitleChange_TriggersRedirect.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_TitleChange_TriggersRedirect()
        {
            // Arrange
            var article = await SeedArticleAsync("Original Title", 1, urlPath: "original-title", published: true);
            
            // Setup mock to detect title change
            string capturedOldTitle = null;
            string capturedOldUrlPath = null;
            MockTitleChangeService
                .Setup(x => x.HandleTitleChangeAsync(
                    It.IsAny<Cosmos.Common.Data.Article>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Callback<Cosmos.Common.Data.Article, string, string>((a, oldTitle, oldUrl) =>
                {
                    capturedOldTitle = oldTitle;
                    capturedOldUrlPath = oldUrl;
                })
                .Returns(Task.CompletedTask);

            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "New Title",  // Changed title
                Content = article.Content,
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Save should succeed");

            // Verify TitleChangeService was called
            MockTitleChangeService.Verify(
                x => x.HandleTitleChangeAsync(
                    It.IsAny<Cosmos.Common.Data.Article>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Once,
                "Should handle title change");

            Assert.AreEqual("Original Title", capturedOldTitle, "Should pass old title");
            Assert.AreEqual("original-title", capturedOldUrlPath, "Should pass old URL path");
        }

        /// <summary>
        /// Tests that HandleAsync_InvalidArticleNumber_ReturnsFailure.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_InvalidArticleNumber_ReturnsFailure()
        {
            // Arrange
            var command = new SaveArticleCommand
            {
                ArticleNumber = 999,  // Doesn't exist
                Title = "Test",
                Content = "<div>Content</div>",
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Should fail with invalid article number");
            Assert.IsNotNull(result.ErrorMessage, "Should have error message");
            StringAssert.Contains(result.ErrorMessage, "not found", 
                "Error should mention article not found");
        }

        /// <summary>
        /// Tests that HandleAsync_WithEmptyTitle_ReturnsFailure.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithEmptyTitle_ReturnsFailure()
        {
            // Arrange
            var article = await SeedArticleAsync("Original", 1);

            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = string.Empty,  // Empty title
                Content = "<div>Content</div>",
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Should fail with empty title");
            Assert.IsTrue(result.Errors.ContainsKey("Title"), "Should have Title error");

            // Verify article was NOT updated
            var unchangedArticle = await DbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == 1);
            Assert.AreEqual("Original", unchangedArticle.Title, 
                "Title should remain unchanged");
        }

        /// <summary>
        /// Tests that HandleAsync_UpdatesCatalog.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_UpdatesCatalog()
        {
            // Arrange
            var article = await SeedArticleAsync("Test Article", 1, published: false);
            
            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "Updated for Catalog",
                Content = "<div>New content</div>",
                Category = "Tech",
                Introduction = "New intro",
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Save should succeed");

            // Verify catalog service was called
            MockCatalogService.Verify(
                x => x.UpsertAsync(It.IsAny<Cosmos.Common.Data.Article>(), It.IsAny<CancellationToken>()),
                Times.Once,
                "Should update catalog after save");
        }

        /// <summary>
        /// Tests that publish behavior matches published state.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_PublishStateScenarios_TriggerExpectedPublishingBehavior()
        {
            var scenarios = new[]
            {
                new
                {
                    Name = "Published",
                    InitialPublished = true,
                    KeepPublished = true,
                    ExpectedPublishCalls = 1,
                    ExpectedCdnCount = (int?)null,
                    ExpectPublishedAfterSave = true,
                },
                new
                {
                    Name = "Unpublished",
                    InitialPublished = false,
                    KeepPublished = false,
                    ExpectedPublishCalls = 0,
                    ExpectedCdnCount = (int?)0,
                    ExpectPublishedAfterSave = false,
                },
            };

            foreach (var scenario in scenarios)
            {
                // Reinitialize to reset mocks and in-memory state per scenario.
                TestInitialize();

                var article = await SeedArticleAsync($"{scenario.Name} Article", 1, published: scenario.InitialPublished);

                var command = new SaveArticleCommand
                {
                    ArticleNumber = 1,
                    Title = $"Updated {scenario.Name} Article",
                    Content = "<div>Updated content</div>",
                    Published = scenario.KeepPublished ? article.Published : null,
                    UserId = Guid.NewGuid(),
                    ArticleType = ArticleType.General
                };

                var result = await _handler.HandleAsync(command);

                Assert.IsTrue(result.IsSuccess, scenario.Name);
                Assert.IsNotNull(result.Data.CdnResults, scenario.Name);
                if (scenario.ExpectedCdnCount.HasValue)
                {
                    Assert.AreEqual(scenario.ExpectedCdnCount.Value, result.Data.CdnResults.Count, scenario.Name);
                }

                MockPublishingService.Verify(
                    x => x.PublishAsync(It.IsAny<Cosmos.Common.Data.Article>()),
                    Times.Exactly(scenario.ExpectedPublishCalls),
                    scenario.Name);

                var updatedArticle = await DbContext.Articles
                    .FirstOrDefaultAsync(a => a.ArticleNumber == 1);
                if (scenario.ExpectPublishedAfterSave)
                {
                    Assert.IsNotNull(updatedArticle.Published, scenario.Name);
                }
                else
                {
                    Assert.IsNull(updatedArticle.Published, scenario.Name);
                }
            }
        }

        /// <summary>
        /// Tests that HandleAsync_ConcurrentEdit_HandlesGracefully.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_ConcurrentEdit_HandlesGracefully()
        {
            // Arrange
            var article = await SeedArticleAsync("Concurrent Test", 1);
            
            // First command
            var command1 = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "First Update",
                Content = "<div>First content</div>",
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Second command (simulates concurrent edit)
            var command2 = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "Second Update",
                Content = "<div>Second content</div>",
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result1 = await _handler.HandleAsync(command1);
            var result2 = await _handler.HandleAsync(command2);

            // Assert
            Assert.IsTrue(result1.IsSuccess, "First save should succeed");
            Assert.IsTrue(result2.IsSuccess, "Second save should also succeed (last write wins)");

            // Verify final state matches the second update
            var finalArticle = await DbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == 1);
            Assert.AreEqual("Second Update", finalArticle.Title, 
                "Last write should win");
            StringAssert.Contains(finalArticle.Content, "Second content",
                "Content should match last update");
        }

        /// <summary>
        /// Tests that HandleAsync_WithEmptyIntroduction_ClearsExistingIntroduction.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithEmptyIntroduction_ClearsExistingIntroduction()
        {
            // Arrange
            var article = await SeedArticleAsync("Intro Test", 1, published: false);
            article.Introduction = "Existing introduction";
            await DbContext.SaveChangesAsync();

            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "Intro Test",
                Content = article.Content,
                Introduction = string.Empty,
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Save should succeed");

            var updatedArticle = await DbContext.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == 1);
            Assert.IsNotNull(updatedArticle);
            Assert.AreEqual(string.Empty, updatedArticle.Introduction);
        }

        /// <summary>
        /// Tests that HandleAsync_WithNullContent_DoesNotFailValidation.
        /// </summary>
        [TestMethod]
        public async Task HandleAsync_WithNullContent_DoesNotFailValidation()
        {
            // Arrange
            var article = await SeedArticleAsync("Null Content Test", 1, published: false);

            var command = new SaveArticleCommand
            {
                ArticleNumber = 1,
                Title = "Null Content Test",
                Content = null!,
                UserId = Guid.NewGuid(),
                ArticleType = ArticleType.General
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Save should succeed when Content is null");

            var updatedArticle = await DbContext.Articles.FirstOrDefaultAsync(a => a.ArticleNumber == 1);
            Assert.IsNotNull(updatedArticle);
            Assert.IsNotNull(updatedArticle.Content);
        }
    }
}

