// <copyright file="KnowledgeContextProviderTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.EditorContext;

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sky.Cms.Api.Shared.Services.EditorContext;

/// <summary>
/// Unit tests for <see cref="KnowledgeContextProvider"/>.
/// </summary>
[TestClass]
public class KnowledgeContextProviderTests
{
    private KnowledgeContextProvider _provider;

    /// <summary>
    /// Test initialization.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _provider = new KnowledgeContextProvider();
    }

    /// <summary>
    /// Verifies article knowledge payload contains expected sections and rules.
    /// </summary>
    [TestMethod]
    public async Task GetArticleKnowledgeAsync_ReturnsExpectedKnowledgePayload()
    {
        // Act
        var result = await _provider.GetArticleKnowledgeAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.RelevantDocumentation);
        Assert.IsTrue(result.RelevantDocumentation.Count > 0);
        Assert.IsNotNull(result.ApplicableSectionKinds);
        CollectionAssert.Contains(result.ApplicableSectionKinds, "articles");
        Assert.IsTrue(result.PreservationRules.Count > 0);
        Assert.IsTrue(result.AntiPatterns.Count > 0);
        Assert.IsNotNull(result.TechnicalConstraints);
        Assert.IsNotNull(result.TechnicalConstraints.HtmlConstraints);
        Assert.IsNotNull(result.TechnicalConstraints.JsConstraints);
    }

    /// <summary>
    /// Verifies layout knowledge payload contains expected sections and constraints.
    /// </summary>
    [TestMethod]
    public async Task GetLayoutKnowledgeAsync_ReturnsExpectedKnowledgePayload()
    {
        // Act
        var result = await _provider.GetLayoutKnowledgeAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.RelevantDocumentation);
        Assert.IsTrue(result.RelevantDocumentation.Count > 0);
        Assert.IsNotNull(result.ApplicableSectionKinds);
        CollectionAssert.Contains(result.ApplicableSectionKinds, "layouts");
        Assert.IsTrue(result.PreservationRules.Count > 0);
        Assert.IsTrue(result.AntiPatterns.Count > 0);
        Assert.IsNotNull(result.TechnicalConstraints);
        Assert.IsNotNull(result.TechnicalConstraints.HtmlConstraints);
        Assert.IsNotNull(result.TechnicalConstraints.CssConstraints);
    }

    /// <summary>
    /// Verifies template knowledge payload contains expected sections and constraints.
    /// </summary>
    [TestMethod]
    public async Task GetTemplateKnowledgeAsync_ReturnsExpectedKnowledgePayload()
    {
        // Act
        var result = await _provider.GetTemplateKnowledgeAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.RelevantDocumentation);
        Assert.IsTrue(result.RelevantDocumentation.Count > 0);
        Assert.IsNotNull(result.ApplicableSectionKinds);
        CollectionAssert.Contains(result.ApplicableSectionKinds, "templates");
        Assert.IsTrue(result.PreservationRules.Count > 0);
        Assert.IsTrue(result.AntiPatterns.Count > 0);
        Assert.IsNotNull(result.TechnicalConstraints);
        Assert.IsNotNull(result.TechnicalConstraints.HtmlConstraints);
        Assert.IsNotNull(result.TechnicalConstraints.JsConstraints);
    }

    /// <summary>
    /// Verifies editor-kind routing for article requests.
    /// </summary>
    [TestMethod]
    public async Task GetKnowledgeContextAsync_ArticleEditorKind_ReturnsArticleKnowledge()
    {
        // Act
        var result = await _provider.GetKnowledgeContextAsync(DocumentKind.Article, EditorKind.Article);

        // Assert
        Assert.IsNotNull(result.ApplicableSectionKinds);
        CollectionAssert.Contains(result.ApplicableSectionKinds, "articles");
    }

    /// <summary>
    /// Verifies editor-kind routing for layout requests.
    /// </summary>
    [TestMethod]
    public async Task GetKnowledgeContextAsync_LayoutEditorKind_ReturnsLayoutKnowledge()
    {
        // Act
        var result = await _provider.GetKnowledgeContextAsync(DocumentKind.Layout, EditorKind.Layout);

        // Assert
        Assert.IsNotNull(result.ApplicableSectionKinds);
        CollectionAssert.Contains(result.ApplicableSectionKinds, "layouts");
    }

    /// <summary>
    /// Verifies editor-kind routing for template requests.
    /// </summary>
    [TestMethod]
    public async Task GetKnowledgeContextAsync_TemplateEditorKind_ReturnsTemplateKnowledge()
    {
        // Act
        var result = await _provider.GetKnowledgeContextAsync(DocumentKind.Template, EditorKind.Template);

        // Assert
        Assert.IsNotNull(result.ApplicableSectionKinds);
        CollectionAssert.Contains(result.ApplicableSectionKinds, "templates");
    }

    /// <summary>
    /// Verifies unsupported editor kinds throw an exception.
    /// </summary>
    [TestMethod]
    public async Task GetKnowledgeContextAsync_UnsupportedEditorKind_ThrowsNotSupportedException()
    {
        // Act + Assert
        try
        {
            await _provider.GetKnowledgeContextAsync(DocumentKind.Unknown, EditorKind.Settings);
            Assert.Fail("Expected NotSupportedException was not thrown.");
        }
        catch (NotSupportedException)
        {
            // Expected
        }
    }
}