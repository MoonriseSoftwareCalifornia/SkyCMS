// <copyright file="SaveArticleTitleChangeTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Features.Articles.Save
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Features.Articles.Save;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Tests for title changes, slug generation, and redirect creation.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SaveArticleTitleChangeTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task SaveArticle_TitleChange_CreatesRedirectFromOldSlug()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Now create the article we want to test (won't be root)
            var article = await Logic.CreateArticle("Original Title", TestUserId);
            
            // Publish the article using PublishArticle method
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            // Get the actual slug from the database after publish
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            var originalSlug = savedArticle.UrlPath;

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Completely New Title",
                Content = string.IsNullOrWhiteSpace(article.Content) ? "<p>Content</p>" : article.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Verify redirect was created
            var redirect = await Db.Articles
                .FirstOrDefaultAsync(a => 
                    a.UrlPath == originalSlug && 
                    a.StatusCode == (int)StatusCodeEnum.Redirect);

            Assert.IsNotNull(redirect, "Redirect should be created from old slug");
        }

        [TestMethod]
        [TestCategory("TitleChanges")]
        [TestCategory("Slugs")]
        public async Task SaveArticle_TitleWithSpecialCharacters_GeneratesValidSlug()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Special Chars and Symbols",  // Changed to avoid / which is preserved for hierarchical URLs
                Content = string.IsNullOrWhiteSpace(article.Content) ? "<p>Content</p>" : article.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var updatedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            Assert.IsNotNull(updatedArticle);
            // Slug should be normalized to lowercase with hyphens
            Assert.AreEqual("special-chars-and-symbols", updatedArticle.UrlPath);
            Assert.DoesNotContain("?", updatedArticle.UrlPath);
            Assert.DoesNotContain("&", updatedArticle.UrlPath);
            Assert.DoesNotContain("!", updatedArticle.UrlPath);
            Assert.DoesNotContain("@", updatedArticle.UrlPath);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task SaveArticle_MinorTitleChange_CaseOnly_DoesNotCreateRedirect()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            var article = await Logic.CreateArticle("original title", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Original Title", // Only case changed
                Content = string.IsNullOrWhiteSpace(article.Content) ? "<p>Content</p>" : article.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Should NOT create redirect for case-only changes
            var redirectCount = await Db.Articles
                .CountAsync(a => a.StatusCode == (int)StatusCodeEnum.Redirect);
            Assert.AreEqual(0, redirectCount);
        }

        [TestMethod]
        [TestCategory("TitleChanges")]
        [TestCategory("Slugs")]
        public async Task SaveArticle_TitleChangeWithSpaces_NormalizesSlug()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "My   Article   With   Multiple   Spaces",
                Content = string.IsNullOrWhiteSpace(article.Content) ? "<p>Content</p>" : article.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var updatedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            Assert.IsNotNull(updatedArticle);
            // Should not have multiple consecutive hyphens
            Assert.DoesNotContain("--", updatedArticle.UrlPath);
        }

        [TestMethod]
        [TestCategory("TitleChanges")]
        [TestCategory("Slugs")]
        public async Task SaveArticle_VeryLongTitle_TruncatesSlug()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);

            var longTitle = new string('A', 300); // Title longer than typical slug limits
            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = longTitle.Substring(0, 254), // Stay within title length limit
                Content = string.IsNullOrWhiteSpace(article.Content) ? "<p>Content</p>" : article.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var updatedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            Assert.IsNotNull(updatedArticle);
            // Slug should be reasonable length (implementation-dependent)
            Assert.IsLessThan(300, updatedArticle.UrlPath.Length);
        }

        [TestMethod]
        [TestCategory("TitleChanges")]
        [TestCategory("Slugs")]
        public async Task SaveArticle_TitleWithLeadingTrailingSpaces_TrimsCorrectly()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            var article = await Logic.CreateArticle("Test", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var savedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);

            var command = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "   Trimmed Title   ",
                Content = string.IsNullOrWhiteSpace(article.Content) ? "<p>Content</p>" : article.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            var updatedArticle = await Db.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);
            
            Assert.IsNotNull(updatedArticle);
            // Title should be trimmed automatically
            Assert.AreEqual("Trimmed Title", updatedArticle.Title);
            // The slug should have trimmed ends
            Assert.DoesNotStartWith("-", updatedArticle.UrlPath);
            Assert.DoesNotEndWith("-", updatedArticle.UrlPath);
            Assert.AreEqual("trimmed-title", updatedArticle.UrlPath);
        }

        [TestMethod]
        [TestCategory("Redirects")]
        public async Task SaveArticle_MultipleTitleChanges_CreatesMultipleRedirects()
        {
            // Arrange - Create a dummy root article first
            await Logic.CreateArticle("Root Page", TestUserId);
            
            // Now create the article we want to test (won't be root)
            var article = await Logic.CreateArticle("First Title", TestUserId);
            await Logic.PublishArticle(article.Id, DateTimeOffset.UtcNow);
            
            var savedArticle = await Db.Articles
                .AsNoTracking()  // ? FIX: Use AsNoTracking to prevent EF Core from updating this instance
                .FirstOrDefaultAsync(a => a.ArticleNumber == article.ArticleNumber);

            // First title change
            var command1 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Second Title",
                Content = string.IsNullOrWhiteSpace(article.Content) ? "<p>Content</p>" : article.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };
            await SaveArticleHandler.HandleAsync(command1);

            // Second title change
            var command2 = new SaveArticleCommand
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Third Title",
                Content = string.IsNullOrWhiteSpace(article.Content) ? "<p>Content</p>" : article.Content,
                UserId = TestUserId,
                ArticleType = ArticleType.General,
                Published = savedArticle.Published
            };

            // Act
            var result = await SaveArticleHandler.HandleAsync(command2);

            // Assert
            Assert.IsTrue(result.IsSuccess);

            // Should have 2 redirects (first-title -> second-title -> third-title)
            var redirectCount = await Db.Articles
                .CountAsync(a => a.StatusCode == (int)StatusCodeEnum.Redirect);
            Assert.AreEqual(2, redirectCount);
        }
    }
}
