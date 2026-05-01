// <copyright file="PathValidatorTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="PathValidator"/> to ensure robust path security validation.
    /// </summary>
    [TestClass]
    public class PathValidatorTests
    {
        private PathValidator validator;

        [TestInitialize]
        public void Initialize()
        {
            this.validator = new PathValidator();
        }

        #region ValidatePath - Basic Cases

        [TestMethod]
        public void ValidatePath_EmptyPath_ReturnsSuccess()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath(string.Empty);

            // Assert
            Assert.IsTrue(result.IsValid, "Empty path (root) should be valid.");
        }

        [TestMethod]
        public void ValidatePath_NullPath_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath(null);

            // Assert
            Assert.IsFalse(result.IsValid, "Null path should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("null"), "Error message should mention null.");
        }

        [TestMethod]
        public void ValidatePath_SimpleFile_ReturnsSuccess()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath("file.txt");

            // Assert
            Assert.IsTrue(result.IsValid, "Simple filename should be valid.");
        }

        [TestMethod]
        public void ValidatePath_SimplePath_ReturnsSuccess()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath("folder/subfolder/file.txt");

            // Assert
            Assert.IsTrue(result.IsValid, "Simple path should be valid.");
        }

        #endregion

        #region ValidatePath - Path Traversal Detection

        [TestMethod]
        public void ValidatePath_DoubleDotAtStart_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath("../etc/passwd");

            // Assert
            Assert.IsFalse(result.IsValid, "Path starting with .. should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("traversal"), "Error should mention traversal.");
        }

        [TestMethod]
        public void ValidatePath_DoubleDotInMiddle_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath("folder/../../../sensitive.txt");

            // Assert
            Assert.IsFalse(result.IsValid, "Path with .. in middle should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("traversal"), "Error should mention traversal.");
        }

        [TestMethod]
        public void ValidatePath_BackslashTraversal_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath("folder\\..\\..\\sensitive.txt");

            // Assert
            Assert.IsFalse(result.IsValid, "Backslash traversal should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("traversal"), "Error should mention traversal.");
        }

        [TestMethod]
        public void ValidatePath_DoubleDotAtEnd_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath("folder/..");

            // Assert
            Assert.IsFalse(result.IsValid, "Path ending with .. should be invalid.");
        }

        [TestMethod]
        public void ValidatePath_MultipleDoubleDots_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath("../../..");

            // Assert
            Assert.IsFalse(result.IsValid, "Multiple traversals should be invalid.");
        }

        #endregion

        #region ValidatePath - Reserved Names

        [TestMethod]
        [DataRow("CON")]
        [DataRow("PRN")]
        [DataRow("AUX")]
        [DataRow("NUL")]
        [DataRow("COM1")]
        [DataRow("LPT1")]
        public void ValidatePath_WindowsReservedName_ReturnsFailure(string reservedName)
        {
            // Arrange & Act
            var result = this.validator.ValidatePath(reservedName);

            // Assert
            Assert.IsFalse(result.IsValid, $"Reserved name '{reservedName}' should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("reserved"), "Error should mention reserved name.");
        }

        [TestMethod]
        [DataRow("folder/CON/file.txt")]
        [DataRow("PRN/subfolder/data.txt")]
        [DataRow("path/to/AUX/file.txt")]
        public void ValidatePath_ReservedNameInPath_ReturnsFailure(string path)
        {
            // Arrange & Act
            var result = this.validator.ValidatePath(path);

            // Assert
            Assert.IsFalse(result.IsValid, $"Path with reserved name '{path}' should be invalid.");
        }

        #endregion

        #region ValidatePath - Dot Segments

        [TestMethod]
        public void ValidatePath_SingleDot_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath(".");

            // Assert
            Assert.IsFalse(result.IsValid, "Single dot (.) should be invalid.");
        }

        [TestMethod]
        public void ValidatePath_DotInPath_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath("folder/./file.txt");

            // Assert
            Assert.IsFalse(result.IsValid, "Path with (.) should be invalid.");
        }

        #endregion

        #region ValidatePath - Control Characters and Null Bytes

        [TestMethod]
        public void ValidatePath_NullByte_ReturnsFailure()
        {
            // Arrange
            var pathWithNull = "folder/file\0.txt";

            // Act
            var result = this.validator.ValidatePath(pathWithNull);

            // Assert
            Assert.IsFalse(result.IsValid, "Path with null byte should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("null"), "Error should mention null byte.");
        }

        [TestMethod]
        public void ValidatePath_ControlCharacter_ReturnsFailure()
        {
            // Arrange
            var pathWithControl = "folder/file\x01.txt";

            // Act
            var result = this.validator.ValidatePath(pathWithControl);

            // Assert
            Assert.IsFalse(result.IsValid, "Path with control character should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("control"), "Error should mention control character.");
        }

        #endregion

        #region ValidatePath - Length Limits

        [TestMethod]
        public void ValidatePath_ExcessiveSegmentLength_ReturnsFailure()
        {
            // Arrange
            var longSegment = new string('a', 256);
            var pathWithLongSegment = $"folder/{longSegment}/file.txt";

            // Act
            var result = this.validator.ValidatePath(pathWithLongSegment);

            // Assert
            Assert.IsFalse(result.IsValid, "Path with segment > 255 chars should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("256"), "Error should mention character count.");
        }

        [TestMethod]
        public void ValidatePath_MaxSegmentLength_ReturnsSuccess()
        {
            // Arrange
            var maxSegment = new string('a', 255);
            var pathWithMaxSegment = $"folder/{maxSegment}/file.txt";

            // Act
            var result = this.validator.ValidatePath(pathWithMaxSegment);

            // Assert
            Assert.IsTrue(result.IsValid, "Path with 255-char segment should be valid.");
        }

        [TestMethod]
        public void ValidatePath_ExcessiveDepth_ReturnsFailure()
        {
            // Arrange
            var segments = string.Join("/", Enumerable.Range(0, 65).Select(_ => "a"));

            // Act
            var result = this.validator.ValidatePath(segments);

            // Assert
            Assert.IsFalse(result.IsValid, "Path with > 64 segments should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("depth"), "Error should mention depth limit.");
        }

        [TestMethod]
        public void ValidatePath_MaxDepth_ReturnsSuccess()
        {
            // Arrange
            var segments = string.Join("/", Enumerable.Range(0, 64).Select(_ => "a"));

            // Act
            var result = this.validator.ValidatePath(segments);

            // Assert
            Assert.IsTrue(result.IsValid, "Path with 64 segments should be valid.");
        }

        #endregion

        #region ValidateFilename - Basic Cases

        [TestMethod]
        public void ValidateFilename_SimpleFilename_ReturnsSuccess()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename("document.txt");

            // Assert
            Assert.IsTrue(result.IsValid, "Simple filename should be valid.");
        }

        [TestMethod]
        public void ValidateFilename_NullFilename_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename(null);

            // Assert
            Assert.IsFalse(result.IsValid, "Null filename should be invalid.");
        }

        [TestMethod]
        public void ValidateFilename_EmptyFilename_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename(string.Empty);

            // Assert
            Assert.IsFalse(result.IsValid, "Empty filename should be invalid.");
        }

        [TestMethod]
        public void ValidateFilename_WhitespaceOnly_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename("   ");

            // Assert
            Assert.IsFalse(result.IsValid, "Whitespace-only filename should be invalid.");
        }

        #endregion

        #region ValidateFilename - Reserved Names

        [TestMethod]
        [DataRow("CON")]
        [DataRow("PRN")]
        [DataRow("AUX")]
        [DataRow("NUL")]
        [DataRow("COM1")]
        [DataRow("LPT1")]
        public void ValidateFilename_WindowsReservedName_ReturnsFailure(string reservedName)
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename(reservedName);

            // Assert
            Assert.IsFalse(result.IsValid, $"Reserved filename '{reservedName}' should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("reserved"), "Error should mention reserved name.");
        }

        #endregion

        #region ValidateFilename - Invalid Characters

        [TestMethod]
        public void ValidateFilename_FilenameWithPathSeparator_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename("folder/file.txt");

            // Assert
            Assert.IsFalse(result.IsValid, "Filename with path separator should be invalid.");
            Assert.IsTrue(result.ErrorMessage.Contains("path"), "Error should mention path separators.");
        }

        [TestMethod]
        public void ValidateFilename_FilenameWithBackslash_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename("folder\\file.txt");

            // Assert
            Assert.IsFalse(result.IsValid, "Filename with backslash should be invalid.");
        }

        [TestMethod]
        public void ValidateFilename_FilenameWithNullByte_ReturnsFailure()
        {
            // Arrange
            var filenameWithNull = "file\0.txt";

            // Act
            var result = this.validator.ValidateFilename(filenameWithNull);

            // Assert
            Assert.IsFalse(result.IsValid, "Filename with null byte should be invalid.");
        }

        #endregion

        #region ValidateFilename - Dot Names

        [TestMethod]
        public void ValidateFilename_SingleDot_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename(".");

            // Assert
            Assert.IsFalse(result.IsValid, "Single dot filename should be invalid.");
        }

        [TestMethod]
        public void ValidateFilename_DoubleDot_ReturnsFailure()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename("..");

            // Assert
            Assert.IsFalse(result.IsValid, "Double dot filename should be invalid.");
        }

        #endregion

        #region ValidateFilename - Length

        [TestMethod]
        public void ValidateFilename_ExcessiveLength_ReturnsFailure()
        {
            // Arrange
            var longFilename = new string('a', 256) + ".txt";

            // Act
            var result = this.validator.ValidateFilename(longFilename);

            // Assert
            Assert.IsFalse(result.IsValid, "Filename > 255 chars should be invalid.");
        }

        [TestMethod]
        public void ValidateFilename_MaxLength_ReturnsSuccess()
        {
            // Arrange
            var maxFilename = new string('a', 251) + ".txt";  // Total 255 chars

            // Act
            var result = this.validator.ValidateFilename(maxFilename);

            // Assert
            Assert.IsTrue(result.IsValid, "Filename with 255 chars should be valid.");
        }

        #endregion

        #region Unicode and Special Characters

        [TestMethod]
        public void ValidatePath_UnicodeCharacters_ReturnsSuccess()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath("café/résumé/document.pdf");

            // Assert
            Assert.IsTrue(result.IsValid, "Path with Unicode characters should be valid.");
        }

        [TestMethod]
        public void ValidateFilename_UnicodeFilename_ReturnsSuccess()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename("café.pdf");

            // Assert
            Assert.IsTrue(result.IsValid, "Filename with Unicode characters should be valid.");
        }

        [TestMethod]
        public void ValidatePath_SpecialCharactersInFilename_ReturnsSuccess()
        {
            // Arrange & Act
            var result = this.validator.ValidatePath("folder/file-name_v2 (1).txt");

            // Assert
            Assert.IsTrue(result.IsValid, "Path with special characters should be valid.");
        }

        #endregion

        #region ValidationResult Helpers

        [TestMethod]
        public void ValidationResult_Success_HasCorrectProperties()
        {
            // Arrange & Act
            var result = PathValidationResult.Success();

            // Assert
            Assert.IsTrue(result.IsValid, "Success result should have IsValid=true.");
            Assert.AreEqual(string.Empty, result.ErrorMessage, "Success result should have empty error message.");
        }

        [TestMethod]
        public void ValidationResult_Failure_HasCorrectProperties()
        {
            // Arrange
            var errorMsg = "Test error message";

            // Act
            var result = PathValidationResult.Failure(errorMsg);

            // Assert
            Assert.IsFalse(result.IsValid, "Failure result should have IsValid=false.");
            Assert.AreEqual(errorMsg, result.ErrorMessage, "Failure result should contain error message.");
        }

        #endregion
    }
}
