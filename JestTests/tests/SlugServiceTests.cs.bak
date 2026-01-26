// <copyright file="SlugServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Slugs
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Services.Slugs;

    /// <summary>
    /// Unit tests for the <see cref="SlugService"/> class.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class SlugServiceTests
    {
        private ISlugService _slugService;

        /// <summary>
        /// Initializes the test class before each test method runs.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            _slugService = new SlugService();
        }

        #region Basic Functionality Tests

        [TestMethod]
        [TestCategory("BasicFunctionality")]
        public void Normalize_WithNullInput_ReturnsEmptyString()
        {
            // Arrange
            string? input = null;

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        [TestCategory("BasicFunctionality")]
        public void Normalize_WithEmptyInput_ReturnsEmptyString()
        {
            // Arrange
            var input = string.Empty;

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        [TestCategory("BasicFunctionality")]
        public void Normalize_WithWhitespaceInput_ReturnsEmptyString()
        {
            // Arrange
            var input = "   ";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        [TestCategory("BasicFunctionality")]
        public void Normalize_WithSimpleText_ReturnsLowercaseSlug()
        {
            // Arrange
            var input = "Hello World";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world", result);
        }

        [TestMethod]
        [TestCategory("BasicFunctionality")]
        public void Normalize_WithUppercaseText_ReturnsLowercaseSlug()
        {
            // Arrange
            var input = "HELLO WORLD";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world", result);
        }

        [TestMethod]
        [TestCategory("BasicFunctionality")]
        public void Normalize_WithMixedCaseText_ReturnsLowercaseSlug()
        {
            // Arrange
            var input = "HeLLo WoRLd";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world", result);
        }

        #endregion

        #region Special Character Tests

        [TestMethod]
        [TestCategory("SpecialCharacters")]
        public void Normalize_WithHyphens_PreservesHyphens()
        {
            // Arrange
            var input = "hello-world-test";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world-test", result);
        }

        [TestMethod]
        [TestCategory("SpecialCharacters")]
        [DataRow("hello@world", "hello-world")]
        [DataRow("hello#world", "hello-world")]
        [DataRow("hello$world", "hello-world")]
        [DataRow("hello%world", "hello-world")]
        [DataRow("hello&world", "hello-world")]
        [DataRow("hello*world", "hello-world")]
        public void Normalize_WithSpecialCharacters_ReplacesWithHyphens(string input, string expected)
        {
            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [TestCategory("SpecialCharacters")]
        public void Normalize_WithMultipleSpaces_CollapsesToSingleHyphen()
        {
            // Arrange
            var input = "hello    world";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world", result);
        }

        [TestMethod]
        [TestCategory("SpecialCharacters")]
        public void Normalize_WithMultipleHyphens_CollapsesToSingleHyphen()
        {
            // Arrange
            var input = "hello---world";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world", result);
        }

        [TestMethod]
        [TestCategory("SpecialCharacters")]
        public void Normalize_WithLeadingAndTrailingSpaces_TrimsSpaces()
        {
            // Arrange
            var input = "  hello world  ";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world", result);
        }

        [TestMethod]
        [TestCategory("SpecialCharacters")]
        public void Normalize_WithLeadingAndTrailingHyphens_TrimsHyphens()
        {
            // Arrange
            var input = "--hello-world--";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world", result);
        }

        #endregion

        #region Diacritics and Accents Tests

        [TestMethod]
        [TestCategory("Diacritics")]
        [DataRow("café", "cafe")]
        [DataRow("naïve", "naive")]
        [DataRow("résumé", "resume")]
        [DataRow("ñoño", "nono")]
        [DataRow("über", "uber")]
        public void Normalize_WithDiacritics_RemovesDiacritics(string input, string expected)
        {
            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [TestCategory("Diacritics")]
        public void Normalize_WithComplexDiacritics_RemovesAllDiacritics()
        {
            // Arrange
            var input = "Crème Brûlée";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("creme-brulee", result);
        }

        #endregion

        #region Numbers and Alphanumeric Tests

        [TestMethod]
        [TestCategory("Alphanumeric")]
        public void Normalize_WithNumbers_PreservesNumbers()
        {
            // Arrange
            var input = "hello 123 world";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-123-world", result);
        }

        [TestMethod]
        [TestCategory("Alphanumeric")]
        public void Normalize_WithAlphanumeric_PreservesAlphanumeric()
        {
            // Arrange
            var input = "product-v1.5";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("product-v1-5", result);
        }

        #endregion

        #region Forward Slash Tests

        [TestMethod]
        [TestCategory("ForwardSlash")]
        public void Normalize_WithForwardSlash_PreservesForwardSlash()
        {
            // Arrange
            var input = "path/to/resource";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("path/to/resource", result);
        }

        [TestMethod]
        [TestCategory("ForwardSlash")]
        public void Normalize_WithLeadingSlash_TrimsLeadingSlash()
        {
            // Arrange
            var input = "/path/to/resource";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("path/to/resource", result);
        }

        [TestMethod]
        [TestCategory("ForwardSlash")]
        public void Normalize_WithTrailingSlash_TrimsTrailingSlash()
        {
            // Arrange
            var input = "path/to/resource/";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("path/to/resource", result);
        }

        #endregion

        #region Reserved Dot-Segment Tests

        [TestMethod]
        [TestCategory("DotSegments")]
        public void Normalize_WithSingleDot_ReplacesWithHyphen()
        {
            // Arrange
            var input = ".";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("-", result);
        }

        [TestMethod]
        [TestCategory("DotSegments")]
        public void Normalize_WithDoubleDot_ReplacesWithHyphens()
        {
            // Arrange
            var input = "..";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("--", result);
        }

        [TestMethod]
        [TestCategory("DotSegments")]
        public void Normalize_WithDotInMiddle_RemovesDot()
        {
            // Arrange
            var input = "hello.world";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world", result);
        }

        #endregion

        #region Blog Key Tests

        [TestMethod]
        [TestCategory("BlogKey")]
        public void Normalize_WithBlogKey_PrependsBlogKey()
        {
            // Arrange
            var input = "my-article";
            var blogKey = "technology";

            // Act
            var result = _slugService.Normalize(input, blogKey);

            // Assert
            Assert.AreEqual("technology/my-article", result);
        }

        [TestMethod]
        [TestCategory("BlogKey")]
        public void Normalize_WithEmptyBlogKey_DoesNotPrependSlash()
        {
            // Arrange
            var input = "my-article";
            var blogKey = string.Empty;

            // Act
            var result = _slugService.Normalize(input, blogKey);

            // Assert
            Assert.AreEqual("my-article", result);
        }

        [TestMethod]
        [TestCategory("BlogKey")]
        public void Normalize_WithWhitespaceBlogKey_DoesNotPrependSlash()
        {
            // Arrange
            var input = "my-article";
            var blogKey = "   ";

            // Act
            var result = _slugService.Normalize(input, blogKey);

            // Assert
            Assert.AreEqual("my-article", result);
        }

        [TestMethod]
        [TestCategory("BlogKey")]
        public void Normalize_WithBlogKeyAndComplexInput_NormalizesAndPrepends()
        {
            // Arrange
            var input = "My Article Title!";
            var blogKey = "tech-blog";

            // Act
            var result = _slugService.Normalize(input, blogKey);

            // Assert
            Assert.AreEqual("tech-blog/my-article-title", result);
        }

        #endregion

        #region Real-World Scenarios

        [TestMethod]
        [TestCategory("RealWorld")]
        [DataRow("The Quick Brown Fox", "the-quick-brown-fox")]
        [DataRow("How to Use ASP.NET Core", "how-to-use-asp-net-core")]
        [DataRow("C# Best Practices 2024", "c-best-practices-2024")]
        [DataRow("Understanding .NET 9 Features", "understanding-net-9-features")]
        [DataRow("What is REST API?", "what-is-rest-api")]
        public void Normalize_WithRealWorldTitles_GeneratesCorrectSlugs(string input, string expected)
        {
            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [TestCategory("RealWorld")]
        public void Normalize_WithLongTitle_HandlesCorrectly()
        {
            // Arrange
            var input = "This is a very long title with many words that should be converted to a slug";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("this-is-a-very-long-title-with-many-words-that-should-be-converted-to-a-slug", result);
        }

        [TestMethod]
        [TestCategory("RealWorld")]
        public void Normalize_WithUnicodeCharacters_HandlesCorrectly()
        {
            // Arrange
            var input = "Hello 世界";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            StringAssert.StartsWith(result, "hello");
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        [TestCategory("EdgeCases")]
        public void Normalize_WithOnlySpecialCharacters_ReturnsEmptyString()
        {
            // Arrange
            var input = "@#$%^&*()";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        [TestCategory("EdgeCases")]
        public void Normalize_WithMixedSlashesAndHyphens_NormalizesCorrectly()
        {
            // Arrange
            var input = "path/to/my-resource";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("path/to/my-resource", result);
        }

        [TestMethod]
        [TestCategory("EdgeCases")]
        public void Normalize_WithUnderscores_ReplacesWithHyphens()
        {
            // Arrange
            var input = "hello_world_test";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world-test", result);
        }

        [TestMethod]
        [TestCategory("EdgeCases")]
        public void Normalize_WithTildes_RemovesTildes()
        {
            // Arrange
            var input = "~hello~world~";

            // Act
            var result = _slugService.Normalize(input);

            // Assert
            Assert.AreEqual("hello-world", result);
        }

        #endregion
    }
}
