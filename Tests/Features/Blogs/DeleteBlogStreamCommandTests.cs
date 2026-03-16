// <copyright file="DeleteBlogStreamCommandTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Blogs
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Blogs.DeleteStream;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for <see cref="DeleteBlogStreamCommand"/> and <see cref="DeleteBlogStreamHandler"/>.
    /// </summary>
    [TestClass]
    public class DeleteBlogStreamCommandTests : SkyCmsTestBase
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
        /// Tests that deleting blog stream succeeds and cascades to entries.
        /// </summary>
        [TestMethod]
        public async Task DeleteBlogStream_SuccessfullyDeletesStreamAndEntries()
        {
            // Arrange
            var blogKey = "test-blog";

            // Create blog stream
            var stream = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Test Blog Stream",
                BlogKey = blogKey,
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream</div>",
                UrlPath = blogKey
            };

            // Create blog entries
            var entry1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                VersionNumber = 1,
                Title = "Blog Entry 1",
                BlogKey = blogKey,
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Entry 1</div>",
                UrlPath = $"{blogKey}/entry-1"
            };

            var entry2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 3,
                VersionNumber = 1,
                Title = "Blog Entry 2",
                BlogKey = blogKey,
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Entry 2</div>",
                UrlPath = $"{blogKey}/entry-2"
            };

            Db.Articles.AddRange(stream, entry1, entry2);
            await Db.SaveChangesAsync();

            var command = new DeleteBlogStreamCommand
            {
                Id = stream.Id,
                UserId = TestUserId
            };

            var handler = new DeleteBlogStreamHandler(
                Db,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Deletion should succeed");

            // Verify all articles are marked as deleted
            var remainingActive = await Db.Articles
                .Where(a => a.BlogKey == blogKey && a.StatusCode != (int)StatusCodeEnum.Deleted)
                .CountAsync();

            Assert.AreEqual(0, remainingActive, "All articles should be deleted");
        }

        /// <summary>
        /// Tests that deletion works with no blog entries.
        /// </summary>
        [TestMethod]
        public async Task DeleteBlogStream_SucceedsWithNoEntries()
        {
            // Arrange
            var stream = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Empty Blog Stream",
                BlogKey = "empty-blog",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream</div>",
                UrlPath = "empty-blog"
            };

            Db.Articles.Add(stream);
            await Db.SaveChangesAsync();

            var command = new DeleteBlogStreamCommand
            {
                Id = stream.Id,
                UserId = TestUserId
            };

            var handler = new DeleteBlogStreamHandler(
                Db,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Deletion should succeed even with no entries");

            var deletedStream = await Db.Articles.FindAsync(stream.Id);
            Assert.AreEqual((int)StatusCodeEnum.Deleted, deletedStream.StatusCode, "Stream should be marked as deleted");
        }

        /// <summary>
        /// Tests that deletion fails with empty ID.
        /// </summary>
        [TestMethod]
        public async Task DeleteBlogStream_FailsWithEmptyId()
        {
            // Arrange
            var command = new DeleteBlogStreamCommand
            {
                Id = Guid.Empty,
                UserId = TestUserId
            };

            var handler = new DeleteBlogStreamHandler(
                Db,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail with empty ID");
            Assert.IsTrue(result.ErrorMessage.Contains("required"), "Error should mention ID is required");
        }

        /// <summary>
        /// Tests that deletion fails when blog stream not found.
        /// </summary>
        [TestMethod]
        public async Task DeleteBlogStream_FailsWhenNotFound()
        {
            // Arrange
            var command = new DeleteBlogStreamCommand
            {
                Id = Guid.NewGuid(),
                UserId = TestUserId
            };

            var handler = new DeleteBlogStreamHandler(
                Db,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Command should fail when not found");
            Assert.IsTrue(result.ErrorMessage.Contains("not found"), "Error should mention not found");
        }

        /// <summary>
        /// Tests that deletion ignores already deleted blog streams.
        /// </summary>
        [TestMethod]
        public async Task DeleteBlogStream_FailsForAlreadyDeletedStream()
        {
            // Arrange
            var stream = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Deleted Blog Stream",
                BlogKey = "deleted-blog",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Deleted, // Already deleted
                Content = "<div>Stream</div>",
                UrlPath = "deleted-blog"
            };

            Db.Articles.Add(stream);
            await Db.SaveChangesAsync();

            var command = new DeleteBlogStreamCommand
            {
                Id = stream.Id,
                UserId = TestUserId
            };

            var handler = new DeleteBlogStreamHandler(
                Db,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Should fail for already deleted stream");
            Assert.IsTrue(result.ErrorMessage.Contains("not found"));
        }

        /// <summary>
        /// Tests that deletion only affects entries with matching blog key.
        /// </summary>
        [TestMethod]
        public async Task DeleteBlogStream_OnlyDeletesMatchingBlogKey()
        {
            // Arrange
            var blogKey1 = "blog-1";
            var blogKey2 = "blog-2";

            // Create first blog stream and entry
            var stream1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Blog Stream 1",
                BlogKey = blogKey1,
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream 1</div>",
                UrlPath = blogKey1
            };

            var entry1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                VersionNumber = 1,
                Title = "Blog 1 Entry",
                BlogKey = blogKey1,
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Entry 1</div>",
                UrlPath = $"{blogKey1}/entry"
            };

            // Create second blog stream and entry (should remain untouched)
            var stream2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 3,
                VersionNumber = 1,
                Title = "Blog Stream 2",
                BlogKey = blogKey2,
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream 2</div>",
                UrlPath = blogKey2
            };

            var entry2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 4,
                VersionNumber = 1,
                Title = "Blog 2 Entry",
                BlogKey = blogKey2,
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Entry 2</div>",
                UrlPath = $"{blogKey2}/entry"
            };

            Db.Articles.AddRange(stream1, entry1, stream2, entry2);
            await Db.SaveChangesAsync();

            var command = new DeleteBlogStreamCommand
            {
                Id = stream1.Id, // Only delete blog 1
                UserId = TestUserId
            };

            var handler = new DeleteBlogStreamHandler(
                Db,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteBlogStreamHandler>());

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Blog 1 should be deleted
            var blog1Remaining = await Db.Articles
                .Where(a => a.BlogKey == blogKey1 && a.StatusCode != (int)StatusCodeEnum.Deleted)
                .CountAsync();
            Assert.AreEqual(0, blog1Remaining, "Blog 1 and its entries should be deleted");

            // Blog 2 should remain untouched
            var blog2Remaining = await Db.Articles
                .Where(a => a.BlogKey == blogKey2 && a.StatusCode != (int)StatusCodeEnum.Deleted)
                .CountAsync();
            Assert.AreEqual(2, blog2Remaining, "Blog 2 and its entry should remain active");
        }

        /// <summary>
        /// Tests that handler throws ArgumentNullException when command is null.
        /// </summary>
        [TestMethod]
        public async Task DeleteBlogStream_ThrowsWhenCommandIsNull()
        {
            // Arrange
            var handler = new DeleteBlogStreamHandler(
                Db,
                Logic,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteBlogStreamHandler>());

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
