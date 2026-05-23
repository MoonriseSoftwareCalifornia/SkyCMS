// <copyright file="ResizeContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;

    /// <summary>
    /// Contract tests for the <c>resize</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/resize.md):
    ///   - Response root key must be "changed" (lowercase).
    ///   - "changed" is an array of elFinder file objects.
    ///   - Missing target returns an error.
    /// </summary>
    [TestClass]
    public class ResizeContractTests : ElFinderContractTestBase
    {
        private ResizeCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            var mock = BuildAdapter();

            mock.Setup(a => a.GetReadStreamAsync(
                    It.Is<string>(p => p == "pub/images/logo.png"),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(() => MakeOnePxPngStream());

            var resized = MakeFile("pub/images/logo.png", "logo.png", size: 512);

            mock.Setup(a => a.UploadFileAsync(
                    It.Is<string>(p => p == "pub/images/logo.png"),
                    It.IsAny<System.IO.Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(resized);

            _handler = new ResizeCommandHandler(mock.Object);
        }

        [TestMethod]
        [Description("Response must have lowercase 'changed' key.")]
        public async Task Resize_ResponseKey_IsLowercaseChanged()
        {
            var command = new ResizeCommand { Target = LogoPngHash, Mode = "resize", Width = 1, Height = 1 };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("changed", out _),
                "Contract violation: 'changed' key missing. See Docs/commands/resize.md.");

            Assert.IsFalse(
                doc.RootElement.TryGetProperty("Changed", out _),
                "PascalCase 'Changed' found — [JsonPropertyName] must be applied.");
        }

        [TestMethod]
        [Description("'changed' must be a JSON array.")]
        public async Task Resize_Changed_IsArray()
        {
            var command = new ResizeCommand { Target = LogoPngHash, Mode = "resize", Width = 1, Height = 1 };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            var changed = AssertArrayProperty(doc.RootElement, "changed");
            Assert.AreEqual(JsonValueKind.Array, changed.ValueKind);
        }

        [TestMethod]
        [Description("Changed object must have all required elFinder fields.")]
        public async Task Resize_ChangedObject_HasRequiredFields()
        {
            var command = new ResizeCommand { Target = LogoPngHash, Mode = "resize", Width = 1, Height = 1 };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            var changed = AssertArrayProperty(doc.RootElement, "changed", minLength: 1);
            foreach (var item in changed.EnumerateArray())
            {
                AssertElFinderObject(item, "resize result");
            }
        }

        [TestMethod]
        [Description("Missing target returns an error.")]
        public async Task Resize_MissingTarget_ReturnsError()
        {
            var command = new ResizeCommand { Target = null, Mode = "resize", Width = 100, Height = 100 };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("error", out _),
                "Missing target must return an error response.");
        }
    }
}
