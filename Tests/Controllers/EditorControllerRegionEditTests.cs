// <copyright file="EditorControllerRegionEditTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using Sky.Editor.Models;

    /// <summary>
    /// Tests for EditorController region edit functionality.
    /// Covers EditSaveRegion and EditSaveBody methods.
    /// </summary>
    [TestClass]
    public class EditorControllerRegionEditTests : SkyCmsTestBase
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
                Cache,
                DynamicConfigurationProvider);

            // Setup user context
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

        #region EditSaveRegion Tests

        /// <summary>
        /// Tests that EditSaveRegion updates specific region content.
        /// </summary>
        [TestMethod]
        public async Task EditSaveRegion_UpdatesSpecificRegion()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div data-ccms-ceid=\"region1\">Original Content</div><div data-ccms-ceid=\"region2\">Other Content</div>";
            await Logic.SaveArticle(article, TestUserId);

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                EditorId = "region1",
                Data = CryptoJsDecryption.Encrypt("<p>Updated Content</p>")
            };

            // Act
            var result = await controller.EditSaveRegion(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Verify the specific region was updated
            var updatedArticle = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderByDescending(a => a.VersionNumber)
                .FirstAsync();

            Assert.Contains("<p>Updated Content</p>", updatedArticle.Content);
            Assert.Contains("region2", updatedArticle.Content); // Other region unchanged
            Assert.Contains("Other Content", updatedArticle.Content);
        }

        /// <summary>
        /// Tests that EditSaveRegion does not update if content is unchanged.
        /// </summary>
        [TestMethod]
        public async Task EditSaveRegion_DoesNotUpdateIfContentUnchanged()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div data-ccms-ceid=\"region1\">Original Content</div>";
            await Logic.SaveArticle(article, TestUserId);

            var originalUpdated = (await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber)).Updated;

            // Wait a moment to ensure timestamp would change if saved
            await Task.Delay(100);

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                EditorId = "region1",
                Data = CryptoJsDecryption.Encrypt("Original Content") // Same content
            };

            // Act
            var result = await controller.EditSaveRegion(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Verify Updated timestamp was NOT changed (content was same)
            var updatedArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            // The Updated field should not have changed since content is the same
            Assert.AreEqual(originalUpdated, updatedArticle.Updated);
        }

        /// <summary>
        /// Tests that EditSaveRegion updates timestamp when content changes.
        /// </summary>
        [TestMethod]
        public async Task EditSaveRegion_UpdatesTimestampWhenContentChanges()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div data-ccms-ceid=\"region1\">Original Content</div>";
            await Logic.SaveArticle(article, TestUserId);

            var originalUpdated = (await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber)).Updated;

            // Wait to ensure timestamp will be different
            await Task.Delay(100);

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                EditorId = "region1",
                Data = CryptoJsDecryption.Encrypt("<p>New Content</p>")
            };

            // Act
            var result = await controller.EditSaveRegion(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            var updatedArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            Assert.IsTrue(updatedArticle.Updated > originalUpdated,
                "Updated timestamp should be newer when content changes");
        }

        /// <summary>
        /// Tests that EditSaveRegion handles encrypted data correctly.
        /// </summary>
        [TestMethod]
        public async Task EditSaveRegion_DecryptsDataCorrectly()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div data-ccms-ceid=\"test-region\">Original</div>";
            await Logic.SaveArticle(article, TestUserId);

            var newContent = "<strong>Encrypted Content</strong>";
            var encrypted = CryptoJsDecryption.Encrypt(newContent);

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                EditorId = "test-region",
                Data = encrypted
            };

            // Act
            var result = await controller.EditSaveRegion(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            var updatedArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            Assert.Contains(newContent, updatedArticle.Content);
        }

        /// <summary>
        /// Tests that EditSaveRegion handles non-existent region gracefully.
        /// </summary>
        [TestMethod]
        public async Task EditSaveRegion_HandlesNonExistentRegion()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div data-ccms-ceid=\"existing-region\">Content</div>";
            await Logic.SaveArticle(article, TestUserId);

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                EditorId = "non-existent-region",
                Data = CryptoJsDecryption.Encrypt("<p>New Content</p>")
            };

            // Act
            var result = await controller.EditSaveRegion(model);

            // Assert - Should not throw, returns OK
            Assert.IsInstanceOfType(result, typeof(OkResult));

            // Original content should remain unchanged
            var updatedArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            Assert.Contains("existing-region", updatedArticle.Content);
            Assert.Contains("Content", updatedArticle.Content);
        }

        #endregion

        #region EditSaveBody Tests

        /// <summary>
        /// Tests that EditSaveBody replaces entire body content.
        /// </summary>
        [TestMethod]
        public async Task EditSaveBody_ReplacesEntireBodyContent()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div>Original Body Content</div>";
            await Logic.SaveArticle(article, TestUserId);

            var newBodyContent = "<div><h1>Completely New Body</h1><p>New paragraph</p></div>";

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Data = CryptoJsDecryption.Encrypt(newBodyContent)
            };

            // Act
            var result = await controller.EditSaveBody(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            var updatedArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            Assert.AreEqual(newBodyContent, updatedArticle.Content);
            Assert.IsFalse(updatedArticle.Content.Contains("Original Body Content"));
        }

        /// <summary>
        /// Tests that EditSaveBody does not update if content is unchanged.
        /// </summary>
        [TestMethod]
        public async Task EditSaveBody_DoesNotUpdateIfContentUnchanged()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            var originalContent = "<div>Unchanged Content</div>";
            article.Content = originalContent;
            await Logic.SaveArticle(article, TestUserId);

            var originalUpdated = (await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber)).Updated;

            await Task.Delay(100);

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Data = CryptoJsDecryption.Encrypt(originalContent)
            };

            // Act
            var result = await controller.EditSaveBody(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            var updatedArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            Assert.AreEqual(originalUpdated, updatedArticle.Updated);
        }

        /// <summary>
        /// Tests that EditSaveBody updates timestamp when content changes.
        /// </summary>
        [TestMethod]
        public async Task EditSaveBody_UpdatesTimestampWhenContentChanges()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div>Original</div>";
            await Logic.SaveArticle(article, TestUserId);

            var originalUpdated = (await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber)).Updated;

            await Task.Delay(100);

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Data = CryptoJsDecryption.Encrypt("<div>Changed</div>")
            };

            // Act
            var result = await controller.EditSaveBody(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            var updatedArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            Assert.IsTrue(updatedArticle.Updated > originalUpdated);
        }

        /// <summary>
        /// Tests that EditSaveBody handles large content correctly.
        /// </summary>
        [TestMethod]
        public async Task EditSaveBody_HandlesLargeContent()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div>Small content</div>";
            await Logic.SaveArticle(article, TestUserId);

            // Create large content
            var largeContent = string.Concat(Enumerable.Repeat("<p>Lorem ipsum dolor sit amet, consectetur adipiscing elit.</p>", 100));

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Data = CryptoJsDecryption.Encrypt(largeContent)
            };

            // Act
            var result = await controller.EditSaveBody(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            var updatedArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            Assert.AreEqual(largeContent, updatedArticle.Content);
        }

        /// <summary>
        /// Tests that EditSaveBody handles HTML with special characters.
        /// </summary>
        [TestMethod]
        public async Task EditSaveBody_HandlesSpecialCharacters()
        {
            // Arrange
            var article = await Logic.CreateArticle("Test Article", TestUserId);
            article.Content = "<div>Original</div>";
            await Logic.SaveArticle(article, TestUserId);

            var contentWithSpecialChars = "<div>Content with &lt;special&gt; &amp; \"quoted\" 'chars'</div>";

            var model = new EditorRegionViewModel
            {
                ArticleNumber = article.ArticleNumber,
                Data = CryptoJsDecryption.Encrypt(contentWithSpecialChars)
            };

            // Act
            var result = await controller.EditSaveBody(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));

            var updatedArticle = await Db.Articles
                .FirstAsync(a => a.ArticleNumber == article.ArticleNumber);

            Assert.AreEqual(contentWithSpecialChars, updatedArticle.Content);
        }

        #endregion
    }
}
