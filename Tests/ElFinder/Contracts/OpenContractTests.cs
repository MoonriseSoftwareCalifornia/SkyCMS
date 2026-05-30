// <copyright file="OpenContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;
    using SkyCMS.Drivers.ElFinder.Responses;

    /// <summary>
    /// Contract tests for the <c>open</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/open.md):
    ///   - Response must contain "cwd" (object), "files" (array), "api" (string).
    ///   - "cwd" must be a valid elFinder file object.
    ///   - Every entry in "files" must be a valid elFinder file object.
    ///   - "api" must equal "2.1".
    ///   - On error, response must contain "error" array key.
    /// </summary>
    [TestClass]
    public class OpenContractTests : ElFinderContractTestBase
    {
        private OpenCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            var adapter = BuildAdapter();
            _handler = new OpenCommandHandler(adapter.Object, BuildPassThroughNameResolver());
        }

        // ------------------------------------------------------------------ //
        //  Top-level shape                                                     //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("Response must contain 'cwd', 'files', and 'api' keys.")]
        public async Task Open_Response_HasRequiredTopLevelKeys()
        {
            var command = new OpenCommand(target: ImagesHash, init: true);
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(doc.RootElement.TryGetProperty("cwd", out _),
                "Contract violation: 'cwd' key missing from open response. See Docs/commands/open.md.");
            Assert.IsTrue(doc.RootElement.TryGetProperty("files", out _),
                "Contract violation: 'files' key missing from open response.");
            Assert.IsTrue(doc.RootElement.TryGetProperty("api", out _),
                "Contract violation: 'api' key missing from init=1 open response.");
        }

        [TestMethod]
        [Description("'api' must be '2.1049' — the protocol version the client expects.")]
        public async Task Open_Api_IsVersion21()
        {
            var command = new OpenCommand(target: ImagesHash, init: true);
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            var api = AssertStringProperty(doc.RootElement, "api");
            Assert.AreEqual("2.1049", api,
                $"Contract violation: 'api' must be '2.1049' but was '{api}'. " +
                $"The elFinder client uses this to negotiate protocol features.");
        }

        [TestMethod]
        [Description("'files' must be a JSON array.")]
        public async Task Open_Files_IsArray()
        {
            var command = new OpenCommand(target: ImagesHash);
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            AssertArrayProperty(doc.RootElement, "files");
        }

        // ------------------------------------------------------------------ //
        //  cwd shape                                                           //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("'cwd' must satisfy the elFinder file object contract.")]
        public async Task Open_Cwd_IsValidElFinderObject()
        {
            var command = new OpenCommand(target: ImagesHash);
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(doc.RootElement.TryGetProperty("cwd", out var cwd),
                "Response missing 'cwd'.");

            Assert.AreEqual(JsonValueKind.Object, cwd.ValueKind,
                "'cwd' must be a JSON object.");

            AssertElFinderObject(cwd, "cwd");
        }

        [TestMethod]
        [Description("'cwd' mime must be 'directory' when opening a folder.")]
        public async Task Open_Cwd_MimeIsDirectory()
        {
            var command = new OpenCommand(target: ImagesHash);
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            doc.RootElement.TryGetProperty("cwd", out var cwd);
            var mime = AssertStringProperty(cwd, "mime");
            Assert.AreEqual("directory", mime,
                $"cwd.mime must be 'directory' when opening a folder. Got '{mime}'.");
        }

        // ------------------------------------------------------------------ //
        //  files entries shape                                                 //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("Every entry in 'files' must satisfy the elFinder file object contract.")]
        public async Task Open_FilesEntries_AreValidElFinderObjects()
        {
            var command = new OpenCommand(target: ImagesHash);
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files");
            var index = 0;
            foreach (var entry in files.EnumerateArray())
            {
                AssertElFinderObject(entry, $"files[{index}]");
                index++;
            }
        }

        [TestMethod]
        [Description("No PascalCase keys should appear anywhere in the response.")]
        public async Task Open_NoPascalCaseKeysLeak()
        {
            var command = new OpenCommand(target: ImagesHash);
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            foreach (var forbiddenKey in new[] { "Cwd", "Files", "Api", "UplMaxSize", "VolumeId" })
            {
                Assert.IsFalse(
                    doc.RootElement.TryGetProperty(forbiddenKey, out _),
                    $"PascalCase key '{forbiddenKey}' found — STJ serializer must be used. " +
                    $"See skycms-implementation-notes.md.");
            }
        }

        // ------------------------------------------------------------------ //
        //  Error paths                                                         //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("Invalid hash returns an error response with 'error' array key.")]
        public async Task Open_InvalidHash_ReturnsErrorResponse()
        {
            var command = new OpenCommand(target: "not_a_valid_hash");
            var response = await _handler.HandleAsync(command, default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Invalid hash must return ElFinderErrorResponse.");

            using var doc = SerializeResponse(response);
            Assert.IsTrue(doc.RootElement.TryGetProperty("error", out var err),
                "Error response must have 'error' key.");
            Assert.AreEqual(JsonValueKind.Array, err.ValueKind,
                "'error' must be a JSON array.");
        }

        [TestMethod]
        [Description("Access denied returns an error response.")]
        public async Task Open_AccessDenied_ReturnsErrorResponse()
        {
            var adapter = BuildAdapter();
            adapter.Setup(a => a.IsAccessibleAsync(
                It.IsAny<string>(),
                It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(false);

            var handler = new OpenCommandHandler(adapter.Object, BuildPassThroughNameResolver());
            var response = await handler.HandleAsync(new OpenCommand(target: ImagesHash), default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Access denied must return ElFinderErrorResponse.");
        }

        // ------------------------------------------------------------------ //
        //  Article-path: title substitution and dual-path contract             //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("When opening /pub/articles/42, the cwd 'name' must be the article title, not the number.")]
        public async Task Open_ArticleFolder_CwdNameIsArticleTitle()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildElFinderNameResolver(ArticleNumber, ArticleTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: ArticleFolderHash), default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(doc.RootElement.TryGetProperty("cwd", out var cwd),
                "Response must contain 'cwd'.");
            var name = AssertStringProperty(cwd, "name");
            Assert.AreEqual(ArticleTitle, name,
                $"cwd.name must be the article title '{ArticleTitle}', not the raw number '{ArticleNumber}'. " +
                $"Check ElFinderNameResolver and OpenCommandHandler.BuildElFinderObjectAsync.");
        }

        [TestMethod]
        [Description("When opening /pub/articles, the article-folder child 'name' must be the article title.")]
        public async Task Open_ArticlesRootFolder_ChildNameIsArticleTitle()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildElFinderNameResolver(ArticleNumber, ArticleTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: ArticlesRootHash), default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files", minLength: 1);
            var found = false;
            foreach (var entry in files.EnumerateArray())
            {
                if (!entry.TryGetProperty("name", out var nameProp))
                {
                    continue;
                }

                if (string.Equals(nameProp.GetString(), ArticleTitle, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found,
                $"No file entry with name='{ArticleTitle}' found in 'files'. " +
                $"Article folder names under /pub/articles/{{number}} must use the article title.");
        }

        [TestMethod]
        [Description("When opening /pub/articles, the article-folder child must carry canonical and display paths.")]
        public async Task Open_ArticlesRootFolder_ChildContainsRealPathAndDisplayPath()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildTitleNameResolver(ArticleNumber, ArticleTitle, TemplateId, TemplateTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: ArticlesRootHash), default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files", minLength: 1);
            string? foundRealPath = null;
            string? foundDisplayPath = null;
            foreach (var entry in files.EnumerateArray())
            {
                if (!entry.TryGetProperty("name", out var nameProp) ||
                    !string.Equals(nameProp.GetString(), ArticleTitle, StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.IsTrue(entry.TryGetProperty("realPath", out var rp),
                    $"Article folder entry (name='{ArticleTitle}') must contain a 'realPath' field.");
                Assert.IsTrue(entry.TryGetProperty("displayPath", out var dp),
                    $"Article folder entry (name='{ArticleTitle}') must contain a 'displayPath' field.");
                foundRealPath = rp.GetString();
                foundDisplayPath = dp.GetString();
                break;
            }

            Assert.IsNotNull(foundRealPath,
                $"Could not find the article entry (name='{ArticleTitle}') in files array.");
            Assert.AreEqual(ArticleRealPath, foundRealPath,
                $"realPath must equal canonical storage path '{ArticleRealPath}', not '{foundRealPath}'.");
            Assert.AreEqual(ArticleDisplayPath, foundDisplayPath,
                $"displayPath must equal friendly path '{ArticleDisplayPath}', not '{foundDisplayPath}'.");
        }

        [TestMethod]
        [Description("Deep article paths must retain canonical IDs in realPath and friendly titles in displayPath.")]
        public async Task Open_DeepArticlePath_EntriesContainDualPaths()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildTitleNameResolver(ArticleNumber, ArticleTitle, TemplateId, TemplateTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: ArticleDeepFileHash), default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(doc.RootElement.TryGetProperty("cwd", out var cwd), "Response must contain 'cwd'.");
            Assert.IsTrue(cwd.TryGetProperty("realPath", out var cwdRealPath), "cwd must include 'realPath'.");
            Assert.IsTrue(cwd.TryGetProperty("displayPath", out var cwdDisplayPath), "cwd must include 'displayPath'.");

            Assert.AreEqual(ArticleDeepFileRealPath, cwdRealPath.GetString(), "cwd.realPath should remain canonical for deep article entries.");
            Assert.AreEqual(ArticleDeepFileDisplayPath, cwdDisplayPath.GetString(), "cwd.displayPath should replace article ID with title for deep article entries.");

            var files = AssertArrayProperty(doc.RootElement, "files", minLength: 1);
            foreach (var entry in files.EnumerateArray())
            {
                Assert.IsTrue(entry.TryGetProperty("realPath", out _), "Every returned entry must include realPath.");
                Assert.IsTrue(entry.TryGetProperty("displayPath", out _), "Every returned entry must include displayPath.");
            }
        }

        [TestMethod]
        [Description("Template folder entries must contain canonical realPath and title-based displayPath.")]
        public async Task Open_TemplatesRootFolder_ChildContainsRealPathAndDisplayPath()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildTitleNameResolver(ArticleNumber, ArticleTitle, TemplateId, TemplateTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: TemplatesRootHash), default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files", minLength: 1);
            string? foundRealPath = null;
            string? foundDisplayPath = null;
            foreach (var entry in files.EnumerateArray())
            {
                if (!entry.TryGetProperty("name", out var nameProp) ||
                    !string.Equals(nameProp.GetString(), TemplateTitle, StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.IsTrue(entry.TryGetProperty("realPath", out var rp),
                    $"Template folder entry (name='{TemplateTitle}') must contain 'realPath'.");
                Assert.IsTrue(entry.TryGetProperty("displayPath", out var dp),
                    $"Template folder entry (name='{TemplateTitle}') must contain 'displayPath'.");
                foundRealPath = rp.GetString();
                foundDisplayPath = dp.GetString();
                break;
            }

            Assert.IsNotNull(foundRealPath,
                $"Could not find the template entry (name='{TemplateTitle}') in files array.");
            Assert.AreEqual(TemplateRealPath, foundRealPath,
                $"realPath must equal canonical template storage path '{TemplateRealPath}', not '{foundRealPath}'.");
            Assert.AreEqual(TemplateDisplayPath, foundDisplayPath,
                $"displayPath must equal friendly template path '{TemplateDisplayPath}', not '{foundDisplayPath}'.");
        }
    }
}
