// <copyright file="UrlContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;

    /// <summary>
    /// Contract tests for the <c>url</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/url.md):
    ///   - Response root key must be "url" (lowercase).
    ///   - "url" is a non-empty string.
    ///   - URL includes the blobPublicUrl base and the decoded path.
    /// </summary>
    [TestClass]
    public class UrlContractTests : ElFinderContractTestBase
    {
        private UrlCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            _handler = new UrlCommandHandler(BuildAdapter().Object);
        }

        [TestMethod]
        [Description("Response must have lowercase 'url' key.")]
        public async Task Url_ResponseKey_IsLowercaseUrl()
        {
            var command = new UrlCommand { Target = LogoPngHash, BlobPublicUrl = "https://cdn.example.com" };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("url", out _),
                "Contract violation: 'url' key missing. See Docs/commands/url.md.");

            Assert.IsFalse(
                doc.RootElement.TryGetProperty("Url", out _),
                "PascalCase 'Url' key found — [JsonPropertyName] must be applied.");
        }

        [TestMethod]
        [Description("'url' must be a non-empty string.")]
        public async Task Url_Value_IsNonEmptyString()
        {
            var command = new UrlCommand { Target = LogoPngHash, BlobPublicUrl = "https://cdn.example.com" };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            var url = AssertStringProperty(doc.RootElement, "url");
            Assert.IsFalse(string.IsNullOrEmpty(url), "'url' must not be empty.");
        }

        [TestMethod]
        [Description("URL contains the blob base and decoded file path.")]
        public async Task Url_ContainsBlobBaseAndPath()
        {
            var command = new UrlCommand { Target = LogoPngHash, BlobPublicUrl = "https://cdn.example.com" };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            var url = AssertStringProperty(doc.RootElement, "url");
            Assert.IsTrue(url.StartsWith("https://cdn.example.com"), $"URL '{url}' must start with blobPublicUrl.");
            Assert.IsTrue(url.Contains("logo.png"), $"URL '{url}' must contain the file name.");
        }

        [TestMethod]
        [Description("Missing target returns an error.")]
        public async Task Url_MissingTarget_ReturnsError()
        {
            var command = new UrlCommand { Target = null, BlobPublicUrl = "https://cdn.example.com" };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(
                doc.RootElement.TryGetProperty("error", out _),
                "Missing target must return an error response.");
        }

        [TestMethod]
        [Description("Works without a BlobPublicUrl — falls back to relative path.")]
        public async Task Url_NoBlobBase_ReturnsRelativePath()
        {
            var command = new UrlCommand { Target = LogoPngHash, BlobPublicUrl = null };
            var response = await _handler.HandleAsync(command, default);
            using var doc = SerializeResponse(response);

            var url = AssertStringProperty(doc.RootElement, "url");
            Assert.IsTrue(url.Contains("logo.png"), $"URL '{url}' must still contain the file name.");
        }
    }
}
