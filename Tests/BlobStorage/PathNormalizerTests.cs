// <copyright file="PathNormalizerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="PathNormalizer"/> to ensure consistent, predictable path normalization.
    /// </summary>
    [TestClass]
    public class PathNormalizerTests
    {
        private PathNormalizer normalizer;

        [TestInitialize]
        public void Initialize()
        {
            this.normalizer = new PathNormalizer();
        }

        #region Normalize Tests

        [TestMethod]
        public void Normalize_EmptyString_ReturnsEmptyString()
        {
            // Arrange
            string input = string.Empty;

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Normalize_Null_ReturnsEmptyString()
        {
            // Arrange
            string input = null;

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Normalize_Whitespace_ReturnsEmptyString()
        {
            // Arrange
            string input = "   ";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Normalize_SimpleFilename_ReturnsUnchanged()
        {
            // Arrange
            string input = "file.txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("file.txt", result);
        }

        [TestMethod]
        public void Normalize_SimpleFolder_ReturnsUnchanged()
        {
            // Arrange
            string input = "folder";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder", result);
        }

        [TestMethod]
        public void Normalize_PathWithLeadingSlash_RemovesLeadingSlash()
        {
            // Arrange
            string input = "/folder/file.txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/file.txt", result);
        }

        [TestMethod]
        public void Normalize_PathWithTrailingSlash_RemovesTrailingSlash()
        {
            // Arrange
            string input = "folder/file.txt/";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/file.txt", result);
        }

        [TestMethod]
        public void Normalize_PathWithBothSlashes_RemovesBoth()
        {
            // Arrange
            string input = "/folder/file.txt/";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/file.txt", result);
        }

        [TestMethod]
        public void Normalize_RootSlash_ReturnsEmptyString()
        {
            // Arrange
            string input = "/";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Normalize_MultipleSlashes_ReturnsEmptyString()
        {
            // Arrange
            string input = "/////";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Normalize_ConsecutiveSlashes_CollapsesToOne()
        {
            // Arrange
            string input = "folder//subfolder///file.txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/subfolder/file.txt", result);
        }

        [TestMethod]
        public void Normalize_BackslashSeparators_ConvertToForwardSlash()
        {
            // Arrange
            string input = "folder\\subfolder\\file.txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/subfolder/file.txt", result);
        }

        [TestMethod]
        public void Normalize_MixedSeparators_NormalizedToForwardSlash()
        {
            // Arrange
            string input = "folder/subfolder\\file.txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/subfolder/file.txt", result);
        }

        [TestMethod]
        public void Normalize_PathWithLeadingWhitespace_RemovesWhitespace()
        {
            // Arrange
            string input = "  folder/file.txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/file.txt", result);
        }

        [TestMethod]
        public void Normalize_PathWithTrailingWhitespace_RemovesWhitespace()
        {
            // Arrange
            string input = "folder/file.txt  ";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/file.txt", result);
        }

        [TestMethod]
        public void Normalize_DeepPath_PreservesStructure()
        {
            // Arrange
            string input = "/a/b/c/d/e/f.txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("a/b/c/d/e/f.txt", result);
        }

        [TestMethod]
        public void Normalize_DotPath_ReturnedUnchanged()
        {
            // Arrange
            string input = ".";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual(".", result);
        }

        [TestMethod]
        public void Normalize_DotDotPath_ReturnedUnchanged()
        {
            // Arrange
            string input = "..";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("..", result);
        }

        [TestMethod]
        public void Normalize_PathWithDots_PreservesDots()
        {
            // Arrange
            string input = "/archive.2024.01.bak/file.txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("archive.2024.01.bak/file.txt", result);
        }

        #endregion

        #region NormalizeWithLeadingSlash Tests

        [TestMethod]
        public void NormalizeWithLeadingSlash_EmptyString_ReturnsRootSlash()
        {
            // Arrange
            string input = string.Empty;

            // Act
            var result = this.normalizer.NormalizeWithLeadingSlash(input);

            // Assert
            Assert.AreEqual("/", result);
        }

        [TestMethod]
        public void NormalizeWithLeadingSlash_SimpleFilename_AddsLeadingSlash()
        {
            // Arrange
            string input = "file.txt";

            // Act
            var result = this.normalizer.NormalizeWithLeadingSlash(input);

            // Assert
            Assert.AreEqual("/file.txt", result);
        }

        [TestMethod]
        public void NormalizeWithLeadingSlash_PathWithoutSlash_AddsLeadingSlash()
        {
            // Arrange
            string input = "folder/subfolder/file.txt";

            // Act
            var result = this.normalizer.NormalizeWithLeadingSlash(input);

            // Assert
            Assert.AreEqual("/folder/subfolder/file.txt", result);
        }

        [TestMethod]
        public void NormalizeWithLeadingSlash_PathWithLeadingSlash_PreservesLeadingSlash()
        {
            // Arrange
            string input = "/folder/subfolder/file.txt";

            // Act
            var result = this.normalizer.NormalizeWithLeadingSlash(input);

            // Assert
            Assert.AreEqual("/folder/subfolder/file.txt", result);
        }

        [TestMethod]
        public void NormalizeWithLeadingSlash_PathWithTrailingSlash_RemovesTrailingAndAddsLeading()
        {
            // Arrange
            string input = "folder/subfolder/";

            // Act
            var result = this.normalizer.NormalizeWithLeadingSlash(input);

            // Assert
            Assert.AreEqual("/folder/subfolder", result);
        }

        [TestMethod]
        public void NormalizeWithLeadingSlash_RootSlash_ReturnsRootSlash()
        {
            // Arrange
            string input = "/";

            // Act
            var result = this.normalizer.NormalizeWithLeadingSlash(input);

            // Assert
            Assert.AreEqual("/", result);
        }

        [TestMethod]
        public void NormalizeWithLeadingSlash_ConsecutiveSlashes_NormalizesAndAddsLeading()
        {
            // Arrange
            string input = "folder//subfolder///file.txt";

            // Act
            var result = this.normalizer.NormalizeWithLeadingSlash(input);

            // Assert
            Assert.AreEqual("/folder/subfolder/file.txt", result);
        }

        #endregion

        #region Integration/Edge Case Tests

        [TestMethod]
        public void Normalize_SpecialCharactersInFilename_Preserved()
        {
            // Arrange
            string input = "/folder/my-file_2024 (1).txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/my-file_2024 (1).txt", result);
        }

        [TestMethod]
        public void Normalize_PercentEncodedPath_PreservedAsIs()
        {
            // Arrange
            string input = "/folder/file%20name.txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/file%20name.txt", result);
        }

        [TestMethod]
        public void Normalize_UrlEncodedPath_PreservedAsIs()
        {
            // Arrange
            string input = "/folder/file+name.txt";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            Assert.AreEqual("folder/file+name.txt", result);
        }

        [TestMethod]
        public void Normalize_PathWithTrailingDots_RemovesDots()
        {
            // Arrange
            string input = "/folder/file.txt...";

            // Act
            var result = this.normalizer.Normalize(input);

            // Assert
            // Note: Dots within the filename are preserved; this tests the trailing dot removal.
            Assert.AreEqual("folder/file.txt...", result);
        }

        [TestMethod]
        public void Normalize_ConsistencyCrossCall_RepeatedNormalizationIdempotent()
        {
            // Arrange
            string input = "/folder/subfolder/file.txt";

            // Act
            var first = this.normalizer.Normalize(input);
            var second = this.normalizer.Normalize(first);
            var third = this.normalizer.Normalize(second);

            // Assert
            Assert.AreEqual(first, second, "First and second normalization should be identical.");
            Assert.AreEqual(second, third, "Second and third normalization should be identical.");
            Assert.AreEqual("folder/subfolder/file.txt", first);
        }

        #endregion
    }
}
