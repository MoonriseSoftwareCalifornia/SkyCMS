// <copyright file="FileContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;

    /// <summary>
    /// Contract tests for the <c>file</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/file.md):
    ///   - Response is NOT a JSON object — it is a <see cref="FileResponse"/> with a Stream.
    ///   - Stream must be non-null for a valid target.
    ///   - ForceDownload is true when download=1.
    ///   - Invalid/missing target returns an <see cref="ElFinderErrorResponse"/>.
    /// </summary>
    [TestClass]
    public class FileContractTests : ElFinderContractTestBase
    {
        private FileCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            var mock = BuildAdapter();

            mock.Setup(a => a.GetReadStreamAsync(
                    It.Is<string>(p => p == "pub/images/logo.png"),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("PNG_DATA")));

            _handler = new FileCommandHandler(mock.Object);
        }

        [TestMethod]
        [Description("Valid target returns a FileResponse with a non-null stream.")]
        public async Task File_ValidTarget_ReturnsStreamResponse()
        {
            var command = new FileCommand { Target = LogoPngHash, Download = "0" };
            var response = await _handler.HandleAsync(command, default);

            Assert.IsInstanceOfType(response, typeof(FileResponse),
                "file command must return FileResponse, not a JSON DTO.");

            var fileResponse = (FileResponse)response;
            Assert.IsNotNull(fileResponse.Stream, "Stream must not be null for a valid file.");
            Assert.IsFalse(string.IsNullOrEmpty(fileResponse.ContentType), "ContentType must be set.");
            Assert.IsFalse(string.IsNullOrEmpty(fileResponse.FileName), "FileName must be set.");

            fileResponse.Stream?.Dispose();
        }

        [TestMethod]
        [Description("download=1 sets ForceDownload=true on the response.")]
        public async Task File_Download1_SetsForceDownload()
        {
            var command = new FileCommand { Target = LogoPngHash, Download = "1" };
            var response = await _handler.HandleAsync(command, default);

            Assert.IsInstanceOfType(response, typeof(FileResponse));
            var fileResponse = (FileResponse)response;
            Assert.IsTrue(fileResponse.ForceDownload, "ForceDownload must be true when download=1.");

            fileResponse.Stream?.Dispose();
        }

        [TestMethod]
        [Description("download=0 leaves ForceDownload=false.")]
        public async Task File_Download0_DoesNotForceDownload()
        {
            var command = new FileCommand { Target = LogoPngHash, Download = "0" };
            var response = await _handler.HandleAsync(command, default);

            Assert.IsInstanceOfType(response, typeof(FileResponse));
            var fileResponse = (FileResponse)response;
            Assert.IsFalse(fileResponse.ForceDownload, "ForceDownload must be false when download=0.");

            fileResponse.Stream?.Dispose();
        }

        [TestMethod]
        [Description("Missing target returns an error response.")]
        public async Task File_MissingTarget_ReturnsError()
        {
            var command = new FileCommand { Target = null };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("error", out _),
                "Missing target must return an error response.");
        }

        [TestMethod]
        [Description("Invalid hash returns an error response.")]
        public async Task File_InvalidHash_ReturnsError()
        {
            var command = new FileCommand { Target = "not_a_valid_hash" };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("error", out _),
                "Invalid hash must return an error response.");
        }
    }
}
