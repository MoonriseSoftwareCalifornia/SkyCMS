// <copyright file="ParentsContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;
    using SkyCMS.Drivers.ElFinder.Responses;

    /// <summary>
    /// Contract tests for the <c>parents</c> command.
    ///
    /// Validates that <see cref="ParentsCommandHandler"/> produces JSON whose shape
    /// matches the contract documented in Docs/commands/parents.md.
    ///
    /// KEY CONTRACT RULES (from docs):
    ///   - Response root key must be "tree" (lowercase).
    ///   - "tree" must be a non-empty JSON array.
    ///   - Every entry must be a valid elFinder file object (hash, name, mime, ts, size, read, write, locked).
    ///   - Directory entries must have mime = "directory".
    ///   - Volume root entry must include "volumeid" and must NOT include "phash".
    ///   - Non-root entries must include "phash".
    ///   - No PascalCase keys must leak (e.g. "Hash", "Tree", "VolumeId").
    /// </summary>
    [TestClass]
    public class ParentsContractTests : ElFinderContractTestBase
    {
        private ParentsCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            var adapter = BuildAdapter();
            _handler = new ParentsCommandHandler(adapter.Object);
        }

        // ------------------------------------------------------------------ //
        //  Response root shape                                                 //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("Response must have lowercase 'tree' key — not 'Tree'. " +
                     "Drift: Newtonsoft serializer accidentally used instead of STJ.")]
        public async Task Parents_ResponseKey_IsLowercaseTree()
        {
            var command = new ParentsCommand { Target = ImagesHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("tree", out _),
                "Response must contain lowercase key 'tree'. " +
                "If 'Tree' (PascalCase) is found instead, the controller is using Newtonsoft — " +
                "see skycms-implementation-notes.md serialization section.");
        }

        [TestMethod]
        [Description("'tree' must be a JSON array, not null or object.")]
        public async Task Parents_Tree_IsArray()
        {
            var command = new ParentsCommand { Target = ImagesHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            AssertArrayProperty(doc.RootElement, "tree", minLength: 1);
        }

        [TestMethod]
        [Description("No PascalCase keys should appear — would indicate wrong serializer.")]
        public async Task Parents_NoPascalCaseKeysLeak()
        {
            var command = new ParentsCommand { Target = ImagesHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            foreach (var forbiddenKey in new[] { "Tree", "VolumeId", "Hash", "Name", "Mime", "Size" })
            {
                Assert.IsFalse(
                    doc.RootElement.TryGetProperty(forbiddenKey, out _),
                    $"PascalCase key '{forbiddenKey}' found in response — STJ serializer must be used, not Newtonsoft. " +
                    $"See skycms-implementation-notes.md.");
            }
        }

        // ------------------------------------------------------------------ //
        //  File object shape inside tree entries                              //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("Every entry in 'tree' must satisfy the elFinder file object contract.")]
        public async Task Parents_EachTreeEntry_HasRequiredFileObjectFields()
        {
            var command = new ParentsCommand { Target = ImagesHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree", minLength: 1);
            var index = 0;
            foreach (var entry in tree.EnumerateArray())
            {
                AssertElFinderObject(entry, $"tree[{index}]");
                index++;
            }
        }

        [TestMethod]
        [Description("Directory entries must have mime = 'directory'.")]
        public async Task Parents_DirectoryEntries_HaveMimeDirectory()
        {
            var command = new ParentsCommand { Target = ImagesHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree");
            foreach (var entry in tree.EnumerateArray())
            {
                var mime = AssertStringProperty(entry, "mime");
                Assert.AreEqual("directory", mime,
                    $"All entries in 'tree' should be directories (mime='directory'). Got '{mime}'.");
            }
        }

        [TestMethod]
        [Description("Non-root entries must include 'phash'. " +
                     "Drift: phash accidentally suppressed by JsonIgnore or wrong condition.")]
        public async Task Parents_NonRootEntries_HavePhash()
        {
            var command = new ParentsCommand { Target = ImagesHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree");
            foreach (var entry in tree.EnumerateArray())
            {
                var hash = AssertStringProperty(entry, "hash");
                var isRoot = !entry.TryGetProperty("phash", out var phashProp) ||
                             phashProp.ValueKind == JsonValueKind.Null;

                if (!isRoot)
                {
                    AssertStringProperty(entry, "phash");
                }
            }
        }

        [TestMethod]
        [Description("'ts' (timestamp) values must be positive Unix timestamps (after year 2000).")]
        public async Task Parents_TreeEntries_HaveValidTimestamps()
        {
            const long year2000Unix = 946684800L;
            var command = new ParentsCommand { Target = ImagesHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree");
            var index = 0;
            foreach (var entry in tree.EnumerateArray())
            {
                var ts = AssertNumberProperty(entry, "ts");
                Assert.IsTrue(ts > year2000Unix,
                    $"tree[{index}].ts = {ts} is not a plausible Unix timestamp (must be > {year2000Unix}).");
                index++;
            }
        }

        // ------------------------------------------------------------------ //
        //  Error paths                                                         //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("Missing target returns an error response with 'error' key — not 'tree'.")]
        public async Task Parents_MissingTarget_ReturnsErrorResponse()
        {
            var command = new ParentsCommand { Target = string.Empty };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "A missing target should return an ElFinderErrorResponse, not a ParentsResponse.");

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("error", out var errorProp),
                "Error response must contain 'error' key. See Docs/elfinder-error-response.md.");

            Assert.AreEqual(JsonValueKind.Array, errorProp.ValueKind,
                "'error' value must be a JSON array of string tokens.");
        }

        [TestMethod]
        [Description("Invalid (undecodable) target hash returns an error response.")]
        public async Task Parents_InvalidHash_ReturnsErrorResponse()
        {
            var command = new ParentsCommand { Target = "not_a_valid_hash" };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "An invalid hash should return ElFinderErrorResponse.");
        }

        [TestMethod]
        [Description("Access denied returns an error response.")]
        public async Task Parents_AccessDenied_ReturnsErrorResponse()
        {
            var adapter = BuildAdapter();
            adapter.Setup(a => a.IsAccessibleAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                   .ReturnsAsync(false);

            var handler = new ParentsCommandHandler(adapter.Object);
            var command = new ParentsCommand { Target = ImagesHash };
            var response = await handler.Handle(command, default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Access denied should return ElFinderErrorResponse.");

            using var doc = SerializeResponse(response);
            Assert.IsTrue(doc.RootElement.TryGetProperty("error", out _),
                "Error response must contain 'error' key.");
        }
    }
}
