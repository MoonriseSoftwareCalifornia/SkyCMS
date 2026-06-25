// <copyright file="AiSourceCodeIndexServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services.Copilot;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Sky.Editor.Services.Copilot;

/// <summary>
/// Tests for <see cref="AiSourceCodeIndexService"/>.
/// </summary>
[TestClass]
public class AiSourceCodeIndexServiceTests
{
    private string tempRoot = string.Empty;
    private Mock<IHostEnvironment> hostEnvironmentMock = null!;
    private AiSourceCodeIndexService service = null!;

    [TestInitialize]
    public void Setup()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), $"SkyCMS-AiCodeIndex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var editorDir = Path.Combine(tempRoot, "Editor");
        Directory.CreateDirectory(editorDir);

        File.WriteAllText(
            Path.Combine(tempRoot, "KnowledgeHelper.cs"),
            "namespace Sky.Editor.Services.Copilot;\npublic sealed class KnowledgeHelper\n{\n    public string BuildHelpContext(string query) => query;\n}");

        File.WriteAllText(
            Path.Combine(tempRoot, "Editor", "HelpController.cs"),
            "namespace Sky.Editor.Controllers;\npublic sealed class HelpController\n{\n    public string SearchDocs(string query) => query.ToUpperInvariant();\n}");

        hostEnvironmentMock = new Mock<IHostEnvironment>();
        hostEnvironmentMock.SetupGet(x => x.ContentRootPath).Returns(editorDir);
        service = new AiSourceCodeIndexService(hostEnvironmentMock.Object, Mock.Of<ILogger<AiSourceCodeIndexService>>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task SearchSourceCodeAsync_ReturnsRankedMatchesFromLocalRepo()
    {
        var results = await service.SearchSourceCodeAsync("help context query");

        Assert.IsTrue(results.Count >= 1);
        Assert.IsTrue(results.Any(result => result.FilePath.EndsWith("KnowledgeHelper.cs", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(results.All(result => result.GitHubUrl.Contains("github.com/CWALabs/SkyCMS/blob/main/")));
    }

    [TestMethod]
    public async Task SearchSourceCodeAsync_ReturnsEmptyResults_WhenQueryIsBlank()
    {
        var results = await service.SearchSourceCodeAsync(string.Empty);

        Assert.AreEqual(0, results.Count);
    }
}