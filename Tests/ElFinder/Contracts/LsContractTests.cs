// <copyright file="LsContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;
    using SkyCMS.Drivers.ElFinder.Responses;

    /// <summary>
    /// Contract tests for the <c>ls</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/ls.md):
    ///   - Response root key must be "list" (lowercase).
    ///   - "list" is a plain string array of item names.
    ///   - When intersect[] is supplied, only matching names are returned.
    /// </summary>
    [TestClass]
    public class LsContractTests : ElFinderContractTestBase
    {
        private LsCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            _handler = new LsCommandHandler(BuildAdapter().Object);
        }

        [TestMethod]
        [Description("Response must have lowercase 'list' key.")]
        public async Task Ls_ResponseKey_IsLowercaseList()
        {
            var command = new LsCommand { Target = RootHash };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("list", out _),
                "Contract violation: 'list' key missing from ls response. See Docs/commands/ls.md.");

            Assert.IsFalse(
                doc.RootElement.TryGetProperty("List", out _),
                "PascalCase 'List' key found — STJ must be used, not Newtonsoft.");
        }

        [TestMethod]
        [Description("'list' must be a JSON array of name strings per the elFinder 2.1 spec.")]
        public async Task Ls_List_IsArray()
        {
            var command = new LsCommand { Target = RootHash };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            doc.RootElement.TryGetProperty("list", out var list);

            Assert.AreEqual(JsonValueKind.Array, list.ValueKind,
                "Contract violation: 'list' must be a JSON array. See Docs/commands/ls.md.");
        }

        [TestMethod]
        [Description("All values in the 'list' array must be non-empty name strings.")]
        public async Task Ls_List_ValuesAreNames()
        {
            var command = new LsCommand { Target = ImagesHash };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            doc.RootElement.TryGetProperty("list", out var list);
            Assert.AreEqual(JsonValueKind.Array, list.ValueKind);

            foreach (var item in list.EnumerateArray())
            {
                Assert.AreEqual(JsonValueKind.String, item.ValueKind,
                    "Each list entry must be a string.");
                Assert.IsFalse(string.IsNullOrEmpty(item.GetString()),
                    "Each list entry must be a non-empty name.");
            }
        }

        [TestMethod]
        [Description("images/ contains logo.png — it must appear in the list.")]
        public async Task Ls_List_ContainsExpectedEntry()
        {
            var command = new LsCommand { Target = ImagesHash };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            doc.RootElement.TryGetProperty("list", out var list);
            var names = new List<string>();
            foreach (var item in list.EnumerateArray())
            {
                names.Add(item.GetString()!);
            }

            CollectionAssert.Contains(names, "logo.png",
                "images/ directory must list 'logo.png'.");
        }

        [TestMethod]
        [Description("intersect[] filter returns only matching names that exist.")]
        public async Task Ls_Intersect_ReturnsOnlyMatchingNames()
        {
            var command = new LsCommand
            {
                Target = ImagesHash,
                Intersect = new[] { "logo.png", "does-not-exist.jpg" },
            };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            doc.RootElement.TryGetProperty("list", out var list);
            Assert.AreEqual(JsonValueKind.Array, list.ValueKind);

            var names = new List<string>();
            foreach (var item in list.EnumerateArray())
            {
                names.Add(item.GetString()!);
            }

            CollectionAssert.Contains(names, "logo.png",
                "logo.png exists so it must appear when intersected.");
            CollectionAssert.DoesNotContain(names, "does-not-exist.jpg",
                "does-not-exist.jpg must not appear — it is not in the directory.");
        }

        [TestMethod]
        [Description("intersect[] with no matches returns an empty array.")]
        public async Task Ls_Intersect_NoMatches_ReturnsEmptyArray()
        {
            var command = new LsCommand
            {
                Target = ImagesHash,
                Intersect = new[] { "no-match.txt" },
            };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            doc.RootElement.TryGetProperty("list", out var list);
            Assert.AreEqual(JsonValueKind.Array, list.ValueKind);

            var count = 0;
            foreach (var _ in list.EnumerateArray()) count++;
            Assert.AreEqual(0, count, "No matching names — list must be empty.");
        }

        [TestMethod]
        [Description("Invalid hash returns an error response.")]
        public async Task Ls_InvalidHash_ReturnsErrorResponse()
        {
            var command = new LsCommand { Target = "bad_hash" };
            var response = await _handler.HandleAsync(command, default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Invalid hash must return ElFinderErrorResponse.");

            using var doc = SerializeResponse(response);
            Assert.IsTrue(doc.RootElement.TryGetProperty("error", out _),
                "Error response must have 'error' key.");
        }

        [TestMethod]
        [Description("Empty directory returns an empty 'list' array (not an error).")]
        public async Task Ls_EmptyDirectory_ReturnsEmptyList()
        {
            var docsHash = AdapterHashHelper.Encode("pub/docs/");
            var command = new LsCommand { Target = docsHash };
            var response = await _handler.HandleAsync(command, default);

            Assert.IsFalse(response is ElFinderErrorResponse,
                "An empty directory should return a list response, not an error.");

            using var doc = SerializeResponse(response);
            Assert.IsTrue(doc.RootElement.TryGetProperty("list", out var list),
                "'list' key must be present even for empty directories.");
            Assert.AreEqual(JsonValueKind.Array, list.ValueKind,
                "'list' must be an array even when empty.");
        }
    }
}
