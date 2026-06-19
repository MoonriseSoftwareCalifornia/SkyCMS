// <copyright file="ElFinderContractTestBase.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Cosmos.BlobService;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SkyCMS.Drivers.ElFinder.Adapters;
    using SkyCMS.Drivers.ElFinder.Responses;
    using SkyCMS.Drivers.ElFinder;

    /// <summary>
    /// Base class for elFinder contract tests.
    ///
    /// PURPOSE
    /// -------
    /// These tests validate that each handler produces JSON output whose shape
    /// matches the documented contract in Drivers/SkyCMS.Drivers.ElFinder/Docs/.
    /// They also confirm error-path responses conform to the documented error schema.
    ///
    /// APPROACH
    /// --------
    /// 1. Build a Moq <see cref="IElFinderStorageAdapter"/> seeded with known test data.
    /// 2. Invoke the real handler (no mocking of handler logic).
    /// 3. Serialize the result with System.Text.Json — exactly as the controller does
    ///    in HandleParentsViaCqrsAsync() and all CQRS response paths.
    /// 4. Parse the JSON and assert required keys exist, values are correct types,
    ///    and no undocumented keys have leaked through.
    ///
    /// DRIFT DETECTION
    /// ---------------
    /// If a test fails after a handler or DTO change, one of three things happened:
    ///   A) The code changed and the docs need updating.
    ///   B) The docs changed and the code has a bug.
    ///   C) A serializer attribute was added/removed unintentionally.
    /// The test failure message identifies which JSON key failed so the cause is
    /// immediately obvious without debugging.
    /// </summary>
    public abstract class ElFinderContractTestBase
    {
        // ------------------------------------------------------------------ //
        //  Constants                                                           //
        // ------------------------------------------------------------------ //

        /// <summary>Volume ID prefix used by the real adapter.</summary>
        protected const string VolumeId = "l1_";

        /// <summary>Hash representing the volume root (pub/).</summary>
        protected const string RootHash = "l1_cHVi";

        /// <summary>Hash representing pub/images/.</summary>
        protected const string ImagesHash = "l1_cHViL2ltYWdlcw";

        /// <summary>Hash representing pub/images/logo.png.</summary>
        protected const string LogoPngHash = "l1_cHViL2ltYWdlcy9sb2dvLnBuZw";

        // ------------------------------------------------------------------ //
        //  Factory helpers                                                     //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Creates a <see cref="FileManagerEntry"/> representing a directory.
        /// </summary>
        protected static FileManagerEntry MakeDir(string path, string name, bool hasChildren = false) =>
            new FileManagerEntry
            {
                Path = path,
                Name = name,
                IsDirectory = true,
                HasDirectories = hasChildren,
                Modified = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedUtc = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                Size = 0,
            };

        /// <summary>
        /// Creates a <see cref="FileManagerEntry"/> representing a file.
        /// </summary>
        protected static FileManagerEntry MakeFile(string path, string name, long size = 1024, string contentType = "image/png") =>
            new FileManagerEntry
            {
                Path = path,
                Name = name,
                IsDirectory = false,
                Modified = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedUtc = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                Size = size,
                ContentType = contentType,
                Extension = System.IO.Path.GetExtension(name),
            };

        /// <summary>
        /// Creates a valid 1×1 PNG stream using ImageSharp for use in resize and dim tests.
        /// </summary>
        protected static MemoryStream MakeOnePxPngStream()
        {
            using var image = new Image<Rgba32>(1, 1);
            var ms = new MemoryStream();
            image.SaveAsPng(ms);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Builds a mock adapter pre-configured with the standard test tree:
        /// <code>
        /// pub/           (root)
        ///   images/
        ///     logo.png
        ///   docs/
        /// </code>
        /// Individual tests can add <c>.Setup()</c> calls on the returned mock.
        /// </summary>
        // ------------------------------------------------------------------ //
        //  Article-path test fixtures                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Article number used in article-path fixture tests.</summary>
        protected const int ArticleNumber = 42;

        /// <summary>Expected article title for the fixture article.</summary>
        protected const string ArticleTitle = "My Great Article";

        /// <summary>Canonical storage path for the article folder.</summary>
        protected const string ArticleRealPath = "/pub/articles/42";

        /// <summary>Canonical storage path for a deep folder under the article tree.</summary>
        protected const string ArticleDeepFileRealPath = "/pub/articles/42/assets/images";

        /// <summary>Expected display path for the article folder.</summary>
        protected const string ArticleDisplayPath = "/pub/articles/My Great Article";

        /// <summary>Expected display path for a deep folder under the article tree.</summary>
        protected const string ArticleDeepFileDisplayPath = "/pub/articles/My Great Article/assets/images";

        /// <summary>Template identifier fixture for template-path tests.</summary>
        protected const string TemplateId = "11111111-1111-1111-1111-111111111111";

        /// <summary>Expected template title for the fixture template.</summary>
        protected const string TemplateTitle = "Main Template";

        /// <summary>Canonical storage path for the template folder.</summary>
        protected const string TemplateRealPath = "/pub/templates/11111111-1111-1111-1111-111111111111";

        /// <summary>Expected display path for the template folder.</summary>
        protected const string TemplateDisplayPath = "/pub/templates/Main Template";

        /// <summary>Hash encoding of the articles root.</summary>
        protected static readonly string ArticlesRootHash = AdapterHashHelper.Encode("pub/articles/");

        /// <summary>Hash encoding of the article folder itself.</summary>
        protected static readonly string ArticleFolderHash = AdapterHashHelper.Encode("pub/articles/42/");

        /// <summary>Hash encoding of a deep folder under the article tree.</summary>
        protected static readonly string ArticleDeepFileHash = AdapterHashHelper.Encode("pub/articles/42/assets/images/");

        /// <summary>Hash encoding of the templates root.</summary>
        protected static readonly string TemplatesRootHash = AdapterHashHelper.Encode("pub/templates/");

        /// <summary>
        /// Builds a mock adapter that extends the standard test tree with an
        /// <c>/pub/articles/</c> root and a single article folder <c>/pub/articles/42/</c>.
        /// </summary>
        protected static Mock<IElFinderStorageAdapter> BuildAdapterWithArticles()
        {
            var mock = BuildAdapter();

            var articlesRoot = MakeDir("pub/articles/", "articles", hasChildren: true);
            var articleFolder = MakeDir("pub/articles/42/", ArticleNumber.ToString(), hasChildren: true);
            var assetsFolder = MakeDir("pub/articles/42/assets/", "assets", hasChildren: true);
            var imagesFolder = MakeDir("pub/articles/42/assets/images/", "images", hasChildren: true);
            var logoFile = MakeFile("pub/articles/42/assets/images/logo.png", "logo.png");

            var templatesRoot = MakeDir("pub/templates/", "templates", hasChildren: true);
            var templateFolder = MakeDir($"pub/templates/{TemplateId}/", TemplateId, hasChildren: false);

            // pub/ children now include images/, docs/, articles/, and templates/
            mock.Setup(a => a.GetEntriesAsync(It.Is<string>(p => p == "pub" || p == "pub/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry>
                {
                    MakeDir("pub/images/", "images", hasChildren: true),
                    MakeDir("pub/docs/", "docs", hasChildren: false),
                    articlesRoot,
                    templatesRoot,
                });

            mock.Setup(a => a.GetEntriesAsync(It.Is<string>(p => p == "pub/articles" || p == "pub/articles/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { articleFolder });

            mock.Setup(a => a.GetEntriesAsync(It.Is<string>(p => p == "pub/articles/42" || p == "pub/articles/42/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { assetsFolder });

            mock.Setup(a => a.GetEntriesAsync(It.Is<string>(p => p == "pub/articles/42/assets" || p == "pub/articles/42/assets/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { imagesFolder });

            mock.Setup(a => a.GetEntriesAsync(It.Is<string>(p => p == "pub/articles/42/assets/images" || p == "pub/articles/42/assets/images/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { logoFile });

            mock.Setup(a => a.GetEntriesAsync(It.Is<string>(p => p == "pub/templates" || p == "pub/templates/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { templateFolder });

            mock.Setup(a => a.GetEntriesAsync(It.Is<string>(p => p == $"pub/templates/{TemplateId}" || p == $"pub/templates/{TemplateId}/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry>());

            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == "pub/articles" || p == "pub/articles/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(articlesRoot);

            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == "pub/articles/42" || p == "pub/articles/42/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(articleFolder);

            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == "pub/articles/42/assets" || p == "pub/articles/42/assets/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(assetsFolder);

            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == "pub/articles/42/assets/images" || p == "pub/articles/42/assets/images/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(imagesFolder);

            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == "pub/articles/42/assets/images/logo.png"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(logoFile);

            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == "pub/templates" || p == "pub/templates/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(templatesRoot);

            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == $"pub/templates/{TemplateId}" || p == $"pub/templates/{TemplateId}/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(templateFolder);

            mock.Setup(a => a.GetAncestorsAsync(It.Is<string>(p => p == "pub/articles" || p == "pub/articles/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { MakeDir("pub/", "pub", hasChildren: true) });

            mock.Setup(a => a.GetAncestorsAsync(It.Is<string>(p => p == "pub/articles/42" || p == "pub/articles/42/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { MakeDir("pub/", "pub", hasChildren: true), articlesRoot });

            mock.Setup(a => a.GetAncestorsAsync(It.Is<string>(p => p == "pub/articles/42/assets" || p == "pub/articles/42/assets/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { MakeDir("pub/", "pub", hasChildren: true), articlesRoot, articleFolder });

            mock.Setup(a => a.GetAncestorsAsync(It.Is<string>(p => p == "pub/articles/42/assets/images" || p == "pub/articles/42/assets/images/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { MakeDir("pub/", "pub", hasChildren: true), articlesRoot, articleFolder, assetsFolder });

            mock.Setup(a => a.GetAncestorsAsync(It.Is<string>(p => p == "pub/articles/42/assets/images/logo.png"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { MakeDir("pub/", "pub", hasChildren: true), articlesRoot, articleFolder, assetsFolder, imagesFolder });

            mock.Setup(a => a.GetAncestorsAsync(It.Is<string>(p => p == "pub/templates" || p == "pub/templates/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { MakeDir("pub/", "pub", hasChildren: true) });

            mock.Setup(a => a.GetAncestorsAsync(It.Is<string>(p => p == $"pub/templates/{TemplateId}" || p == $"pub/templates/{TemplateId}/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { MakeDir("pub/", "pub", hasChildren: true), templatesRoot });

            return mock;
        }

        protected static Mock<IElFinderStorageAdapter> BuildAdapter()
        {
            var mock = new Mock<IElFinderStorageAdapter>(MockBehavior.Strict);

            var root = MakeDir("pub/", "pub", hasChildren: true);
            var images = MakeDir("pub/images/", "images", hasChildren: true);
            var docs = MakeDir("pub/docs/", "docs", hasChildren: false);
            var logoPng = MakeFile("pub/images/logo.png", "logo.png");

            mock.Setup(a => a.EncodePath(It.IsAny<string>()))
                .Returns<string>(AdapterHashHelper.Encode);
            mock.Setup(a => a.DecodePath(It.IsAny<string>()))
                .Returns<string>(AdapterHashHelper.Decode);

            // Accessibility — use loose match on any path
            mock.Setup(a => a.IsAccessibleAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(true);

            // Root children — match with or without trailing slash
            mock.Setup(a => a.GetEntriesAsync(It.Is<string>(p => p == "pub" || p == "pub/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { images, docs });

            // images/ children
            mock.Setup(a => a.GetEntriesAsync(It.Is<string>(p => p == "pub/images" || p == "pub/images/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { logoPng });

            // docs/ children (empty)
            mock.Setup(a => a.GetEntriesAsync(It.Is<string>(p => p == "pub/docs" || p == "pub/docs/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry>());

            // Entry lookups — match with or without trailing slash
            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == "pub" || p == "pub/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(root);
            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == "pub/images" || p == "pub/images/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(images);
            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == "pub/docs" || p == "pub/docs/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(docs);
            mock.Setup(a => a.GetEntryAsync(It.Is<string>(p => p == "pub/images/logo.png"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(logoPng);

            // Ancestors
            mock.Setup(a => a.GetAncestorsAsync(It.Is<string>(p => p == "pub/images" || p == "pub/images/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { root });
            mock.Setup(a => a.GetAncestorsAsync(It.Is<string>(p => p == "pub/docs" || p == "pub/docs/"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { root });
            mock.Setup(a => a.GetAncestorsAsync(It.Is<string>(p => p == "pub/images/logo.png"), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<FileManagerEntry> { root, images });

            return mock;
        }

        // ------------------------------------------------------------------ //
        //  Serialization                                                       //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Serializes a handler response using System.Text.Json — identical to
        /// what the controller's CQRS path does via Content(json, "application/json").
        /// </summary>
        protected static JsonDocument SerializeResponse(IElFinderResponse response)
        {
            var json = JsonSerializer.Serialize(response, response.GetType());
            return JsonDocument.Parse(json);
        }

        // ------------------------------------------------------------------ //
        //  JSON assertion helpers                                              //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Asserts a required string property exists and is non-empty.
        /// Failure message includes the key name to identify drift quickly.
        /// </summary>
        protected static string AssertStringProperty(JsonElement obj, string key)
        {
            Assert.IsTrue(
                obj.TryGetProperty(key, out var prop),
                $"Contract violation: required key '{key}' is missing from response. " +
                $"Check the DTO [JsonPropertyName] attribute and the docs.");

            Assert.AreEqual(
                JsonValueKind.String, prop.ValueKind,
                $"Contract violation: key '{key}' should be a string but was {prop.ValueKind}.");

            var value = prop.GetString()!;
            Assert.IsFalse(
                string.IsNullOrEmpty(value),
                $"Contract violation: key '{key}' must not be null or empty.");

            return value;
        }

        /// <summary>
        /// Asserts a required numeric (integer) property exists.
        /// </summary>
        protected static long AssertNumberProperty(JsonElement obj, string key)
        {
            Assert.IsTrue(
                obj.TryGetProperty(key, out var prop),
                $"Contract violation: required key '{key}' is missing from response.");

            Assert.AreEqual(
                JsonValueKind.Number, prop.ValueKind,
                $"Contract violation: key '{key}' should be a number but was {prop.ValueKind}.");

            return prop.GetInt64();
        }

        /// <summary>
        /// Asserts a required array property exists and optionally has a minimum length.
        /// </summary>
        protected static JsonElement AssertArrayProperty(JsonElement obj, string key, int minLength = 0)
        {
            Assert.IsTrue(
                obj.TryGetProperty(key, out var prop),
                $"Contract violation: required key '{key}' is missing from response.");

            Assert.AreEqual(
                JsonValueKind.Array, prop.ValueKind,
                $"Contract violation: key '{key}' should be an array but was {prop.ValueKind}.");

            var count = 0;
            foreach (var _ in prop.EnumerateArray()) count++;

            Assert.IsTrue(
                count >= minLength,
                $"Contract violation: key '{key}' array has {count} item(s) but at least {minLength} expected.");

            return prop;
        }

        /// <summary>
        /// Asserts a property is absent from the object (e.g. phash on volume root).
        /// </summary>
        protected static void AssertPropertyAbsent(JsonElement obj, string key) =>
            Assert.IsFalse(
                obj.TryGetProperty(key, out _),
                $"Contract violation: key '{key}' should NOT be present but was found. " +
                $"Check [JsonIgnore] attribute.");

        /// <summary>
        /// Validates the standard required fields of an elFinder file/directory object
        /// as documented in Docs/elfinder-file-object.md.
        /// </summary>
        protected static void AssertElFinderObject(JsonElement obj, string contextDescription)
        {
            foreach (var key in new[] { "hash", "name", "mime", "ts", "size", "read", "write", "locked" })
            {
                Assert.IsTrue(
                    obj.TryGetProperty(key, out _),
                    $"elFinder object ({contextDescription}) is missing required key '{key}'. " +
                    $"See Docs/elfinder-file-object.md.");
            }

            // hash must be non-empty string starting with volume id
            var hash = AssertStringProperty(obj, "hash");
            Assert.IsTrue(
                hash.StartsWith(VolumeId, StringComparison.Ordinal),
                $"hash '{hash}' ({contextDescription}) must start with volumeId '{VolumeId}'.");

            // mime must be non-empty
            AssertStringProperty(obj, "mime");

            // ts, size, read, write, locked must be numbers
            AssertNumberProperty(obj, "ts");
            AssertNumberProperty(obj, "size");
            AssertNumberProperty(obj, "read");
            AssertNumberProperty(obj, "write");
            AssertNumberProperty(obj, "locked");
        }
    }

    // ------------------------------------------------------------------ //
    //  Hash helper — delegates to shared ElFinderHashEncoder             //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Thin wrapper around <see cref="ElFinderHashEncoder"/> for use in tests.
    /// Previously duplicated the encoder logic; now delegates so tests stay
    /// in sync automatically if the algorithm changes.
    /// </summary>
    internal static class AdapterHashHelper
    {
        public static string Encode(string path) => ElFinderHashEncoder.Encode(path);

        public static string? Decode(string hash) => ElFinderHashEncoder.Decode(hash);
    }
}
