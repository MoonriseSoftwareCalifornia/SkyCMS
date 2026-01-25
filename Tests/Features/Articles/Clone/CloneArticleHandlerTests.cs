// <copyright file="CloneArticleHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

using Sky.Tests.Editor.Features.Articles;

namespace Sky.Tests.Features.Articles.Clone
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Models;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Features.Articles.Clone;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Features.Shared;

    [TestClass]
    public class CloneArticleHandlerTests : ArticleTestBase
    {
        private CloneArticleHandler _handler;
        private Mock<IMediator> _mockMediator;

        [TestInitialize]
        public new void TestInitialize()
        {
            base.TestInitialize();

            _mockMediator = new Mock<IMediator>();
            
            _handler = new CloneArticleHandler(
                DbContext,
                MockTitleChangeService.Object,
                _mockMediator.Object,
                MockClock.Object,
                Mock.Of<ILogger<CloneArticleHandler>>());
        }


        [TestMethod]
        public async Task HandleAsync_ClonesArticleWithNewTitle()
        {
            // Arrange
            var sourceArticle = await SeedArticleAsync("Source Article", 1);
            var userId = Guid.NewGuid();

            var expectedViewModel = new ArticleViewModel
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                Title = "Cloned Article",
                Content = sourceArticle.Content
            };

            _mockMediator
                .Setup(x => x.SendAsync(It.IsAny<CreateArticleCommand>()))
                .ReturnsAsync(CommandResult<ArticleViewModel>.Success(expectedViewModel));

            var command = new CloneArticleCommand
            {
                SourceArticleId = sourceArticle.Id,
                NewTitle = "Cloned Article",
                UserId = userId
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Clone should succeed");
            Assert.IsNotNull(result.Data, "Result should contain cloned article");
            Assert.AreEqual("Cloned Article", result.Data.Title, "Should have new title");

            _mockMediator.Verify(
                x => x.SendAsync(It.Is<CreateArticleCommand>(cmd => 
                    cmd.Title == "Cloned Article" &&
                    cmd.ContentOverride == sourceArticle.Content)),
                Times.Once,
                "Should dispatch CreateArticleCommand with source content");
        }

        [TestMethod]
        public async Task HandleAsync_WithConflictingTitle_ReturnsFailure()
        {
            // Arrange
            var sourceArticle = await SeedArticleAsync("Source", 1);
            await SeedArticleAsync("Existing Title", 2);

            MockTitleChangeService
                .Setup(x => x.ValidateTitle("Existing Title", null))
                .ReturnsAsync(false);

            var command = new CloneArticleCommand
            {
                SourceArticleId = sourceArticle.Id,
                NewTitle = "Existing Title",
                UserId = Guid.NewGuid()
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Should fail with conflicting title");
            Assert.IsTrue(result.Errors.ContainsKey("NewTitle"), "Should have NewTitle error");
        }

        [TestMethod]
        public async Task HandleAsync_WithNonexistentSource_ReturnsFailure()
        {
            // Arrange
            var command = new CloneArticleCommand
            {
                SourceArticleId = Guid.NewGuid(), // Doesn't exist
                NewTitle = "Clone of Nothing",
                UserId = Guid.NewGuid()
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Should fail when source not found");
            StringAssert.Contains(result.ErrorMessage, "not found", 
                "Error should mention source not found");
        }

        [TestMethod]
        public async Task HandleAsync_CopiesAllProperties()
        {
            // Arrange
            var sourceArticle = await SeedArticleAsync("Source", 1);
            sourceArticle.Category = "Technology";
            sourceArticle.Introduction = "Test intro";
            sourceArticle.BannerImage = "/banner.jpg";
            sourceArticle.HeaderJavaScript = "<script>head</script>";
            sourceArticle.FooterJavaScript = "<script>footer</script>";
            await DbContext.SaveChangesAsync();

            var userId = Guid.NewGuid();

            CreateArticleCommand capturedCommand = null;
            _mockMediator
                .Setup(x => x.SendAsync(It.IsAny<CreateArticleCommand>()))
                .Callback<ICommand<CommandResult<ArticleViewModel>>, CancellationToken>((cmd, ct) => capturedCommand = (CreateArticleCommand)cmd)
                .ReturnsAsync(CommandResult<ArticleViewModel>.Success(new ArticleViewModel
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = 2,
                    Title = "Cloned",
                    Content = sourceArticle.Content
                }));

            var command = new CloneArticleCommand
            {
                SourceArticleId = sourceArticle.Id,
                NewTitle = "Cloned Article",
                UserId = userId
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Clone should succeed");
            Assert.IsNotNull(capturedCommand, "Should have captured CreateArticleCommand");
            
            Assert.AreEqual("Technology", capturedCommand.Category, "Should copy category");
            Assert.AreEqual("Test intro", capturedCommand.Introduction, "Should copy introduction");
            Assert.AreEqual("/banner.jpg", capturedCommand.BannerImage, "Should copy banner image");
            Assert.AreEqual("<script>head</script>", capturedCommand.HeadJavaScript, "Should copy head script");
            Assert.AreEqual("<script>footer</script>", capturedCommand.FooterJavaScript, "Should copy footer script");
        }

        [TestMethod]
        public async Task HandleAsync_WithPublishedOverride_UsesNewPublishDate()
        {
            // Arrange
            var sourceArticle = await SeedArticleAsync("Source", 1, published: true);
            var newPublishDate = TestNow.AddDays(5);

            CreateArticleCommand capturedCommand = null;
            _mockMediator
                .Setup(x => x.SendAsync(It.IsAny<CreateArticleCommand>()))
                .Callback<ICommand<CommandResult<ArticleViewModel>>, CancellationToken>((cmd, ct) => capturedCommand = (CreateArticleCommand)cmd)
                .ReturnsAsync(CommandResult<ArticleViewModel>.Success(new ArticleViewModel
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = 2,
                    Title = "Cloned",
                    Published = newPublishDate
                }));

            var command = new CloneArticleCommand
            {
                SourceArticleId = sourceArticle.Id,
                NewTitle = "Cloned Article",
                UserId = Guid.NewGuid(),
                Published = newPublishDate
            };

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Clone should succeed");
            Assert.AreEqual(newPublishDate, capturedCommand.Published, 
                "Should use new publish date from clone command");
        }
    }
}