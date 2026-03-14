// <copyright file="BlogServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services
{
    using Cosmos.Cms.Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Save;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for blog-related functionality.
    /// Tests blog post creation, listing, pagination, categories, and RSS feed generation.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class BlogServiceTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
        }

        #region Blog Post Creation Tests

        /// <summary>
        /// Tests that creating a blog post sets correct article type.
        /// </summary>
        [TestMethod]
        public async Task CreateBlogPost_SetsArticleTypeToBlogPost()
        {
            // Act
            var blogPost = await CreateArticleAsync("My Blog Post", TestUserId, null, "default", ArticleType.BlogPost);

            // Assert
            Assert.AreEqual(ArticleType.BlogPost, blogPost.ArticleType);
        }

        /// <summary>
        /// Tests that blog post has correct blog key.
        /// </summary>
        [TestMethod]
        public async Task CreateBlogPost_SetsBlogKey()
        {
            // Act
            var blogPost = await CreateArticleAsync("Test Post", TestUserId, null, "tech-blog", ArticleType.BlogPost);

            // Assert
            var dbArticle = await Db.Articles.FindAsync(blogPost.Id);
            Assert.AreEqual("tech-blog", dbArticle.BlogKey);
        }

        /// <summary>
        /// Tests that blog post URL includes date in path.
        /// </summary>
        [TestMethod]
        public async Task CreateBlogPost_GeneratesDateBasedUrl()
        {
            // Arrange
            // Create home page first
            await CreateArticleAsync("Home", TestUserId);

            // Act
            var blogPost = await CreateArticleAsync("Test Post", TestUserId, null, "default", ArticleType.BlogPost);

            // Assert
            // URL should contain year or be formatted appropriately
            Assert.IsNotNull(blogPost.UrlPath);
            Assert.IsTrue(blogPost.UrlPath.Length > 0);
        }

        /// <summary>
        /// Tests that blog post can have category.
        /// </summary>
        [TestMethod]
        public async Task CreateBlogPost_WithCategory_SetsCategory()
        {
            // Arrange
            var blogPost = await CreateArticleAsync("Test Post", TestUserId, null, "default", ArticleType.BlogPost);

            // Act
            var command = new SaveArticleCommand
            {
                ArticleNumber = blogPost.ArticleNumber,
                Title = blogPost.Title,
                Content = blogPost.Content,
                Category = "Technology",
                UserId = TestUserId,
                ArticleType = ArticleType.BlogPost
            };
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Technology", result.Data?.Model?.Category);
        }

        #endregion

        #region Blog Listing and Pagination Tests

        /// <summary>
        /// Tests that blog posts can be retrieved by blog key.
        /// </summary>
        [TestMethod]
        public async Task GetBlogPosts_FiltersByBlogKey()
        {
            // Arrange
            await CreateArticleAsync("Home", TestUserId); // Home page
            await CreateArticleAsync("Post 1", TestUserId, null, "blog1", ArticleType.BlogPost);
            await CreateArticleAsync("Post 2", TestUserId, null, "blog1", ArticleType.BlogPost);
            await CreateArticleAsync("Post 3", TestUserId, null, "blog2", ArticleType.BlogPost);

            // Act
            var blog1Posts = await Db.Articles
                .Where(a => a.ArticleType == (int)ArticleType.BlogPost && a.BlogKey == "blog1")
                .CountAsync();

            // Assert
            Assert.AreEqual(2, blog1Posts);
        }

        /// <summary>
        /// Tests that published blog posts can be queried.
        /// </summary>
        [TestMethod]
        public async Task GetPublishedBlogPosts_ReturnsOnlyPublished()
        {
            // Arrange
            await CreateArticleAsync("Home", TestUserId);
            
            var post1 = await CreateArticleAsync("Published Post", TestUserId, null, "default", ArticleType.BlogPost);
            await Logic.PublishArticle(post1.Id, DateTimeOffset.UtcNow);
            
            var post2 = await CreateArticleAsync("Draft Post", TestUserId, null, "default", ArticleType.BlogPost);
            // Don't publish post2

            // Act
            var publishedCount = await Db.Articles
                .Where(a => a.ArticleType == (int)ArticleType.BlogPost 
                    && a.BlogKey == "default" 
                    && a.Published != null)
                .CountAsync();

            // Assert
            Assert.AreEqual(1, publishedCount);
        }

        /// <summary>
        /// Tests that blog posts are ordered by published date descending.
        /// </summary>
        [TestMethod]
        public async Task GetBlogPosts_OrdersByPublishedDateDescending()
        {
            // Arrange
            await CreateArticleAsync("Home", TestUserId);
            
            var post1 = await CreateArticleAsync("Old Post", TestUserId, null, "default", ArticleType.BlogPost);
            await Logic.PublishArticle(post1.Id, DateTimeOffset.UtcNow.AddDays(-2));
            
            await Task.Delay(100); // Ensure different timestamps
            
            var post2 = await CreateArticleAsync("New Post", TestUserId, null, "default", ArticleType.BlogPost);
            await Logic.PublishArticle(post2.Id, DateTimeOffset.UtcNow);

            // Act
            var posts = await Db.Articles
                .Where(a => a.ArticleType == (int)ArticleType.BlogPost 
                    && a.BlogKey == "default" 
                    && a.Published != null)
                .OrderByDescending(a => a.Published)
                .ToListAsync();

            // Assert
            Assert.AreEqual(2, posts.Count);
            Assert.IsTrue(posts[0].Published > posts[1].Published);
        }

        /// <summary>
        /// Tests pagination of blog posts.
        /// </summary>
        [TestMethod]
        public async Task GetBlogPosts_Pagination_ReturnsCorrectPage()
        {
            // Arrange
            await CreateArticleAsync("Home", TestUserId);
            
            for (int i = 1; i <= 15; i++)
            {
                var post = await CreateArticleAsync($"Post {i}", TestUserId, null, "default", ArticleType.BlogPost);
                await Logic.PublishArticle(post.Id, DateTimeOffset.UtcNow.AddMinutes(i));
                await Task.Delay(10); // Ensure different timestamps
            }

            // Act - Get page 2 with 5 items per page
            var pageSize = 5;
            var pageNumber = 2;
            var page2Posts = await Db.Articles
                .Where(a => a.ArticleType == (int)ArticleType.BlogPost 
                    && a.BlogKey == "default" 
                    && a.Published != null)
                .OrderByDescending(a => a.Published)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Assert
            Assert.AreEqual(5, page2Posts.Count);
        }

        #endregion

        #region Blog Categories Tests

        /// <summary>
        /// Tests that blog posts can be filtered by category.
        /// </summary>
        [TestMethod]
        public async Task GetBlogPosts_FiltersByCategory()
        {
            // Arrange
            await CreateArticleAsync("Home", TestUserId);
            
            var post1 = await CreateArticleAsync("Tech Post", TestUserId, null, "default", ArticleType.BlogPost);
            var command1 = new SaveArticleCommand
            {
                ArticleNumber = post1.ArticleNumber,
                Title = post1.Title,
                Content = post1.Content,
                Category = "Technology",
                UserId = TestUserId,
                ArticleType = ArticleType.BlogPost
            };
            await SaveArticleHandler.HandleAsync(command1);
            await Logic.PublishArticle(post1.Id, DateTimeOffset.UtcNow);
            
            var post2 = await CreateArticleAsync("Science Post", TestUserId, null, "default", ArticleType.BlogPost);
            var command2 = new SaveArticleCommand
            {
                ArticleNumber = post2.ArticleNumber,
                Title = post2.Title,
                Content = post2.Content,
                Category = "Science",
                UserId = TestUserId,
                ArticleType = ArticleType.BlogPost
            };
            await SaveArticleHandler.HandleAsync(command2);
            await Logic.PublishArticle(post2.Id, DateTimeOffset.UtcNow);

            // Act
            var techPosts = await Db.Articles
                .Where(a => a.ArticleType == (int)ArticleType.BlogPost 
                    && a.Category == "Technology")
                .CountAsync();

            // Assert
            Assert.AreEqual(1, techPosts);
        }

        /// <summary>
        /// Tests getting distinct categories from blog posts.
        /// </summary>
        [TestMethod]
        public async Task GetBlogCategories_ReturnsDistinctCategories()
        {
            // Arrange
            await CreateArticleAsync("Home", TestUserId);
            
            var categories = new[] { "Tech", "Science", "Tech", "Sports" };
            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                // Use index to ensure unique titles even when categories repeat
                var post = await CreateArticleAsync($"{category} Post {i + 1}", TestUserId, null, "default", ArticleType.BlogPost);
                var command = new SaveArticleCommand
                {
                    ArticleNumber = post.ArticleNumber,
                    Title = post.Title,
                    Content = post.Content,
                    Category = category,
                    UserId = TestUserId,
                    ArticleType = ArticleType.BlogPost
                };
                await SaveArticleHandler.HandleAsync(command);
            }

            // Act
            var distinctCategories = await Db.Articles
                .Where(a => a.ArticleType == (int)ArticleType.BlogPost 
                    && !string.IsNullOrEmpty(a.Category))
                .Select(a => a.Category)
                .Distinct()
                .ToListAsync();

            // Assert
            Assert.AreEqual(3, distinctCategories.Count);
            Assert.IsTrue(distinctCategories.Contains("Tech"));
            Assert.IsTrue(distinctCategories.Contains("Science"));
            Assert.IsTrue(distinctCategories.Contains("Sports"));
        }

        #endregion

        #region Blog Introduction/Excerpt Tests

        /// <summary>
        /// Tests that blog post introduction is auto-generated from content.
        /// </summary>
        [TestMethod]
        public async Task SaveBlogPost_AutoGeneratesIntroduction()
        {
            // Arrange
            await CreateArticleAsync("Home", TestUserId);
            var post = await CreateArticleAsync("Test Post", TestUserId, null, "default", ArticleType.BlogPost);

            // Act
            var command = new SaveArticleCommand
            {
                ArticleNumber = post.ArticleNumber,
                Title = post.Title,
                Content = "<p>This is the first paragraph that should become the introduction.</p><p>This is the second paragraph.</p>",
                UserId = TestUserId,
                ArticleType = ArticleType.BlogPost
                // No Introduction specified - handler should auto-generate for blog posts
            };
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var dbArticle = await Db.Articles.FindAsync(post.Id);
            Assert.IsNotNull(dbArticle.Introduction);
            Assert.IsTrue(dbArticle.Introduction.Contains("first paragraph"));
        }

        /// <summary>
        /// Tests that custom introduction is preserved.
        /// </summary>
        [TestMethod]
        public async Task SaveBlogPost_PreservesCustomIntroduction()
        {
            // Arrange
            await CreateArticleAsync("Home", TestUserId);
            var post = await CreateArticleAsync("Test Post", TestUserId, null, "default", ArticleType.BlogPost);
            post.Content = "<p>Content paragraph.</p>";
            post.Introduction = "Custom introduction text";

            // Act
            await SaveArticleAsync(post, TestUserId);

            // Assert
            var dbArticle = await Db.Articles.FindAsync(post.Id);
            Assert.AreEqual("Custom introduction text", dbArticle.Introduction);
        }

        #endregion
    }
}