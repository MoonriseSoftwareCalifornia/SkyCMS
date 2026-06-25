// <copyright file="EditorContextPayloadServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services.Copilot;

using Microsoft.Extensions.Logging;
using Moq;
using Sky.Cms.Api.Shared.Services.EditorContext;
using Sky.Editor.Services.Copilot;

/// <summary>
/// Tests for <see cref="EditorContextPayloadService"/>.
/// </summary>
[TestClass]
public class EditorContextPayloadServiceTests
{
    private Mock<IEditorContextBuilder> builderMock = null!;
    private EditorContextPayloadService service = null!;

    [TestInitialize]
    public void Setup()
    {
        builderMock = new Mock<IEditorContextBuilder>();

        builderMock
            .Setup(x => x.BuildEditorContextBaseAsync(
                It.IsAny<EditorSurface>(),
                It.IsAny<EditorKind>(),
                It.IsAny<DocumentKind>(),
                It.IsAny<string>(),
                It.IsAny<LanguageKind>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EditorContextBase
            {
                EditorSurface = EditorSurface.Monaco,
                EditorKind = EditorKind.Article,
                DocumentKind = DocumentKind.Article,
                CurrentField = "Content",
                Language = LanguageKind.Html,
                AiEnabled = true,
            });

        builderMock
            .Setup(x => x.BuildArticleContextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArticleEntityContext
            {
                ArticleNumber = 123,
                Title = "Demo Article",
                UrlPath = "demo-article",
                Content = "<p>Demo</p>",
                LayoutId = "default-layout",
                Status = "draft",
                Version = 1,
                LastModified = DateTimeOffset.UtcNow.ToString("O"),
            });

        builderMock
            .Setup(x => x.BuildKnowledgeContextAsync(It.IsAny<DocumentKind>(), It.IsAny<EditorKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeContext
            {
                PreservationRules = ["Preserve structure"],
                AntiPatterns = ["Do not break layout"],
                RelevantDocumentation =
                [
                    new DocumentationReference
                    {
                        Title = "Docs",
                        Url = "https://docs.sky-cms.com/",
                        Summary = "Docs",
                    },
                ],
            });

        builderMock
            .Setup(x => x.BuildValidationContextAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationContext
            {
                ValidationStatus = [],
            });

        service = new EditorContextPayloadService(builderMock.Object, Mock.Of<ILogger<EditorContextPayloadService>>());
    }

    [TestMethod]
    public async Task BuildPayloadAsync_FullRequest_IncludesEntityAndKnowledgeSections()
    {
        var payload = await service.BuildPayloadAsync(new EditorContextPayloadRequest
        {
            EditorSurface = "monaco",
            DocumentKind = "article",
            Language = "html",
            CurrentField = "Content",
            CurrentFieldValue = "<p>Current</p>",
            ArticleNumber = "123",
            Title = "Demo",
            UrlPath = "demo",
            Lightweight = false,
        });

        Assert.IsTrue(payload.Contains("Editor context payload:", StringComparison.Ordinal));
        Assert.IsTrue(payload.Contains("Article entity context:", StringComparison.Ordinal));
        Assert.IsTrue(payload.Contains("Knowledge constraints:", StringComparison.Ordinal));
        Assert.IsTrue(payload.Contains("Documentation references:", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task BuildPayloadAsync_LightweightRequest_UsesEntitySummary()
    {
        var payload = await service.BuildPayloadAsync(new EditorContextPayloadRequest
        {
            EditorSurface = "monaco",
            DocumentKind = "article",
            Language = "csharp",
            CurrentField = "Code",
            ArticleNumber = "321",
            Lightweight = true,
        });

        Assert.IsTrue(payload.Contains("Entity summary (lightweight):", StringComparison.Ordinal));
        Assert.IsFalse(payload.Contains("Article entity context:", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task BuildPayloadAsync_WhenValidationIsLarge_DropsValidationSectionBeforeHardTruncation()
    {
        builderMock
            .Setup(x => x.BuildValidationContextAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationContext
            {
                ValidationStatus = [],
                Warnings =
                [
                    new ValidationWarning
                    {
                        Field = "Content",
                        Message = new string('W', 20_000),
                    },
                ],
            });

        var payload = await service.BuildPayloadAsync(new EditorContextPayloadRequest
        {
            EditorSurface = "monaco",
            DocumentKind = "article",
            Language = "html",
            CurrentField = "Content",
            ArticleNumber = "123",
            Lightweight = false,
        });

        Assert.IsTrue(payload.Contains("Editor context payload:", StringComparison.Ordinal));
        Assert.IsFalse(payload.Contains("Validation context:", StringComparison.Ordinal));
        Assert.IsFalse(payload.Contains("... (context payload truncated to token budget)", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task BuildPayloadAsync_WhenStillOversized_AppliesBudgetTruncationMarker()
    {
        builderMock
            .Setup(x => x.BuildKnowledgeContextAsync(It.IsAny<DocumentKind>(), It.IsAny<EditorKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeContext
            {
                PreservationRules =
                [
                    new string('P', 20_000),
                    new string('Q', 20_000),
                    new string('R', 20_000),
                ],
                AntiPatterns = [],
                RelevantDocumentation = [],
            });

        var payload = await service.BuildPayloadAsync(new EditorContextPayloadRequest
        {
            EditorSurface = "monaco",
            DocumentKind = "article",
            Language = "html",
            CurrentField = "Content",
            ArticleNumber = "123",
            Lightweight = false,
        });

        Assert.IsTrue(payload.Contains("... (context payload truncated to token budget)", StringComparison.Ordinal));
    }
}