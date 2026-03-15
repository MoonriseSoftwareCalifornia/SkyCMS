// <copyright file="BlogStreamRenderingServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.BlogPublishing
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Services.BlogPublishing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for the <see cref="BlogStreamRenderingService"/> class.
    /// Tests the new client-side orchestration model with embedded JSON metadata.
    /// </summary>
    [TestClass]
    public class BlogStreamRenderingServiceTests : SkyCmsTestBase
    {
        private IBlogStreamRenderingService service = null!;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);
            service = new BlogStreamRenderingService(Db);
        }

        [TestCleanup]
        public void Cleanup() => Db.Dispose();

        #region Constructor Tests

        /// <summary>
        /// Verifies constructor throws ArgumentNullException when database context is null.
        /// </summary>
        [TestMethod]
        public void Constructor_NullDb_ThrowsArgumentNullException()
        {
            // Act & Assert
            try
            {
                new BlogStreamRenderingService(null!);
                Assert.Fail("Should have thrown ArgumentNullException");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("db", ex.ParamName);
            }
        }

        #endregion

        #region GenerateBlogStreamWrapperAsync Tests

        /// <summary>
        /// Verifies that GenerateBlogStreamWrapperAsync throws on null article.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogStreamWrapperAsync_NullArticle_ThrowsArgumentNullException()
        {
            // Act & Assert
            try
            {
                await service.GenerateBlogStreamWrapperAsync(null!, "test-blog");
                Assert.Fail("Should have thrown ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        /// <summary>
        /// Verifies that GenerateBlogStreamWrapperAsync throws when blog key is null.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogStreamWrapperAsync_NullBlogKey_ThrowsArgumentException()
        {
            // Arrange
            var article = CreateBlogStreamArticle();

            // Act & Assert
            try
            {
                await service.GenerateBlogStreamWrapperAsync(article, null!);
                Assert.Fail("Should have thrown ArgumentException");
            }
            catch (ArgumentException)
            {
                // Expected
            }
        }

        /// <summary>
        /// Verifies that GenerateBlogStreamWrapperAsync generates valid HTML with embedded JSON.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogStreamWrapperAsync_ValidInput_ReturnsHtmlWithEmbeddedJson()
        {
            // Arrange
            var article = CreateBlogStreamArticle();
            article.Title = "My Test Blog";
            article.Introduction = "Welcome to my blog";
            article.BlogKey = "test-blog";
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogStreamWrapperAsync(article, "test-blog");

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.Contains("<!DOCTYPE html>", result, "Should contain HTML5 doctype");
            Assert.Contains("My Test Blog", result, "Should contain blog title");
            Assert.Contains("Welcome to my blog", result, "Should contain introduction");
            Assert.Contains("blog-posts-meta", result, "Should contain embedded JSON script");
            Assert.Contains("blog-stream-loader.js", result, "Should reference loader script");
        }

        /// <summary>
        /// Verifies wrapper includes banner image when provided.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogStreamWrapperAsync_WithBannerImage_IncludesImageTag()
        {
            // Arrange
            var article = CreateBlogStreamArticle();
            article.BannerImage = "https://example.com/banner.jpg";
            article.BlogKey = "test-blog";
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogStreamWrapperAsync(article, "test-blog");

            // Assert
            Assert.Contains("<img", result, "Should contain image tag");
            Assert.Contains("https://example.com/banner.jpg", result, "Should contain banner URL");
        }

        /// <summary>
        /// Verifies wrapper excludes banner image when not provided.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogStreamWrapperAsync_NoBannerImage_ExcludesImageTag()
        {
            // Arrange
            var article = CreateBlogStreamArticle();
            article.BannerImage = string.Empty;
            article.BlogKey = "test-blog";
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogStreamWrapperAsync(article, "test-blog");

            // Assert
            Assert.IsFalse(result.Contains("<figure"), "Should not contain figure tag when no banner");
        }

        /// <summary>
        /// Verifies wrapper excludes introduction paragraph when introduction is empty.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogStreamWrapperAsync_NoIntroduction_ExcludesIntroParagraph()
        {
            // Arrange
            var article = CreateBlogStreamArticle();
            article.Introduction = string.Empty;
            article.BlogKey = "test-blog";
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogStreamWrapperAsync(article, "test-blog");

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsFalse(result.Contains("sky-blog-stream-intro"),
                "Should not contain intro paragraph when introduction is empty");
        }

        /// <summary>
        /// Verifies wrapper includes introduction paragraph when introduction is provided.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogStreamWrapperAsync_WithIntroduction_IncludesIntroduction()
        {
            // Arrange
            var article = CreateBlogStreamArticle();
            article.Introduction = "Welcome to my tech blog!";
            article.BlogKey = "test-blog";
            Db.Articles.Add(article);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogStreamWrapperAsync(article, "test-blog");

            // Assert
            Assert.Contains("sky-blog-stream-intro", result);
            Assert.Contains("Welcome to my tech blog!", result);
        }

        #endregion

        #region GenerateBlogPostMetadataJsonAsync Tests

        /// <summary>
        /// Verifies metadata JSON generation returns empty array when no posts.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_NoPosts_ReturnsEmptyArray()
        {
            // Act
            var result = await service.GenerateBlogPostMetadataJsonAsync("empty-blog");

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            var parsed = JsonConvert.DeserializeObject<List<dynamic>>(result);
            Assert.IsNotNull(parsed, "Should deserialize to list");
            Assert.AreEqual(0, parsed.Count, "Should return empty array");
        }

        /// <summary>
        /// Verifies metadata generation throws ArgumentException when blog key is empty.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_EmptyBlogKey_ThrowsArgumentException()
        {
            // Act & Assert
            try
            {
                await service.GenerateBlogPostMetadataJsonAsync(string.Empty);
                Assert.Fail("Should have thrown ArgumentException");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("blogKey", ex.ParamName);
            }
        }

        /// <summary>
        /// Verifies metadata generation throws ArgumentException when blog key is whitespace only.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_WhitespaceBlogKey_ThrowsArgumentException()
        {
            // Act & Assert
            try
            {
                await service.GenerateBlogPostMetadataJsonAsync("   ");
                Assert.Fail("Should have thrown ArgumentException");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("blogKey", ex.ParamName);
            }
        }

        /// <summary>
        /// Verifies metadata includes correct fields.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_WithPosts_IncludesAllFields()
        {
            // Arrange
            const string blogKey = "test-blog";
            var post = CreateBlogPostArticle();
            post.BlogKey = blogKey;
            post.Title = "Test Post";
            post.UrlPath = "test-blog/test-post";
            post.Introduction = "Test excerpt";
            post.BannerImage = "https://example.com/image.jpg";
            post.Published = DateTimeOffset.UtcNow.AddDays(-1);
            post.Updated = DateTimeOffset.UtcNow;

            var page = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = post.UrlPath,
                VersionNumber = 1,
                Published = post.Published,
                Expires = null,
                Title = post.Title,
                Content = "<p>Test content</p>",
                Updated = post.Updated,
                BannerImage = post.BannerImage,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = "test-blog",
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.BlogPost,
                Category = "blog",
                Introduction = post.Introduction,
                BlogKey = blogKey
            };

            Db.Pages.Add(page);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogPostMetadataJsonAsync(blogKey);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            var parsed = JsonConvert.DeserializeObject<List<dynamic>>(result);
            Assert.AreEqual(1, parsed.Count, "Should have one post");

            dynamic post1 = parsed[0];
            Assert.AreEqual("test-blog/test-post", (string)post1["urlPath"]);
            Assert.AreEqual("Test Post", (string)post1["title"]);
            Assert.AreEqual("Test excerpt", (string)post1["introduction"]);
            Assert.AreEqual("https://example.com/image.jpg", (string)post1["bannerImage"]);
            Assert.IsNotNull(post1["published"]);
            Assert.IsNotNull(post1["updated"]);
        }

        /// <summary>
        /// Verifies posts are ordered by published date (newest first).
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_MultiplePosts_OrderedByPublishedDescending()
        {
            // Arrange
            const string blogKey = "test-blog";
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < 3; i++)
            {
                var page = new PublishedPage
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = i + 1,
                    StatusCode = (int)StatusCodeEnum.Active,
                    UrlPath = $"test-blog/post-{i}",
                    VersionNumber = 1,
                    Published = now.AddDays(-(2 - i)), // 2 days ago, 1 day ago, today
                    Expires = null,
                    Title = $"Post {i}",
                    Content = $"<p>Content {i}</p>",
                    Updated = now,
                    BannerImage = string.Empty,
                    HeaderJavaScript = string.Empty,
                    FooterJavaScript = string.Empty,
                    ParentUrlPath = "test-blog",
                    AuthorInfo = string.Empty,
                    ArticleType = (int)ArticleType.BlogPost,
                    Category = "blog",
                    Introduction = $"Excerpt {i}",
                    BlogKey = blogKey
                };
                Db.Pages.Add(page);
            }
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogPostMetadataJsonAsync(blogKey);

            // Assert
            var parsed = JsonConvert.DeserializeObject<List<dynamic>>(result);
            Assert.AreEqual(3, parsed.Count, "Should have three posts");
            Assert.AreEqual("Post 2", (string)parsed[0]["title"], "Most recent post should be first");
            Assert.AreEqual("Post 1", (string)parsed[1]["title"]);
            Assert.AreEqual("Post 0", (string)parsed[2]["title"], "Oldest post should be last");
        }

        /// <summary>
        /// Verifies unpublished posts are excluded.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_UnpublishedPost_IsExcluded()
        {
            // Arrange
            const string blogKey = "test-blog";
            var now = DateTimeOffset.UtcNow;

            var publishedPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = "test-blog/published",
                VersionNumber = 1,
                Published = now.AddDays(-1),
                Expires = null,
                Title = "Published Post",
                Content = "<p>Content</p>",
                Updated = now,
                BannerImage = string.Empty,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = "test-blog",
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.BlogPost,
                Category = "blog",
                Introduction = "Published",
                BlogKey = blogKey
            };

            var unpublishedPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = "test-blog/unpublished",
                VersionNumber = 1,
                Published = now.AddDays(1), // Future date
                Expires = null,
                Title = "Unpublished Post",
                Content = "<p>Content</p>",
                Updated = now,
                BannerImage = string.Empty,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = "test-blog",
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.BlogPost,
                Category = "blog",
                Introduction = "Unpublished",
                BlogKey = blogKey
            };

            Db.Pages.Add(publishedPage);
            Db.Pages.Add(unpublishedPage);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogPostMetadataJsonAsync(blogKey);

            // Assert
            var parsed = JsonConvert.DeserializeObject<List<dynamic>>(result);
            Assert.AreEqual(1, parsed.Count, "Should only include published posts");
            Assert.AreEqual("Published Post", (string)parsed[0]["title"]);
        }

        /// <summary>
        /// Verifies expired posts are excluded.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_ExpiredPost_IsExcluded()
        {
            // Arrange
            const string blogKey = "test-blog";
            var now = DateTimeOffset.UtcNow;

            var activePage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = "test-blog/active",
                VersionNumber = 1,
                Published = now.AddDays(-1),
                Expires = null,
                Title = "Active Post",
                Content = "<p>Content</p>",
                Updated = now,
                BannerImage = string.Empty,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = "test-blog",
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.BlogPost,
                Category = "blog",
                Introduction = "Active",
                BlogKey = blogKey
            };

            var expiredPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = "test-blog/expired",
                VersionNumber = 1,
                Published = now.AddDays(-10),
                Expires = now.AddDays(-1), // Expired yesterday
                Title = "Expired Post",
                Content = "<p>Content</p>",
                Updated = now,
                BannerImage = string.Empty,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = "test-blog",
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.BlogPost,
                Category = "blog",
                Introduction = "Expired",
                BlogKey = blogKey
            };

            Db.Pages.Add(activePage);
            Db.Pages.Add(expiredPage);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogPostMetadataJsonAsync(blogKey);

            // Assert
            var parsed = JsonConvert.DeserializeObject<List<dynamic>>(result);
            Assert.AreEqual(1, parsed.Count, "Should exclude expired posts");
            Assert.AreEqual("Active Post", (string)parsed[0]["title"]);
        }

        /// <summary>
        /// Verifies only posts from the specified blog key are included in metadata.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_DifferentBlogKeys_OnlyIncludesRequestedBlog()
        {
            // Arrange
            const string blogKeyA = "blog-a";
            const string blogKeyB = "blog-b";
            var now = DateTimeOffset.UtcNow;

            var pageA = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = "blog-a/post-1",
                VersionNumber = 1,
                Published = now.AddDays(-1),
                Expires = null,
                Title = "Blog A Post",
                Content = "<p>Content A</p>",
                Updated = now,
                BannerImage = string.Empty,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = "blog-a",
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.BlogPost,
                Category = "blog",
                Introduction = "Post from Blog A",
                BlogKey = blogKeyA
            };

            var pageB = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = "blog-b/post-1",
                VersionNumber = 1,
                Published = now.AddDays(-1),
                Expires = null,
                Title = "Blog B Post",
                Content = "<p>Content B</p>",
                Updated = now,
                BannerImage = string.Empty,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = "blog-b",
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.BlogPost,
                Category = "blog",
                Introduction = "Post from Blog B",
                BlogKey = blogKeyB
            };

            Db.Pages.Add(pageA);
            Db.Pages.Add(pageB);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogPostMetadataJsonAsync(blogKeyA);

            // Assert
            var parsed = JsonConvert.DeserializeObject<List<dynamic>>(result);
            Assert.AreEqual(1, parsed.Count, "Should only include posts from blog-a");
            Assert.AreEqual("Blog A Post", (string)parsed[0]["title"]);
            Assert.AreEqual("blog-a/post-1", (string)parsed[0]["urlPath"]);
        }

        /// <summary>
        /// Verifies only BlogPost article type is included in metadata.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_NonBlogPostType_IsExcluded()
        {
            // Arrange
            const string blogKey = "test-blog";
            var now = DateTimeOffset.UtcNow;

            var blogPost = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = "test-blog/post",
                VersionNumber = 1,
                Published = now.AddDays(-1),
                Expires = null,
                Title = "Blog Post",
                Content = "<p>Post content</p>",
                Updated = now,
                BannerImage = string.Empty,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = "test-blog",
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.BlogPost,
                Category = "blog",
                Introduction = "A blog post",
                BlogKey = blogKey
            };

            var generalPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = "test-blog/general",
                VersionNumber = 1,
                Published = now.AddDays(-1),
                Expires = null,
                Title = "General Page",
                Content = "<p>General content</p>",
                Updated = now,
                BannerImage = string.Empty,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = "test-blog",
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.General,
                Category = "general",
                Introduction = "A general page",
                BlogKey = blogKey
            };

            var blogStreamPage = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 3,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = "test-blog",
                VersionNumber = 1,
                Published = now.AddDays(-1),
                Expires = null,
                Title = "Test Blog Stream",
                Content = "<p>Stream content</p>",
                Updated = now,
                BannerImage = string.Empty,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = string.Empty,
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.BlogStream,
                Category = "blog",
                Introduction = "A blog stream",
                BlogKey = blogKey
            };

            Db.Pages.Add(blogPost);
            Db.Pages.Add(generalPage);
            Db.Pages.Add(blogStreamPage);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogPostMetadataJsonAsync(blogKey);

            // Assert
            var parsed = JsonConvert.DeserializeObject<List<dynamic>>(result);
            Assert.AreEqual(1, parsed.Count, "Should only include BlogPost article type");
            Assert.AreEqual("Blog Post", (string)parsed[0]["title"]);
        }

        /// <summary>
        /// Verifies null introduction is converted to empty string in metadata.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostMetadataJsonAsync_NullIntroduction_ReturnsEmptyString()
        {
            // Arrange
            const string blogKey = "test-blog";
            var now = DateTimeOffset.UtcNow;

            var page = new PublishedPage
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                StatusCode = (int)StatusCodeEnum.Active,
                UrlPath = "test-blog/post",
                VersionNumber = 1,
                Published = now.AddDays(-1),
                Expires = null,
                Title = "Test Post",
                Content = "<p>Content</p>",
                Updated = now,
                BannerImage = string.Empty,
                HeaderJavaScript = string.Empty,
                FooterJavaScript = string.Empty,
                ParentUrlPath = "test-blog",
                AuthorInfo = string.Empty,
                ArticleType = (int)ArticleType.BlogPost,
                Category = "blog",
                Introduction = string.Empty,
                BlogKey = blogKey
            };

            Db.Pages.Add(page);
            await Db.SaveChangesAsync();

            // Act
            var result = await service.GenerateBlogPostMetadataJsonAsync(blogKey);

            // Assert
            var parsed = JsonConvert.DeserializeObject<List<dynamic>>(result);
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual(string.Empty, (string)parsed[0]["introduction"],
                "Null introduction should be converted to empty string");
            Assert.AreEqual(string.Empty, (string)parsed[0]["bannerImage"],
                "Null bannerImage should be converted to empty string");
        }

        #endregion

        #region GenerateBlogPostSnippetAsync Tests

        /// <summary>
        /// Verifies blog post snippet generation throws on null article.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostSnippetAsync_NullArticle_ThrowsArgumentNullException()
        {
            // Act & Assert
            try
            {
                await service.GenerateBlogPostSnippetAsync(null!);
                Assert.Fail("Should have thrown ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        /// <summary>
        /// Verifies blog post snippet returns valid article element.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostSnippetAsync_ValidArticle_ReturnsArticleElement()
        {
            // Arrange
            var article = CreateBlogPostArticle();
            article.Title = "Test Post";
            article.Content = "<p>Post content here</p>";
            article.Updated = DateTimeOffset.UtcNow;

            // Act
            var result = await service.GenerateBlogPostSnippetAsync(article);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.Contains("<article", result, "Should contain article element");
            Assert.Contains("Test Post", result, "Should contain post title");
            Assert.Contains("Post content here", result, "Should contain post content");
            Assert.Contains("sky-blog-post", result, "Should use blog post CSS classes");
        }

        /// <summary>
        /// Verifies banner image is included in snippet when provided.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostSnippetAsync_WithBannerImage_IncludesImage()
        {
            // Arrange
            var article = CreateBlogPostArticle();
            article.BannerImage = "https://example.com/post-banner.jpg";

            // Act
            var result = await service.GenerateBlogPostSnippetAsync(article);

            // Assert
            Assert.Contains("<img", result, "Should include image tag");
            Assert.Contains("https://example.com/post-banner.jpg", result, "Should include image URL");
        }

        /// <summary>
        /// Verifies blog post snippet excludes figure tag when banner image is empty.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostSnippetAsync_EmptyBannerImage_ExcludesFigureTag()
        {
            // Arrange
            var article = CreateBlogPostArticle();
            article.BannerImage = string.Empty;

            // Act
            var result = await service.GenerateBlogPostSnippetAsync(article);

            // Assert
            Assert.IsFalse(result.Contains("<figure"),
                "Should not contain figure tag when banner image is empty");
        }

        /// <summary>
        /// Verifies HTML entities are properly escaped in snippet.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostSnippetAsync_WithSpecialCharacters_EscapesHtml()
        {
            // Arrange
            var article = CreateBlogPostArticle();
            article.Title = "Test & Special <Characters>";

            // Act
            var result = await service.GenerateBlogPostSnippetAsync(article);

            // Assert
            Assert.Contains("&amp;", result, "Should escape ampersand");
            Assert.Contains("&lt;", result, "Should escape less-than");
            Assert.Contains("&gt;", result, "Should escape greater-than");
        }

        /// <summary>
        /// Verifies blog post snippet correctly formats the updated date.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostSnippetAsync_DateFormatting_DisplaysCorrectFormat()
        {
            // Arrange
            var article = CreateBlogPostArticle();
            var testDate = new DateTimeOffset(2024, 3, 15, 10, 30, 0, TimeSpan.Zero);
            article.Updated = testDate;

            // Act
            var result = await service.GenerateBlogPostSnippetAsync(article);

            // Assert
            Assert.Contains("March 15, 2024", result,
                "Should display date in 'MMMM d, yyyy' format");
            Assert.Contains("2024-03-15T10:30:00+00:00", result,
                "Should include ISO 8601 format in datetime attribute");
        }

        /// <summary>
        /// Verifies blog post snippet properly escapes special HTML characters in banner image alt text.
        /// </summary>
        [TestMethod]
        public async Task GenerateBlogPostSnippetAsync_SpecialCharactersInBanner_EscapesAltText()
        {
            // Arrange
            var article = CreateBlogPostArticle();
            article.Title = "Test & Special <Title>";
            article.BannerImage = "https://example.com/image.jpg?size=large&quality=high";

            // Act
            var result = await service.GenerateBlogPostSnippetAsync(article);

            // Assert
            Assert.Contains("&amp;", result, "Should escape ampersand in alt text");
            Assert.Contains("&lt;", result, "Should escape less-than in title");
            Assert.Contains("&gt;", result, "Should escape greater-than in title");
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Verifies end-to-end blog stream rendering with metadata and snippets.
        /// </summary>
        [TestMethod]
        public async Task IntegrationTest_CompleteWorkflow_RendersStreamWithPostsAndSnippets()
        {
            // Arrange
            const string blogKey = "integration-test-blog";
            var now = DateTimeOffset.UtcNow;

            // Create blog stream article
            var streamArticle = CreateBlogStreamArticle();
            streamArticle.BlogKey = blogKey;
            streamArticle.Title = "Integration Test Blog";
            streamArticle.Introduction = "Testing the complete workflow";
            streamArticle.BannerImage = "https://example.com/stream-banner.jpg";
            Db.Articles.Add(streamArticle);

            // Create blog posts via PublishedPage (as they appear in database)
            var posts = new List<PublishedPage>();
            for (int i = 1; i <= 3; i++)
            {
                var page = new PublishedPage
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = 100 + i,
                    StatusCode = (int)StatusCodeEnum.Active,
                    UrlPath = $"{blogKey}/post-{i}",
                    VersionNumber = 1,
                    Published = now.AddDays(-(4 - i)),
                    Expires = null,
                    Title = $"Blog Post {i}",
                    Content = $"<p>Content for post {i}</p>",
                    Updated = now,
                    BannerImage = i % 2 == 0 ? $"https://example.com/post-{i}.jpg" : string.Empty,
                    HeaderJavaScript = string.Empty,
                    FooterJavaScript = string.Empty,
                    ParentUrlPath = blogKey,
                    AuthorInfo = string.Empty,
                    ArticleType = (int)ArticleType.BlogPost,
                    Category = "blog",
                    Introduction = $"Excerpt from post {i}",
                    BlogKey = blogKey
                };
                posts.Add(page);
                Db.Pages.Add(page);
            }
            await Db.SaveChangesAsync();

            // Act - Generate wrapper with embedded metadata
            var wrapperHtml = await service.GenerateBlogStreamWrapperAsync(streamArticle, blogKey);
            var metadataJson = await service.GenerateBlogPostMetadataJsonAsync(blogKey);

            // Act - Generate individual post snippets
            var postSnippets = new List<string>();
            foreach (var post in posts)
            {
                var postArticle = CreateBlogPostArticle();
                postArticle.Title = post.Title;
                postArticle.Content = post.Content;
                postArticle.BannerImage = post.BannerImage;
                var snippet = await service.GenerateBlogPostSnippetAsync(postArticle);
                postSnippets.Add(snippet);
            }

            // Assert - Verify wrapper
            Assert.Contains("<!DOCTYPE html>", wrapperHtml);
            Assert.Contains("Integration Test Blog", wrapperHtml);
            Assert.Contains("Testing the complete workflow", wrapperHtml);
            Assert.Contains("blog-posts-meta", wrapperHtml);
            Assert.Contains("blog-stream-loader.js", wrapperHtml);

            // Assert - Verify metadata
            var parsedMetadata = JsonConvert.DeserializeObject<List<dynamic>>(metadataJson);
            Assert.AreEqual(3, parsedMetadata.Count, "Should have 3 posts");
            Assert.AreEqual("Blog Post 3", (string)parsedMetadata[0]["title"],
                "Most recent post should be first");
            Assert.AreEqual("Blog Post 1", (string)parsedMetadata[2]["title"],
                "Oldest post should be last");

            // Assert - Verify snippets
            Assert.AreEqual(3, postSnippets.Count);
            foreach (var snippet in postSnippets)
            {
                Assert.Contains("<article", snippet);
                Assert.Contains("sky-blog-post", snippet);
                Assert.Contains("sky-blog-post-title", snippet);
                Assert.Contains("sky-blog-post-content", snippet);
            }

            // Verify specific post features
            Assert.Contains("https://example.com/post-2.jpg", postSnippets[1],
                "Post 2 should have banner image");
            Assert.IsFalse(postSnippets[0].Contains("<figure"),
                "Post 1 should not have figure tag (no banner)");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a sample blog stream article.
        /// </summary>
        private Article CreateBlogStreamArticle()
        {
            return new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                UrlPath = "test-blog-stream",
                Title = "Test Blog Stream",
                Content = "<p>Stream content</p>",
                Published = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString(),
                StatusCode = (int)StatusCodeEnum.Active,
                ArticleType = (int)ArticleType.BlogStream,
                Category = "blog-stream",
                Introduction = "A test blog stream",
                BlogKey = "test-blog-stream",
                VersionNumber = 1
            };
        }

        /// <summary>
        /// Creates a sample blog post article.
        /// </summary>
        private Article CreateBlogPostArticle()
        {
            return new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 2,
                UrlPath = "test-blog/test-post",
                Title = "Test Blog Post",
                Content = "<p>Post content</p>",
                Published = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow,
                UserId = TestUserId.ToString(),
                StatusCode = (int)StatusCodeEnum.Active,
                ArticleType = (int)ArticleType.BlogPost,
                Category = "blog",
                Introduction = "A test blog post",
                BlogKey = "test-blog",
                VersionNumber = 1
            };
        }

        #endregion
    }
}
