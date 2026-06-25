// <copyright file="AiHelpQueryContextServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services.Copilot;

using Moq;
using Sky.Editor.Services.Copilot;

/// <summary>
/// Tests for <see cref="AiHelpQueryContextService"/>.
/// </summary>
[TestClass]
public class AiHelpQueryContextServiceTests
{
    private Mock<IAiDocumentationContextService> documentationContextServiceMock = null!;
    private Mock<IAiSourceCodeIndexService> sourceCodeIndexServiceMock = null!;
    private Mock<IAiFaqIndexService> faqIndexServiceMock = null!;
    private AiHelpQueryContextService service = null!;

    [TestInitialize]
    public void Setup()
    {
        documentationContextServiceMock = new Mock<IAiDocumentationContextService>();
        sourceCodeIndexServiceMock = new Mock<IAiSourceCodeIndexService>();
        faqIndexServiceMock = new Mock<IAiFaqIndexService>();

        documentationContextServiceMock
            .Setup(s => s.GetDocumentationContextAsync(It.IsAny<AiContextEnrichmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiDocumentationContextResult
            {
                ContextText = "Documentation context from docs.sky-cms.com:\n- Page editor guidance\nSources:\n- https://docs.sky-cms.com/for-editors/page-editor/",
            });

        sourceCodeIndexServiceMock
            .Setup(s => s.SearchSourceCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AiSourceCodeSearchResult
                {
                    FilePath = "Editor/Controllers/AiProxyController.cs",
                    SymbolName = "AiProxyController",
                    Signature = "public async Task<IActionResult> Chat([FromBody] CopilotChatRequest request)",
                    Snippet = "public async Task<IActionResult> Chat([FromBody] CopilotChatRequest request)",
                    GitHubUrl = "https://github.com/CWALabs/SkyCMS/blob/main/Editor/Controllers/AiProxyController.cs",
                    RelevanceScore = 42,
                },
            ]);

        faqIndexServiceMock
            .Setup(s => s.SearchFaqAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AiFaqMatch
                {
                    Question = "How do I publish a page?",
                    Answer = "Navigate to the Pages list, select the page, and click Publish.",
                    SourceUrl = "https://docs.sky-cms.com/for-editors/publishing/",
                    RelevanceScore = 5,
                },
            ]);

        service = new AiHelpQueryContextService(
            documentationContextServiceMock.Object,
            sourceCodeIndexServiceMock.Object,
            faqIndexServiceMock.Object);
    }

    [TestMethod]
    public async Task BuildContextAsync_ReturnsCombinedContextAndAttributions()
    {
        var result = await service.BuildContextAsync(new AiHelpQueryContextRequest
        {
            Query = "How does chat endpoint work?",
            DocumentKind = "article",
            SectionKind = "article-content",
        });

        Assert.IsTrue(result.ContextText.Contains("Documentation context from docs.sky-cms.com", StringComparison.Ordinal));
        Assert.IsTrue(result.ContextText.Contains("Source code context from SkyCMS repository", StringComparison.Ordinal));
        Assert.IsTrue(result.ContextText.Contains("FAQ context from SkyCMS documentation", StringComparison.Ordinal));
        Assert.IsTrue(result.ContextText.Contains("Q: How do I publish a page?", StringComparison.Ordinal));
        Assert.AreEqual(3, result.Sources.Count);
        Assert.IsTrue(result.Sources.Exists(s => s.SourceType == "docs"), "Expected docs attribution.");
        Assert.IsTrue(result.Sources.Exists(s => s.SourceType == "code"), "Expected code attribution.");
        Assert.IsTrue(result.Sources.Exists(s => s.SourceType == "faq"), "Expected faq attribution.");
    }

    [TestMethod]
    public async Task BuildContextAsync_BlankQuery_ReturnsEmptyResult()
    {
        var result = await service.BuildContextAsync(new AiHelpQueryContextRequest
        {
            Query = string.Empty,
        });

        Assert.AreEqual(string.Empty, result.ContextText);
        Assert.AreEqual(0, result.Sources.Count);
    }
}
