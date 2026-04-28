// <copyright file="SearchContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;

    /// <summary>
    /// Contract tests for the <c>search</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/search.md):
    ///   - Response root key must be "files" (lowercase).
    ///   - "files" is a JSON array of full elFinder file objects.
    ///   - Each object must have hash, name, mime, ts, size, read, write, locked.
    ///   - When q does not match anything, "files" is an empty array.
    /// </summary>
    [TestClass]
    public class SearchContractTests : ElFinderContractTestBase
    {
        private SearchCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            var mock = BuildAdapter();

            // Wire up SearchAsync: returns logo.png for any query containing "logo",
            // and both images+docs for a broader query.
            mock.Setup(a => a.SearchAsync(
                    It.Is<string>(q => q == "logo"),
                    It.IsAny<string>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<(FileManagerEntry, string)>
                {
                    (MakeFile("pub/images/logo.png", "logo.png"), "pub/images/logo.png"),
                });

            mock.Setup(a => a.SearchAsync(
                    It.Is<string>(q => q == "nomatch"),
                    It.IsAny<string>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<(FileManagerEntry, string)>());

            mock.Setup(a => a.SearchAsync(
                    It.Is<string>(q => q == "images"),
                    It.IsAny<string>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<(FileManagerEntry, string)>
                {
                    (MakeDir("pub/images/", "images"), "pub/images/"),
                });

            _handler = new SearchCommandHandler(mock.Object);
        }

        [TestMethod]
        [Description("Response must have lowercase 'files' key.")]
        public async Task Search_ResponseKey_IsLowercaseFiles()
        {
            var command = new SearchCommand { Query = "logo" };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("files", out _),
                "Contract violation: 'files' key missing from search response. See Docs/commands/search.md.");

            Assert.IsFalse(
                doc.RootElement.TryGetProperty("Files", out _),
                "PascalCase 'Files' key found — STJ must serialize with [JsonPropertyName].");
        }

        [TestMethod]
        [Description("'files' must be a JSON array.")]
        public async Task Search_Files_IsArray()
        {
            var command = new SearchCommand { Query = "logo" };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files");
            Assert.AreEqual(JsonValueKind.Array, files.ValueKind);
        }

        [TestMethod]
        [Description("Each file object must have required elFinder fields.")]
        public async Task Search_FileObjects_HaveRequiredFields()
        {
            var command = new SearchCommand { Query = "logo" };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files", minLength: 1);
            foreach (var item in files.EnumerateArray())
            {
                AssertElFinderObject(item, "search result");
            }
        }

        [TestMethod]
        [Description("No query match returns empty 'files' array, not an error.")]
        public async Task Search_NoMatch_ReturnsEmptyArray()
        {
            var command = new SearchCommand { Query = "nomatch" };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files");
            var count = 0;
            foreach (var _ in files.EnumerateArray()) count++;
            Assert.AreEqual(0, count, "Expected empty files array when nothing matches.");
        }

        [TestMethod]
        [Description("Directory results have mime 'directory'.")]
        public async Task Search_DirectoryResult_HasDirectoryMime()
        {
            var command = new SearchCommand { Query = "images" };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files", minLength: 1);
            foreach (var item in files.EnumerateArray())
            {
                var mime = item.GetProperty("mime").GetString();
                Assert.AreEqual("directory", mime, "Directory search result must have mime='directory'.");
            }
        }

        [TestMethod]
        [Description("Null or empty query returns an error, not a files array.")]
        public async Task Search_EmptyQuery_ReturnsError()
        {
            var command = new SearchCommand { Query = "" };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("error", out _),
                "Empty query must return an error response.");
        }
    }
}
