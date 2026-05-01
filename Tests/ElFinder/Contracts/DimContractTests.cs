// <copyright file="DimContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;

    /// <summary>
    /// Contract tests for the <c>dim</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/dim.md):
    ///   - Response root key must be "dim" (lowercase).
    ///   - "dim" value is a string in "WxH" format (e.g. "1x1").
    ///   - Missing/invalid target returns an error.
    /// </summary>
    [TestClass]
    public class DimContractTests : ElFinderContractTestBase
    {
        private DimCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            var mock = BuildAdapter();

            mock.Setup(a => a.GetReadStreamAsync(
                    It.Is<string>(p => p == "pub/images/logo.png"),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(() => MakeOnePxPngStream());

            _handler = new DimCommandHandler(mock.Object);
        }

        [TestMethod]
        [Description("Response must have lowercase 'dim' key.")]
        public async Task Dim_ResponseKey_IsLowercaseDim()
        {
            var command = new DimCommand { Target = LogoPngHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("dim", out _),
                "Contract violation: 'dim' key missing. See Docs/commands/dim.md.");

            Assert.IsFalse(
                doc.RootElement.TryGetProperty("Dim", out _),
                "PascalCase 'Dim' key found — [JsonPropertyName] must be applied.");
        }

        [TestMethod]
        [Description("'dim' value must be a non-empty string in WxH format.")]
        public async Task Dim_Value_IsWxHFormat()
        {
            var command = new DimCommand { Target = LogoPngHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var dim = AssertStringProperty(doc.RootElement, "dim");
            Assert.IsTrue(dim.Contains('x'), $"'dim' value '{dim}' must be in 'WxH' format.");

            var parts = dim.Split('x');
            Assert.AreEqual(2, parts.Length, "dim must have exactly two parts separated by 'x'.");
            Assert.IsTrue(int.TryParse(parts[0], out var w) && w >= 1, "Width must be a positive integer.");
            Assert.IsTrue(int.TryParse(parts[1], out var h) && h >= 1, "Height must be a positive integer.");
        }

        [TestMethod]
        [Description("1x1 image returns '1x1'.")]
        public async Task Dim_OnePxImage_Returns1x1()
        {
            var command = new DimCommand { Target = LogoPngHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var dim = AssertStringProperty(doc.RootElement, "dim");
            Assert.AreEqual("1x1", dim, "1x1 PNG must return dim='1x1'.");
        }

        [TestMethod]
        [Description("Missing target returns an error.")]
        public async Task Dim_MissingTarget_ReturnsError()
        {
            var command = new DimCommand { Target = null };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("error", out _),
                "Missing target must return an error response.");
        }

        [TestMethod]
        [Description("Invalid hash returns an error.")]
        public async Task Dim_InvalidHash_ReturnsError()
        {
            var command = new DimCommand { Target = "not_a_real_hash" };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("error", out _),
                "Invalid hash must return an error response.");
        }
    }
}
