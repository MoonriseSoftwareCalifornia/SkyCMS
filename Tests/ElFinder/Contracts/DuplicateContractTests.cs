// <copyright file="DuplicateContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System.Text.Json;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;

    /// <summary>
    /// Contract tests for the <c>duplicate</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/duplicate.md):
    ///   - Response root key must be "added" (lowercase).
    ///   - "added" is an array of elFinder file objects.
    ///   - Duplicate name uses "~" suffix before extension (logo~ .png → logo~.png).
    /// </summary>
    [TestClass]
    public class DuplicateContractTests : ElFinderContractTestBase
    {
        private DuplicateCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            var mock = BuildAdapter();

            var copy = MakeFile("pub/images/logo~.png", "logo~.png");

            mock.Setup(a => a.CopyAsync(
                    It.Is<string>(p => p == "pub/images/logo.png"),
                    It.IsAny<string>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(copy);

            _handler = new DuplicateCommandHandler(mock.Object);
        }

        [TestMethod]
        [Description("Response must have lowercase 'added' key.")]
        public async Task Duplicate_ResponseKey_IsLowercaseAdded()
        {
            var command = new DuplicateCommand { Targets = LogoPngHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("added", out _),
                "Contract violation: 'added' key missing. See Docs/commands/duplicate.md.");

            Assert.IsFalse(
                doc.RootElement.TryGetProperty("Added", out _),
                "PascalCase 'Added' found — [JsonPropertyName] must be applied.");
        }

        [TestMethod]
        [Description("'added' must be a JSON array.")]
        public async Task Duplicate_Added_IsArray()
        {
            var command = new DuplicateCommand { Targets = LogoPngHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var added = AssertArrayProperty(doc.RootElement, "added");
            Assert.AreEqual(JsonValueKind.Array, added.ValueKind);
        }

        [TestMethod]
        [Description("Each added object must have all required elFinder fields.")]
        public async Task Duplicate_AddedObjects_HaveRequiredFields()
        {
            var command = new DuplicateCommand { Targets = LogoPngHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var added = AssertArrayProperty(doc.RootElement, "added", minLength: 1);
            foreach (var item in added.EnumerateArray())
            {
                AssertElFinderObject(item, "duplicate result");
            }
        }

        [TestMethod]
        [Description("Missing targets returns empty 'added', not an error.")]
        public async Task Duplicate_EmptyTargets_ReturnsError()
        {
            var command = new DuplicateCommand { Targets = null };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("error", out _),
                "Null targets must return an error response.");
        }
    }
}
