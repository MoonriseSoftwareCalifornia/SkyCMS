// <copyright file="BlogViewRenderingTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Models;
    using Cosmos.Common.Models.Blog;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for blog view models to ensure ArticleViewModel with different ArticleTypes
    /// has the correct data structure and properties for rendering through the shared Razor views.
    /// These tests validate the models that feed into _BlogStreamPartial, _BlogPostPartial, etc.
    /// </summary>
    [TestClass]
    public class BlogViewRenderingTests
    {
        /// <summary>
        /// Creates a mock ArticleViewModel for testing.
        /// </summary>
        private ArticleViewModel CreateMockArticleViewModel(ArticleType articleType)
        {
            var layout = new LayoutViewModel
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = true,
                Head = "<meta name=\"test\" content=\"layout-head\">",
                HtmlHeader = "<header><nav>Test Navigation</nav></header>",
                FooterHtmlContent = "<footer>Test Footer Content</footer>",
                Notes = string.Empty
            };

            var model = new ArticleViewModel
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                Title = "Test Article Title",
                UrlPath = "/test-article",
                Content = "<p>This is test article content.</p>",
                LanguageCode = "en",
                LanguageName = "English",
                Updated = DateTimeOffset.UtcNow,
                Published = DateTimeOffset.UtcNow.AddDays(-1),
                StatusCode = Cosmos.Common.Data.Logic.StatusCodeEnum.Active,
                VersionNumber = 1,
                Layout = layout,
                ReadWriteMode = false,
                PreviewMode = false,
                EditModeOn = false,
                CacheDuration = 3600,
                BannerImage = "/images/test-banner.jpg",
                OGImage = "/images/test-og.jpg",
                OGDescription = "Test article description for Open Graph",
                OGUrl = "https://example.com/test-article",
                AuthorInfo = "Test Author",
                Category = "Technology",
                Introduction = "This is a test introduction for the article.",
                ArticleType = articleType,
                HeadJavaScript = "<script>console.log('head');</script>",
                FooterJavaScript = "<script>console.log('footer');</script>"
            };

            return model;
        }

        /// <summary>
        /// Creates a mock BlogIndexViewModel for testing blog streams.
        /// </summary>
        private BlogIndexViewModel CreateMockBlogIndexViewModel(int postCount = 3, int currentPage = 1, int totalPages = 1)
        {
            var posts = new List<BlogListItem>();

            for (int i = 1; i <= postCount; i++)
            {
                posts.Add(new BlogListItem
                {
                    Id = Guid.NewGuid(),
                    ArticleNumber = i,
                    Title = $"Blog Post {i}",
                    UrlPath = $"/blog/post-{i}",
                    Published = DateTimeOffset.UtcNow.AddDays(-i),
                    Updated = DateTimeOffset.UtcNow.AddDays(-i),
                    BannerImage = $"/images/blog-{i}.jpg",
                    Introduction = $"Introduction for blog post {i}.",
                    Category = i % 2 == 0 ? "Technology" : "Design",
                    AuthorInfo = $"Author {i}"
                });
            }

            return new BlogIndexViewModel
            {
                Posts = posts,
                Page = currentPage,
                PageSize = 10,
                TotalPages = totalPages,
                Category = string.Empty
            };
        }

        #region General Article Tests

        [TestMethod]
        [TestCategory("ViewModels")]
        public void ArticleViewModel_WithGeneralType_HasAllRequiredProperties()
        {
            var model = CreateMockArticleViewModel(ArticleType.General);

            Assert.AreEqual(ArticleType.General, model.ArticleType);
            Assert.IsNotNull(model.Title);
            Assert.IsNotNull(model.Content);
            Assert.IsNotNull(model.Layout);
            Assert.IsNotNull(model.Layout.Head);
            Assert.IsNotNull(model.Layout.HtmlHeader);
            Assert.IsNotNull(model.Layout.FooterHtmlContent);
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void ArticleViewModel_WithGeneralType_HasValidOpenGraphMetadata()
        {
            var model = CreateMockArticleViewModel(ArticleType.General);

            Assert.IsNotNull(model.OGImage);
            Assert.IsNotNull(model.OGUrl);
            Assert.IsNotNull(model.OGDescription);
            Assert.IsTrue(model.OGImage.StartsWith("/") || model.OGImage.StartsWith("http"));
        }

        #endregion

        #region Blog Stream Tests

        [TestMethod]
        [TestCategory("ViewModels")]
        public void ArticleViewModel_WithBlogStreamType_IsConfiguredCorrectly()
        {
            var model = CreateMockArticleViewModel(ArticleType.BlogStream);

            Assert.AreEqual(ArticleType.BlogStream, model.ArticleType);
            Assert.IsNotNull(model.Title, "Blog stream must have a title");
            Assert.IsNotNull(model.Introduction, "Blog stream should have an introduction");
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void BlogIndexViewModel_WithPosts_HasCorrectStructure()
        {
            var blogIndex = CreateMockBlogIndexViewModel(3);

            Assert.IsNotNull(blogIndex.Posts);
            Assert.AreEqual(3, blogIndex.Posts.Count());
            Assert.AreEqual(1, blogIndex.Page);
            Assert.AreEqual(10, blogIndex.PageSize);
            Assert.AreEqual(1, blogIndex.TotalPages);
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void BlogIndexViewModel_EmptyPosts_IsValid()
        {
            var emptyBlogIndex = new BlogIndexViewModel
            {
                Posts = new List<BlogListItem>(),
                Page = 1,
                PageSize = 10,
                TotalPages = 0
            };

            Assert.IsNotNull(emptyBlogIndex.Posts);
            Assert.AreEqual(0, emptyBlogIndex.Posts.Count());
            Assert.AreEqual(1, emptyBlogIndex.Page);
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void BlogListItem_HasAllRequiredPropertiesForCardRendering()
        {
            var blogListItem = new BlogListItem
            {
                Id = Guid.NewGuid(),
                ArticleNumber = 1,
                Title = "Test Blog Post",
                UrlPath = "/blog/test-post",
                Published = DateTimeOffset.UtcNow.AddDays(-1),
                Updated = DateTimeOffset.UtcNow,
                BannerImage = "/images/test.jpg",
                Introduction = "Test introduction text.",
                Category = "Technology",
                AuthorInfo = "Test Author"
            };

            Assert.IsNotNull(blogListItem.Title);
            Assert.IsNotNull(blogListItem.UrlPath);
            Assert.IsTrue(blogListItem.UrlPath.StartsWith("/"));
            Assert.IsNotNull(blogListItem.BannerImage);
            Assert.IsNotNull(blogListItem.Introduction);
            Assert.IsNotNull(blogListItem.AuthorInfo);
            Assert.IsTrue(blogListItem.Published.HasValue);
        }

        #endregion

        #region Blog Post Tests

        [TestMethod]
        [TestCategory("ViewModels")]
        public void ArticleViewModel_WithBlogPostType_HasAllBlogMetadata()
        {
            var model = CreateMockArticleViewModel(ArticleType.BlogPost);

            Assert.AreEqual(ArticleType.BlogPost, model.ArticleType);
            Assert.IsNotNull(model.Title);
            Assert.IsNotNull(model.Content);
            Assert.IsNotNull(model.AuthorInfo);
            Assert.IsNotNull(model.Category);
            Assert.IsNotNull(model.Introduction);
            Assert.IsNotNull(model.BannerImage);
            Assert.IsTrue(model.Published.HasValue);
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void ArticleViewModel_BlogPost_HasValidDateTimes()
        {
            var model = CreateMockArticleViewModel(ArticleType.BlogPost);

            Assert.IsTrue(model.Updated != DateTimeOffset.MinValue);
            Assert.IsTrue(model.Published.HasValue);
            Assert.IsTrue(model.Published.Value <= DateTimeOffset.UtcNow);
        }

        #endregion

        #region Pagination Tests

        [TestMethod]
        [TestCategory("ViewModels")]
        public void BlogIndexViewModel_FirstPage_HasCorrectPaginationState()
        {
            var blogIndex = CreateMockBlogIndexViewModel(5, currentPage: 1, totalPages: 3);

            Assert.AreEqual(1, blogIndex.Page);
            Assert.AreEqual(3, blogIndex.TotalPages);
            Assert.IsTrue(blogIndex.Page == 1, "Should be on first page");
            Assert.IsTrue(blogIndex.Page < blogIndex.TotalPages, "Should have more pages");
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void BlogIndexViewModel_LastPage_HasCorrectPaginationState()
        {
            var blogIndex = CreateMockBlogIndexViewModel(5, currentPage: 3, totalPages: 3);

            Assert.AreEqual(3, blogIndex.Page);
            Assert.AreEqual(3, blogIndex.TotalPages);
            Assert.IsTrue(blogIndex.Page == blogIndex.TotalPages, "Should be on last page");
            Assert.IsTrue(blogIndex.Page > 1, "Should have previous pages");
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void BlogIndexViewModel_MiddlePage_HasCorrectPaginationState()
        {
            var blogIndex = CreateMockBlogIndexViewModel(5, currentPage: 2, totalPages: 3);

            Assert.AreEqual(2, blogIndex.Page);
            Assert.AreEqual(3, blogIndex.TotalPages);
            Assert.IsTrue(blogIndex.Page > 1, "Should have previous pages");
            Assert.IsTrue(blogIndex.Page < blogIndex.TotalPages, "Should have next pages");
        }

        #endregion

        #region View Logic Tests

        [TestMethod]
        [TestCategory("ViewLogic")]
        public void BlogListItem_DisplayDate_UsesPublishedOverUpdated()
        {
            var published = DateTimeOffset.UtcNow.AddDays(-5);
            var updated = DateTimeOffset.UtcNow.AddDays(-1);

            var item = new BlogListItem
            {
                Published = published,
                Updated = updated
            };

            var displayDate = item.Published ?? item.Updated;
            Assert.AreEqual(published, displayDate, "Should prefer Published date when available");
        }

        [TestMethod]
        [TestCategory("ViewLogic")]
        public void BlogListItem_DisplayDate_FallsBackToUpdated()
        {
            var updated = DateTimeOffset.UtcNow.AddDays(-1);

            var item = new BlogListItem
            {
                Published = null,
                Updated = updated
            };

            var displayDate = item.Published ?? item.Updated;
            Assert.AreEqual(updated, displayDate, "Should use Updated date when Published is null");
        }

        [TestMethod]
        [TestCategory("ViewLogic")]
        public void ArticleViewModel_BlogPost_DisplayDate_UsesPublishedOverUpdated()
        {
            var model = CreateMockArticleViewModel(ArticleType.BlogPost);
            var displayDate = model.Published ?? model.Updated;

            Assert.AreEqual(model.Published.Value, displayDate, "Should use Published date for blog posts");
        }

        #endregion

        #region Layout Tests

        [TestMethod]
        [TestCategory("ViewModels")]
        public void LayoutViewModel_HasAllRequiredSections()
        {
            var layout = new LayoutViewModel
            {
                Id = Guid.NewGuid(),
                LayoutName = "Test Layout",
                IsDefault = true,
                Head = "<meta charset=\"utf-8\">",
                HtmlHeader = "<header>Header</header>",
                FooterHtmlContent = "<footer>Footer</footer>"
            };

            Assert.IsNotNull(layout.LayoutName);
            Assert.IsNotNull(layout.Head);
            Assert.IsNotNull(layout.HtmlHeader);
            Assert.IsNotNull(layout.FooterHtmlContent);
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void ArticleViewModel_AllTypes_HaveValidLayout()
        {
            var generalArticle = CreateMockArticleViewModel(ArticleType.General);
            var blogStream = CreateMockArticleViewModel(ArticleType.BlogStream);
            var blogPost = CreateMockArticleViewModel(ArticleType.BlogPost);

            Assert.IsNotNull(generalArticle.Layout);
            Assert.IsNotNull(blogStream.Layout);
            Assert.IsNotNull(blogPost.Layout);

            Assert.IsNotNull(generalArticle.Layout.LayoutName);
            Assert.IsNotNull(blogStream.Layout.LayoutName);
            Assert.IsNotNull(blogPost.Layout.LayoutName);
        }

        #endregion

        #region SEO and Metadata Tests

        [TestMethod]
        [TestCategory("ViewModels")]
        public void ArticleViewModel_HasValidLanguageCode()
        {
            var model = CreateMockArticleViewModel(ArticleType.General);

            Assert.IsNotNull(model.LanguageCode);
            Assert.AreEqual(2, model.LanguageCode.Length, "Language code should be ISO 639-1 two-letter code");
            Assert.AreEqual("en", model.LanguageCode);
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void ArticleViewModel_BlogPost_HasValidSEOMetadata()
        {
            var model = CreateMockArticleViewModel(ArticleType.BlogPost);

            Assert.IsNotNull(model.Title);
            Assert.IsNotNull(model.Introduction, "Introduction used for meta description");
            Assert.IsNotNull(model.OGImage, "OG Image for social sharing");
            Assert.IsNotNull(model.OGUrl, "OG URL for canonical reference");
            Assert.IsNotNull(model.BannerImage, "Banner image for visual presentation");
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        [TestCategory("ViewModels")]
        public void BlogListItem_WithNullDates_HandlesGracefully()
        {
            var item = new BlogListItem
            {
                Id = Guid.NewGuid(),
                Title = "Test Post",
                UrlPath = "/test",
                Published = null,
                Updated = null
            };

            var displayDate = item.Published ?? item.Updated;
            Assert.IsNull(displayDate, "Should handle null dates");
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void BlogIndexViewModel_SinglePage_HasCorrectPaginationState()
        {
            var blogIndex = CreateMockBlogIndexViewModel(5, currentPage: 1, totalPages: 1);

            Assert.AreEqual(1, blogIndex.Page);
            Assert.AreEqual(1, blogIndex.TotalPages);
            Assert.IsTrue(blogIndex.Page == blogIndex.TotalPages, "Single page scenario");
        }

        [TestMethod]
        [TestCategory("ViewModels")]
        public void ArticleViewModel_WithEmptyOptionalFields_IsValid()
        {
            var model = new ArticleViewModel
            {
                Id = Guid.NewGuid(),
                Title = "Minimal Article",
                UrlPath = "/minimal",
                Content = "<p>Content</p>",
                Layout = new LayoutViewModel(),
                ArticleType = ArticleType.General,
                Updated = DateTimeOffset.UtcNow
            };

            Assert.IsNotNull(model);
            Assert.AreEqual(string.Empty, model.BannerImage);
            Assert.AreEqual(string.Empty, model.Introduction);
            Assert.AreEqual(string.Empty, model.Category);
        }

        #endregion
    }
}
