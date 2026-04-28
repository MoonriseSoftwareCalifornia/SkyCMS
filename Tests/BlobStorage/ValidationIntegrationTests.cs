// <copyright file="ValidationIntegrationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests verifying that path validation correctly rejects dangerous paths across StorageContext operations.
    /// </summary>
    [TestClass]
    public class ValidationIntegrationTests
    {
        private PathValidator validator;

        [TestInitialize]
        public void Initialize()
        {
            this.validator = new PathValidator();
        }

        #region Integration: Normalization + Validation

        [TestMethod]
        public void Normalize_ThenValidate_ValidPathSucceeds()
        {
            // Arrange
            var pathNormalizer = new PathNormalizer();
            var path = "/pub/articles/2024/post.md";

            // Act
            var normalized = pathNormalizer.Normalize(path);
            var validationResult = this.validator.ValidatePath(normalized);

            // Assert
            Assert.AreEqual("pub/articles/2024/post.md", normalized, "Normalization should strip leading slash.");
            Assert.IsTrue(validationResult.IsValid, "Normalized valid path should pass validation.");
        }

        [TestMethod]
        public void Normalize_ThenValidate_TraversalPathFails()
        {
            // Arrange
            var pathNormalizer = new PathNormalizer();
            var traversalPath = "folder/../../sensitive.txt";

            // Act
            var normalized = pathNormalizer.Normalize(traversalPath);
            var validationResult = this.validator.ValidatePath(normalized);

            // Assert
            Assert.IsNotNull(normalized, "Normalization should not return null.");
            Assert.IsFalse(validationResult.IsValid, "Normalized traversal path should fail validation.");
            Assert.IsTrue(validationResult.ErrorMessage.Contains("traversal"), "Error should mention traversal.");
        }

        [TestMethod]
        public void Normalize_ThenValidate_ReservedNameFails()
        {
            // Arrange
            var pathNormalizer = new PathNormalizer();
            var reservedPath = "/CON/folder/file.txt";

            // Act
            var normalized = pathNormalizer.Normalize(reservedPath);
            var validationResult = this.validator.ValidatePath(normalized);

            // Assert
            Assert.AreEqual("CON/folder/file.txt", normalized);
            Assert.IsFalse(validationResult.IsValid, "Path with reserved name should fail validation.");
            Assert.IsTrue(validationResult.ErrorMessage.Contains("reserved"), "Error should mention reserved name.");
        }

        #endregion

        #region Edge Cases: Mixed Normalization + Validation

        [TestMethod]
        public void MixedSeparators_Normalized_ThenValidated_Succeeds()
        {
            // Arrange
            var pathNormalizer = new PathNormalizer();
            var mixedPath = "folder\\subfolder/file.txt";

            // Act
            var normalized = pathNormalizer.Normalize(mixedPath);
            var validationResult = this.validator.ValidatePath(normalized);

            // Assert
            Assert.AreEqual("folder/subfolder/file.txt", normalized);
            Assert.IsTrue(validationResult.IsValid, "Mixed separators should normalize and validate successfully.");
        }

        [TestMethod]
        public void ConsecutiveSeparators_Normalized_ThenValidated_Succeeds()
        {
            // Arrange
            var pathNormalizer = new PathNormalizer();
            var pathWithConsecutiveSeparators = "/pub///articles//file.txt";

            // Act
            var normalized = pathNormalizer.Normalize(pathWithConsecutiveSeparators);
            var validationResult = this.validator.ValidatePath(normalized);

            // Assert
            Assert.AreEqual("pub/articles/file.txt", normalized);
            Assert.IsTrue(validationResult.IsValid, "Consecutive separators should normalize and validate successfully.");
        }

        [TestMethod]
        public void TraversalWithBackslash_Fails()
        {
            // Arrange
            var pathNormalizer = new PathNormalizer();
            var traversalPath = "folder\\..\\sensitive";

            // Act
            var normalized = pathNormalizer.Normalize(traversalPath);
            var validationResult = this.validator.ValidatePath(normalized);

            // Assert
            Assert.IsFalse(validationResult.IsValid, "Backslash traversal should fail validation after normalization.");
        }

        #endregion

        #region Security: Critical Attack Vectors

        [TestMethod]
        [DataRow("../../../etc/passwd")]
        [DataRow("...\\..\\..\\windows\\system32")]
        [DataRow("folder/../../../../../root")]
        [DataRow("pub/../../admin.aspx")]
        public void CommonTraversalAttacks_FailValidation(string attackPath)
        {
            // Arrange
            var pathNormalizer = new PathNormalizer();

            // Act
            var normalized = pathNormalizer.Normalize(attackPath);
            var validationResult = this.validator.ValidatePath(normalized);

            // Assert
            Assert.IsFalse(validationResult.IsValid, $"Attack path '{attackPath}' should fail validation after normalization.");
        }

        [TestMethod]
        [DataRow("CON", "CON")]
        [DataRow("prn", "PRN")]
        [DataRow("AUX/file.txt", "AUX")]
        [DataRow("nul/folder", "NUL")]
        public void ReservedNamesInAnyPosition_FailValidation(string testPath, string expectedReservedName)
        {
            // Arrange
            var validationResult = this.validator.ValidatePath(testPath);

            // Assert
            Assert.IsFalse(validationResult.IsValid, $"Path with reserved name '{expectedReservedName}' should fail.");
            Assert.IsTrue(validationResult.ErrorMessage.Contains("reserved"), "Error should mention reserved name.");
        }

        #endregion

        #region Acceptance: Valid Paths

        [TestMethod]
        [DataRow("pub/images/logo.png")]
        [DataRow("articles/2024/01/post-slug")]
        [DataRow("temp/uploads/file-v2 (1).pdf")]
        [DataRow("café/résumé.pdf")]
        [DataRow("folder/subfolder/deep/nested/structure/file.txt")]
        public void ValidPaths_PassValidation(string validPath)
        {
            // Arrange & Act
            var validationResult = this.validator.ValidatePath(validPath);

            // Assert
            Assert.IsTrue(validationResult.IsValid, $"Valid path '{validPath}' should pass validation.");
            Assert.AreEqual(string.Empty, validationResult.ErrorMessage, "Valid path should have no error message.");
        }

        #endregion

        #region Filename Validation

        [TestMethod]
        public void FilenameOnly_WithoutPath_Validates()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename("document.pdf");

            // Assert
            Assert.IsTrue(result.IsValid, "Simple filename should validate.");
        }

        [TestMethod]
        public void FilenameWithPath_Rejects()
        {
            // Arrange & Act
            var result = this.validator.ValidateFilename("folder/document.pdf");

            // Assert
            Assert.IsFalse(result.IsValid, "Filename with path separator should be rejected.");
            Assert.IsTrue(result.ErrorMessage.Contains("path"), "Error should mention path separators.");
        }

        #endregion
    }
}
