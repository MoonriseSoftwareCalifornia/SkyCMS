// <copyright file="TreeContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;
    using SkyCMS.Drivers.ElFinder.Responses;

    /// <summary>
    /// Contract tests for the <c>tree</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/tree.md):
    ///   - Response root key must be "tree" (lowercase array).
    ///   - Only directories are returned (mime = "directory").
    ///   - Every entry must be a valid elFinder file object.
    ///   - Entries must have "phash" pointing to the requested target.
    /// </summary>
    [TestClass]
    public class TreeContractTests : ElFinderContractTestBase
    {
        private TreeCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            _handler = new TreeCommandHandler(BuildAdapter().Object);
        }

        [TestMethod]
        [Description("Response must have lowercase 'tree' key containing an array.")]
        public async Task Tree_ResponseKey_IsLowercaseTreeArray()
        {
            var command = new TreeCommand { Target = RootHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            AssertArrayProperty(doc.RootElement, "tree");
        }

        [TestMethod]
        [Description("All entries in 'tree' must be valid elFinder file objects.")]
        public async Task Tree_Entries_AreValidElFinderObjects()
        {
            var command = new TreeCommand { Target = RootHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree");
            var index = 0;
            foreach (var entry in tree.EnumerateArray())
            {
                AssertElFinderObject(entry, $"tree[{index}]");
                index++;
            }
        }

        [TestMethod]
        [Description("Tree command returns only directories — no files.")]
        public async Task Tree_Entries_AreDirectoriesOnly()
        {
            var command = new TreeCommand { Target = RootHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree");
            var index = 0;
            foreach (var entry in tree.EnumerateArray())
            {
                var mime = AssertStringProperty(entry, "mime");
                Assert.AreEqual("directory", mime,
                    $"tree[{index}].mime must be 'directory'. Got '{mime}'. " +
                    $"The tree command must return only directories — files should not appear.");
                index++;
            }
        }

        [TestMethod]
        [Description("No PascalCase keys should appear in the response.")]
        public async Task Tree_NoPascalCaseKeysLeak()
        {
            var command = new TreeCommand { Target = RootHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            foreach (var forbiddenKey in new[] { "Tree", "Hash", "Name", "Mime" })
            {
                Assert.IsFalse(
                    doc.RootElement.TryGetProperty(forbiddenKey, out _),
                    $"PascalCase key '{forbiddenKey}' found — STJ must be used, not Newtonsoft.");
            }
        }

        [TestMethod]
        [Description("Invalid hash returns an error response.")]
        public async Task Tree_InvalidHash_ReturnsErrorResponse()
        {
            var command = new TreeCommand { Target = "not_a_hash" };
            var response = await _handler.Handle(command, default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Invalid hash must return ElFinderErrorResponse.");

            using var doc = SerializeResponse(response);
            Assert.IsTrue(doc.RootElement.TryGetProperty("error", out _),
                "Error response must contain 'error' key.");
        }
    }
}
