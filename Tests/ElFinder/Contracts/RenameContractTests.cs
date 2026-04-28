// <copyright file="RenameContractTests.cs" company="Moonrise Software, LLC">
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
    /// Contract tests for the <c>rename</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/rename.md):
    ///   - Response must contain "added" (array with the renamed entry) and
    ///     "removed" (array with the old hash string).
    ///   - The added entry must be a valid elFinder file object with the new name.
    ///   - The removed array must contain the original hash string.
    ///   - New hash must differ from old hash (hash encodes path).
    /// </summary>
    [TestClass]
    public class RenameContractTests : ElFinderContractTestBase
    {
        [TestMethod]
        [Description("Response must have lowercase 'added' and 'removed' keys.")]
        public async Task Rename_Response_HasAddedAndRemovedKeys()
        {
            var adapter = BuildAdapter();
            var renamedEntry = MakeFile("pub/images/logo-new.png", "logo-new.png");
            adapter.Setup(a => a.RenameAsync(
                    "pub/images/logo.png",
                    "pub/images/logo-new.png",
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(renamedEntry);

            var handler = new RenameCommandHandler(adapter.Object);
            var command = new RenameCommand { Target = LogoPngHash, Name = "logo-new.png" };
            var response = await handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            AssertArrayProperty(doc.RootElement, "added", minLength: 1);
            AssertArrayProperty(doc.RootElement, "removed", minLength: 1);

            Assert.IsFalse(doc.RootElement.TryGetProperty("Added", out _),
                "PascalCase 'Added' key found — STJ must be used.");
            Assert.IsFalse(doc.RootElement.TryGetProperty("Removed", out _),
                "PascalCase 'Removed' key found — STJ must be used.");
        }

        [TestMethod]
        [Description("The added entry must be a valid elFinder file object with the new name.")]
        public async Task Rename_AddedEntry_IsValidWithNewName()
        {
            var adapter = BuildAdapter();
            var renamedEntry = MakeFile("pub/images/logo-new.png", "logo-new.png");
            adapter.Setup(a => a.RenameAsync(
                    "pub/images/logo.png",
                    "pub/images/logo-new.png",
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(renamedEntry);

            var handler = new RenameCommandHandler(adapter.Object);
            var command = new RenameCommand { Target = LogoPngHash, Name = "logo-new.png" };
            var response = await handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var added = AssertArrayProperty(doc.RootElement, "added", minLength: 1);
            var enumerator = added.EnumerateArray();
            enumerator.MoveNext();
            var entry = enumerator.Current;

            AssertElFinderObject(entry, "added[0]");

            var name = AssertStringProperty(entry, "name");
            Assert.AreEqual("logo-new.png", name,
                $"Renamed entry must have the new name 'logo-new.png'. Got '{name}'.");
        }

        [TestMethod]
        [Description("The removed array must contain the original hash string (not an object).")]
        public async Task Rename_RemovedContainsOriginalHashString()
        {
            var adapter = BuildAdapter();
            var renamedEntry = MakeFile("pub/images/logo-new.png", "logo-new.png");
            adapter.Setup(a => a.RenameAsync(
                    "pub/images/logo.png",
                    "pub/images/logo-new.png",
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(renamedEntry);

            var handler = new RenameCommandHandler(adapter.Object);
            var command = new RenameCommand { Target = LogoPngHash, Name = "logo-new.png" };
            var response = await handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var removed = AssertArrayProperty(doc.RootElement, "removed", minLength: 1);
            var enumerator = removed.EnumerateArray();
            enumerator.MoveNext();
            var removedItem = enumerator.Current;

            Assert.AreEqual(JsonValueKind.String, removedItem.ValueKind,
                "'removed' entries must be plain hash strings, not objects. " +
                "The client uses these to remove stale entries from its model.");

            Assert.AreEqual(LogoPngHash, removedItem.GetString(),
                $"'removed[0]' must equal the original hash '{LogoPngHash}'.");
        }

        [TestMethod]
        [Description("New entry hash must differ from the original hash (hash encodes the path).")]
        public async Task Rename_AddedHash_DiffersFromOriginal()
        {
            var adapter = BuildAdapter();
            var renamedEntry = MakeFile("pub/images/logo-new.png", "logo-new.png");
            adapter.Setup(a => a.RenameAsync(
                    "pub/images/logo.png",
                    "pub/images/logo-new.png",
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(renamedEntry);

            var handler = new RenameCommandHandler(adapter.Object);
            var command = new RenameCommand { Target = LogoPngHash, Name = "logo-new.png" };
            var response = await handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var added = AssertArrayProperty(doc.RootElement, "added", minLength: 1);
            var enumerator = added.EnumerateArray();
            enumerator.MoveNext();
            var newHash = AssertStringProperty(enumerator.Current, "hash");

            Assert.AreNotEqual(LogoPngHash, newHash,
                "Hash must change after rename because the path changes. " +
                "If hashes are identical the client will not update its model correctly.");
        }

        [TestMethod]
        [Description("Missing target returns an error response.")]
        public async Task Rename_MissingTarget_ReturnsError()
        {
            var handler = new RenameCommandHandler(BuildAdapter().Object);
            var command = new RenameCommand { Target = string.Empty, Name = "new.png" };
            var response = await handler.Handle(command, default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Missing target must return ElFinderErrorResponse.");
            using var doc = SerializeResponse(response);
            Assert.IsTrue(doc.RootElement.TryGetProperty("error", out _),
                "Error response must contain 'error' key.");
        }

        [TestMethod]
        [Description("Missing name returns an error response.")]
        public async Task Rename_MissingName_ReturnsError()
        {
            var handler = new RenameCommandHandler(BuildAdapter().Object);
            var command = new RenameCommand { Target = LogoPngHash, Name = string.Empty };
            var response = await handler.Handle(command, default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Missing name must return ElFinderErrorResponse.");
        }
    }
}
