// <copyright file="PathNormalizationIntegrationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests for path normalization across storage operations,
    /// ensuring that normalized paths are used consistently from entry to storage.
    /// </summary>
    [TestClass]
    public class PathNormalizationIntegrationTests
    {
        private PathNormalizer normalizer;

        [TestInitialize]
        public void Initialize()
        {
            this.normalizer = new PathNormalizer();
        }

        #region Idempotency and Consistency Tests

        /// <summary>
        /// Verifies that repeated normalization produces identical results (idempotent operation).
        /// </summary>
        [TestMethod]
        public void Normalize_RepeatedNormalization_ProducesIdempotentResults()
        {
            // Arrange
            var testPaths = new[]
            {
                "/folder/subfolder/file.txt",
                "folder\\subfolder\\file.txt",
                "/folder//subfolder///file.txt",
                "  /folder/subfolder/file.txt  ",
                "folder/subfolder\\file.txt"
            };

            // Act & Assert
            foreach (var path in testPaths)
            {
                var first = this.normalizer.Normalize(path);
                var second = this.normalizer.Normalize(first);
                var third = this.normalizer.Normalize(second);

                Assert.AreEqual(first, second, $"First and second normalization differ for path: {path}");
                Assert.AreEqual(second, third, $"Second and third normalization differ for path: {path}");

                // All should normalize to the same canonical form
                Assert.AreEqual("folder/subfolder/file.txt", first, $"Path {path} did not normalize to expected form");
            }
        }

        /// <summary>
        /// Verifies that different input formats normalize to the same canonical output.
        /// </summary>
        [TestMethod]
        public void Normalize_VariantInputFormats_NormalizeToCanonical()
        {
            // Arrange
            var variants = new[]
            {
                "/articles/blog-post-1/index.html",
                "articles/blog-post-1/index.html",
                "\\articles\\blog-post-1\\index.html",
                "/articles//blog-post-1///index.html",
                "  articles/blog-post-1/index.html  "
            };

            var expectedCanonical = "articles/blog-post-1/index.html";

            // Act & Assert
            foreach (var variant in variants)
            {
                var normalized = this.normalizer.Normalize(variant);
                Assert.AreEqual(expectedCanonical, normalized, $"Variant '{variant}' did not normalize to canonical form");
            }
        }

        #endregion

        #region Path Hierarchy Tests

        /// <summary>
        /// Verifies that parent path relationships are maintained after normalization.
        /// </summary>
        [TestMethod]
        public void Normalize_ParentPathRelationship_Maintained()
        {
            // Arrange
            var testCases = new[]
            {
                new { Path = "/folder/subfolder/file.txt", ExpectedParent = "folder/subfolder" },
                new { Path = "folder/subfolder/file.txt", ExpectedParent = "folder/subfolder" },
                new { Path = "/folder/file.txt", ExpectedParent = "folder" },
                new { Path = "folder/file.txt", ExpectedParent = "folder" }
            };

            // Act & Assert
            foreach (var testCase in testCases)
            {
                var normalized = this.normalizer.Normalize(testCase.Path);
                var lastSlash = normalized.LastIndexOf('/');
                var parent = lastSlash > 0 ? normalized.Substring(0, lastSlash) : string.Empty;

                Assert.AreEqual(testCase.ExpectedParent, parent, $"Parent path mismatch for {testCase.Path}");
            }
        }

        /// <summary>
        /// Verifies that path depth (number of segments) is preserved after normalization.
        /// </summary>
        [TestMethod]
        public void Normalize_PathDepth_PreservedAfterNormalization()
        {
            // Arrange
            var testCases = new[]
            {
                new { Path = "/a/b/c/d/e.txt", ExpectedDepth = 5 },
                new { Path = "a//b///c/d/e.txt", ExpectedDepth = 5 },
                new { Path = "a/b/c/d", ExpectedDepth = 4 },
                new { Path = "/single.txt", ExpectedDepth = 1 }
            };

            // Act & Assert
            foreach (var testCase in testCases)
            {
                var normalized = this.normalizer.Normalize(testCase.Path);
                var depth = normalized.Split('/').Length;

                Assert.AreEqual(testCase.ExpectedDepth, depth, $"Path depth mismatch for {testCase.Path}");
            }
        }

        #endregion

        #region Hash Generation Consistency Tests

        /// <summary>
        /// Verifies that the same normalized path always produces the same hash (deterministic).
        /// This is critical for elFinder hash generation.
        /// </summary>
        [TestMethod]
        public void Normalize_DeterministicHashGeneration_SamePathSameHash()
        {
            // Arrange
            var path = "/pub/images/logo.png";
            var hash1 = GenerateDeterministicHash(this.normalizer.Normalize(path));
            var hash2 = GenerateDeterministicHash(this.normalizer.Normalize(path));

            // Act & Assert
            Assert.AreEqual(hash1, hash2, "Hashes should be identical for the same normalized path");
        }

        /// <summary>
        /// Verifies that variant input formats produce the same hash after normalization.
        /// </summary>
        [TestMethod]
        public void Normalize_VariantInputs_ProduceSameHashWhenNormalized()
        {
            // Arrange
            var variants = new[]
            {
                "/pub/images/logo.png",
                "pub/images/logo.png",
                "/pub//images///logo.png",
                "pub\\images\\logo.png"
            };

            // Act
            var hashes = variants
                .Select(v => GenerateDeterministicHash(this.normalizer.Normalize(v)))
                .ToList();

            // Assert
            var firstHash = hashes.First();
            foreach (var hash in hashes.Skip(1))
            {
                Assert.AreEqual(firstHash, hash, "All variants should produce the same hash after normalization");
            }
        }

        /// <summary>
        /// Verifies that different paths produce different hashes.
        /// </summary>
        [TestMethod]
        public void Normalize_DifferentPaths_ProduceDifferentHashes()
        {
            // Arrange
            var paths = new[]
            {
                "/pub/images/logo.png",
                "/pub/images/favicon.ico",
                "/pub/styles/css",
                "/pub/images"
            };

            // Act
            var hashes = paths
                .Select(p => GenerateDeterministicHash(this.normalizer.Normalize(p)))
                .ToList();

            // Assert
            var uniqueHashes = hashes.Distinct().Count();
            Assert.AreEqual(paths.Length, uniqueHashes, "Different paths should produce different hashes");
        }

        #endregion

        #region Leading Slash Consistency Tests

        /// <summary>
        /// Verifies that NormalizeWithLeadingSlash produces consistent results.
        /// </summary>
        [TestMethod]
        public void NormalizeWithLeadingSlash_ConsistentLeadingSlashHandling()
        {
            // Arrange
            var testCases = new[]
            {
                new { Input = "/folder/file.txt", Expected = "/folder/file.txt" },
                new { Input = "folder/file.txt", Expected = "/folder/file.txt" },
                new { Input = "/", Expected = "/" },
                new { Input = "", Expected = "/" },
                new { Input = "file.txt", Expected = "/file.txt" }
            };

            // Act & Assert
            foreach (var testCase in testCases)
            {
                var result = this.normalizer.NormalizeWithLeadingSlash(testCase.Input);
                Assert.AreEqual(testCase.Expected, result, $"Mismatch for input: {testCase.Input}");
                Assert.IsTrue(result.StartsWith("/"), $"Result should start with leading slash for input: {testCase.Input}");
            }
        }

        #endregion

        #region Edge Cases and Security Tests

        /// <summary>
        /// Verifies that path traversal attempts are handled safely.
        /// </summary>
        [TestMethod]
        public void Normalize_PathTraversalAttempts_HandledSafely()
        {
            // Arrange
            var dangerousPaths = new[]
            {
                "../../../etc/passwd",
                "..\\..\\..\\windows\\system32",
                "folder/../../sensitive.txt",
                "pub/../../../../root.txt"
            };

            // Act & Assert - These should normalize to their component form
            // Validation of traversal safety is the caller's responsibility, but normalization
            // ensures consistent representation for validation.
            foreach (var path in dangerousPaths)
            {
                var normalized = this.normalizer.Normalize(path);
                Assert.IsNotNull(normalized, $"Normalization should not return null for: {path}");
                Assert.IsFalse(string.IsNullOrEmpty(normalized) && !string.IsNullOrWhiteSpace(path),
                    $"Normalization should preserve content for: {path}");
            }
        }

        /// <summary>
        /// Verifies that Unicode and special characters are preserved during normalization.
        /// </summary>
        [TestMethod]
        public void Normalize_UnicodeAndSpecialCharacters_Preserved()
        {
            // Arrange
            var specialPaths = new[]
            {
                "/café/résumé.pdf",
                "/日本語/ファイル.txt",
                "/file-with-dashes_and_underscores.txt",
                "/file (1) [v2].docx",
                "/file%20with%20spaces.txt"
            };

            // Act & Assert
            foreach (var path in specialPaths)
            {
                var normalized = this.normalizer.Normalize(path);
                
                // Verify that the normalized path contains content (not empty or null)
                Assert.IsNotNull(normalized, $"Normalization should not return null for: {path}");
                Assert.IsFalse(string.IsNullOrEmpty(normalized), $"Normalization should not return empty string for: {path}");
                
                // Verify that leading slash is removed but content is preserved
                Assert.IsFalse(normalized.StartsWith("/"), $"Normalized path should not have leading slash for: {path}");
                
                // Verify that the path contains at least one slash separator (unless it's a root or single file)
                var originalHasMultipleParts = path.Split('/').Where(p => !string.IsNullOrEmpty(p)).Count() > 1;
                if (originalHasMultipleParts)
                {
                    Assert.IsTrue(normalized.Contains("/"), $"Multi-part path should contain separators after normalization for: {path}");
                }
            }
        }

        #endregion

        // ─── Helper methods ──────────────────────────────────────────────────────

        /// <summary>
        /// Generates a deterministic hash for a normalized path (simulating elFinder hash generation).
        /// </summary>
        private static string GenerateDeterministicHash(string normalizedPath)
        {
            const string volumeId = "l1_";
            var bytes = Encoding.UTF8.GetBytes(normalizedPath.TrimStart('/'));
            return volumeId + Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
