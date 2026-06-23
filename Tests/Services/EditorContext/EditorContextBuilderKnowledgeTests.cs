// <copyright file="EditorContextBuilderKnowledgeTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.EditorContext;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.Common.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Sky.Cms.Api.Shared.Services.EditorContext;

/// <summary>
/// Unit tests for <see cref="EditorContextBuilder"/> knowledge context methods.
/// </summary>
[TestClass]
public class EditorContextBuilderKnowledgeTests
{
    private TestApplicationDbContext _mockDbContext;
    private Mock<IKnowledgeContextProvider> _mockKnowledgeProvider;
    private EditorContextBuilder _builder;

    /// <summary>
    /// Test initialization.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _mockDbContext = new TestApplicationDbContext();
        _mockKnowledgeProvider = new Mock<IKnowledgeContextProvider>();
        _builder = new EditorContextBuilder(_mockDbContext, _mockKnowledgeProvider.Object);
    }

    /// <summary>
    /// Verifies BuildKnowledgeContextAsync delegates to provider and returns provider result.
    /// </summary>
    [TestMethod]
    public async Task BuildKnowledgeContextAsync_DelegatesToProvider_ReturnsExpectedContext()
    {
        // Arrange
        var expected = new KnowledgeContext
        {
            RelevantDocumentation = new List<DocumentationReference>
            {
                new()
                {
                    Title = "Test Doc",
                    Url = "https://docs.example.com/test",
                    Summary = "Test summary",
                    RelatedTopics = new List<string> { "test" },
                },
            },
            EditorialConventions = new EditorialConventions
            {
                TitleFormat = "Test title format",
                ContentGuidelines = new List<string> { "Guideline" },
                SeoRules = new List<string> { "SEO Rule" },
            },
            TechnicalConstraints = new TechnicalConstraints
            {
                HtmlConstraints = new List<string> { "HTML Rule" },
                CssConstraints = new List<string> { "CSS Rule" },
                JsConstraints = new List<string> { "JS Rule" },
            },
            PreservationRules = new List<string> { "Keep placeholders" },
            AntiPatterns = new List<string> { "Do not remove regions" },
            ApplicableDocVersion = "latest",
            ApplicableSectionKinds = new List<string> { "articles" },
        };

        _mockKnowledgeProvider
            .Setup(p => p.GetKnowledgeContextAsync(DocumentKind.Article, EditorKind.Article, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _builder.BuildKnowledgeContextAsync(DocumentKind.Article, EditorKind.Article);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreSame(expected, result);

        _mockKnowledgeProvider.Verify(
            p => p.GetKnowledgeContextAsync(DocumentKind.Article, EditorKind.Article, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}