// <copyright file="MkdirContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;
    using SkyCMS.Drivers.ElFinder.Responses;

    /// <summary>
    /// Contract tests for the <c>mkdir</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/mkdir.md):
    ///   - Response must contain "added" array with the new directory's file object.
    ///   - The new entry must have mime = "directory".
    ///   - The new entry's "phash" must equal the target (parent) hash.
    ///   - On error, response must contain "error" array.
    /// </summary>
    [TestClass]
    public class MkdirContractTests : ElFinderContractTestBase
    {
        [TestMethod]
        [Description("Response must have lowercase 'added' key containing an array.")]
        public async Task Mkdir_Response_HasAddedArray()
        {
            var adapter = BuildAdapter();
            var newDir = MakeDir("pub/images/thumbnails/", "thumbnails");
            adapter.Setup(a => a.CreateFolderAsync("pub/images/thumbnails", It.IsAny<System.Threading.CancellationToken>()))
                   .ReturnsAsync(newDir);

            var handler = new MkdirCommandHandler(adapter.Object);
            var command = new MkdirCommand { Target = ImagesHash, Name = "thumbnails" };
            var response = await handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            AssertArrayProperty(doc.RootElement, "added", minLength: 1);

            Assert.IsFalse(doc.RootElement.TryGetProperty("Added", out _),
                "PascalCase 'Added' key found — STJ must be used, not Newtonsoft.");
        }

        [TestMethod]
        [Description("The created entry in 'added' must be a valid elFinder file object.")]
        public async Task Mkdir_AddedEntry_IsValidElFinderObject()
        {
            var adapter = BuildAdapter();
            var newDir = MakeDir("pub/images/thumbnails/", "thumbnails");
            adapter.Setup(a => a.CreateFolderAsync("pub/images/thumbnails", It.IsAny<System.Threading.CancellationToken>()))
                   .ReturnsAsync(newDir);

            var handler = new MkdirCommandHandler(adapter.Object);
            var command = new MkdirCommand { Target = ImagesHash, Name = "thumbnails" };
            var response = await handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            var added = AssertArrayProperty(doc.RootElement, "added", minLength: 1);
            var entry = added.EnumerateArray().GetEnumerator();
            entry.MoveNext();
            AssertElFinderObject(entry.Current, "added[0]");
        }

        [TestMethod]
        [Description("The new directory entry must have mime = 'directory'.")]
        public async Task Mkdir_AddedEntry_MimeIsDirectory()
        {
            var adapter = BuildAdapter();
            var newDir = MakeDir("pub/images/thumbnails/", "thumbnails");
            adapter.Setup(a => a.CreateFolderAsync("pub/images/thumbnails", It.IsAny<System.Threading.CancellationToken>()))
                   .ReturnsAsync(newDir);

            var handler = new MkdirCommandHandler(adapter.Object);
            var command = new MkdirCommand { Target = ImagesHash, Name = "thumbnails" };
            var response = await handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            var added = AssertArrayProperty(doc.RootElement, "added", minLength: 1);
            var enumerator = added.EnumerateArray();
            enumerator.MoveNext();
            var mime = AssertStringProperty(enumerator.Current, "mime");
            Assert.AreEqual("directory", mime,
                $"New directory must have mime='directory'. Got '{mime}'.");
        }

        [TestMethod]
        [Description("The new entry's 'phash' must equal the target (parent) hash.")]
        public async Task Mkdir_AddedEntry_PhashEqualsTarget()
        {
            var adapter = BuildAdapter();
            var newDir = MakeDir("pub/images/thumbnails/", "thumbnails");
            adapter.Setup(a => a.CreateFolderAsync("pub/images/thumbnails", It.IsAny<System.Threading.CancellationToken>()))
                   .ReturnsAsync(newDir);

            var handler = new MkdirCommandHandler(adapter.Object);
            var command = new MkdirCommand { Target = ImagesHash, Name = "thumbnails" };
            var response = await handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            var added = AssertArrayProperty(doc.RootElement, "added", minLength: 1);
            var enumerator = added.EnumerateArray();
            enumerator.MoveNext();
            var phash = AssertStringProperty(enumerator.Current, "phash");
            Assert.AreEqual(ImagesHash, phash,
                $"New entry phash must equal the target (parent) hash '{ImagesHash}'. Got '{phash}'.");
        }

        [TestMethod]
        [Description("Missing target returns an error response.")]
        public async Task Mkdir_MissingTarget_ReturnsError()
        {
            var handler = new MkdirCommandHandler(BuildAdapter().Object);
            var command = new MkdirCommand { Target = string.Empty, Name = "test" };
            var response = await handler.HandleAsync(command, default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Missing target must return ElFinderErrorResponse.");
            using var doc = SerializeResponse(response);
            Assert.IsTrue(doc.RootElement.TryGetProperty("error", out _),
                "Error response must contain 'error' key.");
        }

        [TestMethod]
        [Description("Missing name returns an error response.")]
        public async Task Mkdir_MissingName_ReturnsError()
        {
            var handler = new MkdirCommandHandler(BuildAdapter().Object);
            var command = new MkdirCommand { Target = ImagesHash, Name = string.Empty };
            var response = await handler.HandleAsync(command, default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Missing name must return ElFinderErrorResponse.");
        }
    }
}
