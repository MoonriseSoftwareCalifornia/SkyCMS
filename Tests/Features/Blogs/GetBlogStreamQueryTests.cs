// <copyright file="GetBlogStreamQueryTests.cs" company="Moonrise Software, LLC">
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
    using Sky.Editor.Features.Blogs.GetStream;

    /// <summary>
    /// Tests for <see cref="GetBlogStreamQuery"/> and <see cref="GetBlogStreamQueryHandler"/>.
    /// </summary>
    [TestClass]
    public class GetBlogStreamQueryTests : SkyCmsTestBase
    {
        /// <summary>
        /// Initialize test context.
        /// </summary>
        [TestInitialize]
        public new void Setup()
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
        /// Tests that query retrieves blog stream successfully.
        /// </summary>
        [TestMethod]
        public async Task GetBlogStream_SucceedsWithValidId()
        {
            // Arrange
            var layout = Db.Layouts.First();
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Blog Stream",
                BlogKey = "test-blog",
                Introduction = "Test Description",
                BannerImage = "/images/hero.jpg",
                Published = DateTimeOffset.UtcNow,
                UrlPath = "test-blog",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Content</div>"
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            var query = new GetBlogStreamQuery { Id = article.Id };
            var handler = new GetBlogStreamQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetBlogStreamQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Query should succeed");
            Assert.IsNotNull(result.Data, "Result should contain data");
            Assert.AreEqual(article.Id, result.Data.Article.Id);
            Assert.AreEqual("Test Blog Stream", result.Data.Title);
            Assert.AreEqual("test-blog", result.Data.BlogKey);
            Assert.AreEqual("Test Description", result.Data.Description);
            Assert.AreEqual("/images/hero.jpg", result.Data.HeroImage);
            Assert.IsNotNull(result.Data.Published);
        }

        /// <summary>
        /// Tests that query fails with empty ID.
        /// </summary>
        [TestMethod]
        public async Task GetBlogStream_FailsWithEmptyId()
        {
            // Arrange
            var query = new GetBlogStreamQuery { Id = Guid.Empty };
            var handler = new GetBlogStreamQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetBlogStreamQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Query should fail with empty ID");
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("required"), "Error should mention ID is required");
        }

        /// <summary>
        /// Tests that query fails when blog stream not found.
        /// </summary>
        [TestMethod]
        public async Task GetBlogStream_FailsWhenNotFound()
        {
            // Arrange
            var query = new GetBlogStreamQuery { Id = Guid.NewGuid() };
            var handler = new GetBlogStreamQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetBlogStreamQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Query should fail when not found");
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("not found"), "Error should mention not found");
        }

        /// <summary>
        /// Tests that query ignores non-blog-stream articles.
        /// </summary>
        [TestMethod]
        public async Task GetBlogStream_IgnoresNonBlogStreamArticles()
        {
            // Arrange
            var regularArticle = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Regular Article",
                ArticleType = (int)ArticleType.General, // Not a blog stream
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Content</div>"
            };
            Db.Articles.Add(regularArticle);
            await Db.SaveChangesAsync();

            var query = new GetBlogStreamQuery { Id = regularArticle.Id };
            var handler = new GetBlogStreamQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetBlogStreamQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Query should fail for non-blog-stream articles");
            Assert.IsTrue(result.ErrorMessage.Contains("not found"), "Should report not found");
        }

        /// <summary>
        /// Tests that query ignores deleted blog streams.
        /// </summary>
        [TestMethod]
        public async Task GetBlogStream_IgnoresDeletedArticles()
        {
            // Arrange
            var deletedArticle = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Deleted Blog",
                BlogKey = "deleted-blog",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Deleted, // Deleted
                Content = "<div>Content</div>"
            };
            Db.Articles.Add(deletedArticle);
            await Db.SaveChangesAsync();

            var query = new GetBlogStreamQuery { Id = deletedArticle.Id };
            var handler = new GetBlogStreamQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetBlogStreamQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Query should fail for deleted articles");
            Assert.IsTrue(result.ErrorMessage.Contains("not found"));
        }

        /// <summary>
        /// Tests that query retrieves latest version when multiple versions exist.
        /// </summary>
        [TestMethod]
        public async Task GetBlogStream_RetrievesLatestVersion()
        {
            // Arrange
            var articleId = Guid.NewGuid();
            var articleNumber = 1;

            // Create multiple versions
            var version1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumber,
                VersionNumber = 1,
                Title = "Blog V1",
                BlogKey = "test-blog",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>V1</div>"
            };

            var version2 = new Article
            {
                Id = articleId, // This is the ID we'll query
                ArticleNumber = articleNumber,
                VersionNumber = 2,
                Title = "Blog V2",
                BlogKey = "test-blog",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>V2</div>"
            };

            Db.Articles.AddRange(version1, version2);
            await Db.SaveChangesAsync();

            var query = new GetBlogStreamQuery { Id = articleId };
            var handler = new GetBlogStreamQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetBlogStreamQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Data.Article.VersionNumber, "Should retrieve version 2");
            Assert.AreEqual("Blog V2", result.Data.Title);
        }

        /// <summary>
        /// Tests that query handles null/empty description and hero image.
        /// </summary>
        [TestMethod]
        public async Task GetBlogStream_HandlesNullFields()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Blog Without Extras",
                BlogKey = "minimal-blog",
                Introduction = string.Empty, // Empty description to test handling
                BannerImage = string.Empty, // Empty hero image to test handling
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Content</div>"
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            var query = new GetBlogStreamQuery { Id = article.Id };
            var handler = new GetBlogStreamQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetBlogStreamQueryHandler>());

            // Act
            var result = await handler.HandleAsync(query);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(string.Empty, result.Data.Description, "Null description should be empty string");
            Assert.AreEqual(string.Empty, result.Data.HeroImage, "Null hero image should be empty string");
        }

        /// <summary>
        /// Tests that handler throws ArgumentNullException when query is null.
        /// </summary>
        [TestMethod]
        public async Task GetBlogStream_ThrowsWhenQueryIsNull()
        {
            // Arrange
            var handler = new GetBlogStreamQueryHandler(
                Db,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<GetBlogStreamQueryHandler>());

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
