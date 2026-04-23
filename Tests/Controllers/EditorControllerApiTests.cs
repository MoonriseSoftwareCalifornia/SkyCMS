// <copyright file="EditorControllerApiTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using Sky.Editor.Models;
    using Sky.Editor.Models.GrapesJs;
    using System;
    using System.Collections;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for EditorController API endpoints.
    /// Covers JSON/API methods that return data to the frontend.
    /// </summary>
    [TestClass]
    public class EditorControllerApiTests : SkyCmsTestBase
    {
        private EditorController controller = null!;

        [TestInitialize]
        public new void Setup()
        {
            base.Setup();

            controller = new EditorController(
                Logger,
                Db,
                UserManager,
                RoleManager,
                Logic,
                EditorSettings,
                ViewRenderService,
                Storage,
                Hub.Object,
                PublishingService,
                ArticleHtmlService,
                ReservedPaths,
                TitleChangeService,
                TemplateService,
                Mediator,
                LayoutCacheService,
                DynamicConfigurationProvider);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "test@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        #region GetDesignerData Tests

        /// <summary>
        /// Tests that GetDesignerData returns article content for GrapeJS.
        /// </summary>
        [TestMethod]
        public async Task GetDesignerData_ReturnsArticleContent()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            article.Content = "<div>Test content for designer</div>";
            await SaveArticleAsync(article, TestUserId);

            // Act
            var result = await controller.GetDesignerData(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            Assert.IsInstanceOfType(jsonResult.Value, typeof(Project));
        }

        /// <summary>
        /// Tests that GetDesignerData returns NotFound for non-existent article.
        /// </summary>
        [TestMethod]
        public async Task GetDesignerData_ReturnsNotFound_WhenArticleDoesNotExist()
        {
            // Act
            var result = await controller.GetDesignerData(99999);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that GetDesignerData ensures editable markers in content.
        /// </summary>
        [TestMethod]
        public async Task GetDesignerData_EnsuresEditableMarkers()
        {
            // Arrange
            var article = await CreateArticleAsync("Test Article", TestUserId);
            article.Content = "<div contenteditable='true'>Editable content</div>";
            await SaveArticleAsync(article, TestUserId);

            // Act
            var result = await controller.GetDesignerData(article.ArticleNumber);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var projectData = (Project)jsonResult.Value!;

            // The ArticleHtmlService should have processed the content
            Assert.IsNotNull(projectData);
        }

        #endregion

        #region GetTemplateInfo Tests

        /// <summary>
        /// Tests that GetTemplateInfo returns template data.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateInfo_ReturnsTemplateData()
        {
            // Arrange
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = "Test Template",
                Content = "<div>Template content</div>",
                Description = "Test description"
            };
            Db.Templates.Add(template);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetTemplateInfo(template.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);

            var returnedTemplate = jsonResult.Value as Template;
            Assert.IsNotNull(returnedTemplate);
            Assert.AreEqual(template.Id, returnedTemplate.Id);
            Assert.AreEqual("Test Template", returnedTemplate.Title);
        }

        /// <summary>
        /// Tests that GetTemplateInfo returns empty string for null ID.
        /// </summary>
        [TestMethod]
        public async Task GetTemplateInfo_ReturnsEmptyString_WhenIdIsNull()
        {
            // Act
            var result = await controller.GetTemplateInfo(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.AreEqual(string.Empty, jsonResult.Value);
        }

        #endregion

        #region GetArticleList Tests

        /// <summary>
        /// Tests that GetArticleList returns published articles.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_ReturnsPublishedArticles()
        {
            // Arrange
            var article1 = await CreateArticleAsync("Published Article", TestUserId);
            await SaveArticleAsync(article1, TestUserId);
            var article1Entity = await Db.Articles.FirstAsync(a => a.Id == article1.Id); await PublishingService.PublishAsync(article1Entity);

            var article2 = await CreateArticleAsync("Unpublished Article", TestUserId);
            await SaveArticleAsync(article2, TestUserId);

            // Act
            var result = await controller.GetArticleList(publishedOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that GetArticleList filters by search term.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_FiltersBySearchTerm()
        {
            // Arrange
            var article1 = await CreateArticleAsync("Test Article About Dogs", TestUserId);
            await SaveArticleAsync(article1, TestUserId);
            var article1Entity = await Db.Articles.FirstAsync(a => a.Id == article1.Id); await PublishingService.PublishAsync(article1Entity);

            var article2 = await CreateArticleAsync("Test Article About Cats", TestUserId);
            await SaveArticleAsync(article2, TestUserId);
            var article2Entity = await Db.Articles.FirstAsync(a => a.Id == article2.Id); await PublishingService.PublishAsync(article2Entity);

            // Act
            var result = await controller.GetArticleList(term: "dogs");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            // The result should be filtered to only articles containing "dogs"
        }

        /// <summary>
        /// Tests that GetArticleList returns all articles when publishedOnly is false.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_ReturnsAllArticles_WhenPublishedOnlyIsFalse()
        {
            // Arrange
            var publishedArticle = await CreateArticleAsync("Published", TestUserId);
            await SaveArticleAsync(publishedArticle, TestUserId);
            var publishedArticleEntity = await Db.Articles.FirstAsync(a => a.Id == publishedArticle.Id); await PublishingService.PublishAsync(publishedArticleEntity);

            var unpublishedArticle = await CreateArticleAsync("Unpublished", TestUserId);
            await SaveArticleAsync(unpublishedArticle, TestUserId);

            // Act
            var result = await controller.GetArticleList(publishedOnly: false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that GetArticleList sets HtmlEditorEnabled to true when content has editable markers.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_SetsHtmlEditorEnabledTrue_WhenContentHasEditableMarkers()
        {
            // Arrange
            var article = await CreateArticleAsync("Editable Marker Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            entity.Content = "<div data-ccms-ceid='region-1'>Editable content</div>";
            entity.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetArticleList(term: "Editable Marker Article", publishedOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var items = ((IEnumerable)jsonResult.Value!).Cast<object>().ToList();
            var target = items.Single(i => string.Equals(GetPropertyValue<string>(i, "Title"), "Editable Marker Article", StringComparison.Ordinal));

            Assert.IsTrue(GetPropertyValue<bool>(target, "HtmlEditorEnabled"));
        }

        /// <summary>
        /// Tests that GetArticleList sets HtmlEditorEnabled to false when content has no editable markers.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_SetsHtmlEditorEnabledFalse_WhenContentHasNoEditableMarkers()
        {
            // Arrange
            var article = await CreateArticleAsync("Non Editable Marker Article", TestUserId);
            var entity = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            entity.Content = "<div>Static content</div>";
            entity.Published = DateTimeOffset.UtcNow;
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetArticleList(term: "Non Editable Marker Article", publishedOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var items = ((IEnumerable)jsonResult.Value!).Cast<object>().ToList();
            var target = items.Single(i => string.Equals(GetPropertyValue<string>(i, "Title"), "Non Editable Marker Article", StringComparison.Ordinal));

            Assert.IsFalse(GetPropertyValue<bool>(target, "HtmlEditorEnabled"));
        }

        /// <summary>
        /// Tests that GetArticleList nests blog posts under their owning blog stream.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_NestsBlogPostsUnderBlogStream()
        {
            // Arrange
            var blogStream = await CreateArticleAsync("News Blog", TestUserId);
            var blogStreamEntity = await Db.Articles.FirstAsync(a => a.Id == blogStream.Id);
            blogStreamEntity.ArticleType = (int)ArticleType.BlogStream;
            blogStreamEntity.BlogKey = "news";
            blogStreamEntity.UrlPath = "news";
            blogStreamEntity.Published = DateTimeOffset.UtcNow;

            var blogPost = await CreateArticleAsync("Welcome Post", TestUserId);
            var blogPostEntity = await Db.Articles.FirstAsync(a => a.Id == blogPost.Id);
            blogPostEntity.ArticleType = (int)ArticleType.BlogPost;
            blogPostEntity.BlogKey = "news";
            blogPostEntity.UrlPath = "welcome-post";
            blogPostEntity.Published = DateTimeOffset.UtcNow;

            var page = await CreateArticleAsync("About Us", TestUserId);
            var pageEntity = await Db.Articles.FirstAsync(a => a.Id == page.Id);
            pageEntity.ArticleType = (int)ArticleType.General;
            pageEntity.UrlPath = "about-us";
            pageEntity.Published = DateTimeOffset.UtcNow;

            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetArticleList(publishedOnly: false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var items = ((IEnumerable<EditorInventoryItem>)jsonResult.Value!).ToList();

            Assert.AreEqual(2, items.Count, "Only the page and blog stream should be top-level rows.");

            var blogRow = items.Single(i => i.ArticleNumber == blogStream.ArticleNumber);
            Assert.AreEqual(EditorInventoryRowType.Blog, blogRow.RowType);
            Assert.AreEqual(1, blogRow.ChildCount);
            Assert.AreEqual(1, blogRow.Children.Count);
            Assert.AreEqual(blogPost.ArticleNumber, blogRow.Children[0].ArticleNumber);
            Assert.AreEqual(EditorInventoryRowType.BlogPost, blogRow.Children[0].RowType);
            Assert.AreEqual("news/welcome-post", blogRow.Children[0].PreviewUrlPath);

            var pageRow = items.Single(i => i.ArticleNumber == page.ArticleNumber);
            Assert.AreEqual(EditorInventoryRowType.Page, pageRow.RowType);
            Assert.AreEqual(0, pageRow.Children.Count);
        }

        /// <summary>
        /// Tests that search by blog post title keeps the blog stream parent row.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_SearchByBlogPostTitle_PreservesBlogParent()
        {
            // Arrange
            var blogStream = await CreateArticleAsync("Company Updates", TestUserId);
            var blogStreamEntity = await Db.Articles.FirstAsync(a => a.Id == blogStream.Id);
            blogStreamEntity.ArticleType = (int)ArticleType.BlogStream;
            blogStreamEntity.BlogKey = "updates";
            blogStreamEntity.UrlPath = "updates";
            blogStreamEntity.Published = DateTimeOffset.UtcNow;

            var matchingPost = await CreateArticleAsync("Quarterly Recap", TestUserId);
            var matchingPostEntity = await Db.Articles.FirstAsync(a => a.Id == matchingPost.Id);
            matchingPostEntity.ArticleType = (int)ArticleType.BlogPost;
            matchingPostEntity.BlogKey = "updates";
            matchingPostEntity.UrlPath = "quarterly-recap";
            matchingPostEntity.Published = DateTimeOffset.UtcNow;

            var nonMatchingPost = await CreateArticleAsync("Engineering Notes", TestUserId);
            var nonMatchingPostEntity = await Db.Articles.FirstAsync(a => a.Id == nonMatchingPost.Id);
            nonMatchingPostEntity.ArticleType = (int)ArticleType.BlogPost;
            nonMatchingPostEntity.BlogKey = "updates";
            nonMatchingPostEntity.UrlPath = "engineering-notes";
            nonMatchingPostEntity.Published = DateTimeOffset.UtcNow;

            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetArticleList(term: "recap", publishedOnly: false);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var items = ((IEnumerable<EditorInventoryItem>)jsonResult.Value!).ToList();

            Assert.AreEqual(1, items.Count, "Only the parent blog stream should remain in filtered results.");
            var blogRow = items.Single();
            Assert.AreEqual(EditorInventoryRowType.Blog, blogRow.RowType);
            Assert.AreEqual("Company Updates", blogRow.Title);
            Assert.AreEqual(1, blogRow.Children.Count, "Only matching child posts should remain when parent title does not match.");
            Assert.AreEqual("Quarterly Recap", blogRow.Children[0].Title);
        }

        /// <summary>
        /// Tests that article type filtering for blog posts returns top-level post rows.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_FilterByBlogPostType_ReturnsBlogPostRows()
        {
            // Arrange
            var blogStream = await CreateArticleAsync("Tech Blog", TestUserId);
            var blogStreamEntity = await Db.Articles.FirstAsync(a => a.Id == blogStream.Id);
            blogStreamEntity.ArticleType = (int)ArticleType.BlogStream;
            blogStreamEntity.BlogKey = "tech";
            blogStreamEntity.UrlPath = "tech";
            blogStreamEntity.Published = DateTimeOffset.UtcNow;

            var blogPost = await CreateArticleAsync("Platform Update", TestUserId);
            var blogPostEntity = await Db.Articles.FirstAsync(a => a.Id == blogPost.Id);
            blogPostEntity.ArticleType = (int)ArticleType.BlogPost;
            blogPostEntity.BlogKey = "tech";
            blogPostEntity.UrlPath = "platform-update";
            blogPostEntity.Published = DateTimeOffset.UtcNow;

            var page = await CreateArticleAsync("Contact", TestUserId);
            var pageEntity = await Db.Articles.FirstAsync(a => a.Id == page.Id);
            pageEntity.ArticleType = (int)ArticleType.General;
            pageEntity.UrlPath = "contact";
            pageEntity.Published = DateTimeOffset.UtcNow;

            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetArticleList(publishedOnly: false, articleType: (int)ArticleType.BlogPost);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var items = ((IEnumerable<EditorInventoryItem>)jsonResult.Value!).ToList();

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(blogPost.ArticleNumber, items[0].ArticleNumber);
            Assert.AreEqual(EditorInventoryRowType.BlogPost, items[0].RowType);
            Assert.AreEqual(0, items[0].Children.Count);
        }

        /// <summary>
        /// Tests that publishedOnly returns only published blog rows and published child posts.
        /// </summary>
        [TestMethod]
        public async Task GetArticleList_PublishedOnly_FiltersUnpublishedBlogContent()
        {
            // Arrange
            var publishedBlog = await CreateArticleAsync("Published Blog", TestUserId);
            var publishedBlogEntity = await Db.Articles.FirstAsync(a => a.Id == publishedBlog.Id);
            publishedBlogEntity.ArticleType = (int)ArticleType.BlogStream;
            publishedBlogEntity.BlogKey = "published-blog";
            publishedBlogEntity.UrlPath = "published-blog";
            publishedBlogEntity.Published = DateTimeOffset.UtcNow;

            var publishedPost = await CreateArticleAsync("Published Post", TestUserId);
            var publishedPostEntity = await Db.Articles.FirstAsync(a => a.Id == publishedPost.Id);
            publishedPostEntity.ArticleType = (int)ArticleType.BlogPost;
            publishedPostEntity.BlogKey = "published-blog";
            publishedPostEntity.UrlPath = "published-post";
            publishedPostEntity.Published = DateTimeOffset.UtcNow;

            var unpublishedPost = await CreateArticleAsync("Draft Post", TestUserId);
            var unpublishedPostEntity = await Db.Articles.FirstAsync(a => a.Id == unpublishedPost.Id);
            unpublishedPostEntity.ArticleType = (int)ArticleType.BlogPost;
            unpublishedPostEntity.BlogKey = "published-blog";
            unpublishedPostEntity.UrlPath = "draft-post";
            unpublishedPostEntity.Published = null;

            var unpublishedBlog = await CreateArticleAsync("Unpublished Blog", TestUserId);
            var unpublishedBlogEntity = await Db.Articles.FirstAsync(a => a.Id == unpublishedBlog.Id);
            unpublishedBlogEntity.ArticleType = (int)ArticleType.BlogStream;
            unpublishedBlogEntity.BlogKey = "unpublished-blog";
            unpublishedBlogEntity.UrlPath = "unpublished-blog";
            unpublishedBlogEntity.Published = null;

            var unpublishedBlogPost = await CreateArticleAsync("Unpublished Blog Post", TestUserId);
            var unpublishedBlogPostEntity = await Db.Articles.FirstAsync(a => a.Id == unpublishedBlogPost.Id);
            unpublishedBlogPostEntity.ArticleType = (int)ArticleType.BlogPost;
            unpublishedBlogPostEntity.BlogKey = "unpublished-blog";
            unpublishedBlogPostEntity.UrlPath = "unpublished-blog-post";
            unpublishedBlogPostEntity.Published = DateTimeOffset.UtcNow;

            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetArticleList(publishedOnly: true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var items = ((IEnumerable<EditorInventoryItem>)jsonResult.Value!).ToList();

            var publishedBlogRow = items.Single(i => i.ArticleNumber == publishedBlog.ArticleNumber);
            Assert.AreEqual(EditorInventoryRowType.Blog, publishedBlogRow.RowType);
            Assert.AreEqual(1, publishedBlogRow.Children.Count, "Only published posts under published blog should be returned.");
            Assert.AreEqual("Published Post", publishedBlogRow.Children[0].Title);

            Assert.IsFalse(items.Any(i => i.ArticleNumber == unpublishedBlog.ArticleNumber), "Unpublished blog should not be present in publishedOnly results.");
            Assert.IsFalse(publishedBlogRow.Children.Any(c => c.ArticleNumber == unpublishedPost.ArticleNumber), "Unpublished child post should be filtered out.");
        }

        #endregion

        #region GetEncryptionKey Tests

        /// <summary>
        /// Tests that GetEncryptionKey returns existing encryption key.
        /// </summary>
        [TestMethod]
        public async Task GetEncryptionKey_ReturnsExistingKey()
        {
            // Arrange - Create encryption key setting
            var setting = new Cosmos.Common.Data.Setting
            {
                Description = "EncryptionKey",
                Value = "TestEncryptionKey123"
            };
            Db.Settings.Add(setting);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetEncryptionKey();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.AreEqual("TestEncryptionKey123", jsonResult.Value);
        }

        /// <summary>
        /// Tests that GetEncryptionKey creates new key if none exists.
        /// </summary>
        [TestMethod]
        public async Task GetEncryptionKey_CreatesNewKey_WhenNoneExists()
        {
            // Act
            var result = await controller.GetEncryptionKey();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);

            var keyValue = jsonResult.Value as string;
            Assert.IsNotNull(keyValue);
            Assert.IsTrue(keyValue.Length > 0);

            // Verify it was saved to database
            var setting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Description == "EncryptionKey");
            Assert.IsNotNull(setting);
            Assert.AreEqual(keyValue, setting.Value);
        }

        #endregion

        private static T GetPropertyValue<T>(object item, string propertyName)
        {
            var property = item.GetType().GetProperty(propertyName);
            Assert.IsNotNull(property, $"Expected property '{propertyName}' was not found on JSON result item.");

            var value = property.GetValue(item);
            Assert.IsNotNull(value, $"Property '{propertyName}' value is null.");

            return (T)value;
        }
    }
}
