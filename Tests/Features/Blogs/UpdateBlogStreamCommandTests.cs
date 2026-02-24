// <copyright file="UpdateBlogStreamCommandTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Blogs
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Blogs.UpdateStream;

    /// <summary>
    /// Tests for <see cref="UpdateBlogStreamCommand"/> and <see cref="UpdateBlogStreamHandler"/>.
    /// </summary>
    [TestClass]
    public class UpdateBlogStreamCommandTests : SkyCmsTestBase
    {
        /// <summary>
        /// Initialize test context.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            InitializeTestContext(seedLayout: true);
        }

        /// <summary>
        /// Cleanup after each test.
        /// </summary>
        [TestCleanup]
        public async Task Cleanup()
        {
            await DisposeAsync();
        }

        /// <summary>
        /// Tests that updating blog stream succeeds with valid data.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_SucceedsWithValidData()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Original Blog",
                BlogKey = "original-blog",
                Introduction = "Original Description",
                BannerImage = "/images/old.jpg",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Old Content</div>",
                UrlPath = "original-blog"
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            var command = new UpdateBlogStreamCommand
            {
                Id = article.Id,
                Title = "Updated Blog",
                Description = "Updated Description",
                HeroImage = "/images/new.jpg",
                Published = DateTimeOffset.UtcNow,
                UserId = TestUserId
            };

            var handler = new UpdateBlogStreamHandler(
                Db,
                SlugService,
                TitleChangeService,
                BlogStreamRenderingService,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed");
            Assert.IsNotNull(result.Data, "Result should contain article");

            // Verify changes in database
            var updatedArticle = await Db.Articles.FindAsync(article.Id);
            Assert.AreEqual("Updated Blog", updatedArticle.Title);
            Assert.AreEqual("updated-blog", updatedArticle.UrlPath); // Normalized by slug service
            Assert.AreEqual("Updated Description", updatedArticle.Introduction);
            Assert.AreEqual("/images/new.jpg", updatedArticle.BannerImage);
            Assert.IsNotNull(updatedArticle.Published);
        }

        /// <summary>
        /// Tests that updating trims whitespace from title.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_TrimsWhitespaceFromTitle()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Original Blog",
                BlogKey = "original-blog",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Content</div>",
                UrlPath = "original-blog"
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            var command = new UpdateBlogStreamCommand
            {
                Id = article.Id,
                Title = "   Whitespace Blog   ",
                Description = "Description",
                UserId = TestUserId
            };

            var handler = new UpdateBlogStreamHandler(
                Db,
                SlugService,
                TitleChangeService,
                BlogStreamRenderingService,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Whitespace Blog", result.Data.Title, "Title should be trimmed");
        }

        /// <summary>
        /// Tests that update fails with empty ID.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_FailsWithEmptyId()
        {
            // Arrange
            var command = new UpdateBlogStreamCommand
            {
                Id = Guid.Empty,
                Title = "Test Blog",
                UserId = TestUserId
            };

            var handler = new UpdateBlogStreamHandler(
                Db,
                SlugService,
                TitleChangeService,
                BlogStreamRenderingService,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail with empty ID");
            Assert.IsTrue(result.ErrorMessage.Contains("required"), "Error should mention ID is required");
        }

        /// <summary>
        /// Tests that update fails with empty title.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_FailsWithEmptyTitle()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Original Blog",
                BlogKey = "original-blog",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Content</div>",
                UrlPath = "original-blog"
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            var command = new UpdateBlogStreamCommand
            {
                Id = article.Id,
                Title = string.Empty,
                UserId = TestUserId
            };

            var handler = new UpdateBlogStreamHandler(
                Db,
                SlugService,
                TitleChangeService,
                BlogStreamRenderingService,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail with empty title");
            Assert.IsTrue(result.ErrorMessage.Contains("title"), "Error should mention title");
        }

        /// <summary>
        /// Tests that update fails when blog stream not found.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_FailsWhenNotFound()
        {
            // Arrange
            var command = new UpdateBlogStreamCommand
            {
                Id = Guid.NewGuid(),
                Title = "Test Blog",
                UserId = TestUserId
            };

            var handler = new UpdateBlogStreamHandler(
                Db,
                SlugService,
                TitleChangeService,
                BlogStreamRenderingService,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail when not found");
            Assert.IsTrue(result.ErrorMessage.Contains("not found"), "Error should mention not found");
        }

        /// <summary>
        /// Tests that update allows empty description and hero image.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_AllowsEmptyOptionalFields()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Original Blog",
                BlogKey = "original-blog",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Content</div>",
                UrlPath = "original-blog"
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            var command = new UpdateBlogStreamCommand
            {
                Id = article.Id,
                Title = "Updated Blog",
                Description = string.Empty,
                HeroImage = string.Empty,
                UserId = TestUserId
            };

            var handler = new UpdateBlogStreamHandler(
                Db,
                SlugService,
                TitleChangeService,
                BlogStreamRenderingService,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Command should succeed with empty optional fields");
            Assert.AreEqual(string.Empty, result.Data.Introduction);
            Assert.AreEqual(string.Empty, result.Data.BannerImage);
        }

        /// <summary>
        /// Tests that handler throws ArgumentNullException when command is null.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_ThrowsWhenCommandIsNull()
        {
            // Arrange
            var handler = new UpdateBlogStreamHandler(
                Db,
                SlugService,
                TitleChangeService,
                BlogStreamRenderingService,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateBlogStreamHandler>());

            // Act & Assert
            try
            {
                await handler.HandleAsync(null);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }
    }
}
