// <copyright file="ArticleHtmlServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Html
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Services.Html;
    using System;

    /// <summary>
    /// Unit tests for <see cref="ArticleHtmlService"/>.
    /// Tests HTML manipulation functionality including editable marker injection,
    /// Angular base tag handling, and introduction text extraction.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class ArticleHtmlServiceTests : SkyCmsTestBase
    {
        private IArticleHtmlService articleHtmlService = null!;

        /// <summary>
        /// Initialize test context before each test.
        /// </summary>
        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: false);
            articleHtmlService = new ArticleHtmlService();
        }

        #region EnsureEditableMarkers Tests

        /// <summary>
        /// Tests that HTML with editable element gets CCMS IDs added.
        /// </summary>
        [TestMethod]
        public void EnsureEditableMarkers_HtmlWithEditableElement_AddsCcmsIds()
        {
            // Arrange
            var html = "<div contenteditable='true'>Test Content</div>";

            // Act
            var result = articleHtmlService.EnsureEditableMarkers(html);

            // Assert
            Assert.Contains("data-ccms-ceid=", result);
            Assert.Contains("data-ccms-index=", result);
            Assert.Contains("Test Content", result);
        }

        /// <summary>
        /// Tests that multiple editable elements get indexed correctly.
        /// </summary>
        [TestMethod]
        public void EnsureEditableMarkers_MultipleEditableElements_AssignsSequentialIndices()
        {
            // Arrange
            var html = "<div contenteditable='true'>First</div><div contenteditable='true'>Second</div>";

            // Act
            var result = articleHtmlService.EnsureEditableMarkers(html);

            // Assert
            Assert.Contains("data-ccms-index=\"0\"", result);
            Assert.Contains("data-ccms-index=\"1\"", result);
        }

        /// <summary>
        /// Tests that existing CCMS IDs are preserved.
        /// </summary>
        [TestMethod]
        public void EnsureEditableMarkers_ExistingCcmsId_PreservesId()
        {
            // Arrange
            var existingId = Guid.NewGuid().ToString("N");
            var html = $"<div contenteditable='true' data-ccms-ceid='{existingId}'>Content</div>";

            // Act
            var result = articleHtmlService.EnsureEditableMarkers(html);

            // Assert
            Assert.Contains(existingId, result);
        }

        #endregion

        #region EnsureAngularBase Tests

        /// <summary>
        /// Tests that null header fragment returns empty string.
        /// </summary>
        [TestMethod]
        public void EnsureAngularBase_NullHeader_ReturnsEmpty()
        {
            // Act
            var result = articleHtmlService.EnsureAngularBase(null, "/test");

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        /// <summary>
        /// Tests that non-Angular header is returned unchanged.
        /// </summary>
        [TestMethod]
        public void EnsureAngularBase_NonAngularHeader_ReturnsUnchanged()
        {
            // Arrange
            var header = "<meta name='viewport' content='width=device-width'>";

            // Act
            var result = articleHtmlService.EnsureAngularBase(header, "/test");

            // Assert
            Assert.AreEqual(header, result);
        }

        /// <summary>
        /// Tests that Angular header without base tag gets one added.
        /// </summary>
        [TestMethod]
        public void EnsureAngularBase_AngularHeaderWithoutBase_AddsBaseTag()
        {
            // Arrange
            var header = "<meta name='ccms:framework' value='angular'>";

            // Act
            var result = articleHtmlService.EnsureAngularBase(header, "/blog");

            // Assert
            Assert.Contains("<base", result);
            Assert.Contains("href=\"/blog/\"", result);
        }

        /// <summary>
        /// Tests that root path normalizes correctly.
        /// </summary>
        [TestMethod]
        public void EnsureAngularBase_RootPath_NormalizesToSlash()
        {
            // Arrange
            var header = "<meta name='ccms:framework' value='angular'>";

            // Act
            var result = articleHtmlService.EnsureAngularBase(header, string.Empty);

            // Assert
            Assert.Contains("href=\"/\"", result);
        }

        #endregion

        #region ExtractIntroduction Tests

        /// <summary>
        /// Tests that null HTML returns empty string.
        /// </summary>
        [TestMethod]
        public void ExtractIntroduction_NullHtml_ReturnsEmpty()
        {
            // Act
            var result = articleHtmlService.ExtractIntroduction(null);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        /// <summary>
        /// Tests that empty HTML returns empty string.
        /// </summary>
        [TestMethod]
        public void ExtractIntroduction_EmptyHtml_ReturnsEmpty()
        {
            // Act
            var result = articleHtmlService.ExtractIntroduction(string.Empty);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        /// <summary>
        /// Tests that first paragraph text is extracted.
        /// </summary>
        [TestMethod]
        public void ExtractIntroduction_ValidHtml_ExtractsFirstParagraph()
        {
            // Arrange
            var html = "<p>This is the introduction text.</p><p>This is second paragraph.</p>";

            // Act
            var result = articleHtmlService.ExtractIntroduction(html);

            // Assert
            Assert.AreEqual("This is the introduction text.", result);
        }

        /// <summary>
        /// Tests that text longer than 512 characters is truncated.
        /// </summary>
        [TestMethod]
        public void ExtractIntroduction_LongText_TruncatesTo512Characters()
        {
            // Arrange
            var longText = new string('A', 600);
            var html = $"<p>{longText}</p>";

            // Act
            var result = articleHtmlService.ExtractIntroduction(html);

            // Assert
            Assert.AreEqual(512, result.Length);
        }

        /// <summary>
        /// Tests that HTML tags are stripped from introduction.
        /// </summary>
        [TestMethod]
        public void ExtractIntroduction_HtmlWithTags_StripsTagsFromIntroduction()
        {
            // Arrange
            var html = "<p>This is <strong>bold</strong> and <em>italic</em> text.</p>";

            // Act
            var result = articleHtmlService.ExtractIntroduction(html);

            // Assert
            Assert.AreEqual("This is bold and italic text.", result);
        }

        /// <summary>
        /// Tests that empty paragraphs are skipped.
        /// </summary>
        [TestMethod]
        public void ExtractIntroduction_EmptyParagraphs_SkipsToFirstNonEmpty()
        {
            // Arrange
            var html = "<p></p><p>   </p><p>First real content</p>";

            // Act
            var result = articleHtmlService.ExtractIntroduction(html);

            // Assert
            Assert.AreEqual("First real content", result);
        }

        /// <summary>
        /// Tests that HTML without paragraphs returns empty string.
        /// </summary>
        [TestMethod]
        public void ExtractIntroduction_NoParagraphs_ReturnsEmpty()
        {
            // Arrange
            var html = "<div>No paragraph here</div>";

            // Act
            var result = articleHtmlService.ExtractIntroduction(html);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        #endregion

        #region ExtractIntroduction Tests

        [TestMethod]
        public void ExtractIntroduction_HtmlEntities_DecodesEntities()  // ? New test
        {
            // Arrange
            var html = "<p>This &amp; that &lt;tag&gt;</p>";

            // Act
            var result = articleHtmlService.ExtractIntroduction(html);

            // Assert
            Assert.AreEqual("This & that <tag>", result);
        }

        #endregion

        /// <summary>
        /// Clean up after each test.
        /// </summary>
        [TestCleanup]
        public async System.Threading.Tasks.Task Cleanup()
        {
            await DisposeAsync();
        }
    }
}
