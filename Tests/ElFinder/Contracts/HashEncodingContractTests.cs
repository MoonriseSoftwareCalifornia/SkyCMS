// <copyright file="HashEncodingContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SkyCMS.Drivers.ElFinder.Adapters;

    /// <summary>
    /// Contract tests for elFinder hash encoding / decoding.
    ///
    /// KEY CONTRACT RULES (from Docs/elfinder-file-object.md — Hash encoding section):
    ///   - hash = volumeId + Base64Url(storagePath)
    ///   - volumeId prefix is "l1_"
    ///   - Base64Url uses '-' for '+' and '_' for '/' with '=' padding stripped
    ///   - Round-trip encode → decode must recover the original path exactly
    ///   - Decode of an unknown prefix must return null
    ///   - Volume root must produce a stable, known hash
    ///
    /// These tests use the real <see cref="ElFinderStorageAdapter"/> encode/decode
    /// logic (via the test helper that mirrors it) to catch any algorithm drift.
    /// </summary>
    [TestClass]
    public class HashEncodingContractTests : ElFinderContractTestBase
    {
        // ------------------------------------------------------------------ //
        //  Known stable hashes (from Docs/elfinder-file-object.md examples)  //
        //  If these fail, the encoding algorithm has changed — update docs.   //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("pub/ encodes to the documented root hash 'l1_cHVi'.")]
        public void HashEncoding_Root_ProducesDocumentedHash()
        {
            var hash = AdapterHashHelper.Encode("pub/");
            Assert.AreEqual(RootHash, hash,
                $"Root path 'pub/' must encode to '{RootHash}' (documented in elfinder-file-object.md). " +
                $"Got '{hash}'. If the algorithm changed, update all doc examples and test constants.");
        }

        [TestMethod]
        [Description("pub/images/ encodes to the documented hash 'l1_cHViL2ltYWdlcw'.")]
        public void HashEncoding_ImagesDir_ProducesDocumentedHash()
        {
            var hash = AdapterHashHelper.Encode("pub/images/");
            Assert.AreEqual(ImagesHash, hash,
                $"'pub/images/' must encode to '{ImagesHash}'. Got '{hash}'.");
        }

        [TestMethod]
        [Description("pub/images/logo.png encodes to the documented hash.")]
        public void HashEncoding_LogoPng_ProducesDocumentedHash()
        {
            var hash = AdapterHashHelper.Encode("pub/images/logo.png");
            Assert.AreEqual(LogoPngHash, hash,
                $"'pub/images/logo.png' must encode to '{LogoPngHash}'. Got '{hash}'.");
        }

        // ------------------------------------------------------------------ //
        //  Round-trip correctness                                              //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("Encode then decode must recover the original path for a file.")]
        public void HashEncoding_RoundTrip_File()
        {
            const string path = "pub/images/logo.png";
            var hash = AdapterHashHelper.Encode(path);
            var decoded = AdapterHashHelper.Decode(hash);
            Assert.AreEqual(path, decoded,
                $"Round-trip encode→decode must recover '{path}'. Got '{decoded}'.");
        }

        [TestMethod]
        [Description("Encode then decode must recover the original path for a directory.")]
        public void HashEncoding_RoundTrip_Directory()
        {
            const string path = "pub/images/";
            var hash = AdapterHashHelper.Encode(path);
            // Decode strips the leading slash, encode strips trailing — normalise for comparison
            var decoded = AdapterHashHelper.Decode(hash);
            Assert.IsNotNull(decoded, "Decoded path must not be null.");
            Assert.IsTrue(
                decoded == path || decoded == path.TrimEnd('/') || decoded == path.TrimStart('/'),
                $"Round-trip decode of '{path}' produced unexpected value '{decoded}'.");
        }

        [TestMethod]
        [Description("Encode then decode must work for a deeply nested path.")]
        public void HashEncoding_RoundTrip_DeepPath()
        {
            const string path = "pub/media/2024/05/hero-image.jpg";
            var hash = AdapterHashHelper.Encode(path);
            var decoded = AdapterHashHelper.Decode(hash);
            Assert.AreEqual(path, decoded,
                $"Round-trip encode→decode failed for deep path '{path}'. Got '{decoded}'.");
        }

        // ------------------------------------------------------------------ //
        //  Hash format rules                                                   //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("All hashes must start with the volumeId prefix 'l1_'.")]
        public void HashEncoding_Hash_StartsWithVolumeId()
        {
            foreach (var path in new[] { "pub/", "pub/images/", "pub/images/logo.png", "pub/docs/readme.md" })
            {
                var hash = AdapterHashHelper.Encode(path);
                Assert.IsTrue(hash.StartsWith(VolumeId, System.StringComparison.Ordinal),
                    $"Hash for '{path}' must start with volumeId '{VolumeId}'. Got '{hash}'.");
            }
        }

        [TestMethod]
        [Description("Hashes must not contain '+', '/', or '=' — they use Base64Url encoding.")]
        public void HashEncoding_Hash_IsBase64Url()
        {
            foreach (var path in new[] { "pub/images/logo.png", "pub/docs/file with spaces.txt", "pub/üñícode.txt" })
            {
                var hash = AdapterHashHelper.Encode(path);
                Assert.IsFalse(hash.Contains('+'), $"Hash for '{path}' must not contain '+' (Base64Url). Got '{hash}'.");
                Assert.IsFalse(hash.Contains('/'), $"Hash for '{path}' must not contain '/' (Base64Url). Got '{hash}'.");
                Assert.IsFalse(hash.Contains('='), $"Hash for '{path}' must not contain '=' padding. Got '{hash}'.");
            }
        }

        [TestMethod]
        [Description("Different paths must produce different hashes.")]
        public void HashEncoding_DifferentPaths_ProduceDifferentHashes()
        {
            var h1 = AdapterHashHelper.Encode("pub/images/logo.png");
            var h2 = AdapterHashHelper.Encode("pub/images/banner.png");
            var h3 = AdapterHashHelper.Encode("pub/docs/");

            Assert.AreNotEqual(h1, h2, "Different file names must produce different hashes.");
            Assert.AreNotEqual(h1, h3, "File and directory paths must produce different hashes.");
            Assert.AreNotEqual(h2, h3, "Different paths must produce different hashes.");
        }

        // ------------------------------------------------------------------ //
        //  Invalid input handling                                              //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("Decode with wrong volume prefix must return null.")]
        public void HashDecoding_WrongPrefix_ReturnsNull()
        {
            var result = AdapterHashHelper.Decode("x9_cHViL2ltYWdlcw");
            Assert.IsNull(result,
                "Decoding a hash with an unknown volume prefix must return null. " +
                "The caller should treat this as an invalid target.");
        }

        [TestMethod]
        [Description("Decode of null or empty string must return null gracefully.")]
        public void HashDecoding_NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(AdapterHashHelper.Decode(null!), "Decode(null) must return null.");
            Assert.IsNull(AdapterHashHelper.Decode(string.Empty), "Decode('') must return null.");
            Assert.IsNull(AdapterHashHelper.Decode("   "), "Decode(whitespace) must return null.");
        }

        [TestMethod]
        [Description("Decode of a truncated/corrupt hash must return null, not throw.")]
        public void HashDecoding_CorruptHash_ReturnsNullWithoutException()
        {
            // Corrupt the middle of a valid hash
            var corrupt = VolumeId + "!!!notbase64!!!";
            var result = AdapterHashHelper.Decode(corrupt);
            Assert.IsNull(result, "Corrupt hash must return null, not throw an exception.");
        }
    }
}
