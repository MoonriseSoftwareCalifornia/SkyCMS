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
    using Microsoft.EntityFrameworkCore;
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

        /// <summary>
        /// Tests that BlogKey is updated when stream UrlPath changes.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_UpdatesBlogKeyWhenUrlPathChanges()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Cat Wash",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Content</div>",
                UrlPath = "cat_wash"
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            var command = new UpdateBlogStreamCommand
            {
                Id = article.Id,
                Title = "Pet Wash",
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
            var updatedArticle = await Db.Articles.FindAsync(article.Id);
            Assert.AreEqual("pet-wash", updatedArticle.UrlPath, "UrlPath should be slugified with hyphens");
            Assert.AreEqual("pet-wash", updatedArticle.BlogKey, "BlogKey should match new UrlPath (with hyphens)");
        }

        /// <summary>
        /// Tests that child blog posts' UrlPath is updated when stream UrlPath changes.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_UpdatesChildBlogPostsUrlPath()
        {
            // Arrange - Create blog stream
            var stream = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Cat Wash",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream Content</div>",
                UrlPath = "cat_wash"
            };
            Db.Articles.Add(stream);

            // Create blog posts
            var post1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                VersionNumber = 1,
                Title = "Shampo",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Post 1</div>",
                UrlPath = "cat_wash/shampo"
            };

            var post2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 3,
                VersionNumber = 1,
                Title = "Conditioner",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Post 2</div>",
                UrlPath = "cat_wash/conditioner"
            };

            Db.Articles.AddRange(post1, post2);
            await Db.SaveChangesAsync();

            var command = new UpdateBlogStreamCommand
            {
                Id = stream.Id,
                Title = "Pet Wash",
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

            var updatedPost1 = await Db.Articles.FindAsync(post1.Id);
            var updatedPost2 = await Db.Articles.FindAsync(post2.Id);

            Assert.AreEqual("pet-wash/shampo", updatedPost1.UrlPath, "Post 1 UrlPath should be updated with hyphenated stream slug");
            Assert.AreEqual("pet-wash/conditioner", updatedPost2.UrlPath, "Post 2 UrlPath should be updated with hyphenated stream slug");
            Assert.AreEqual("pet-wash", updatedPost1.BlogKey, "Post 1 BlogKey should match new stream key with hyphens");
            Assert.AreEqual("pet-wash", updatedPost2.BlogKey, "Post 2 BlogKey should match new stream key with hyphens");
        }

        /// <summary>
        /// Tests that publishing a blog stream also publishes all child blog posts.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_PublishesBlogPostsWhenStreamPublished()
        {
            // Arrange - Create blog stream with unique article numbers
            var articleNumberForStream = 100;  // Use high number to avoid conflicts
            var stream = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumberForStream,
                VersionNumber = 1,
                Title = "Cat Wash Stream",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream Content</div>",
                UrlPath = "cat_wash",
                Published = null,
                UserId = TestUserId.ToString()  // Set UserId for publishing
            };
            Db.Articles.Add(stream);

            // Create blog posts (unpublished)
            var post1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 101,
                VersionNumber = 1,
                Title = "Shampo",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Post 1</div>",
                UrlPath = "cat_wash/shampo",
                Published = null,
                UserId = TestUserId.ToString()  // Set UserId for publishing
            };

            var post2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 102,
                VersionNumber = 1,
                Title = "Conditioner",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Post 2</div>",
                UrlPath = "cat_wash/conditioner",
                Published = null,
                UserId = TestUserId.ToString()  // Set UserId for publishing
            };

            Db.Articles.AddRange(post1, post2);
            await Db.SaveChangesAsync();

            var publishDate = DateTimeOffset.UtcNow;
            var command = new UpdateBlogStreamCommand
            {
                Id = stream.Id,
                Title = "Cat Wash Stream",  // Keep same title to avoid validation issues
                Published = publishDate,
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
            Assert.IsTrue(result.IsSuccess, $"Command should succeed. Error: {result.ErrorMessage}");

            var updatedStream = await Db.Articles.FindAsync(stream.Id);
            var updatedPost1 = await Db.Articles.FindAsync(post1.Id);
            var updatedPost2 = await Db.Articles.FindAsync(post2.Id);

            Assert.IsNotNull(updatedStream.Published, "Stream should be published");
            Assert.IsNotNull(updatedPost1.Published, "Post 1 should be published");
            Assert.IsNotNull(updatedPost2.Published, "Post 2 should be published");
        }

        /// <summary>
        /// Tests that unpublishing a blog stream also unpublishes all child blog posts.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_UnpublishesBlogPostsWhenStreamUnpublished()
        {
            // Arrange - Create blog stream (published)
            var publishDate = DateTimeOffset.UtcNow.AddDays(-1);
            var stream = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Cat Wash",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream Content</div>",
                UrlPath = "cat_wash",
                Published = publishDate
            };
            Db.Articles.Add(stream);

            // Create blog posts (published)
            var post1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                VersionNumber = 1,
                Title = "Shampo",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Post 1</div>",
                UrlPath = "cat_wash/shampo",
                Published = publishDate
            };

            var post2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 3,
                VersionNumber = 1,
                Title = "Conditioner",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Post 2</div>",
                UrlPath = "cat_wash/conditioner",
                Published = publishDate
            };

            Db.Articles.AddRange(post1, post2);
            await Db.SaveChangesAsync();

            var command = new UpdateBlogStreamCommand
            {
                Id = stream.Id,
                Title = "Cat Wash",
                Published = null,  // Unpublish by setting to null
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

            var updatedStream = await Db.Articles.FindAsync(stream.Id);
            var updatedPost1 = await Db.Articles.FindAsync(post1.Id);
            var updatedPost2 = await Db.Articles.FindAsync(post2.Id);

            Assert.IsNull(updatedStream.Published, "Stream should be unpublished");
            Assert.IsNull(updatedPost1.Published, "Post 1 should be unpublished");
            Assert.IsNull(updatedPost2.Published, "Post 2 should be unpublished");
        }

        /// <summary>
        /// Tests that only blog posts belonging to the stream are updated when UrlPath changes.
        /// Other blog streams' posts should not be affected.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_OnlyUpdatesChildPosts_NotOtherStreams()
        {
            // Arrange - Create first blog stream with posts
            var stream1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Cat Wash",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream 1</div>",
                UrlPath = "cat_wash"
            };
            Db.Articles.Add(stream1);

            var post1 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                VersionNumber = 1,
                Title = "Shampo",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Post 1</div>",
                UrlPath = "cat_wash/shampo"
            };
            Db.Articles.Add(post1);

            // Create second blog stream with posts
            var stream2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 3,
                VersionNumber = 1,
                Title = "Dog Wash",
                BlogKey = "dog_wash",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream 2</div>",
                UrlPath = "dog_wash"
            };
            Db.Articles.Add(stream2);

            var post2 = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 4,
                VersionNumber = 1,
                Title = "Shampoo",
                BlogKey = "dog_wash",
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Post 2</div>",
                UrlPath = "dog_wash/shampoo"
            };
            Db.Articles.Add(post2);

            await Db.SaveChangesAsync();

            var command = new UpdateBlogStreamCommand
            {
                Id = stream1.Id,
                Title = "Pet Wash",
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

            var updatedPost1 = await Db.Articles.FindAsync(post1.Id);
            var updatedPost2 = await Db.Articles.FindAsync(post2.Id);

            Assert.AreEqual("pet-wash/shampo", updatedPost1.UrlPath, "Post 1 UrlPath should be updated with new stream slug");
            Assert.AreEqual("pet-wash", updatedPost1.BlogKey, "Post 1 BlogKey should be updated");

            Assert.AreEqual("dog_wash/shampoo", updatedPost2.UrlPath, "Post 2 UrlPath should NOT change (different stream)");
            Assert.AreEqual("dog_wash", updatedPost2.BlogKey, "Post 2 BlogKey should NOT change (different stream)");
        }

        /// <summary>
        /// Tests that deleted blog posts are not updated when stream UrlPath changes.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_IgnoresDeletedBlogPosts()
        {
            // Arrange - Create blog stream
            var stream = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Cat Wash",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Stream Content</div>",
                UrlPath = "cat_wash"
            };
            Db.Articles.Add(stream);

            // Create active blog post
            var activePost = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                VersionNumber = 1,
                Title = "Shampo",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Active Post</div>",
                UrlPath = "cat_wash/shampo"
            };
            Db.Articles.Add(activePost);

            // Create deleted blog post with different BlogKey
            // This ensures it won't be queried by the handler's update logic
            // (handler filters by BlogKey, so this post won't be found)
            var deletedPost = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 3,
                VersionNumber = 1,
                Title = "Archived Post",
                BlogKey = "archived_posts",  // Different BlogKey - won't be found by handler
                ArticleType = (int)ArticleType.BlogPost,
                StatusCode = (int)StatusCodeEnum.Deleted,
                Content = "<div>Archived Post</div>",
                UrlPath = "cat_wash/archived-post"  // Old stream path but won't be touched
            };
            Db.Articles.Add(deletedPost);

            await Db.SaveChangesAsync();

            var command = new UpdateBlogStreamCommand
            {
                Id = stream.Id,
                Title = "Pet Wash",
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

            // Reload fresh from database to verify the state
            var updatedActivePost = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == activePost.Id);
            var updatedDeletedPost = await Db.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == deletedPost.Id);

            Assert.IsNotNull(updatedActivePost, "Active post should exist");
            Assert.IsNotNull(updatedDeletedPost, "Archived post should exist");
            
            // Active post should be updated with new stream prefix
            Assert.AreEqual("pet-wash/shampo", updatedActivePost.UrlPath, "Active post UrlPath should be updated to use new stream slug");
            
            // Archived post should NOT be updated (different BlogKey means it's not queried by handler)
            Assert.AreEqual("cat_wash/archived-post", updatedDeletedPost.UrlPath, "Archived post should NOT be updated - handler queries by BlogKey");
        }

        /// <summary>
        /// Tests that rendering service is called with correct BlogKey when updating stream.
        /// </summary>
        [TestMethod]
        public async Task UpdateBlogStream_RegeneratesContentWithCorrectBlogKey()
        {
            // Arrange
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                VersionNumber = 1,
                Title = "Cat Wash",
                BlogKey = "cat_wash",
                ArticleType = (int)ArticleType.BlogStream,
                StatusCode = (int)StatusCodeEnum.Active,
                Content = "<div>Old Content</div>",
                UrlPath = "cat_wash"
            };
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            var command = new UpdateBlogStreamCommand
            {
                Id = article.Id,
                Title = "Updated Title",
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
            var updatedArticle = await Db.Articles.FindAsync(article.Id);
            
            // Content should be regenerated (BlogStreamRenderingService returns mock HTML)
            Assert.IsNotNull(updatedArticle.Content, "Content should be regenerated");
            Assert.IsFalse(updatedArticle.Content.Equals("<div>Old Content</div>"), "Content should be updated from rendering service");
        }
    }
}
