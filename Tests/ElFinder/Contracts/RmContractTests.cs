// <copyright file="RmContractTests.cs" company="Moonrise Software, LLC">
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
    /// Contract tests for the <c>rm</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/rm.md):
    ///   - Response must contain "removed" array of hash strings.
    ///   - Each successfully deleted hash must appear in "removed".
    ///   - On complete failure, an "error" key should be present.
    ///   - "removed" must contain string values (hashes), not objects.
    /// </summary>
    [TestClass]
    public class RmContractTests : ElFinderContractTestBase
    {
        [TestMethod]
        [Description("Response must have lowercase 'removed' key containing an array.")]
        public async Task Rm_Response_HasRemovedArray()
        {
            var adapter = BuildAdapter();
            adapter.Setup(a => a.DeleteAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                   .Returns(System.Threading.Tasks.Task.CompletedTask);

            var handler = new RmCommandHandler(adapter.Object);
            var command = new RmCommand { Target = LogoPngHash };
            var response = await handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            AssertArrayProperty(doc.RootElement, "removed");

            Assert.IsFalse(doc.RootElement.TryGetProperty("Removed", out _),
                "PascalCase 'Removed' key found — STJ must be used, not Newtonsoft.");
        }

        [TestMethod]
        [Description("Successfully deleted hash must appear in the 'removed' array.")]
        public async Task Rm_DeletedHash_AppearsInRemoved()
        {
            var adapter = BuildAdapter();
            adapter.Setup(a => a.DeleteAsync("pub/images/logo.png", It.IsAny<System.Threading.CancellationToken>()))
                   .Returns(System.Threading.Tasks.Task.CompletedTask);

            var handler = new RmCommandHandler(adapter.Object);
            var command = new RmCommand { Target = LogoPngHash };
            var response = await handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var removed = AssertArrayProperty(doc.RootElement, "removed", minLength: 1);
            var found = false;
            foreach (var item in removed.EnumerateArray())
            {
                Assert.AreEqual(JsonValueKind.String, item.ValueKind,
                    "Each entry in 'removed' must be a string hash, not an object.");
                if (item.GetString() == LogoPngHash) found = true;
            }

            Assert.IsTrue(found,
                $"Hash '{LogoPngHash}' must appear in 'removed' after successful deletion.");
        }

        [TestMethod]
        [Description("Batch delete: all successfully deleted hashes appear in 'removed'.")]
        public async Task Rm_BatchDelete_AllHashesInRemoved()
        {
            var docsHash = AdapterHashHelper.Encode("pub/docs/");
            var adapter = BuildAdapter();
            adapter.Setup(a => a.DeleteAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                   .Returns(System.Threading.Tasks.Task.CompletedTask);

            var handler = new RmCommandHandler(adapter.Object);
            var command = new RmCommand { Target = $"{LogoPngHash},{docsHash}" };
            var response = await handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var removed = AssertArrayProperty(doc.RootElement, "removed", minLength: 2);
        }

        [TestMethod]
        [Description("Missing target returns an error response.")]
        public async Task Rm_MissingTarget_ReturnsError()
        {
            var handler = new RmCommandHandler(BuildAdapter().Object);
            var command = new RmCommand { Target = string.Empty };
            var response = await handler.Handle(command, default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Missing target must return ElFinderErrorResponse.");
            using var doc = SerializeResponse(response);
            Assert.IsTrue(doc.RootElement.TryGetProperty("error", out _),
                "Error response must contain 'error' key.");
        }

        [TestMethod]
        [Description("'removed' values must be hash strings, not file objects.")]
        public async Task Rm_RemovedValues_AreHashStrings()
        {
            var adapter = BuildAdapter();
            adapter.Setup(a => a.DeleteAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                   .Returns(System.Threading.Tasks.Task.CompletedTask);

            var handler = new RmCommandHandler(adapter.Object);
            var command = new RmCommand { Target = LogoPngHash };
            var response = await handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var removed = AssertArrayProperty(doc.RootElement, "removed");
            foreach (var item in removed.EnumerateArray())
            {
                Assert.AreEqual(JsonValueKind.String, item.ValueKind,
                    "'removed' entries must be plain hash strings. " +
                    "Drift: if objects appear here the client won't know what to remove from its model.");
            }
        }
    }
}
